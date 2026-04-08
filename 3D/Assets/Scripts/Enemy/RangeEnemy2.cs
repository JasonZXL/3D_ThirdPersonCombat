using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 远程敌人行为逻辑：追击 → 攻击 ↔ 撤离
/// </summary>
public class RangeEnemy2 : BaseEnemy, IKnockbackReceiver
{
    #region 状态枚举
    public enum RangeEnemyState
    {
        Idle,       // 空闲
        Chasing,    // 追击
        Attacking,  // 攻击
        Retreating  // 撤离
    }
    #endregion

    #region 范围设置
    [Header("范围设置")]
    [SerializeField] private float attackRange = 10f;        // 攻击范围
    [SerializeField] private float retreatRange = 3f;        // 撤离范围(必须小于攻击范围)
    #endregion

    #region 移动设置
    [Header("移动设置")]
    [SerializeField] private float chaseSpeed = 4f;          // 追击速度
    [SerializeField] private float retreatSpeed = 5f;        // 撤离速度
    [SerializeField] private float rotationSpeed = 5f;       // 旋转速度
    [SerializeField] private float stopDistance = 8f;        // 攻击停止距离
    #endregion

    #region 攻击设置
    [Header("攻击设置")]
    [SerializeField] private float attackInterval = 3f;      // 攻击间隔
    [SerializeField] private GameObject projectilePrefab;    // 子弹预制体
    [SerializeField] private Transform firePoint;            // 射击位置
    [SerializeField] private float projectileSpeed = 10f;    // 子弹速度
    #endregion

    #region 调试设置
    [Header("调试设置")]
    [SerializeField] private bool showStateDebug = true;
    [SerializeField] private bool showRangeGizmos = true;
    [SerializeField] private Color attackRangeColor = new Color(1f, 0f, 0f, 0.1f);
    [SerializeField] private Color retreatRangeColor = new Color(1f, 1f, 0f, 0.2f);
    #endregion

    #region 内部变量
    private RangeEnemyState currentState = RangeEnemyState.Idle;
    private Transform player;
    private NavMeshAgent navAgent;
    private CharacterController charController;
    private KnockbackSystem knockbackSystem;
    private Animator animator;  // 动画控制器
    
    // 攻击相关 - 关键修复：攻击计时器独立于状态
    private float attackTimer = 0f;
    private float lastAttackTime = -999f;  // 上次攻击的时间
    
    // 移动状态追踪
    private Vector3 lastPosition;
    private float currentSpeed = 0f;
    private bool isMoving = false;
    
    // 撤离相关
    private Vector3 retreatTarget;
    private bool hasLineOfSight = false;
    private Vector3 lastRetreatPosition;  // 上次撤离时的位置
    private float retreatStuckTimer = 0f;
    private bool isRetreatStuck = false;  // 是否处于撤离卡住状态
    private const float RETREAT_STUCK_TIMEOUT = 2f;  // 撤离卡住超时时间
    private const float RETREAT_POSITION_EPSILON = 0.5f;  // 判定位置是否移动的阈值
    
    // 状态稳定性
    private float stateChangeTimer = 0f;
    private const float STATE_CHANGE_COOLDOWN = 0.2f;  // 状态切换冷却时间
    
    // 击退状态追踪
    private bool wasKnockbackActive = false;
    private RangeEnemyState stateBeforeKnockback = RangeEnemyState.Idle;
    
    // 动画状态标志
    private bool isRetreating = false;  // 是否正在撤离（触发后退动画）
    private bool isAttacking = false;   // 是否正在攻击（触发射击动画）
    #endregion

    #region Unity生命周期
    protected override void Awake()
    {
        base.Awake();
        
        InitializeComponents();
        FindPlayer();
        
        lastPosition = transform.position;
    }

    private void Start()
    {
        TransitionToState(RangeEnemyState.Idle);
        lastRetreatPosition = transform.position;
        retreatStuckTimer = 0f;
        isRetreatStuck = false;
    }

