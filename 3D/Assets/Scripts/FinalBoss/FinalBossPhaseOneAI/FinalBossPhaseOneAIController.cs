using UnityEngine;
using UnityEngine.AI;

public class FinalBossPhaseOneAIController : MonoBehaviour
{
    public enum MoveAnimationState
    {
        Idle = 0,
        Approach = 1,
        ObserveStrafe = 2,
        QuickstepForward = 3,
        QuickstepBackward = 4
    }

    public enum AttackAnimationType
    {
        None = 0,
        Punch = 1,
        Kick = 2
    }

    public enum DistanceBand
    {
        DirectAttack = 0,
        MidOrNear = 1,
        Far = 2
    }

    [Header("Config")]
    [SerializeField] private FinalBossPhaseOneConfig config;

    [Header("References")]
    [SerializeField] private HealthSystem bossHealth;
    [SerializeField] private ColorComponent bossColor;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private ColorComponent playerColor;
    [SerializeField] private GameObject prepareAttackTelegraphPrefab;
    [SerializeField] private Transform prepareAttackTelegraphSpawnPoint;

    [Header("Hitboxes")]
    [Tooltip("拳头上的 Hitbox（挂有 FinalBossPhaseOneHitbox 的 GameObject）")]
    [SerializeField] private FinalBossPhaseOneHitbox punchHitbox;
    [Tooltip("脚上的 Hitbox（挂有 FinalBossPhaseOneHitbox 的 GameObject）")]
    [SerializeField] private FinalBossPhaseOneHitbox kickHitbox;

    [Header("Special Attacks")]
    [Tooltip("冲击波发射器（三连冲击波用）")]
    [SerializeField] private FinalBossShockwaveEmitter phaseOneShockwaveEmitter;
    [Tooltip("远程射击弹体 Prefab")]
    [SerializeField] private GameObject rangedShotPrefab;
    [Tooltip("远程射击发射点")]
    [SerializeField] private Transform rangedShotSpawnPoint;
    [Tooltip("弹体发射速度")]
    [SerializeField] private float rangedShotSpeed = 15f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private FinalBossStageOneController stageController;
    private FinalBossPhaseOneContext context;
    private FinalBossPhaseOneState currentState;
    private bool stageActive;
    private bool playerAggroRequested;

    private FinalBossPhaseOneSpawnIntroState spawnIntroState;
    private FinalBossPhaseOneIdleObserveState idleObserveState;
    private FinalBossPhaseOneApproachState approachState;
    private FinalBossPhaseOneStrafePressureState strafePressureState;
    private FinalBossPhaseOnePrepareAttackState prepareAttackState;
    private FinalBossPhaseOneAttackCommitState attackCommitState;
    private FinalBossPhaseOneAttackRecoverState attackRecoverState;
    private FinalBossPhaseOneStunnedState stunnedState;
    private FinalBossPhaseOneRetreatResetState retreatResetState;

    public string CurrentStateName => currentState != null ? currentState.StateName : "None";
    public bool IsInIdleObserve => currentState == idleObserveState;
    public bool IsInAttackCommit => currentState == attackCommitState;
    public MoveAnimationState CurrentMoveAnimationState { get; private set; } = MoveAnimationState.Idle;
    public AttackAnimationType CurrentAttackAnimationType { get; private set; } = AttackAnimationType.None;
    public AttackAnimationType QueuedAttackAnimationType { get; private set; } = AttackAnimationType.None;
    public bool IsStunnedAnimationActive { get; private set; }
    private bool suppressNextPrepareTelegraph;

    public int IntroTriggerVersion { get; private set; }
    public int QuickstepForwardTriggerVersion { get; private set; }
    public int QuickstepBackwardTriggerVersion { get; private set; }
    public int PunchTriggerVersion { get; private set; }
    public int KickTriggerVersion { get; private set; }
    public int StunnedTriggerVersion { get; private set; }
    public int SurpriseAttackTriggerVersion { get; private set; }
    public int ShockwaveCastTriggerVersion { get; private set; }
    public int RangedShotTriggerVersion { get; private set; }
    public int ColorSwitchTriggerVersion { get; private set; }

    // --- 颜色切换共享状态（Approach / StrafePressure 跨状态保留） ---
    private float colorSwitchCooldownRemaining;
    private float colorSwitchRollTimer;
    private bool colorSwitchAnimPlaying;
    private float colorSwitchAnimTimer;

