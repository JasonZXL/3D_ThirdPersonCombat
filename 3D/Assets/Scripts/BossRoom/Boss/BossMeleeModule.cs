using UnityEngine;

/// <summary>
/// Boss Phase1 近战行为模块：
/// - 追击玩家（复用 ChasingEnemyMovementController）
/// - 近战攻击（复用 ChasingEnemyAttackController）
/// - 同色攻击玩家触发眩晕 / 被击退触发眩晕（复用 ChasingEnemyStunController）
///
/// 注意：这个模块是“Boss专用拼装器”，不再依赖 ChasingEnemy.cs 总控，
/// 避免 Phase1 / Phase2 抢 Update、抢 NavAgent、抢移动权的问题。
/// </summary>
public class BossMeleeModule : MonoBehaviour
{
    [Header("Controllers (复用追击敌人组件)")]
    [SerializeField] private ChasingEnemyMovementController movementController;
    [SerializeField] private ChasingEnemyAttackController attackController;
    [SerializeField] private ChasingEnemyStunController stunController;
    [SerializeField] private ChasingEnemyAnimationController animationController;

    [Header("Module Settings")]
    [SerializeField] private bool showDebugLogs = true;

    private Transform player;
    private bool isActive = false;

    // 内部攻击状态（等价于 ChasingEnemy.cs 的攻击状态机）
    private bool isAttacking = false;
    private float attackTimer = 0f;

    public bool IsActive => isActive;
    public bool IsAttacking => isAttacking;
    public bool IsStunning => stunController != null && stunController.IsStunning;

    #region LifeCycle

    private void Awake()
    {
        AutoWireControllers(includeInactive: true);
    }

    private void OnDestroy()
    {
        UnbindAttackEvents();
    }

    #endregion

    #region Public API

    /// <summary>
    /// BossController 在生成 Boss 后调用一次初始化
    /// </summary>
    public void Initialize(Transform playerTransform)
    {
        player = playerTransform;

        AutoWireControllers(includeInactive: true);

        if (movementController != null)
            movementController.Initialize(player, animationController);

        if (attackController != null)
            attackController.Initialize(player, animationController);

        if (stunController != null)
            stunController.Initialize(animationController);

        BindAttackEvents();

        if (showDebugLogs)
            Debug.Log($"🗡️ [BossMeleeModule] Initialize 完成 -> Player={(player != null ? player.name : "NULL")}");
    }

    /// <summary>
    /// BossController 切换 Phase1/Phase2 时调用
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;

        if (!isActive)
        {
            // Phase1 关闭时必须把自身状态清掉，避免残留攻击锁死移动
            ForceStopAllBehaviors("Module Disabled");
        }
        else
        {
            // Phase1 开启时：确保攻击冷却可以正常开始计算
            attackController?.ResetAttackCooldown();
            movementController?.ForceRecalculatePath();
        }

        if (showDebugLogs)
            Debug.Log($"🗡️ [BossMeleeModule] SetActive({active})");
    }

    /// <summary>
    /// BossController 的 Update() 每帧调用它
    /// </summary>
    public void Tick()
    {
        if (!isActive) return;

        // 如果玩家引用丢失，尝试自动找
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (player == null) return;

        // 眩晕期间：模块完全停止移动/攻击（眩晕窗口交给 BossStunWindow + 弱点系统）
        if (IsStunning)
        {
            if (isAttacking)
            {
                ForceEndAttack("Stunning");
            }

            movementController?.StopMovement();
            return;
        }

        // 更新攻击冷却
        attackController?.UpdateAttackCooldown();

        // 攻击状态计时（AttackController 本身不负责持续时间）
        if (isAttacking)
        {
            attackTimer += Time.deltaTime;
            if (attackController != null && attackTimer >= attackController.AttackDuration)
            {
                attackController.EndAttack();
                // OnAttackEnded 事件会把 isAttacking 复位
            }
            return;
        }

        // 进入攻击
        if (attackController != null && attackController.CanAttackPlayer())
        {
            attackController.StartAttack();
            return;
        }

        // 否则追击
        movementController?.UpdateMovement();
    }

    /// <summary>
    /// BossController / 其他系统（比如强制切阶段）可以调用
    /// </summary>
    public void ForceStopAllBehaviors(string reason = "")
    {
        if (showDebugLogs)
            Debug.Log($"🛑 [BossMeleeModule] ForceStopAllBehaviors: {reason}");

        ForceEndAttack(reason);
        movementController?.StopMovement();

        // 保险：清动画（避免卡在Attack/Move）
        if (animationController != null)
        {
            animationController.SetAttackAnimation(false);
            animationController.SetMovingAnimation(false);
            // stun 动画由 stunController 自己维护，这里不强关
        }
    }

    #endregion

    #region Internal

    private void AutoWireControllers(bool includeInactive)
    {
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

        if (showDebugLogs)
        {
            Debug.Log($"🧩 [BossMeleeModule] AutoWire -> " +
                      $"Move={(movementController != null ? "OK" : "NULL")}, " +
                      $"Atk={(attackController != null ? "OK" : "NULL")}, " +
                      $"Stun={(stunController != null ? "OK" : "NULL")}, " +
                      $"Anim={(animationController != null ? "OK" : "NULL")}");
        }
    }

    private void BindAttackEvents()
    {
        if (attackController == null) return;

        // 防止重复绑定
        UnbindAttackEvents();

        attackController.OnAttackStarted += HandleAttackStarted;
        attackController.OnAttackEnded += HandleAttackEnded;
    }

    private void UnbindAttackEvents()
    {
        if (attackController == null) return;

        attackController.OnAttackStarted -= HandleAttackStarted;
        attackController.OnAttackEnded -= HandleAttackEnded;
    }

    private void HandleAttackStarted()
    {
        isAttacking = true;
        attackTimer = 0f;

        movementController?.StopMovement();

        if (showDebugLogs)
            Debug.Log($"🔥 [BossMeleeModule] AttackStarted -> StopMovement");
    }

    private void HandleAttackEnded()
    {
        if (!isAttacking) return; // 防重复

        isAttacking = false;
        attackTimer = 0f;

        movementController?.ResumeMovement();
        movementController?.ForceRecalculatePath();

        if (showDebugLogs)
            Debug.Log($"✅ [BossMeleeModule] AttackEnded -> ResumeMovement + RePath");
    }

    private void ForceEndAttack(string reason = "")
    {
        if (!isAttacking) return;

        if (showDebugLogs)
            Debug.Log($"🛑 [BossMeleeModule] ForceEndAttack: {reason}");

        // 通过 attackController.EndAttack() 正常触发 OnAttackEnded
        attackController?.EndAttack();

        // 保险：就算事件没触发也强行复位
        isAttacking = false;
        attackTimer = 0f;
    }

    #endregion
}