    private void Update()
    {
        // 检查是否处于击退状态
        if (knockbackSystem != null && knockbackSystem.IsKnockbackActive)
        {
            isMoving = false;
            
            // 记录击退前的状态
            if (!wasKnockbackActive)
            {
                wasKnockbackActive = true;
                stateBeforeKnockback = currentState;
                if (showStateDebug)
                    Debug.Log($"💨 远程敌人进入击退状态，保存状态: {stateBeforeKnockback}");
            }
            return;
        }
        
        // 检查是否刚从击退恢复
        if (wasKnockbackActive)
        {
            wasKnockbackActive = false;
            if (showStateDebug)
                Debug.Log($"✅ 远程敌人从击退恢复，之前状态: {stateBeforeKnockback}");
            
            // 击退结束后不立即切换状态，给一个缓冲时间
            stateChangeTimer = STATE_CHANGE_COOLDOWN * 2f;
        }
        
        if (player == null)
        {
            FindPlayer();
            if (player == null)
            {
                isMoving = false;
                return;
            }
        }

        // 更新状态切换冷却
        if (stateChangeTimer > 0f)
        {
            stateChangeTimer -= Time.deltaTime;
        }
        
        // 持续更新攻击计时器(不受状态影响)
        attackTimer += Time.deltaTime;
        
        // 更新移动速度
        CalculateMovementSpeed();
        
        // 更新状态机
        UpdateState();
    }

    private void FixedUpdate()
    {
        UpdateLineOfSight();
    }
    #endregion

    #region 初始化系统
    private void InitializeComponents()
    {
        // NavMeshAgent
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        }

        // 配置NavMeshAgent
        navAgent.speed = chaseSpeed;
        navAgent.stoppingDistance = stopDistance;
        navAgent.angularSpeed = rotationSpeed * 100f;
        navAgent.acceleration = 8f;
        navAgent.autoBraking = true;
        navAgent.updateRotation = false;
        navAgent.updatePosition = false;
        
        // CharacterController
        charController = GetComponent<CharacterController>();
        
        // KnockbackSystem
        knockbackSystem = GetComponent<KnockbackSystem>();
        