    private void Awake()
    {
        AutoWireReferences();
        BuildContext();
        BuildStates();
        enabled = false;
    }

    private void Update()
    {
        if (!stageActive || currentState == null)
            return;

        currentState.Tick();
    }

    public void BeginStage(FinalBossStageOneController owner)
    {
        if (config == null)
        {
            Debug.LogError("[FinalBossPhaseOneAIController] Missing FinalBossPhaseOneConfig");
            return;
        }

        stageController = owner;
        stageActive = true;
        enabled = true;
        playerAggroRequested = false;
        context?.SetBossVisualVisible(true);
        CurrentMoveAnimationState = MoveAnimationState.Idle;
        CurrentAttackAnimationType = AttackAnimationType.None;
        QueuedAttackAnimationType = AttackAnimationType.None;
        IsStunnedAnimationActive = false;
        suppressNextPrepareTelegraph = false;
        colorSwitchCooldownRemaining = 0f;
        colorSwitchRollTimer = 0f;
        colorSwitchAnimPlaying = false;
        colorSwitchAnimTimer = 0f;
        ChangeState(spawnIntroState);
    }

    public void EndStage()
    {
        stageActive = false;
        enabled = false;
        currentState?.Exit();
        currentState = null;
        context?.StopMovement();
        context?.SetBossVisualVisible(true);
        CurrentMoveAnimationState = MoveAnimationState.Idle;
        CurrentAttackAnimationType = AttackAnimationType.None;
        QueuedAttackAnimationType = AttackAnimationType.None;
        IsStunnedAnimationActive = false;
        suppressNextPrepareTelegraph = false;
    }

    public void NotifyPlayerAggroHit()
    {
        if (!stageActive) return;
        playerAggroRequested = true;
        Log("NotifyPlayerAggroHit");
    }

    public bool ConsumePlayerAggroRequest()
    {
        if (!playerAggroRequested) return false;
        playerAggroRequested = false;
        return true;
    }

    public void EnterIdleObserve() => ChangeState(idleObserveState);
    public void EnterApproachPlayer() => ChangeState(approachState);
    public void EnterStrafePressure() => ChangeState(strafePressureState);
    public void EnterPrepareAttack() => ChangeState(prepareAttackState);
    public void EnterAttackCommit() => ChangeState(attackCommitState);
    public void EnterAttackRecover() => ChangeState(attackRecoverState);

    public void EnterCombatStateByDistance()
    {
        DistanceBand band = EvaluateDistanceBand();
        switch (band)
        {
            case DistanceBand.DirectAttack:
                EnterPrepareAttack();
                break;
            case DistanceBand.MidOrNear:
                EnterStrafePressure();
                break;
            default:
                EnterApproachPlayer();
                break;
        }
    }

    public void EnterRetreatReset() => ChangeState(retreatResetState);

    public void EnterStunned()
    {
        if (!stageActive || currentState == null || !currentState.CanBeInterruptedByStun)
            return;

        ChangeState(stunnedState);
    }

    public void ChangeState(FinalBossPhaseOneState nextState)
    {
        if (nextState == null) return;

        currentState?.Exit();
        currentState = nextState;
        currentState.Enter();
        Log($"ChangeState -> {currentState.StateName}");
    }

    public DistanceBand EvaluateDistanceBand()
    {
        if (context == null || config == null)
            return DistanceBand.Far;

        float distance = context.DistanceToPlayer;
        if (distance <= config.directAttackDistance)
            return DistanceBand.DirectAttack;
        if (distance <= config.farDistanceThreshold)
            return DistanceBand.MidOrNear;
        return DistanceBand.Far;
    }

    public bool IsSameColorAsPlayer()
    {
        if (bossColor == null || playerColor == null)
            return false;

        return bossColor.CurrentColor == playerColor.CurrentColor;
    }

    public void SetMoveAnimationState(MoveAnimationState state)
    {
        CurrentMoveAnimationState = state;
    }

    public void SetAttackAnimationType(AttackAnimationType type)
    {
        CurrentAttackAnimationType = type;
    }

    public void QueueAttackAnimationType(AttackAnimationType type)
    {
        QueuedAttackAnimationType = type;
    }

    public AttackAnimationType ConsumeQueuedAttackAnimationType()
    {
        AttackAnimationType result = QueuedAttackAnimationType;
        QueuedAttackAnimationType = AttackAnimationType.None;
        return result;
    }

