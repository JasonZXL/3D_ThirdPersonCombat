using UnityEngine;
using UnityEngine.AI;

public class ChasingEnemy2 : BaseEnemy
{
    [Header("追击设置")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float turnSpeed = 3f;
    
    [Header("攻击设置")]
    [SerializeField] private float attackDuration = 1f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private bool useBuiltInAttack = true; // 🔧 新增：是否使用内置攻击系统
    
    [Header("眩晕设置")]
    [SerializeField] private float stunDuration = 3f;
    [SerializeField] private bool showStunDebug = true;
    
    [Header("击退设置")]
    [SerializeField] private bool stunOnKnockback = true;
    
    [Header("自动颜色转换")]
    [SerializeField] private bool useAutoColorChange = true;
    
    [Header("动画设置")]
    [SerializeField] private Animator animator;
    
    private NavMeshAgent navAgent;
    private Transform player;
    private Vector3 desVelocity;
    private CharacterController charControl;
    private KnockbackSystem knockbackSystem;
    private ColorAutoChangeBehavior colorAutoChange;
    
    // 攻击相关变量
    private bool isAttacking = false;
    private float attackTimer = 0f;
    private float cooldownTimer = 0f;
    private bool canAttack = true;
    
    // 移动相关变量
    private Vector3 lastPosition;
    private float currentSpeed = 0f;
    private bool isMoving = false;
    
    // 眩晕相关变量
    private bool isStunning = false;
    private float stunTimer = 0f;
    private Vector3 stunStartPosition;
    private Quaternion stunStartRotation;
    private bool stunJustEnded = false; // 🔧 新增：标记眩晕刚结束，防止重复进入
    
    // 动画参数名
    private const string IS_ATTACKING_BOOL = "isAttacking";
    private const string IS_MOVING_BOOL = "isMoving";
    private const string SPEED_FLOAT = "speed";
    private const string IS_STUNNING_BOOL = "isStunning";
    
    private void Awake()
    {
        // 🔧 注意：不能在这里调用base.Awake()，因为这不是override方法
        
        navAgent = GetComponent<NavMeshAgent>();
        charControl = GetComponent<CharacterController>();
        knockbackSystem = GetComponent<KnockbackSystem>();
        colorAutoChange = GetComponent<ColorAutoChangeBehavior>();
        
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        }
        
        // 🔧 配置NavMeshAgent
        navAgent.speed = moveSpeed;
        navAgent.acceleration = 8f; // 加速度
        navAgent.angularSpeed = 120f; // 旋转速度
        navAgent.stoppingDistance = attackRange - 0.5f; // 停止距离
        
        lastPosition = transform.position;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        // 🔧 禁用EnemyAttackDetector，使用内置攻击系统
        if (useBuiltInAttack)
        {
            EnemyAttackDetector externalDetector = GetComponent<EnemyAttackDetector>();
            if (externalDetector != null)
            {
                externalDetector.enabled = false;
                Debug.Log($"🔧 禁用外部EnemyAttackDetector，使用ChasingEnemy2内置攻击系统");
            }
        }
        
        Debug.Log($"🏃 追击敌人初始化: {gameObject.name}, 速度: {moveSpeed}, 自动颜色转换: {useAutoColorChange}");
    }
    
    void Start()
    {
        if (player != null && navAgent.isOnNavMesh)
        {
            navAgent.destination = player.position;
        }
    }
    
