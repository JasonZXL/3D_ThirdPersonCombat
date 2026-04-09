using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Boss Phase2 远程行为模块
/// - Root NavMeshAgent 移动（撤退）
/// - 停下站桩射弹
/// - 玩家靠近 -> Retreating
/// - Retreating 卡住 -> 触发 OnRetreatStuck（由 BossController 传送回 SpawnPoint）
/// </summary>
public class BossRangedModule : MonoBehaviour
{
    public enum RangedState
    {
        Attacking,
        Retreating
    }

    [Header("References")]
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private Transform player;

    [Header("Projectile")]
    [Tooltip("射弹预制体")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("子弹发射点")]
    [SerializeField] private Transform firePoint;

    [Tooltip("射速（秒/发）")]
    [SerializeField] private float attackInterval = 1.2f;

    [Tooltip("子弹移动速度（单位/秒）")]
    [SerializeField] private float projectileSpeed = 12f;

    [Tooltip("攻击时是否面向玩家")]
    [SerializeField] private bool rotateToPlayerWhileAttacking = true;

    [Header("Ranges")]
    [Tooltip("玩家距离 <= 该值时进入撤退")]
    [SerializeField] private float retreatTriggerRange = 5.0f;

    [Tooltip("撤退目标距离（设置越大越能拉开身位）")]
    [SerializeField] private float retreatDistance = 6.0f;

    [Tooltip("撤退时与玩家保持的最小距离（用于避免一直抖动）")]
    [SerializeField] private float safeDistance = 6.5f;

    [Header("Retreat Stuck Detection")]
    [Tooltip("进入撤退后，延迟多久开始检测卡住（避免刚进入就误判）")]
    [SerializeField] private float retreatCheckDelay = 0.5f;

    [Tooltip("物理卡住检测：撤退时间超过该值且位移小于阈值则判定卡住")]
    [SerializeField] private float physicalStuckTime = 1.5f;

    [Tooltip("物理卡住检测：最小位移阈值（米）")]
    [SerializeField] private float physicalStuckDistance = 1.0f;

    [Tooltip("逻辑卡住检测：连续找不到有效撤退路径的次数达到该值则判定卡住")]
    [SerializeField] private int maxConsecutiveSampleFailures = 8;

    [Header("NavAgent Settings (Phase2)")]
    [SerializeField] private float phase2MoveSpeed = 5.0f;
    [SerializeField] private float phase2Acceleration = 20f;
    [SerializeField] private float phase2AngularSpeed = 720f;
    [SerializeField] private float phase2StoppingDistance = 0.1f;

    [Header("移动组件")]
    [SerializeField] private CharacterController characterController;
    [Header("动画引用")]
    [SerializeField] private Animator animator;
    [Header("动画参数")]
    [SerializeField] private string speedParam = "speed";
    [SerializeField] private string isMovingParam = "isMoving";
    [SerializeField] private string isRetreatingParam = "isRetreating";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    [SerializeField] private ChasingEnemyStunController stunController;

    // Runtime
    private bool isActive = false;
    private RangedState state = RangedState.Attacking;

    private float attackTimer = 0f;

    // Retreat tracking
    private float retreatStateTimer = 0f;
    private Vector3 retreatEnterPos;
    
    // 卡住检测
    private int consecutiveSampleFailures = 0;
    private bool hasTriggeredStuckEvent = false;

    /// <summary>
    /// Retreat 卡住时通知 BossController 传送回 SpawnPoint
    /// </summary>
    public event Action OnRetreatStuck;

    public bool IsActive => isActive;
    public RangedState CurrentState => state;