    public AttackAnimationType RollNextAttackAnimationType()
    {
        FinalBossPhaseOneAttackCommitStateData attackData = config != null ? config.attackCommit : null;
        if (attackData == null)
            return AttackAnimationType.Punch;

        return Random.value <= attackData.punchChance
            ? AttackAnimationType.Punch
            : AttackAnimationType.Kick;
    }

    public float GetAttackRangeForType(AttackAnimationType type)
    {
        if (config == null || config.attackCommit == null)
            return 0f;

        return type == AttackAnimationType.Kick
            ? config.attackCommit.kickAttackRange
            : config.attackCommit.punchAttackRange;
    }

    public float GetMaxAttackRange()
    {
        if (config == null || config.attackCommit == null)
            return 0f;

        return Mathf.Max(config.attackCommit.punchAttackRange, config.attackCommit.kickAttackRange);
    }

    public float GetDirectAttackEnterDistance()
    {
        if (config == null)
            return 0f;

        return config.directAttackDistance;
    }

    public void SetStunnedAnimationActive(bool active)
    {
        IsStunnedAnimationActive = active;
    }

    public void SuppressNextPrepareTelegraph()
    {
        suppressNextPrepareTelegraph = true;
    }

    public bool ConsumePrepareTelegraphSuppression()
    {
        bool result = suppressNextPrepareTelegraph;
        suppressNextPrepareTelegraph = false;
        return result;
    }

    public void TriggerIntroAnimation() => IntroTriggerVersion++;
    public void TriggerQuickstepForwardAnimation() => QuickstepForwardTriggerVersion++;
    public void TriggerQuickstepBackwardAnimation() => QuickstepBackwardTriggerVersion++;
    public void TriggerPunchAnimation() => PunchTriggerVersion++;
    public void TriggerKickAnimation() => KickTriggerVersion++;
    public void TriggerStunnedAnimation() => StunnedTriggerVersion++;
    public void TriggerSurpriseAttackAnimation() => SurpriseAttackTriggerVersion++;
    public void TriggerShockwaveCastAnimation() => ShockwaveCastTriggerVersion++;
    public void TriggerRangedShotAnimation() => RangedShotTriggerVersion++;
    public void TriggerColorSwitchAnimation() => ColorSwitchTriggerVersion++;

    // ══════════════════════════════════════════════════════════════
    // 颜色切换（Approach / StrafePressure 共享）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 每帧由 Approach / StrafePressure 调用，更新冷却和滚动计时器。
    /// 返回 true 表示本帧触发了颜色切换动画（调用方应跳过其他行为）。
    /// </summary>
    public bool TickColorSwitch()
    {
        if (config == null) return false;

        // 正在播放切换动画
        if (colorSwitchAnimPlaying)
        {
            colorSwitchAnimTimer += Time.deltaTime;
            if (colorSwitchAnimTimer >= config.colorSwitchAnimDuration)
            {
                // 动画结束，真正切换颜色
                PerformColorSwitch();
                colorSwitchAnimPlaying = false;
            }
            return true;
        }

        // 冷却中
        if (colorSwitchCooldownRemaining > 0f)
        {
            colorSwitchCooldownRemaining -= Time.deltaTime;
            return false;
        }

        // 滚动间隔
        colorSwitchRollTimer += Time.deltaTime;
        if (colorSwitchRollTimer < config.colorSwitchRollInterval)
            return false;

        // 滚动概率
        colorSwitchRollTimer = 0f;
        float roll = Random.value;
        if (roll > config.colorSwitchChance)
            return false;

        // 触发切换动画
        colorSwitchAnimPlaying = true;
        colorSwitchAnimTimer = 0f;
        colorSwitchCooldownRemaining = config.colorSwitchCooldown;
        TriggerColorSwitchAnimation();
        Log($"ColorSwitch -> animation started (roll={roll:F3} <= {config.colorSwitchChance:F3})");
        return true;
    }

    /// <summary>
    /// 颜色切换是否正在播放动画（调用方用来决定是否冻结其他行为）
    /// </summary>
    public bool IsColorSwitchPlaying => colorSwitchAnimPlaying;

    private void PerformColorSwitch()
    {
        if (bossColor == null) return;

        ColorType oldColor = bossColor.CurrentColor;
        bossColor.ToggleColor();
        Log($"ColorSwitch -> {oldColor} => {bossColor.CurrentColor}");
    }