        // Animator
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        Debug.Log($"🎯 远程敌人初始化: {gameObject.name}, 攻击范围: {attackRange}, 撤离范围: {retreatRange}");
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            Debug.Log($"🎯 远程敌人找到玩家: {player.name}");
        }
    }
    #endregion

    #region IKnockbackReceiver 实现
    public void OnKnockbackStart()
    {
        if (showStateDebug)
            Debug.Log($"🚀 [RangeEnemy] 击退开始回调");
        
        // 击退开始时：停止所有移动
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }
        
        isMoving = false;
    }

    public void OnKnockbackEnd()
    {
        if (showStateDebug)
            Debug.Log($"🛑 [RangeEnemy] 击退结束回调");
        
        // 击退结束后：恢复NavMeshAgent，但不立即开始移动
        // 由Update中的状态机来决定下一步行动
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.nextPosition = transform.position;
        }
    }
    #endregion

    #region 状态机系统
    private void UpdateState()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // 添加状态切换冷却，防止频繁切换
        bool canChangeState = stateChangeTimer <= 0f;

        switch (currentState)
        {
            case RangeEnemyState.Idle:
                if (canChangeState)
                {
                    TransitionToState(RangeEnemyState.Chasing);
                }
                break;

            case RangeEnemyState.Chasing:
                if (canChangeState)
                {
                    if (distanceToPlayer <= attackRange && hasLineOfSight)
                    {
                        TransitionToState(RangeEnemyState.Attacking);
                    }
                }
                break;

            case RangeEnemyState.Attacking:
                if (canChangeState)
                {
                    // 如果处于卡住状态，只有当玩家距离改变显著时才能退出攻击状态
                    if (isRetreatStuck)
                    {
                        // 玩家远离了，可以尝试追击
                        if (distanceToPlayer > attackRange * 1.5f)
                        {
                            if (showStateDebug)
                                Debug.Log($"✅ 玩家远离，解除撤离卡住状态，转为追击");
                            isRetreatStuck = false;
                            retreatStuckTimer = 0f;
                            TransitionToState(RangeEnemyState.Chasing);
                        }
                        // 玩家换了角度接近，可以尝试重新撤离
                        else if (distanceToPlayer <= retreatRange)
                        {
                            if (showStateDebug)
                                Debug.Log($"✅ 玩家从新角度接近，解除撤离卡住状态，尝试重新撤离");
                            isRetreatStuck = false;
                            retreatStuckTimer = 0f;
                            TransitionToState(RangeEnemyState.Retreating);
                        }
                        // 否则保持在攻击状态
                        break;
                    }
                    
                    if (distanceToPlayer <= retreatRange)
                    {
                        // 玩家太近 → 撤离
                        TransitionToState(RangeEnemyState.Retreating);
                    }
                    else if (distanceToPlayer > attackRange * 1.2f || !hasLineOfSight)
                    {
                        // 超出攻击范围较多或失去视线 → 追击
                        TransitionToState(RangeEnemyState.Chasing);
                    }
                }
                break;

            case RangeEnemyState.Retreating:
                if (canChangeState)
                {
                    // 检查撤离是否卡住
                    if (CheckRetreatStuck())
                    {
                        if (showStateDebug)
                            Debug.LogWarning($"⚠️ 撤离位置未移动超过{RETREAT_STUCK_TIMEOUT}秒，强制切换到攻击状态并锁定");
                        isRetreatStuck = true;  // 标记为卡住状态
                        TransitionToState(RangeEnemyState.Attacking);
                        break;
                    }
                    
                    if (distanceToPlayer > retreatRange * 1.3f && distanceToPlayer <= attackRange && hasLineOfSight)
                    {
                        // 离开撤离范围但在攻击范围内 → 攻击
                        TransitionToState(RangeEnemyState.Attacking);
                    }
                    else if (distanceToPlayer > attackRange * 1.2f)
                    {
                        // 超出攻击范围 → 追击
                        TransitionToState(RangeEnemyState.Chasing);
                    }
                }
                break;
        }

        // 执行当前状态行为
        ExecuteStateBehavior();
    }

    private void TransitionToState(RangeEnemyState newState)
    {
        if (currentState == newState) return;

        // 退出当前状态
        ExitCurrentState();

        // 记录旧状态
        RangeEnemyState oldState = currentState;
        
        // 设置新状态
        currentState = newState;

        // 进入新状态
        EnterNewState(newState);
        
        // 设置状态切换冷却
        stateChangeTimer = STATE_CHANGE_COOLDOWN;

        if (showStateDebug)
        {
            Debug.Log($"🔄 远程敌人状态转换: {oldState} → {newState} (攻击冷却: {GetAttackCooldownRemaining():F1}s)");
        }
    }

    private void ExitCurrentState()
    {
        switch (currentState)
        {
            case RangeEnemyState.Chasing:
            case RangeEnemyState.Retreating:
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.isStopped = true;
                }
                break;

            case RangeEnemyState.Attacking:
                // 不再重置攻击计时器
                break;
        }
    }

    private void EnterNewState(RangeEnemyState newState)
    {
        // 确保NavMeshAgent处于正确状态
        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
        {
            if (showStateDebug)
                Debug.LogWarning($"⚠️ NavMeshAgent不可用，无法进入状态: {newState}");
            return;
        }

        switch (newState)
        {
            case RangeEnemyState.Idle:
                navAgent.isStopped = true;
                SetRetreatingAnimation(false);
                SetAttackingAnimation(false);
                break;

            case RangeEnemyState.Chasing:
                navAgent.speed = chaseSpeed;
                navAgent.isStopped = false;
                if (player != null)
                {
                    navAgent.SetDestination(player.position);
                }
                SetRetreatingAnimation(false);
                SetAttackingAnimation(false);
                break;

            case RangeEnemyState.Attacking:
                navAgent.isStopped = true;
                // 不再在进入攻击状态时重置计时器
                SetRetreatingAnimation(false);
                SetAttackingAnimation(true);
                break;

            case RangeEnemyState.Retreating:
                navAgent.speed = retreatSpeed;
                navAgent.isStopped = false;
                CalculateRetreatTarget();
                navAgent.SetDestination(retreatTarget);
                
                // 记录进入撤离状态时的位置，用于检测是否卡住
                lastRetreatPosition = transform.position;
                retreatStuckTimer = 0f;
                
                SetRetreatingAnimation(true);
                SetAttackingAnimation(false);
                
                if (showStateDebug)
                    Debug.Log($"🏃 进入撤离状态，目标: {retreatTarget}, 当前位置: {transform.position}");
                break;
        }
    }

    private void ExecuteStateBehavior()
    {
        switch (currentState)
        {
            case RangeEnemyState.Idle:
                ExecuteIdleBehavior();
                break;

            case RangeEnemyState.Chasing:
                ExecuteChasingBehavior();
                break;

            case RangeEnemyState.Attacking:
                ExecuteAttackingBehavior();
                break;

            case RangeEnemyState.Retreating:
                ExecuteRetreatingBehavior();
                break;
        }
    }
    #endregion

    #region 状态行为实现
    private void ExecuteIdleBehavior()
    {
        isMoving = false;
        RotateTowardsPlayer();
    }

    private void ExecuteChasingBehavior()
    {
        if (player == null || navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) return;

        // 更新目标位置
        navAgent.SetDestination(player.position);

        // 移动
        if (navAgent.hasPath && !navAgent.isStopped)
        {
            Vector3 desiredVelocity = navAgent.desiredVelocity;
            
            if (charController != null && charController.enabled)
            {
                charController.Move(desiredVelocity.normalized * navAgent.speed * Time.deltaTime);
                navAgent.velocity = charController.velocity;
            }
            else
            {
                transform.position += desiredVelocity.normalized * navAgent.speed * Time.deltaTime;
            }
            
            isMoving = desiredVelocity.magnitude > 0.1f;
        }
        else
        {
            isMoving = false;
        }

        // 面向玩家
        RotateTowardsPlayer();
    }

    private void ExecuteAttackingBehavior()
    {
        if (player == null) return;

        isMoving = false;
        
        // 面向玩家
        RotateTowardsPlayer();

        // 检查是否可以攻击(基于全局攻击计时器)
        if (CanAttackNow())
        {
            PerformAttack();
            lastAttackTime = Time.time;
            attackTimer = 0f;
        }
    }

    private void ExecuteRetreatingBehavior()
    {
        if (player == null || navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float distanceToTarget = Vector3.Distance(transform.position, retreatTarget);

        // 检测实际位置是否移动
        float distanceMoved = Vector3.Distance(transform.position, lastRetreatPosition);
        
        if (distanceMoved < RETREAT_POSITION_EPSILON)
        {
            // 位置几乎没变，累积卡住时间
            retreatStuckTimer += Time.deltaTime;
            
            if (showStateDebug && Time.frameCount % 60 == 0)
                Debug.LogWarning($"⚠️ 撤离中位置未移动 ({retreatStuckTimer:F1}s/{RETREAT_STUCK_TIMEOUT}s) - 移动距离: {distanceMoved:F2}m");
        }
        else
        {
            // 位置改变了，更新记录并重置计时器
            lastRetreatPosition = transform.position;
            retreatStuckTimer = 0f;
        }

        // 重新计算撤离目标的条件
        if (distanceToPlayer <= retreatRange * 1.1f || distanceToTarget < 1f)
        {
            CalculateRetreatTarget();
            navAgent.SetDestination(retreatTarget);
            
            if (showStateDebug && Time.frameCount % 60 == 0)
                Debug.Log($"🏃 更新撤离目标: {retreatTarget}");
        }

        // 移动
        if (navAgent.hasPath && !navAgent.isStopped)
        {
            Vector3 desiredVelocity = navAgent.desiredVelocity;
            
            if (charController != null && charController.enabled)
            {
                charController.Move(desiredVelocity.normalized * navAgent.speed * Time.deltaTime);
                navAgent.velocity = charController.velocity;
            }
            else
            {
                transform.position += desiredVelocity.normalized * navAgent.speed * Time.deltaTime;
            }
            
            isMoving = desiredVelocity.magnitude > 0.1f;
            
            if (showStateDebug && Time.frameCount % 120 == 0)
                Debug.Log($"🏃 撤离中 - 速度: {desiredVelocity.magnitude:F2}, 到玩家: {distanceToPlayer:F2}, 到目标: {distanceToTarget:F2}");
        }
        else
        {
            isMoving = false;
            
            if (showStateDebug && Time.frameCount % 60 == 0)
                Debug.LogWarning($"⚠️ 撤离状态但无路径或已停止");
        }

        // 面向撤离方向
        RotateAwayFromPlayer();
    }
    #endregion

    #region 攻击系统
    /// <summary>
    /// 检查是否可以攻击(独立于状态的冷却系统)
    /// </summary>
    private bool CanAttackNow()
    {
        // 必须在攻击状态
        if (currentState != RangeEnemyState.Attacking) return false;
        
        // 检查攻击间隔
        float timeSinceLastAttack = Time.time - lastAttackTime;
        return timeSinceLastAttack >= attackInterval;
    }
    
    /// <summary>
    /// 获取剩余攻击冷却时间
    /// </summary>
    private float GetAttackCooldownRemaining()
    {
        float timeSinceLastAttack = Time.time - lastAttackTime;
        return Mathf.Max(0f, attackInterval - timeSinceLastAttack);
    }

    private void PerformAttack()
    {
        if (projectilePrefab == null || firePoint == null || player == null)
        {
            Debug.LogWarning($"⚠️ 远程敌人无法攻击: 缺少预制体或射击点");
            return;
        }

        // 触发射击动画事件（如果有的话）
        TriggerShootAnimation();

        // 创建子弹
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        
        // 设置子弹的颜色(继承敌人的颜色)
        ColorComponent projectileColor = projectile.GetComponent<ColorComponent>();
        if (projectileColor != null && colorComponent != null)
        {
            projectileColor.CurrentColor = colorComponent.CurrentColor;
        }
        
        // 获取子弹的ProjectileController组件
        ProjectileController projectileController = projectile.GetComponent<ProjectileController>();
        
        if (projectileController != null)
        {
            projectileController.InitializeProjectile(
                player.position,
                projectileSpeed,
                this.gameObject
            );
        }

        if (showStateDebug)
        {
            Debug.Log($"💥 远程敌人射击: {gameObject.name} (颜色: {colorComponent?.CurrentColor}, 下次攻击: {attackInterval}s后)");
        }
    }
    #endregion

    #region 撤离系统
    /// <summary>
    /// 检查撤离是否卡住（位置长时间不移动）
    /// </summary>
    private bool CheckRetreatStuck()
    {
        if (currentState != RangeEnemyState.Retreating) return false;
        
        return retreatStuckTimer >= RETREAT_STUCK_TIMEOUT;
    }
    
    private void CalculateRetreatTarget()
    {
        if (player == null) return;

        Vector3 playerPosition = player.position;
        Vector3 enemyPosition = transform.position;
        
        // 计算远离方向
        Vector3 retreatDirection = (enemyPosition - playerPosition).normalized;
        
        // 计算目标位置(安全距离)
        float safeDistance = attackRange * 0.8f;
        retreatTarget = playerPosition + retreatDirection * safeDistance;
        
        // 确保目标在NavMesh上
        NavMeshHit hit;
        if (NavMesh.SamplePosition(retreatTarget, out hit, 10f, NavMesh.AllAreas))
        {
            retreatTarget = hit.position;
        }

        if (showStateDebug)
        {
            Debug.Log($"🏃 远程敌人计算撤离目标: {retreatTarget}");
        }
    }
    #endregion

    #region 动画控制系统
    /// <summary>
    /// 设置撤离动画状态（后退动画）
    /// </summary>
    /// <param name="isRetreating">是否正在撤离</param>
    private void SetRetreatingAnimation(bool isRetreating)
    {
        if (this.isRetreating == isRetreating) return;
        
        this.isRetreating = isRetreating;
        
        if (animator != null)
        {
            animator.SetBool("IsRetreating", isRetreating);
            animator.SetBool("WalkBackwards", isRetreating);  // 兼容不同的参数名
            
            if (showStateDebug)
                Debug.Log($"🎬 设置撤离动画: {isRetreating}");
        }
    }
    
    /// <summary>
    /// 设置攻击动画状态
    /// </summary>
    /// <param name="isAttacking">是否正在攻击</param>
    private void SetAttackingAnimation(bool isAttacking)
    {
        if (this.isAttacking == isAttacking) return;
        
        this.isAttacking = isAttacking;
        
        if (animator != null)
        {
            animator.SetBool("IsAttacking", isAttacking);
            
            if (showStateDebug)
                Debug.Log($"🎬 设置攻击动画: {isAttacking}");
        }
    }
    
    /// <summary>
    /// 触发射击动画（一次性触发）
    /// </summary>
    private void TriggerShootAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("Shoot");
            animator.SetTrigger("Attack");  // 兼容不同的参数名
            
            if (showStateDebug)
                Debug.Log($"🎬 触发射击动画");
        }
    }
    
    /// <summary>
    /// 更新移动动画（基于实际速度）
    /// </summary>
    private void UpdateMovementAnimation()
    {
        if (animator != null)
        {
            animator.SetBool("IsMoving", isMoving);
            animator.SetFloat("speed", currentSpeed);
        }
    }
    #endregion

    #region 公共动画接口
    /// <summary>
    /// 获取是否正在撤离（用于外部查询）
    /// </summary>
    public bool IsRetreating => isRetreating;
    
    /// <summary>
    /// 获取是否正在攻击（用于外部查询）
    /// </summary>
    public bool IsAttacking => isAttacking;
    
    /// <summary>
    /// 手动设置撤离动画（用于外部控制）
    /// </summary>
    public void SetRetreating(bool retreating)
    {
        SetRetreatingAnimation(retreating);
    }
    
    /// <summary>
    /// 手动设置攻击动画（用于外部控制）
    /// </summary>
    public void SetAttacking(bool attacking)
    {
        SetAttackingAnimation(attacking);
    }
    
    /// <summary>
    /// 手动触发射击动画（用于外部控制）
    /// </summary>
    public void TriggerShoot()
    {
        TriggerShootAnimation();
    }
    #endregion

    #region 辅助功能
    private void CalculateMovementSpeed()
    {
        Vector3 currentPosition = transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        currentSpeed = distanceMoved / Time.deltaTime;
        lastPosition = currentPosition;
        
        // 更新移动动画
        UpdateMovementAnimation();
    }

    private void UpdateLineOfSight()
    {
        if (player == null) return;

        Vector3 directionToPlayer = player.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;
        
        RaycastHit hit;
        bool obstacle = Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            directionToPlayer.normalized,
            out hit,
            distanceToPlayer
        );

        hasLineOfSight = !obstacle || (obstacle && hit.transform == player);
    }

    private void RotateTowardsPlayer()
    {
        if (player == null) return;

        Vector3 lookDirection = player.position - transform.position;
        lookDirection.y = 0;
        
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
        }
    }

    private void RotateAwayFromPlayer()
    {
        if (player == null) return;

        Vector3 lookDirection = transform.position - player.position;
        lookDirection.y = 0;
        
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
        }
    }
    #endregion

    #region 调试和可视化
    private void OnDrawGizmosSelected()
    {
        if (!showRangeGizmos) return;

        // 绘制攻击范围
        Gizmos.color = attackRangeColor;
        Gizmos.DrawSphere(transform.position, attackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 绘制撤离范围
        Gizmos.color = retreatRangeColor;
        Gizmos.DrawSphere(transform.position, retreatRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, retreatRange);

        // 绘制撤离目标
        if (currentState == RangeEnemyState.Retreating)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(retreatTarget, 0.5f);
            Gizmos.DrawLine(transform.position, retreatTarget);
        }

        #if UNITY_EDITOR
        float attackCooldown = GetAttackCooldownRemaining();
        string navStatus = navAgent != null ? 
            $"NavAgent: {(navAgent.enabled ? "启用" : "禁用")} | OnMesh: {navAgent.isOnNavMesh} | Stopped: {navAgent.isStopped}" :
            "NavAgent: NULL";
        
        string retreatInfo = currentState == RangeEnemyState.Retreating ?
            $"\n撤离卡住: {retreatStuckTimer:F1}s/{RETREAT_STUCK_TIMEOUT}s\n移动距离: {Vector3.Distance(transform.position, lastRetreatPosition):F2}m" : 
            (isRetreatStuck ? "\n状态: 撤离卡住锁定中" : "");
        
        string animInfo = animator != null ?
            $"\n动画 - 撤离: {isRetreating} | 攻击: {isAttacking}" : "";
        
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (attackRange + 1f),
            $"状态: {currentState}\n" +
            $"移动: {isMoving}\n" +
            $"速度: {currentSpeed:F1}\n" +
            $"视线: {hasLineOfSight}\n" +
            $"攻击冷却: {attackCooldown:F1}s\n" +
            $"击退: {(knockbackSystem != null && knockbackSystem.IsKnockbackActive ? "是" : "否")}\n" +
            navStatus + retreatInfo + animInfo
        );
        #endif
    }
    #endregion

    #region 公共接口
    public RangeEnemyState GetCurrentState() => currentState;
    public bool IsMoving => isMoving;
    public float GetCurrentSpeed() => currentSpeed;
    public float GetAttackCooldown() => GetAttackCooldownRemaining();

    public void ForceStateChange(RangeEnemyState newState)
    {
        TransitionToState(newState);
    }

    public void SetAttackInterval(float interval)
    {
        attackInterval = Mathf.Max(0.5f, interval);
    }

    public void SetAttackRange(float range)
    {
        attackRange = Mathf.Max(retreatRange + 1f, range);
    }

    public void SetRetreatRange(float range)
    {
        retreatRange = Mathf.Min(attackRange - 1f, Mathf.Max(1f, range));
    }
    
    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;
        if (showStateDebug)
            Debug.Log($"🎬 设置Animator: {(animator != null ? animator.name : "NULL")}");
    }
    #endregion

    #region 事件处理
    public override void OnColorInteraction(ColorInteractionEvent interaction)
    {
        base.OnColorInteraction(interaction);
        
        // 被玩家攻击时的响应
        if (interaction.Type == ColorInteractionType.PlayerAttackEnemy && 
            interaction.Target == gameObject)
        {
            // 被击退时不重置攻击冷却
            if (showStateDebug)
            {
                Debug.Log($"👹 远程敌人被攻击 (攻击冷却: {GetAttackCooldownRemaining():F1}s)");
            }
        }
    }
    #endregion
}