    private void Update()
    {
        UpdateTimers();
        CalculateMovementSpeed();

        // 🔧 优先级1：检查击退状态
        bool isKnockbackActive = knockbackSystem != null && knockbackSystem.IsKnockbackActive;
        
        if (isKnockbackActive)
        {
            HandleKnockbackState();
            return;
        }
        
        // 🔧 优先级2：检查眩晕状态
        if (isStunning)
        {
            HandleStunState();
            return;
        }
        
        // 🔧 重置眩晕刚结束标记
        if (stunJustEnded)
        {
            stunJustEnded = false;
        }
        
        // 🔧 优先级3：检查攻击状态
        if (isAttacking)
        {
            HandleAttackState();
            return;
        }
        
        // 查找玩家
        if (player == null) 
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null) 
            {
                SetMovingAnimation(false);
                return;
            }
        }
        
        // 🔧 优先级4：检查是否应该攻击
        if (useBuiltInAttack && CanPerformAttack())
        {
            StartAttack();
            return;
        }
        
        // 🔧 优先级5：正常追击
        PerformChasing();
    }
    
    /// <summary>
    /// 处理击退状态
    /// </summary>
    private void HandleKnockbackState()
    {
        if (isAttacking)
        {
            isAttacking = false;
            attackTimer = 0f;
        }
        
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
        }
        
        if (charControl != null)
        {
            charControl.Move(Vector3.zero);
        }
        
        SetMovingAnimation(false);
        SetAttackAnimation(false);
        SetStunAnimation(true);
    }
    
    /// <summary>
    /// 🔧 修复：处理眩晕状态
    /// </summary>
    private void HandleStunState()
    {
        // 确保所有行为被禁用
        if (isAttacking)
        {
            isAttacking = false;
            attackTimer = 0f;
        }
        
        // 完全停止NavMeshAgent
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
        }
        
        // 停止CharacterController移动
        if (charControl != null)
        {
            charControl.Move(Vector3.zero);
        }
        
        // 记录初始位置（只在第一帧）
        if (stunTimer < Time.deltaTime)
        {
            stunStartPosition = transform.position;
            stunStartRotation = transform.rotation;
            
            if (showStunDebug)
            {
                Debug.Log($"🔒 锁定眩晕位置: {stunStartPosition}");
            }
        }
        
        // 强制保持位置和旋转
        transform.position = stunStartPosition;
        transform.rotation = stunStartRotation;
        
        // 更新眩晕计时器
        stunTimer += Time.deltaTime;
        
        // 检查眩晕是否结束
        if (stunTimer >= stunDuration)
        {
            EndStun();
            return;
        }
        
        // 更新动画
        SetStunAnimation(true);
        SetMovingAnimation(false);
        SetAttackAnimation(false);
        
        if (showStunDebug && stunTimer % 0.5f < Time.deltaTime)
        {
            Debug.Log($"🌀 敌人眩晕中: {gameObject.name}, 剩余时间: {(stunDuration - stunTimer):F1}秒, 位置: {transform.position}");
        }
    }
    
    /// <summary>
    /// 处理攻击状态
    /// </summary>
    private void HandleAttackState()
    {
        // 攻击期间停止移动
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
            navAgent.updatePosition = false;
        }
        
        // 攻击期间保持面向玩家
        if (player != null)
        {
            Vector3 lookPos = player.position - transform.position;
            lookPos.y = 0;
            if (lookPos != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookPos);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);
            }
        }
        
        SetMovingAnimation(false);
        SetAttackAnimation(true);
    }
    
    /// <summary>
    /// 执行追击行为
    /// </summary>
    private void PerformChasing()
    {
        // 🔧 关键修复：确保NavMeshAgent完全启用
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            // 确保NavMeshAgent不是停止状态
            if (navAgent.isStopped)
            {
                navAgent.isStopped = false;
                if (showStunDebug)
                {
                    Debug.Log($"🔧 NavAgent从停止状态恢复: {gameObject.name}");
                }
            }
            
            // 🔧 关键：先启用updatePosition，再设置目标
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
            
            // 更新目标
            if (player != null && navAgent.isOnNavMesh)
            {
                // 🔧 检查是否需要重新计算路径
                if (!navAgent.hasPath || navAgent.remainingDistance < 0.5f)
                {
                    navAgent.SetDestination(player.position);
                }
                else
                {
                    // 定期更新目标位置
                    float distanceToDestination = Vector3.Distance(navAgent.destination, player.position);
                    if (distanceToDestination > 1f) // 如果玩家移动超过1米，更新目标
                    {
                        navAgent.SetDestination(player.position);
                    }
                }
            }
        }
        
        // 处理旋转
        if (player != null)
        {
            Vector3 lookPos = player.position - transform.position;
            lookPos.y = 0;
            if (lookPos != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookPos);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);
            }
        }
        
        // 如果NavMeshAgent有路径且未到达，使用CharacterController移动
        if (navAgent != null && navAgent.hasPath && !navAgent.isStopped)
        {
            desVelocity = navAgent.desiredVelocity;
            
            if (desVelocity.magnitude > 0.1f) // 🔧 只有在有明确移动方向时才移动
            {
                charControl.Move(desVelocity.normalized * moveSpeed * Time.deltaTime);
                navAgent.velocity = charControl.velocity;
                
                SetMovingAnimation(true);
            }
            else
            {
                SetMovingAnimation(false);
            }
        }
        else
        {
            SetMovingAnimation(false);
        }
    }
    
    /// <summary>
    /// 🔧 修复：开始眩晕状态
    /// </summary>
    public void StartStun(float customDuration = 0f)
    {
        // 🔧 关键修复：如果刚结束眩晕，忽略新的眩晕请求
        if (stunJustEnded)
        {
            if (showStunDebug)
            {
                Debug.Log($"⏳ 眩晕刚结束，忽略新的眩晕请求: {gameObject.name}");
            }
            return;
        }
        
        if (isStunning)
        {
            if (showStunDebug)
            {
                Debug.Log($"⚠️ 敌人已在眩晕中，忽略重复调用: {gameObject.name}");
            }
            return;
        }
        
        isStunning = true;
        stunTimer = 0f;
        
        stunStartPosition = transform.position;
        stunStartRotation = transform.rotation;
        
        if (customDuration > 0)
        {
            stunDuration = customDuration;
        }
        
        // 停止当前所有行为
        if (isAttacking)
        {
            isAttacking = false;
            attackTimer = 0f;
        }
        
        // 完全停止移动
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
        }
        
        if (charControl != null)
        {
            charControl.Move(Vector3.zero);
        }
        
        // 设置动画
        SetStunAnimation(true);
        SetMovingAnimation(false);
        SetAttackAnimation(false);
        
        if (showStunDebug)
        {
            Debug.Log($"🌀 敌人进入眩晕状态: {gameObject.name}, 持续时间: {stunDuration}秒, 位置: {stunStartPosition}");
        }
    }
    
    /// <summary>
    /// 🔧 修复：结束眩晕状态
    /// </summary>
    public void EndStun()
    {
        if (!isStunning)
        {
            if (showStunDebug)
            {
                Debug.Log($"⚠️ 敌人不在眩晕中，忽略EndStun: {gameObject.name}");
            }
            return;
        }
        
        // 🔧 关键修复：先重置所有状态
        isStunning = false;
        stunTimer = 0f;
        stunJustEnded = true; // 🔧 标记眩晕刚结束
        
        // 🔧 重置动画（必须在恢复NavAgent之前）
        SetStunAnimation(false);
        
        // 🔧 恢复NavMeshAgent
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            // 先检查是否在NavMesh上
            if (!navAgent.isOnNavMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
                {
                    navAgent.Warp(hit.position);
                    transform.position = hit.position;
                    
                    if (showStunDebug)
                    {
                        Debug.Log($"📍 敌人位置已调整到NavMesh: {hit.position}");
                    }
                }
                else
                {
                    Debug.LogWarning($"⚠️ NavMeshAgent不在NavMesh上且找不到附近的NavMesh: {gameObject.name}");
                    // 即使找不到NavMesh，也要尝试恢复
                }
            }
            
            // 🔧 关键修复：恢复NavMeshAgent的所有设置
            navAgent.enabled = true;
            navAgent.isStopped = false;
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            navAgent.velocity = Vector3.zero;
            
            // 🔧 强制重新计算路径
            if (player != null && navAgent.isOnNavMesh)
            {
                navAgent.ResetPath(); // 先清除旧路径
                navAgent.SetDestination(player.position);
                
                if (showStunDebug)
                {
                    Debug.Log($"🎯 重新设置追击目标: {player.position}, 当前位置: {transform.position}, 距离: {Vector3.Distance(transform.position, player.position):F2}");
                }
            }
        }
        
        if (showStunDebug)
        {
            Debug.Log($"✅ 敌人眩晕状态结束: {gameObject.name}，恢复追击");
        }
    }
    
    /// <summary>
    /// 强制结束眩晕（用于被击杀等情况）
    /// </summary>
    public void ForceEndStun()
    {
        isStunning = false;
        stunTimer = 0f;
        stunJustEnded = false;
        
        if (navAgent != null)
        {
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            navAgent.isStopped = false;
        }
        
        SetStunAnimation(false);
    }

    /// <summary>
    /// 击退开始回调
    /// </summary>
    public void OnKnockbackStart()
    {
        if (showStunDebug)
        {
            Debug.Log($"💥 敌人开始被击退: {gameObject.name}");
        }
        
        // 击退开始时，如果设置了stunOnKnockback，则进入眩晕
        if (stunOnKnockback && !isStunning)
        {
            StartStun();
        }
    }

    /// <summary>
    /// 击退结束回调
    /// </summary>
    public void OnKnockbackEnd()
    {
        if (showStunDebug)
        {
            Debug.Log($"💨 敌人击退结束: {gameObject.name}, 眩晕状态: {isStunning}");
        }
        
        // 如果不在眩晕状态，恢复NavAgent
        if (!isStunning && navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            navAgent.isStopped = false;
            
            if (player != null && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(player.position);
            }
            
            if (showStunDebug)
            {
                Debug.Log($"✅ 击退结束，NavAgent已恢复: {gameObject.name}");
            }
        }
    }
    
    private void CalculateMovementSpeed()
    {
        Vector3 currentPosition = transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        currentSpeed = distanceMoved / Time.deltaTime;
        lastPosition = currentPosition;
    }
    
    private void UpdateTimers()
    {
        if (isAttacking)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackDuration)
            {
                EndAttack();
            }
        }
        
        if (!canAttack)
        {
            cooldownTimer += Time.deltaTime;
            if (cooldownTimer >= attackCooldown)
            {
                canAttack = true;
                cooldownTimer = 0f;
            }
        }
    }
    
    private bool CanPerformAttack()
    {
        if (player == null || !canAttack || isStunning) return false;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        return distanceToPlayer <= attackRange;
    }
    
    private void StartAttack()
    {
        isAttacking = true;
        attackTimer = 0f;
        canAttack = false;
        
        // 停止移动
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
            navAgent.updatePosition = false;
        }
        
        SetAttackAnimation(true);
        
        // 🔧 触发攻击事件
        if (player != null)
        {
            ColorEventBus.PublishEnemyAttack(gameObject, player.gameObject);
        }
        
        Debug.Log($"⚔️ 敌人开始攻击，将持续 {attackDuration} 秒");
    }
    
    private void EndAttack()
    {
        isAttacking = false;
        attackTimer = 0f;
        
        // 恢复移动
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = false;
            navAgent.updatePosition = true;
        }
        
        SetAttackAnimation(false);
        
        Debug.Log("✅ 敌人攻击结束，恢复追击");
    }
    
    private void SetAttackAnimation(bool attacking)
    {
        if (animator == null) return;
        animator.SetBool(IS_ATTACKING_BOOL, attacking);
    }
    
    private void SetMovingAnimation(bool moving)
    {
        if (animator == null) return;
        
        isMoving = moving;
        animator.SetBool(IS_MOVING_BOOL, moving);
        
        float normalizedSpeed = moving ? Mathf.Clamp01(currentSpeed / moveSpeed) : 0f;
        animator.SetFloat(SPEED_FLOAT, normalizedSpeed);
    }
    
    private void SetStunAnimation(bool stunning)
    {
        if (animator == null) return;
        animator.SetBool(IS_STUNNING_BOOL, stunning);
    }
    
    /// <summary>
    /// 🔧 颜色交互事件处理（重写BaseEnemy的方法）
    /// </summary>
    public override void OnColorInteraction(ColorInteractionEvent interaction)
    {
        // 🔧 先调用基类方法（如果有的话）
        // base.OnColorInteraction(interaction);
        
        if (colorAutoChange != null)
        {
            colorAutoChange.HandleEnemyColorInteraction(interaction);
        }
        
        // 🔧 情况1：处理同色攻击 - 敌人攻击同色玩家时，自身进入眩晕状态
        if (interaction.Type == ColorInteractionType.EnemyAttackPlayer)
        {
            // 只有当自己是攻击者时才处理
            if (interaction.Source == gameObject)
            {
                ColorComponent sourceColor = interaction.Source?.GetComponent<ColorComponent>();
                ColorComponent targetColor = interaction.Target?.GetComponent<ColorComponent>();
                
                if (sourceColor != null && targetColor != null && sourceColor.IsSameColor(targetColor))
                {
                    StartStun();
                }
            }
        }
        
        // 🔧 情况2：当敌人被玩家攻击时
        if (interaction.Type == ColorInteractionType.PlayerAttackEnemy)
        {
            // 只有当自己是被攻击者时才处理
            if (interaction.Target == gameObject)
            {
                if (showStunDebug)
                {
                    ColorComponent playerColor = interaction.Source?.GetComponent<ColorComponent>();
                    ColorComponent enemyColor = interaction.Target?.GetComponent<ColorComponent>();
                    bool isOppositeColor = playerColor != null && enemyColor != null && !playerColor.IsSameColor(enemyColor);
                }
            }
        }
    }
    
    public void ForceRecalculatePath()
    {
        if (player != null && navAgent != null && !isStunning && navAgent.isOnNavMesh)
        {
            navAgent.SetDestination(player.position);
            navAgent.isStopped = false;
        }
    }
    
    public void SetAutoColorChangeEnabled(bool enabled)
    {
        if (colorAutoChange != null)
        {
            colorAutoChange.SetEnabled(enabled);
        }
    }
    
    // 属性访问器
    public bool IsAttacking { get { return isAttacking; } }
    public bool IsMoving { get { return isMoving; } }
    public bool IsStunning { get { return isStunning; } }
    
    // 运行时调整方法
    public void SetAttackDuration(float duration) { attackDuration = duration; }
    public void SetAttackCooldown(float cooldown) { attackCooldown = cooldown; }
    public void SetStunDuration(float duration) { stunDuration = duration; }
    public void SetAnimator(Animator newAnimator) { animator = newAnimator; }
    public void SetUseBuiltInAttack(bool use) { useBuiltInAttack = use; }
    
    // 调试方法
    [ContextMenu("手动触发眩晕")]
    public void DebugTriggerStun() { StartStun(); }
    
    [ContextMenu("手动结束眩晕")]
    public void DebugEndStun() { EndStun(); }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        if (navAgent != null && navAgent.hasPath)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < navAgent.path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(navAgent.path.corners[i], navAgent.path.corners[i + 1]);
            }
        }
        
        if (isAttacking)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange * 1.2f);
        }
        
        if (isStunning)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 1.5f);
            
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(stunStartPosition, 0.2f);
            Gizmos.DrawLine(transform.position, stunStartPosition);
            
            #if UNITY_EDITOR
            Vector3 iconPos = transform.position + Vector3.up * 2f;
            UnityEditor.Handles.Label(iconPos, $"💫 STUNNED\n{(stunDuration - stunTimer):F1}s");
            #endif
        }
    }
}