    public FinalBossPhaseOneConfig GetConfig() => config;
    public FinalBossPhaseOneContext GetContext() => context;

    public FinalBossPhaseOneHitbox GetPunchHitbox() => punchHitbox;
    public FinalBossPhaseOneHitbox GetKickHitbox() => kickHitbox;

    /// <summary>
    /// 根据攻击类型获取对应的 Hitbox
    /// </summary>
    public FinalBossPhaseOneHitbox GetHitboxForType(AttackAnimationType type)
    {
        return type == AttackAnimationType.Kick ? kickHitbox : punchHitbox;
    }

    public FinalBossShockwaveEmitter GetPhaseOneShockwaveEmitter() => phaseOneShockwaveEmitter;

    /// <summary>
    /// 发射冲击波（供 StrafePressure 三连冲击波调用）
    /// </summary>
    public void FireShockwave()
    {
        if (phaseOneShockwaveEmitter == null) return;

        // 设置冲击波颜色为boss当前颜色
        phaseOneShockwaveEmitter.SetSourceColorComponent(bossColor);
        phaseOneShockwaveEmitter.FireOnce();
        Log("FireShockwave -> emitter fired once");
    }

    /// <summary>
    /// 发射远程弹体（供 StrafePressure 等待射击调用）
    /// </summary>
    public void FireRangedShot()
    {
        if (rangedShotPrefab == null || playerTransform == null) return;

        Transform spawnPoint = rangedShotSpawnPoint != null ? rangedShotSpawnPoint : transform;

        GameObject projectile = Object.Instantiate(rangedShotPrefab, spawnPoint.position, spawnPoint.rotation);

        // 继承 Boss 颜色
        ColorComponent projectileColor = projectile.GetComponent<ColorComponent>();
        if (projectileColor != null && bossColor != null)
            projectileColor.CurrentColor = bossColor.CurrentColor;

        // 通过 ProjectileController 初始化飞行（与 RangeEnemy2 一致）
        ProjectileController projectileController = projectile.GetComponent<ProjectileController>();
        if (projectileController != null)
        {
            projectileController.InitializeProjectile(
                playerTransform.position,
                rangedShotSpeed,
                gameObject
            );
        }

        Log($"FireRangedShot -> {rangedShotPrefab.name} toward player, speed={rangedShotSpeed}");
    }

    private void AutoWireReferences()
    {
        bossHealth ??= GetComponent<HealthSystem>();
        bossColor ??= GetComponent<ColorComponent>();
        navAgent ??= GetComponent<NavMeshAgent>();
        animator ??= GetComponentInChildren<Animator>();

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerColor = player.GetComponent<ColorComponent>();
            }
        }

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void BuildContext()
    {
        context = new FinalBossPhaseOneContext
        {
            BossTransform = transform,
            PlayerTransform = playerTransform,
            CameraTransform = cameraTransform,
            NavAgent = navAgent,
            Animator = animator,
            BossHealth = bossHealth,
            BossColor = bossColor,
            PlayerColor = playerColor,
            BossGameObject = gameObject,
            Config = config,
            PrepareAttackTelegraphPrefab = prepareAttackTelegraphPrefab,
            PrepareAttackTelegraphSpawnPoint = prepareAttackTelegraphSpawnPoint,
            BossRenderers = GetComponentsInChildren<Renderer>(true),
            BossColorVisualizers = GetComponentsInChildren<ColorVisualizer>(true)
        };

        if (navAgent != null)
            navAgent.updateRotation = false;
    }

    private void BuildStates()
    {
        spawnIntroState = new FinalBossPhaseOneSpawnIntroState(this, context);
        idleObserveState = new FinalBossPhaseOneIdleObserveState(this, context);
        approachState = new FinalBossPhaseOneApproachState(this, context);
        strafePressureState = new FinalBossPhaseOneStrafePressureState(this, context);
        prepareAttackState = new FinalBossPhaseOnePrepareAttackState(this, context);
        attackCommitState = new FinalBossPhaseOneAttackCommitState(this, context);
        attackRecoverState = new FinalBossPhaseOneAttackRecoverState(this, context);
        stunnedState = new FinalBossPhaseOneStunnedState(this, context);
        retreatResetState = new FinalBossPhaseOneRetreatResetState(this, context);
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossPhaseOneAIController] {message}");
    }
}
