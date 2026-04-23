using UnityEngine;
using UnityEngine.AI;

public class FinalBossPhaseOneAnimationBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FinalBossPhaseOneAIController phaseOneAI;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private Animator animator;

    [Header("Parameters")]
    [SerializeField] private string moveStateParam = "MoveState";
    [SerializeField] private string attackTypeParam = "AttackType";
    [SerializeField] private string isStunnedParam = "IsStunned";
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string introTrigger = "Intro";
    [SerializeField] private string quickstepForwardTrigger = "DoQuickstepForward";
    [SerializeField] private string quickstepBackwardTrigger = "DoQuickstepBackward";
    [SerializeField] private string punchTrigger = "DoPunch";
    [SerializeField] private string kickTrigger = "DoKick";
    [SerializeField] private string stunnedTrigger = "DoStunned";
    [SerializeField] private string surpriseAttackTrigger = "DoSurpriseAttack";
    [SerializeField] private string shockwaveCastTrigger = "DoShockwaveCast";
    [SerializeField] private string rangedShotTrigger = "DoRangedShot";
    [SerializeField] private string colorSwitchTrigger = "DoColorSwitch";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private int lastIntroVersion = -1;
    private int lastQuickstepForwardVersion = -1;
    private int lastQuickstepBackwardVersion = -1;
    private int lastPunchVersion = -1;
    private int lastKickVersion = -1;
    private int lastStunnedVersion = -1;
    private int lastSurpriseAttackVersion = -1;
    private int lastShockwaveCastVersion = -1;
    private int lastRangedShotVersion = -1;
    private int lastColorSwitchVersion = -1;
    private FinalBossPhaseOneAIController.MoveAnimationState lastMoveState = (FinalBossPhaseOneAIController.MoveAnimationState)(-1);
    private FinalBossPhaseOneAIController.AttackAnimationType lastAttackType = (FinalBossPhaseOneAIController.AttackAnimationType)(-1);
    private bool lastIsStunned;

    private void Awake()
    {
        phaseOneAI ??= GetComponent<FinalBossPhaseOneAIController>();
        navAgent ??= GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        PrimeRuntimeState();
    }

    private void Update()
    {
        if (phaseOneAI == null || animator == null || !phaseOneAI.enabled)
            return;

        SyncSpeedParameter();
        SyncStateParameters();
        SyncTriggers();
    }

    private void SyncSpeedParameter()
    {
        if (animator == null || string.IsNullOrWhiteSpace(speedParam))
            return;

        float speed = 0f;
        if (navAgent != null && navAgent.enabled)
            speed = navAgent.velocity.magnitude;

        animator.SetFloat(speedParam, speed);
    }

    private void SyncStateParameters()
    {
        if (phaseOneAI.CurrentMoveAnimationState != lastMoveState)
        {
            animator.SetInteger(moveStateParam, (int)phaseOneAI.CurrentMoveAnimationState);
            lastMoveState = phaseOneAI.CurrentMoveAnimationState;
        }

        if (phaseOneAI.CurrentAttackAnimationType != lastAttackType)
        {
            animator.SetInteger(attackTypeParam, (int)phaseOneAI.CurrentAttackAnimationType);
            lastAttackType = phaseOneAI.CurrentAttackAnimationType;
        }

        if (phaseOneAI.IsStunnedAnimationActive != lastIsStunned)
        {
            animator.SetBool(isStunnedParam, phaseOneAI.IsStunnedAnimationActive);
            lastIsStunned = phaseOneAI.IsStunnedAnimationActive;
        }
    }

    private void SyncTriggers()
    {
        if (phaseOneAI.IntroTriggerVersion != lastIntroVersion)
        {
            animator.SetTrigger(introTrigger);
            lastIntroVersion = phaseOneAI.IntroTriggerVersion;
            Log($"Trigger -> {introTrigger}");
        }

        if (phaseOneAI.QuickstepForwardTriggerVersion != lastQuickstepForwardVersion)
        {
            animator.SetTrigger(quickstepForwardTrigger);
            lastQuickstepForwardVersion = phaseOneAI.QuickstepForwardTriggerVersion;
            Log($"Trigger -> {quickstepForwardTrigger}");
        }

        if (phaseOneAI.QuickstepBackwardTriggerVersion != lastQuickstepBackwardVersion)
        {
            animator.SetTrigger(quickstepBackwardTrigger);
            lastQuickstepBackwardVersion = phaseOneAI.QuickstepBackwardTriggerVersion;
            Log($"Trigger -> {quickstepBackwardTrigger}");
        }

        if (phaseOneAI.PunchTriggerVersion != lastPunchVersion)
        {
            animator.SetTrigger(punchTrigger);
            lastPunchVersion = phaseOneAI.PunchTriggerVersion;
            Log($"Trigger -> {punchTrigger}");
        }

        if (phaseOneAI.KickTriggerVersion != lastKickVersion)
        {
            animator.SetTrigger(kickTrigger);
            lastKickVersion = phaseOneAI.KickTriggerVersion;
            Log($"Trigger -> {kickTrigger}");
        }

        if (phaseOneAI.StunnedTriggerVersion != lastStunnedVersion)
        {
            animator.SetTrigger(stunnedTrigger);
            lastStunnedVersion = phaseOneAI.StunnedTriggerVersion;
            Log($"Trigger -> {stunnedTrigger}");
        }

        if (phaseOneAI.SurpriseAttackTriggerVersion != lastSurpriseAttackVersion)
        {
            animator.SetTrigger(surpriseAttackTrigger);
            lastSurpriseAttackVersion = phaseOneAI.SurpriseAttackTriggerVersion;
            Log($"Trigger -> {surpriseAttackTrigger}");
        }

        if (phaseOneAI.ShockwaveCastTriggerVersion != lastShockwaveCastVersion)
        {
            animator.SetTrigger(shockwaveCastTrigger);
            lastShockwaveCastVersion = phaseOneAI.ShockwaveCastTriggerVersion;
            Log($"Trigger -> {shockwaveCastTrigger}");
        }

        if (phaseOneAI.RangedShotTriggerVersion != lastRangedShotVersion)
        {
            animator.SetTrigger(rangedShotTrigger);
            lastRangedShotVersion = phaseOneAI.RangedShotTriggerVersion;
            Log($"Trigger -> {rangedShotTrigger}");
        }

        if (phaseOneAI.ColorSwitchTriggerVersion != lastColorSwitchVersion)
        {
            animator.SetTrigger(colorSwitchTrigger);
            lastColorSwitchVersion = phaseOneAI.ColorSwitchTriggerVersion;
            Log($"Trigger -> {colorSwitchTrigger}");
        }
    }

    private void PrimeRuntimeState()
    {
        if (phaseOneAI == null || animator == null)
            return;

        lastIntroVersion = phaseOneAI.IntroTriggerVersion;
        lastQuickstepForwardVersion = phaseOneAI.QuickstepForwardTriggerVersion;
        lastQuickstepBackwardVersion = phaseOneAI.QuickstepBackwardTriggerVersion;
        lastPunchVersion = phaseOneAI.PunchTriggerVersion;
        lastKickVersion = phaseOneAI.KickTriggerVersion;
        lastStunnedVersion = phaseOneAI.StunnedTriggerVersion;
        lastSurpriseAttackVersion = phaseOneAI.SurpriseAttackTriggerVersion;
        lastShockwaveCastVersion = phaseOneAI.ShockwaveCastTriggerVersion;
        lastRangedShotVersion = phaseOneAI.RangedShotTriggerVersion;
        lastColorSwitchVersion = phaseOneAI.ColorSwitchTriggerVersion;
        lastMoveState = phaseOneAI.CurrentMoveAnimationState;
        lastAttackType = phaseOneAI.CurrentAttackAnimationType;
        lastIsStunned = phaseOneAI.IsStunnedAnimationActive;

        if (!string.IsNullOrWhiteSpace(moveStateParam))
            animator.SetInteger(moveStateParam, (int)lastMoveState);

        if (!string.IsNullOrWhiteSpace(attackTypeParam))
            animator.SetInteger(attackTypeParam, (int)lastAttackType);

        if (!string.IsNullOrWhiteSpace(isStunnedParam))
            animator.SetBool(isStunnedParam, lastIsStunned);
    }

    // ===== 动画事件回调：Hitbox 开关 =====
    // 在 Punch/Kick 攻击动画的关键帧上添加 Animation Event 调用以下方法

    /// <summary>
    /// 动画事件：开启 Punch Hitbox 判定窗口
    /// </summary>
    public void OnPunchHitboxEnable()
    {
        FinalBossPhaseOneHitbox hitbox = phaseOneAI != null ? phaseOneAI.GetPunchHitbox() : null;
        if (hitbox != null)
            hitbox.EnableHitbox();
        Log("AnimEvent -> OnPunchHitboxEnable");
    }

    /// <summary>
    /// 动画事件：关闭 Punch Hitbox 判定窗口
    /// </summary>
    public void OnPunchHitboxDisable()
    {
        FinalBossPhaseOneHitbox hitbox = phaseOneAI != null ? phaseOneAI.GetPunchHitbox() : null;
        if (hitbox != null)
            hitbox.DisableHitbox();
        Log("AnimEvent -> OnPunchHitboxDisable");
    }

    /// <summary>
    /// 动画事件：开启 Kick Hitbox 判定窗口
    /// </summary>
    public void OnKickHitboxEnable()
    {
        FinalBossPhaseOneHitbox hitbox = phaseOneAI != null ? phaseOneAI.GetKickHitbox() : null;
        if (hitbox != null)
            hitbox.EnableHitbox();
        Log("AnimEvent -> OnKickHitboxEnable");
    }

    /// <summary>
    /// 动画事件：关闭 Kick Hitbox 判定窗口
    /// </summary>
    public void OnKickHitboxDisable()
    {
        FinalBossPhaseOneHitbox hitbox = phaseOneAI != null ? phaseOneAI.GetKickHitbox() : null;
        if (hitbox != null)
            hitbox.DisableHitbox();
        Log("AnimEvent -> OnKickHitboxDisable");
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossPhaseOneAnimationBridge] {message}");
    }
}