    private void Awake()
    {
        AutoWire(includeInactive: true);
        if (stunController == null)
            stunController = GetComponent<ChasingEnemyStunController>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    #region Public API

    public void Initialize(Transform playerTransform)
    {
        player = playerTransform;
        AutoWire(includeInactive: true);

        if (bodyTransform == null)
            bodyTransform = transform.root;

        if (navAgent == null)
            navAgent = bodyTransform.GetComponent<NavMeshAgent>();

        ApplyPhase2AgentSettings();

        if (showDebugLogs)
        {
            Debug.Log($"🏹 [BossRangedModule] Initialize -> Player={(player != null ? player.name : "NULL")}, " +
                      $"Body={bodyTransform.name}, Agent={(navAgent != null ? "OK" : "NULL")}");
        }
    }

    public void SetActive(bool active)
    {
        isActive = active;

        if (!isActive)
        {
            ForceStopAllBehaviors("Module Disabled");
        }
        else
        {
            ApplyPhase2AgentSettings();
            SetState(RangedState.Attacking, "Module Enabled");
        }

        if (showDebugLogs)
            Debug.Log($"🏹 [BossRangedModule] SetActive({active})");
    }

    public void Tick()
    {
        if (stunController != null && stunController.IsStunning)
        {
            StopForStun();
            return;
        }
        if (!isActive) return;

        // 动态生成情况下：玩家可能晚于boss出现
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null) return;

        if (navAgent == null || bodyTransform == null)
        {
            AutoWire(includeInactive: true);
            if (navAgent == null || bodyTransform == null) return;
        }

        float dist = Vector3.Distance(bodyTransform.position, player.position);
        
        // 添加距离调试
        if (showDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"📏 [BossRangedModule] 与玩家距离: {dist:F2}, " +
                    $"撤退触发距离={retreatTriggerRange}, " +
                    $"安全距离={safeDistance}, " +
                    $"当前状态={state}");
        }

        // 状态切换规则
        if (dist <= retreatTriggerRange)
        {
            // 玩家靠近，进入撤退状态
            if (state != RangedState.Retreating)
            {
                SetState(RangedState.Retreating, $"玩家靠近: {dist:F2} <= {retreatTriggerRange}");
                
                // 立即计算一次撤退路径
                UpdateRetreatDestination();
            }
        }
        else if (dist > safeDistance && state == RangedState.Retreating)
        {
            // 玩家远离到安全距离，回到攻击状态
            SetState(RangedState.Attacking, $"安全距离: {dist:F2} > {safeDistance}");
        }
        else if (dist >= safeDistance && state == RangedState.Attacking)
        {
            // 玩家在安全距离外，保持攻击状态
            if (rotateToPlayerWhileAttacking)
                RotateBodyToPlayer();
        }

        // 状态执行
        switch (state)
        {
            case RangedState.Attacking:
                TickAttacking(dist);
                break;

            case RangedState.Retreating:
                TickRetreating(dist);
                break;
        }

