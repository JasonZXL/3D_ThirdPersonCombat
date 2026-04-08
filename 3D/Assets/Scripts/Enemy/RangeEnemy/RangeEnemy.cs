using UnityEngine;

/// <summary>
/// 远程敌人主控制器：协调移动/攻击/眩晕/动画/击退系统
/// </summary>
public class RangeEnemy : BaseEnemy, IKnockbackReceiver
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

    #region 核心组件引用
    [Header("核心组件引用")]
    [SerializeField] private RangeEnemyMovementController movementController;
    [SerializeField] private RangeEnemyAttackController attackController;
    [SerializeField] private RangeEnemyStunController stunController;
    [SerializeField] private RangeEnemyAnimationController animationController;
    [SerializeField] private KnockbackSystem knockbackSystem;
    #endregion

    #region 调试设置
    [Header("调试设置")]
    [SerializeField] private bool showStateDebug = true;
    [SerializeField] private bool showRangeGizmos = true;
    [SerializeField] private bool checkComponentReferences = true;
    [SerializeField] private Color attackRangeColor = new Color(1f, 0f, 0f, 0.1f);
    [SerializeField] private Color retreatRangeColor = new Color(1f, 1f, 0f, 0.2f);
    #endregion

    #region 内部变量
    private RangeEnemyState currentState = RangeEnemyState.Idle;
    private Transform player;
    
    // 状态稳定性
    private float stateChangeTimer = 0f;
    private const float STATE_CHANGE_COOLDOWN = 0.2f;
    
    // 击退状态追踪
    private bool wasKnockbackActive = false;
    private RangeEnemyState stateBeforeKnockback = RangeEnemyState.Idle;
    
    // 撤离卡住状态
    private bool isRetreatStuck = false;

    // 攻击状态（事件驱动计时）
    private bool isAttacking = false;
    private float attackTimer = 0f;

    #endregion

    #region Auto-Wire Controllers
    private void AutoWireControllers(bool includeInactive = true)
    {
        // 同物体 -> 子物体 -> 父物体
        movementController ??=
            GetComponent<RangeEnemyMovementController>() ??
            GetComponentInChildren<RangeEnemyMovementController>(includeInactive) ??
            GetComponentInParent<RangeEnemyMovementController>();

        attackController ??=
            GetComponent<RangeEnemyAttackController>() ??
            GetComponentInChildren<RangeEnemyAttackController>(includeInactive) ??
            GetComponentInParent<RangeEnemyAttackController>();

        stunController ??=
            GetComponent<RangeEnemyStunController>() ??
            GetComponentInChildren<RangeEnemyStunController>(includeInactive) ??
            GetComponentInParent<RangeEnemyStunController>();

        animationController ??=
            GetComponent<RangeEnemyAnimationController>() ??
            GetComponentInChildren<RangeEnemyAnimationController>(includeInactive) ??
            GetComponentInParent<RangeEnemyAnimationController>();

        knockbackSystem ??=
            GetComponent<KnockbackSystem>() ??
            GetComponentInChildren<KnockbackSystem>(includeInactive) ??
            GetComponentInParent<KnockbackSystem>();

        if (showStateDebug)
        {
            Debug.Log($"🧩 [RangeEnemy] AutoWire结果: " +
                      $"Move={(movementController != null ? "OK" : "NULL")}, " +
                      $"Atk={(attackController != null ? "OK" : "NULL")}, " +
                      $"Stun={(stunController != null ? "OK" : "NULL")}, " +
                      $"Anim={(animationController != null ? "OK" : "NULL")}, " +
                      $"Knockback={(knockbackSystem != null ? "OK" : "NULL")}");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoWireControllers(includeInactive: true);
    }
#endif

    [ContextMenu("Auto Wire Controllers")]
    private void DebugAutoWireControllers()
    {
        AutoWireControllers(includeInactive: true);
    }
    #endregion

    #region Unity生命周期
    protected override void Awake()
    {
        base.Awake();
        
        if (showStateDebug) Debug.Log($"🔄 [RangeEnemy] Awake开始: {gameObject.name}");

        // 关键：先自动找齐，再做检查/初始化
        AutoWireControllers(includeInactive: true);
        
        // 检查组件引用
        if (checkComponentReferences)
        {
            CheckComponentReferences();
        }

        // 核心组件缺失时禁用，避免Update里不断刷警告
        if (movementController == null || attackController == null)
        {
            Debug.LogError($"❌ [RangeEnemy] 核心Controller缺失（Movement/Attack），已禁用脚本: {gameObject.name}");
            enabled = false;
            return;
        }
        
        // 查找玩家
        FindPlayer();
        
        if (showStateDebug) Debug.Log($"✅ [RangeEnemy] Awake完成: {gameObject.name}");
    }

    private void Start()
    {
        // 初始化所有控制器
        InitializeControllers();
        
        // 进入初始状态
        TransitionToState(RangeEnemyState.Idle);
        
        if (showStateDebug) Debug.Log($"🚀 [RangeEnemy] Start完成: {gameObject.name}");
    }

    private void Update()
    {
        // 如果缺少核心组件，直接返回
        if (movementController == null || attackController == null)
        {
            if (showStateDebug && Time.frameCount % 120 == 0)
                Debug.LogWarning($"⚠️ [RangeEnemy] 缺少核心组件，无法执行逻辑");
            return;
        }

        // ✅ 玩家可能晚于敌人生成（或被重生），为空时持续尝试重新绑定
        if (player == null)
        {
            FindPlayer();
            if (player != null)
            {
                InitializeControllers();
            }
            else
            {
                return;
            }
        }
        
        // 优先级1：击退状态
        if (knockbackSystem != null && knockbackSystem.IsKnockbackActive)
        {
            if (showStateDebug && Time.frameCount % 60 == 0)
                Debug.Log($"💨 [RangeEnemy] 处于击退状态");
            HandleKnockbackState();
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
        
        // 优先级2：眩晕状态
        if (stunController != null && stunController.IsStunning)
        {
            if (showStateDebug && Time.frameCount % 60 == 0)
                Debug.Log($"🌀 [RangeEnemy] 处于眩晕状态");
            return;
        }
        
        // 优先级3：攻击状态（由事件驱动开始，由主控计时结束）
        if (isAttacking)
        {
            UpdateAttackState();
            return;
        }

        // 优先级4：正常状态机
        UpdateStateMachine();
    }
    #endregion

    #region 初始化系统
    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            if (showStateDebug)
                Debug.Log($"🎯 远程敌人找到玩家: {player.name}");
        }
        else
        {
            Debug.LogError($"❌ [RangeEnemy] 未找到玩家对象! 请确保场景中有Tag为Player的对象");
        }
    }

    private void InitializeControllers()
    {
        if (player == null)
        {
            Debug.LogError($"❌ [RangeEnemy] 无法初始化：玩家未找到");
            return;
        }

        // 初始化移动控制器
        if (movementController != null)
        {
            movementController.Initialize(player, animationController);
            if (showStateDebug) Debug.Log($"✅ [RangeEnemy] 移动控制器初始化完成");
        }

        // 初始化攻击控制器
        if (attackController != null)
        {
            attackController.Initialize(player, animationController);
            attackController.OnAttackStarted += HandleAttackStarted;
            attackController.OnAttackEnded += HandleAttackEnded;
            attackController.OnAttackPerformed += HandleAttackPerformed;
            if (showStateDebug) Debug.Log($"✅ [RangeEnemy] 攻击控制器初始化完成");
        }

        // 初始化眩晕控制器
        if (stunController != null)
        {
            stunController.Initialize(animationController);
            if (showStateDebug) Debug.Log($"✅ [RangeEnemy] 眩晕控制器初始化完成");
        }

        // 初始化动画控制器
        if (animationController != null)
        {
            if (showStateDebug) Debug.Log($"✅ [RangeEnemy] 动画控制器初始化完成");
        }
    }

    private void CheckComponentReferences()
    {
        if (showStateDebug)
        {
            Debug.Log($"🔍 [RangeEnemy] 检查组件引用:");
            Debug.Log($"   - MovementController: {(movementController != null ? "已分配" : "⚠️ 未分配")}");
            Debug.Log($"   - AttackController: {(attackController != null ? "已分配" : "⚠️ 未分配")}");
            Debug.Log($"   - StunController: {(stunController != null ? "已分配" : "⚠️ 未分配")}");
            Debug.Log($"   - AnimationController: {(animationController != null ? "已分配" : "⚠️ 未分配")}");
            Debug.Log($"   - KnockbackSystem: {(knockbackSystem != null ? "已分配" : "⚠️ 未分配")}");
        }
    }
    #endregion

    #region 状态机系统
    private void UpdateStateMachine()
    {
        if (player == null) return;

        // 更新状态切换冷却
        if (stateChangeTimer > 0f)
        {
            stateChangeTimer -= Time.deltaTime;
        }

        float distanceToPlayer = movementController.DistanceToPlayer();
        bool hasLineOfSight = attackController.HasLineOfSight;
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
                    if (distanceToPlayer <= attackController.AttackRange && hasLineOfSight)
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
                        if (distanceToPlayer > attackController.AttackRange * 1.5f)
                        {
                            if (showStateDebug)
                                Debug.Log($"✅ 玩家远离，解除撤离卡住状态，转为追击");
                            isRetreatStuck = false;
                            movementController.ResetRetreatStuck();
                            TransitionToState(RangeEnemyState.Chasing);
                        }
                        else if (distanceToPlayer <= movementController.RetreatRange)
                        {
                            if (showStateDebug)
                                Debug.Log($"✅ 玩家从新角度接近，解除撤离卡住状态，尝试重新撤离");
                            isRetreatStuck = false;
                            movementController.ResetRetreatStuck();
                            TransitionToState(RangeEnemyState.Retreating);
                        }
                        break;
                    }

                    if (distanceToPlayer <= movementController.RetreatRange)
                    {
                        TransitionToState(RangeEnemyState.Retreating);
                    }
                    else if (distanceToPlayer > attackController.AttackRange * 1.2f || !hasLineOfSight)
                    {
                        TransitionToState(RangeEnemyState.Chasing);
                    }
                }
                break;

            case RangeEnemyState.Retreating:
                if (canChangeState)
                {
                    // 检查撤离是否卡住
                    if (movementController.IsRetreatStuck())
                    {
                        if (showStateDebug)
                            Debug.LogWarning($"⚠️ 撤离卡住，强制切换到攻击状态并锁定");
                        isRetreatStuck = true;
                        TransitionToState(RangeEnemyState.Attacking);
                        break;
                    }

                    if (distanceToPlayer > movementController.RetreatRange * 1.3f && 
                        distanceToPlayer <= attackController.AttackRange && 
                        hasLineOfSight)
                    {
                        TransitionToState(RangeEnemyState.Attacking);
                    }
                    else if (distanceToPlayer > attackController.AttackRange * 1.2f)
                    {
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
            Debug.Log($"🔄 远程敌人状态转换: {oldState} → {newState} (攻击冷却: {attackController.GetAttackCooldownRemaining():F1}s)");
        }
    }

    private void ExitCurrentState()
    {
        switch (currentState)
        {
            case RangeEnemyState.Chasing:
            case RangeEnemyState.Retreating:
                movementController?.StopMovement();
                break;
        }
    }

    private void EnterNewState(RangeEnemyState newState)
    {
        switch (newState)
        {
            case RangeEnemyState.Idle:
                movementController?.StopMovement();
                if (animationController != null)
                {
                    animationController.SetRetreatingAnimation(false);
                    animationController.SetAttackingAnimation(false);
                }
                break;

            case RangeEnemyState.Chasing:
                movementController?.ResumeMovement();
                if (animationController != null)
                {
                    animationController.SetRetreatingAnimation(false);
                    animationController.SetAttackingAnimation(false);
                }
                break;

            case RangeEnemyState.Attacking:
                movementController?.StopMovement();
                if (animationController != null)
                {
                    animationController.SetRetreatingAnimation(false);
                    animationController.SetAttackingAnimation(true);
                }
                break;

            case RangeEnemyState.Retreating:
                movementController?.ResumeMovement();
                if (animationController != null)
                {
                    animationController.SetRetreatingAnimation(true);
                    animationController.SetAttackingAnimation(false);
                }
                break;
        }
    }

    private void ExecuteStateBehavior()
    {
        switch (currentState)
        {
            case RangeEnemyState.Idle:
                // 空闲状态不需要额外操作
                break;

            case RangeEnemyState.Chasing:
                movementController?.UpdateChaseMovement();
                break;

            case RangeEnemyState.Attacking:
                // 检查是否可以攻击
                if (!isAttacking && attackController.CanAttackPlayer())
                {
                    attackController.StartAttack();
                }
                break;

            case RangeEnemyState.Retreating:
                movementController?.UpdateRetreatMovement();
                break;
        }
    }
    #endregion

    
    #region 攻击事件驱动
    private void HandleAttackStarted()
    {
        isAttacking = true;
        attackTimer = 0f;

        // 进入攻击状态时停止移动，避免边跑边射导致抖动
        movementController?.StopMovement();

        // 统一由动画控制器维持攻击态（StartAttack里也会设一次，这里保证一致）
        if (animationController != null)
        {
            animationController.SetAttackingAnimation(true);
        }

        if (showStateDebug)
            Debug.Log($"⚔️ [RangeEnemy] 攻击开始（事件）");
    }

    private void HandleAttackPerformed()
    {
        if (showStateDebug)
            Debug.Log($"💥 [RangeEnemy] 攻击已执行（发射子弹）（事件）");
    }

    private void HandleAttackEnded()
    {
        isAttacking = false;
        attackTimer = 0f;

        if (animationController != null)
        {
            animationController.SetAttackingAnimation(false);
        }

        // 给状态切换一点缓冲，避免攻击结束立刻抖动切状态
        stateChangeTimer = STATE_CHANGE_COOLDOWN;

        if (showStateDebug)
            Debug.Log($"✅ [RangeEnemy] 攻击结束（事件）");
    }

    private void UpdateAttackState()
    {
        if (attackController == null)
        {
            isAttacking = false;
            return;
        }

        attackTimer += Time.deltaTime;

        // 攻击持续时间到 -> 结束攻击（由主控驱动）
        if (attackTimer >= attackController.AttackDuration)
        {
            attackController.EndAttack();
            // HandleAttackEnded 会被事件回调置isAttacking=false
        }
    }
    #endregion

