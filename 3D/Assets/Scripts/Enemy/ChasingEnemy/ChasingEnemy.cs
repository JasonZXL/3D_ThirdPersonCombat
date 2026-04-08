using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 追击敌人主控制器：协调移动/攻击/眩晕/动画/击退系统。
/// </summary>
public class ChasingEnemy : BaseEnemy, IKnockbackReceiver
{
    [Header("核心组件引用")]
    [SerializeField] private ChasingEnemyMovementController movementController;
    [SerializeField] private ChasingEnemyAttackController attackController;
    [SerializeField] private ChasingEnemyStunController stunController;
    [SerializeField] private ChasingEnemyAnimationController animationController;
    [SerializeField] private KnockbackSystem knockbackSystem;
    
    [Header("状态设置")]
    [SerializeField] private bool useBuiltInAttack = true;
    
    [Header("调试设置")]
    [SerializeField] private bool checkComponentReferences = true;
    
    // 状态变量
    private bool isAttacking = false;
    private float attackTimer = 0f;

    #region Auto-Wire Controllers
    private void AutoWireControllers(bool includeInactive = true)
    {
        // 同物体 -> 子物体 -> 父物体
        movementController ??=
            GetComponent<ChasingEnemyMovementController>() ??
            GetComponentInChildren<ChasingEnemyMovementController>(includeInactive) ??
            GetComponentInParent<ChasingEnemyMovementController>();

        attackController ??=
            GetComponent<ChasingEnemyAttackController>() ??
            GetComponentInChildren<ChasingEnemyAttackController>(includeInactive) ??
            GetComponentInParent<ChasingEnemyAttackController>();

        stunController ??=
            GetComponent<ChasingEnemyStunController>() ??
            GetComponentInChildren<ChasingEnemyStunController>(includeInactive) ??
            GetComponentInParent<ChasingEnemyStunController>();

        animationController ??=
            GetComponent<ChasingEnemyAnimationController>() ??
            GetComponentInChildren<ChasingEnemyAnimationController>(includeInactive) ??
            GetComponentInParent<ChasingEnemyAnimationController>();

        knockbackSystem ??=
            GetComponent<KnockbackSystem>() ??
            GetComponentInChildren<KnockbackSystem>(includeInactive) ??
            GetComponentInParent<KnockbackSystem>();

        if (showDebugLogs)
        {
            Debug.Log($"🧩 [ChasingEnemy] AutoWire结果: " +
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
        // 编辑器中也回填，避免Prefab/场景里忘拖
        AutoWireControllers(includeInactive: true);
    }
#endif

    [ContextMenu("Auto Wire Controllers")]
    private void DebugAutoWireControllers()
    {
        AutoWireControllers(includeInactive: true);
    }
    #endregion
    
    protected override void Awake()
    {
        base.Awake();
        
        if (showDebugLogs) Debug.Log($"🔄 [ChasingEnemy] Awake开始: {gameObject.name}");

        // ✅ 关键：先自动找齐，再做检查/初始化
        AutoWireControllers(includeInactive: true);
        
        // 检查组件引用
        if (checkComponentReferences)
        {
            CheckComponentReferences();
        }

        // 核心组件缺失时禁用，避免Update里不断刷警告
        if (movementController == null || attackController == null)
        {
            Debug.LogError($"❌ [ChasingEnemy] 核心Controller缺失（Movement/Attack），已禁用脚本: {gameObject.name}");
            enabled = false;
            return;
        }
        
        // 初始化组件
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
        {
            if (showDebugLogs) Debug.Log($"🎯 [ChasingEnemy] 找到玩家: {player.name}");
            
            // 初始化移动控制器
            if (movementController != null)
            {
                movementController.Initialize(player, animationController);
                if (showDebugLogs) Debug.Log($"✅ [ChasingEnemy] 移动控制器初始化完成");
            }
            
            // 初始化攻击控制器
            if (attackController != null)
            {
                attackController.Initialize(player, animationController);
                attackController.OnAttackStarted += HandleAttackStarted;
                attackController.OnAttackEnded += HandleAttackEnded;
                if (showDebugLogs) Debug.Log($"✅ [ChasingEnemy] 攻击控制器初始化完成");
            }
        }
        else
        {
            Debug.LogError($"❌ [ChasingEnemy] 未找到玩家对象! 请确保场景中有Tag为Player的对象");
        }
        
        // 初始化眩晕控制器
        if (stunController != null)
        {
            stunController.Initialize(animationController);
            if (showDebugLogs) Debug.Log($"✅ [ChasingEnemy] 眩晕控制器初始化完成");
        }
        if (showDebugLogs) Debug.Log($"✅ [ChasingEnemy] Awake完成: {gameObject.name}");
    }
    
    private void CheckComponentReferences()
    {
        if (showDebugLogs)
        {
            Debug.Log($"🔍 [ChasingEnemy] 检查组件引用:");
            Debug.Log($"   - MovementController: {(movementController != null ? "已分配" : "⚠️ 未分配")}");
            Debug.Log($"   - AttackController: {(attackController != null ? "已分配" : "⚠️ 未分配")}");
            Debug.Log($"   - StunController: {(stunController != null ? "已分配" : "⚠️ 未分配")}");
            Debug.Log($"   - AnimationController: {(animationController != null ? "已分配" : "⚠️ 未分配")}");
            Debug.Log($"   - KnockbackSystem: {(knockbackSystem != null ? "已分配" : "⚠️ 未分配")}");
            
            // 检查必要的Unity组件
            NavMeshAgent navAgent = GetComponent<NavMeshAgent>();
            CharacterController charController = GetComponent<CharacterController>();
            
            Debug.Log($"   - NavMeshAgent: {(navAgent != null ? "已添加" : "⚠️ 未添加")}");
            Debug.Log($"   - CharacterController: {(charController != null ? "已添加" : "⚠️ 未添加")}");
        }
    }
    
    private void Start()
    {
        if (showDebugLogs) Debug.Log($"🚀 [ChasingEnemy] Start: {gameObject.name}");
    }
    
    private void Update()
    {
        // 如果缺少核心组件，直接返回
        if (movementController == null || attackController == null)
        {
            if (showDebugLogs && Time.frameCount % 120 == 0)
                Debug.LogWarning($"⚠️ [ChasingEnemy] 缺少核心组件，无法执行逻辑");
            return;
        }
        
        // 优先级1：击退状态
        if (knockbackSystem != null && knockbackSystem.IsKnockbackActive)
        {
            if (showDebugLogs && Time.frameCount % 60 == 0) 
                Debug.Log($"💨 [ChasingEnemy] 处于击退状态");
            HandleKnockbackState();
            return;
        }
        
        // 优先级2：眩晕状态
        if (stunController != null && stunController.IsStunning)
        {
            if (showDebugLogs && Time.frameCount % 60 == 0) 
                Debug.Log($"🌀 [ChasingEnemy] 处于眩晕状态");
            
            // 关键修复：如果眩晕开始时正在攻击，强制结束攻击
            if (isAttacking)
            {
                if (showDebugLogs) Debug.Log($"🔄 [ChasingEnemy] 眩晕中强制结束攻击状态");
                ForceEndAttackState();
            }
            
            // 确保移动被停止
            movementController?.StopMovement();
            
            return;
        }
        
        // 优先级3：攻击状态
        if (isAttacking)
        {
            if (showDebugLogs && Time.frameCount % 60 == 0) 
                Debug.Log($"⚔️ [ChasingEnemy] 处于攻击状态, 计时: {attackTimer:F1}/{attackController.AttackDuration}");
            UpdateAttackState();
            return;
        }
        
        // 优先级4：正常状态
        UpdateNormalState();
    }

    private void ForceEndAttackState()
    {
        // 关键修复：先重置本地状态，避免重复调用
        if (!isAttacking) return; // 如果已经不在攻击状态，直接返回
        
        isAttacking = false;
        attackTimer = 0f;
        
        // 通知攻击控制器结束攻击（会触发OnAttackEnded事件）
        attackController?.EndAttack();
        
        // 确保动画状态正确
        animationController?.SetAttackAnimation(false);
        
        // 确保移动被恢复
        movementController?.ResumeMovement();
        
        if (showDebugLogs) Debug.Log($"🛑 [ChasingEnemy] 强制结束攻击状态完成");
    }
    
    private void UpdateNormalState()
    {
        // 关键修复：确保攻击状态已清除（防止状态不同步）
        if (isAttacking)
        {
            if (showDebugLogs) Debug.LogWarning($"⚠️ [ChasingEnemy] 检测到状态不同步：isAttacking=true但不在攻击状态处理中，强制修复");
            ForceEndAttackState();
            return;
        }
        
        // 更新攻击冷却
        attackController.UpdateAttackCooldown();
        
        // 检查是否可以攻击
        if (useBuiltInAttack && attackController.CheckIfInAttackRange())
        {
            if (showDebugLogs) Debug.Log($"🎯 [ChasingEnemy] 玩家在攻击范围内，开始攻击");
            attackController.StartAttack();
            return;
        }
        
        // 正常移动
        movementController?.UpdateMovement();
        if (showDebugLogs && Time.frameCount % 120 == 0 && movementController != null && movementController.IsMoving)
            Debug.Log($"🏃 [ChasingEnemy] 正在追击玩家");
    }
    
    private void HandleAttackStarted()
    {
        isAttacking = true;
        attackTimer = 0f;
        
        if (showDebugLogs) Debug.Log($"🔥 [ChasingEnemy] 攻击开始");
        
        movementController.StopMovement();
    }
    
    private void HandleAttackEnded()
    {
        // 关键修复：防止重复调用导致状态混乱
        if (!isAttacking)
        {
            if (showDebugLogs) Debug.LogWarning($"⚠️ [ChasingEnemy] HandleAttackEnded被重复调用，已忽略");
            return;
        }
        
        isAttacking = false;
        attackTimer = 0f; // 重置计时器
        
        if (showDebugLogs) Debug.Log($"✅ [ChasingEnemy] 攻击结束");
        
        // 确保移动被恢复
        movementController?.ResumeMovement();
        
        // 强制重新计算路径，确保敌人能继续追击
        movementController?.ForceRecalculatePath();
    }
    
    private void UpdateAttackState()
    {
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackController.AttackDuration)
        {
            // 关键修复：通过攻击控制器结束攻击，确保状态同步
            attackController?.EndAttack();
            // HandleAttackEnded() 会通过 OnAttackEnded 事件被调用
        }
    }
    
    private void HandleKnockbackState()
    {
        if (showDebugLogs && Time.frameCount % 30 == 0) 
            Debug.Log($"💥 [ChasingEnemy] 处理击退状态");
        
        movementController.StopMovement();
        
        if (animationController != null)
        {
            animationController.SetStunAnimation(true);
            animationController.SetAttackAnimation(false);
            animationController.SetMovingAnimation(false);
        }
    }
    


    // IKnockbackReceiver callbacks（由 KnockbackSystem 在击退开始/结束时调用）
    public void OnKnockbackStart()
    {
        HandleKnockbackStart();
    }

    public void OnKnockbackEnd()
    {
        HandleKnockbackEnd();
    }

    private void HandleKnockbackStart()
    {
        if (showDebugLogs) Debug.Log($"🚀 [ChasingEnemy] 击退开始回调");
        // 击退开始时：停止追击/打断攻击，避免 NavMeshAgent 与 CharacterController.Move 冲突
        if (isAttacking)
        {
            isAttacking = false;
            attackTimer = 0f;
            animationController?.SetAttackAnimation(false);
        }
        movementController?.StopMovement();

        // 这里不调用 stunController.StartStun() —— StunController 会锁死位置，反而会把击退效果“钉住”
        if (animationController != null)
        {
            animationController.SetStunAnimation(true);
            animationController.SetMovingAnimation(false);
        }
    }
    
    private void HandleKnockbackEnd()
    {
        if (showDebugLogs) Debug.Log($"🛑 [ChasingEnemy] 击退结束回调");

        // 击退结束后，恢复动画状态
        if (animationController != null)
        {
            animationController.SetStunAnimation(false);
        }

        // 如果眩晕还在（例如同色成功防御触发的眩晕），不要恢复移动
        if (stunController != null && stunController.IsStunning) return;

        // 保险：如果击退还没真正结束，不做恢复
        if (knockbackSystem != null && knockbackSystem.IsKnockbackActive) return;

        // 如果马上又进入攻击状态，也不恢复
        if (isAttacking) return;
        movementController?.ResumeMovement();
        movementController?.ForceRecalculatePath();
    }
    
    public override void OnColorInteraction(ColorInteractionEvent interaction)
    {
        base.OnColorInteraction(interaction);
        
        if (showDebugLogs) 
            Debug.Log($"🎨 [ChasingEnemy] 收到颜色交互: {interaction.Type}");
        
        // 同色攻击触发眩晕（给玩家防御成功反馈）
        if (interaction.Type == ColorInteractionType.EnemyAttackPlayer &&
            interaction.Source == gameObject)
        {
            ColorComponent sourceColor = GetColorComponent();
            ColorComponent targetColor = interaction.Target?.GetComponent<ColorComponent>();
            
            if (sourceColor != null && targetColor != null && sourceColor.IsSameColor(targetColor))
            {
                if (showDebugLogs) 
                    Debug.Log($"🌀🎯 [ChasingEnemy] 攻击同色玩家，触发眩晕");
                stunController?.StartStun();
            }
        }
    }
    
    private void OnDestroy()
    {
        // 清理事件订阅
        if (attackController != null)
        {
            attackController.OnAttackStarted -= HandleAttackStarted;
            attackController.OnAttackEnded -= HandleAttackEnded;
        }
        if (showDebugLogs) Debug.Log($"♻️ [ChasingEnemy] 清理完成: {gameObject.name}");
    }
    
    #region 对外接口
    public void SetAttackEnabled(bool enabled)
    {
        useBuiltInAttack = enabled;
        if (showDebugLogs) Debug.Log($"🔧 [ChasingEnemy] 设置攻击启用: {enabled}");
    }
    
    public void SetMovementSpeed(float speed)
    {
        movementController?.SetMovementSpeed(speed);
        if (showDebugLogs) Debug.Log($"🔧 [ChasingEnemy] 设置移动速度: {speed}");
    }
    
    public void StartStun(float duration = 0f)
    {
        if (showDebugLogs) Debug.Log($"🌀 [ChasingEnemy] 手动触发眩晕");
        stunController?.StartStun(duration);
    }
    
    public void EndStun()
    {
        if (showDebugLogs) Debug.Log($"✅ [ChasingEnemy] 手动结束眩晕");
        stunController?.EndStun();
    }
    
    public void ForceEndStun()
    {
        if (showDebugLogs) Debug.Log($"🛑 [ChasingEnemy] 强制结束眩晕");
        stunController?.ForceEndStun();
    }
    
    // 属性访问器
    public bool IsAttacking => isAttacking;
    public bool IsStunning => stunController != null && stunController.IsStunning;
    public bool IsMoving => movementController != null && movementController.IsMoving;
    
    // 设置方法
    public void SetAttackParameters(float range, float duration, float cooldown)
    {
        if (attackController != null)
        {
            attackController.SetAttackParameters(range, duration, cooldown);
            if (showDebugLogs) Debug.Log($"🔧 [ChasingEnemy] 设置攻击参数: 范围={range}, 持续时间={duration}, 冷却={cooldown}");
        }
    }
    
    public void SetStunDuration(float duration)
    {
        if (stunController != null)
        {
            stunController.SetStunDuration(duration);
            if (showDebugLogs) Debug.Log($"🔧 [ChasingEnemy] 设置眩晕持续时间: {duration}");
        }
    }
    
    public void SetStunOnKnockback(bool enable)
    {
        if (stunController != null)
        {
            stunController.SetStunOnKnockback(enable);
            if (showDebugLogs) Debug.Log($"🔧 [ChasingEnemy] 设置击退时眩晕: {enable}");
        }
    }
    
    public void SetAnimator(Animator newAnimator)
    {
        if (animationController != null)
        {
            animationController.Initialize(newAnimator);
            if (showDebugLogs) Debug.Log($"🔧 [ChasingEnemy] 设置Animator: {newAnimator}");
        }
    }
    #endregion
    
    #region 调试方法
    [ContextMenu("手动触发眩晕")]
    public void DebugTriggerStun()
    {
        StartStun();
    }
    
    [ContextMenu("手动结束眩晕")]
    public void DebugEndStun()
    {
        EndStun();
    }
    
    [ContextMenu("打印状态信息")]
    public void DebugPrintStatus()
    {
        Debug.Log($"📊 [ChasingEnemy] 状态信息:");
        Debug.Log($"   - 是否在攻击: {IsAttacking}");
        Debug.Log($"   - 是否在眩晕: {IsStunning}");
        Debug.Log($"   - 是否在移动: {IsMoving}");
        Debug.Log($"   - 击退状态: {(knockbackSystem != null && knockbackSystem.IsKnockbackActive ? "激活" : "未激活")}");
        Debug.Log($"   - 玩家距离: {(movementController != null ? movementController.DistanceToPlayer().ToString("F2") : "N/A")}");
    }
    
    [ContextMenu("检查组件引用")]
    public void DebugCheckComponents()
    {
        CheckComponentReferences();
    }
    #endregion
}