        // 每帧同步 Animator speed 参数（攻击静止时为 0，撤退移动时 > 0）
        UpdateAnimatorSpeed();
    }

    public void ForceStopAllBehaviors(string reason = "")
    {
        if (showDebugLogs)
            Debug.Log($"🛑 [BossRangedModule] ForceStopAllBehaviors: {reason}");

        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        attackTimer = 0f;
        retreatStateTimer = 0f;
        consecutiveSampleFailures = 0;

        // 强制将所有动画参数归零，确保动画回到待机
        if (animator != null)
        {
            animator.SetFloat(speedParam, 0f);
            animator.SetBool(isMovingParam, false);
            animator.SetBool(isRetreatingParam, false);
        }
        
        // ✅ 修复：不要重置 hasTriggeredStuckEvent！让它保持 true 直到状态改变
        // hasTriggeredStuckEvent = false;  // 已删除这行
        
        // ✅ 修复：同时重置状态为攻击状态，避免继续卡住检测循环
        if (state == RangedState.Retreating)
        {
            SetState(RangedState.Attacking, "ForceStopAllBehaviors");
        }
    }

    #endregion

    #region Internal

    private void AutoWire(bool includeInactive)
    {
        if (bodyTransform == null)
            bodyTransform = transform.root;

        if (navAgent == null && bodyTransform != null)
            navAgent = bodyTransform.GetComponent<NavMeshAgent>();

        if (firePoint == null)
            firePoint = GetComponentInChildren<Transform>(includeInactive) != null ? firePoint : null;
    }

    private void ApplyPhase2AgentSettings()
    {
        if (navAgent == null) return;

        // 恢复 agent 驱动 Transform
        navAgent.enabled = true;
        navAgent.updatePosition = false;
        navAgent.updateRotation = false;

        navAgent.isStopped = false;

        navAgent.speed = phase2MoveSpeed;
        navAgent.acceleration = phase2Acceleration;
        navAgent.angularSpeed = phase2AngularSpeed;
        navAgent.stoppingDistance = phase2StoppingDistance;
    }

    private void SetState(RangedState newState, string reason = "")
    {
        if (state == newState) return;

        if (showDebugLogs)
            Debug.Log($"🔄 [BossRangedModule] State {state} -> {newState} ({reason})");

        // Exit old state
        if (state == RangedState.Retreating)
        {
            retreatStateTimer = 0f;
            consecutiveSampleFailures = 0;
            // ✅ 修复：只有在离开Retreating状态时才重置卡住标志
            hasTriggeredStuckEvent = false;
        }

        state = newState;

        // Enter new state
        if (state == RangedState.Attacking)
        {
            if (navAgent != null)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
            }
            attackTimer = 0f;
        }
        else if (state == RangedState.Retreating)
        {
            retreatEnterPos = bodyTransform.position;
            retreatStateTimer = 0f;
            consecutiveSampleFailures = 0;
            hasTriggeredStuckEvent = false; // 进入Retreating时重置

            if (navAgent != null)
            {
                navAgent.enabled = true;
                navAgent.updatePosition = false;
                navAgent.updateRotation = false;
                navAgent.isStopped = false;
            }

            // 立刻算一次撤退目标
            UpdateRetreatDestination();
        }
    }

    private void TickAttacking(float distToPlayer)
    {
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        if (rotateToPlayerWhileAttacking)
            RotateBodyToPlayer();

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            FireProjectile();
        }
    }

    private void TickRetreating(float distToPlayer)
    {
        retreatStateTimer += Time.deltaTime;
        
        // 确保组件可用
        if (characterController == null)
        {
            characterController = bodyTransform.GetComponent<CharacterController>();
        }
        
        // 检查NavMeshAgent状态（但使用CharacterController移动）
        if (navAgent != null)
        {
            // 检查是否在NavMesh上，如果不在则尝试重新定位
            if (!navAgent.isOnNavMesh)
            {
                if (showDebugLogs && Time.frameCount % 60 == 0)
                    Debug.LogWarning($"⚠️ [BossRangedModule] NavAgent不在NavMesh上，尝试重新定位");
                
                NavMeshHit hit;
                if (NavMesh.SamplePosition(bodyTransform.position, out hit, 10.0f, NavMesh.AllAreas))
                {
                    navAgent.Warp(hit.position);
                    // 同时移动CharacterController
                    if (characterController != null)
                        characterController.transform.position = hit.position;
                    
                    if (showDebugLogs)
                        Debug.Log($"📍 [BossRangedModule] 重新定位到NavMesh: {hit.position}");
                }
            }
            
            // 更新路径
            if (Time.frameCount % 15 == 0)
            {
                UpdateRetreatDestination();
            }
            
            // 使用CharacterController执行移动（模仿一阶段）
            if (navAgent.hasPath && !navAgent.isStopped)
            {
                Vector3 desiredVelocity = navAgent.desiredVelocity;
                
                if (desiredVelocity.magnitude > 0.1f && characterController != null)
                {
                    // 关键：使用CharacterController移动
                    characterController.Move(desiredVelocity.normalized * phase2MoveSpeed * Time.deltaTime);
                    
                    // 同步NavAgent的速度（可选）
                    navAgent.velocity = characterController.velocity;
                    
                    // 手动旋转（模仿一阶段的FaceTarget）
                    RotateBodyToMovementDirection(desiredVelocity);
                    
                    if (showDebugLogs && Time.frameCount % 30 == 0)
                    {
                        Debug.Log($"🚶 [BossRangedModule] 混合移动: " +
                                $"速度={desiredVelocity.magnitude:F2}, " +
                                $"移动方向={desiredVelocity.normalized}");
                    }
                }
            }
        }
        
        // 详细的调试信息
        if (showDebugLogs && Time.frameCount % 30 == 0)
        {
            bool hasPath = navAgent != null && navAgent.hasPath;
            float remainingDist = hasPath ? navAgent.remainingDistance : 0;
            Vector3 agentVelocity = characterController != null ? characterController.velocity : Vector3.zero;
            
            Debug.Log($"📊 [BossRangedModule] 撤退状态: " +
                    $"时间={retreatStateTimer:F2}, " +
                    $"是否有路径={hasPath}, " +
                    $"剩余距离={remainingDist:F2}, " +
                    $"实际速度={agentVelocity.magnitude:F2}, " +
                    $"连续Sample失败={consecutiveSampleFailures}");
        }
        
        // 卡住检测
        CheckIfStuck();
    }

    /// <summary>
    /// 卡住检测：使用双重检测机制
    /// </summary>
    private void CheckIfStuck()
    {
        // 防止重复触发
        if (hasTriggeredStuckEvent) 
        {
            // ✅ 修复：如果已经触发过卡住事件，确保状态正确
            if (state != RangedState.Retreating)
            {
                hasTriggeredStuckEvent = false; // 不在Retreating状态时重置
            }
            return;
        }
        
        // 延迟检测，避免刚进入撤退就误判
        if (retreatStateTimer < retreatCheckDelay) return;
        
        // 检测1：物理卡住（长时间几乎不动）
        float moved = Vector3.Distance(bodyTransform.position, retreatEnterPos);
        bool physicallyStuck = retreatStateTimer >= physicalStuckTime && moved < physicalStuckDistance;
        
        // 检测2：逻辑卡住（连续找不到有效撤退路径）
        bool logicallyStuck = consecutiveSampleFailures >= maxConsecutiveSampleFailures;
        
        if (showDebugLogs && (logicallyStuck || physicallyStuck))
        {
            Debug.LogWarning($"🔍 [BossRangedModule] 卡住检测: " +
                    $"物理卡住={physicallyStuck} (移动={moved:F2}m, 阈值={physicalStuckDistance}m, 时间={retreatStateTimer:F2}s), " +
                    $"逻辑卡住={logicallyStuck} (连续失败={consecutiveSampleFailures}/{maxConsecutiveSampleFailures})");
        }
        
        // 触发条件：逻辑卡住 OR 物理卡住
        if (logicallyStuck || physicallyStuck)
        {
            // 立即切换为攻击状态，停止撤退行为
            SetState(RangedState.Attacking, "Stuck Detected - Transition to Attacking");

            string reason = logicallyStuck ? 
                $"逻辑卡住: 连续Sample失败{consecutiveSampleFailures}次 >= {maxConsecutiveSampleFailures}" :
                $"物理卡住: 移动={moved:F2}m < {physicalStuckDistance}m, 时间={retreatStateTimer:F2}s >= {physicalStuckTime}s";
            
            if (showDebugLogs)
                Debug.LogWarning($"🧯 [BossRangedModule] 检测到撤退卡住，触发传送: {reason}");
            
            // 标记已触发，防止重复
            hasTriggeredStuckEvent = true;
            
            // 触发事件，并检查订阅者
            if (OnRetreatStuck != null)
            {
                if (showDebugLogs)
                {
                    int subscriberCount = OnRetreatStuck.GetInvocationList().Length;
                    Debug.Log($"🧯 [BossRangedModule] 触发 OnRetreatStuck 事件，订阅者数量: {subscriberCount}");
                }
                
                OnRetreatStuck.Invoke();
            }
            else
            {
                if (showDebugLogs)
                    Debug.LogError($"❌ [BossRangedModule] OnRetreatStuck 事件无订阅者!");
            }
            
            // ✅ 修复：重置检测计数器
            consecutiveSampleFailures = 0;
            retreatStateTimer = 0f;
        }
    }

    private void RotateBodyToMovementDirection(Vector3 movementDirection)
    {
        if (movementDirection.sqrMagnitude < 0.001f) return;
        
        movementDirection.y = 0f;
        movementDirection.Normalize();
        
        Quaternion targetRot = Quaternion.LookRotation(movementDirection, Vector3.up);
        bodyTransform.rotation = Quaternion.Slerp(bodyTransform.rotation, targetRot, Time.deltaTime * 8f);
    }

    private void UpdateRetreatDestination()
    {
        if (navAgent == null || player == null) return;

        Vector3 away = bodyTransform.position - player.position;
        away.y = 0f;

        if (away.sqrMagnitude < 0.001f)
            away = bodyTransform.forward;

        away.Normalize();

        Vector3 desired = bodyTransform.position + away * retreatDistance;

        // 添加调试信息
        if (showDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"🏃 [BossRangedModule] 撤退计算: 自身位置={bodyTransform.position}, " +
                    $"玩家位置={player.position}, 远离方向={away}, " +
                    $"期望目标={desired}");
        }

        // 确保目标点在 NavMesh 上
        bool sampleSuccess = NavMesh.SamplePosition(desired, out NavMeshHit hit, 4.0f, NavMesh.AllAreas);
        
        if (sampleSuccess)
        {
            // 成功找到路径，重置失败计数
            consecutiveSampleFailures = 0;
            
            navAgent.SetDestination(hit.position);
            
            if (showDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"📍 [BossRangedModule] 设置NavMesh目标: {hit.position}, " +
                        $"路径状态={navAgent.pathStatus}, " +
                        $"剩余距离={navAgent.remainingDistance}");
            }
        }
        else
        {
            // Sample失败，增加失败计数
            consecutiveSampleFailures++;
            
            // 如果 sample 失败，也设置一个近一点的目标（避免 agent 卡死）
            Vector3 fallback = bodyTransform.position + away * Mathf.Max(2f, retreatDistance * 0.5f);
            if (NavMesh.SamplePosition(fallback, out NavMeshHit hit2, 4.0f, NavMesh.AllAreas))
            {
                navAgent.SetDestination(hit2.position);
                
                if (showDebugLogs)
                    Debug.Log($"⚠️ [BossRangedModule] 撤退目标sample失败({consecutiveSampleFailures}次)，使用备用目标: {hit2.position}");
            }
            else
            {
                if (showDebugLogs)
                    Debug.LogWarning($"⚠️ [BossRangedModule] 无法找到任何撤退目标({consecutiveSampleFailures}次失败)，位置={bodyTransform.position}");
                
                // 如果完全找不到NavMesh点，尝试直接向远离方向移动一小段距离
                Vector3 simpleTarget = bodyTransform.position + away * 2f;
                navAgent.SetDestination(simpleTarget);
            }
        }
    }

    private void RotateBodyToPlayer()
    {
        Vector3 dir = (player.position - bodyTransform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        bodyTransform.rotation = Quaternion.Slerp(bodyTransform.rotation, targetRot, Time.deltaTime * 8f);
    }

    /// <summary>
    /// 根据 NavMeshAgent / CharacterController 的实际速度更新 Animator speed 参数。
    /// speed > 0 时自动触发行走动画，speed == 0 时回到待机动画。
    /// </summary>
    private void UpdateAnimatorSpeed()
    {
        if (animator == null) return;

        float speed = 0f;

        // 优先使用 CharacterController 的实际速度
        if (characterController != null)
        {
            Vector3 horizontalVel = characterController.velocity;
            horizontalVel.y = 0f;
            speed = horizontalVel.magnitude;
        }
        else if (navAgent != null && navAgent.isOnNavMesh)
        {
            // 回退：使用 NavMeshAgent 的速度
            Vector3 horizontalVel = navAgent.velocity;
            horizontalVel.y = 0f;
            speed = horizontalVel.magnitude;
        }

        bool isMoving = speed > 0.01f;

        animator.SetFloat(speedParam, speed);
        animator.SetBool(isMovingParam, isMoving);
        animator.SetBool(isRetreatingParam, state == RangedState.Retreating);
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("⚠️ [BossRangedModule] projectilePrefab 未设置，无法射击");
            return;
        }

        if (firePoint == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("⚠️ [BossRangedModule] firePoint 未设置，无法射击");
            return;
        }

        Vector3 spawnPos = firePoint.position;
        Vector3 dir = (player.position - spawnPos);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
            dir = firePoint.forward;

        dir.Normalize();

        // 计算目标位置（玩家当前位置）
        Vector3 targetPos = player.position;
        targetPos.y = spawnPos.y; // 保持与发射点相同高度

        // 实例化射弹
        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(dir, Vector3.up));

        // 获取射弹控制器并初始化
        ProjectileController projectileController = proj.GetComponent<ProjectileController>();
        if (projectileController != null)
        {
            // 调用InitializeProjectile方法初始化射弹
            projectileController.InitializeProjectile(targetPos, projectileSpeed, bodyTransform.gameObject);
            
            if (showDebugLogs)
                Debug.Log($"💥 [BossRangedModule] FireProjectile -> {proj.name} 目标: {targetPos}, 速度: {projectileSpeed}");
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning($"⚠️ [BossRangedModule] 射弹没有找到ProjectileController组件: {proj.name}");
        }
    }

    private void StopForStun()
    {
        // 停止撤退移动
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        // 清掉射击计时，避免刚眩晕结束立刻补一发
        attackTimer = 0f;
    }
    #endregion
}