#region IKnockbackReceiver实现
    public void OnKnockbackStart()
    {
        if (showStateDebug)
            Debug.Log($"🚀 [RangeEnemy] 击退开始回调");

        // 记录击退前的状态
        if (!wasKnockbackActive)
        {
            wasKnockbackActive = true;
            stateBeforeKnockback = currentState;
            if (showStateDebug)
                Debug.Log($"💨 远程敌人进入击退状态，保存状态: {stateBeforeKnockback}");
        }

        // 停止所有移动
        movementController?.StopMovement();

        // 设置动画
        if (animationController != null)
        {
            animationController.SetStunningAnimation(true);
            animationController.SetAttackingAnimation(false);
            animationController.SetMovingAnimation(false);
            animationController.SetRetreatingAnimation(false);
        }
    }

    public void OnKnockbackEnd()
    {
        if (showStateDebug)
            Debug.Log($"🛑 [RangeEnemy] 击退结束回调");

        // 恢复动画状态
        if (animationController != null)
        {
            animationController.SetStunningAnimation(false);
        }

        // 如果眩晕还在，不要恢复移动
        if (stunController != null && stunController.IsStunning) return;

        // 如果击退还没真正结束，不做恢复
        if (knockbackSystem != null && knockbackSystem.IsKnockbackActive) return;

        // 恢复移动
        movementController?.ResumeMovement();
    }
    #endregion

    #region 击退状态处理
    private void HandleKnockbackState()
    {
        if (showStateDebug && Time.frameCount % 30 == 0)
            Debug.Log($"💥 [RangeEnemy] 处理击退状态");

        movementController?.StopMovement();

        if (animationController != null)
        {
            animationController.SetStunningAnimation(true);
            animationController.SetAttackingAnimation(false);
            animationController.SetMovingAnimation(false);
            animationController.SetRetreatingAnimation(false);
        }
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
            if (showStateDebug)
            {
                Debug.Log($"💹 远程敌人被攻击 (攻击冷却: {attackController.GetAttackCooldownRemaining():F1}s)");
            }
        }
    }
    #endregion

    #region 调试和可视化
    private void OnDrawGizmosSelected()
    {
        if (!showRangeGizmos) return;

        float attackRange = attackController != null ? attackController.AttackRange : 10f;
        float retreatRange = movementController != null ? movementController.RetreatRange : 3f;

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

#if UNITY_EDITOR
        float attackCooldown = attackController != null ? attackController.GetAttackCooldownRemaining() : 0f;
        bool isMoving = movementController != null && movementController.IsMoving;
        float currentSpeed = movementController != null ? movementController.CurrentSpeed : 0f;

        string retreatInfo = isRetreatStuck ? "\n状态: 撤离卡住锁定中" : "";

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (attackRange + 1f),
            $"状态: {currentState}\n" +
            $"移动: {isMoving}\n" +
            $"速度: {currentSpeed:F1}\n" +
            $"视线: {(attackController != null ? attackController.HasLineOfSight : false)}\n" +
            $"攻击冷却: {attackCooldown:F1}s\n" +
            $"击退: {(knockbackSystem != null && knockbackSystem.IsKnockbackActive ? "是" : "否")}" +
            retreatInfo
        );
#endif
    }
    #endregion

    #region 公共接口
    public RangeEnemyState GetCurrentState() => currentState;
    public bool IsMoving => movementController != null && movementController.IsMoving;
    public float GetCurrentSpeed() => movementController != null ? movementController.CurrentSpeed : 0f;
    public float GetAttackCooldown() => attackController != null ? attackController.GetAttackCooldownRemaining() : 0f;
    public bool IsStunning => stunController != null && stunController.IsStunning;

    public void ForceStateChange(RangeEnemyState newState)
    {
        TransitionToState(newState);
    }

    public void SetAnimator(Animator newAnimator)
    {
        animationController?.SetAnimator(newAnimator);
    }

    public void StartStun(float duration = 0f)
    {
        stunController?.StartStun(duration);
    }

    public void EndStun()
    {
        stunController?.EndStun();
    }
    #endregion

    #region 调试方法
    [ContextMenu("打印状态信息")]
    public void DebugPrintStatus()
    {
        Debug.Log($"📊 [RangeEnemy] 状态信息:");
        Debug.Log($"   - 当前状态: {currentState}");
        Debug.Log($"   - 是否在移动: {IsMoving}");
        Debug.Log($"   - 是否在眩晕: {IsStunning}");
        Debug.Log($"   - 击退状态: {(knockbackSystem != null && knockbackSystem.IsKnockbackActive ? "激活" : "未激活")}");
        Debug.Log($"   - 到玩家距离: {(movementController != null ? movementController.DistanceToPlayer().ToString("F2") : "N/A")}");
    }

    [ContextMenu("检查组件引用")]
    public void DebugCheckComponents()
    {
        CheckComponentReferences();
    }

    [ContextMenu("手动触发眩晕")]
    public void DebugTriggerStun()
    {
        StartStun();
    }
    #endregion


    private void OnDestroy()
    {
        if (attackController != null)
        {
            attackController.OnAttackStarted -= HandleAttackStarted;
            attackController.OnAttackEnded -= HandleAttackEnded;
            attackController.OnAttackPerformed -= HandleAttackPerformed;
        }
    }
}

