using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Summoner 敌人的 Alert 状态模块。
/// 功能：负责召唤师在发现玩家后的"战斗宣告"过渡表现，包含原地停驻、面向玩家、播放嚎叫表现（音效/动画/特效）三个固定行为。
/// 工作链路：由状态总控脚本持有；总控负责决定何时进入/退出 Alert，本模块只负责 Alert 内部所有逻辑的驱动与维护。
/// 设计原则：行为固定、短时、不可分支；不包含移动、攻击、召唤、自卫等战斗逻辑；不包含随机行为；模块自治，总控只通过公开接口交互。
/// </summary>
public class SummonerAlertModule
{
    // ──────────────────────────────────────────────
    // 外部引用
    // ──────────────────────────────────────────────

    private Transform    selfTransform;
    private NavMeshAgent navAgent;
    private Animator     animator;
    private AudioSource  audioSource;

    // ──────────────────────────────────────────────
    // 配置（从 AlertConfig 拷贝，Initialize 后只读）
    // ──────────────────────────────────────────────

    private bool   enableAlertDebug;
    private float  alertDuration;
    private float  alertTurnSpeed;
    private bool   playAlertSound;
    private bool   playAlertEffect;
    private bool   useAlertTrigger;
    private string alertTriggerName;

    // ──────────────────────────────────────────────
    // 运行时状态
    // ──────────────────────────────────────────────

    private Transform targetPlayer;
    private bool      isInAlert;
    private bool      isFinished;
    private float     alertEndTime;

    // ──────────────────────────────────────────────
    // Debug 节流
    // ──────────────────────────────────────────────

    private float nextTickDebugTime;
    private const float TickDebugInterval = 1.0f; // Tick 阶段朝向 debug 每秒最多输出一次

    // ══════════════════════════════════════════════
    // 公开接口
    // ══════════════════════════════════════════════

    /// <summary>
    /// 初始化 Alert 模块所需引用与配置。
    /// 功能：将外部依赖与 AlertConfig 配置数据写入模块；重置运行时标记；完成后模块可被 EnterState 正常启用。
    /// 工作链路：由状态总控脚本在对象 Awake/Start 阶段调用一次；完成后模块进入待命状态，等待 EnterState。
    /// 对下游影响：Initialize 完成后，所有配置字段只读；targetPlayer 为 null，isInAlert = false，isFinished = false。
    /// </summary>
    public void Initialize(
        Transform    selfTransform,
        NavMeshAgent navAgent,
        Animator     animator,
        AudioSource  audioSource,
        AlertConfig  config)
    {
        this.selfTransform = selfTransform;
        this.navAgent      = navAgent;
        this.animator      = animator;
        this.audioSource   = audioSource;

        enableAlertDebug = config.enableAlertDebug;
        alertDuration    = config.alertDuration;
        alertTurnSpeed   = config.alertTurnSpeed;
        playAlertSound   = config.playAlertSound;
        playAlertEffect  = config.playAlertEffect;
        useAlertTrigger  = config.useAlertTrigger;
        alertTriggerName = config.alertTriggerName;

        targetPlayer = null;
        isInAlert    = false;
        isFinished   = false;

        LogAlert("[Init] selfTransform = "   + selfTransform.name);
        LogAlert("[Init] alertDuration = "   + alertDuration);
        LogAlert("[Init] alertTurnSpeed = "  + alertTurnSpeed);
        LogAlert("[Init] playAlertSound = "  + playAlertSound);
        LogAlert("[Init] playAlertEffect = " + playAlertEffect);
        LogAlert("[Init] useAlertTrigger = " + useAlertTrigger);
        LogAlert("[Init] alertTriggerName = " + alertTriggerName);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 进入 Alert 状态，锁定目标玩家并启动警戒表现。
    /// 功能：接收当前玩家 Transform 引用；停止 NavMeshAgent 移动；关闭 Walk 动画；设置计时与状态标记；触发嚎叫表现（音效/动画/特效）。
    /// 工作链路：由状态总控脚本在从 Idle 切换到 Alert 时调用；之后由每帧 Tick 驱动剩余计时与面向玩家旋转。
    /// 对下游影响：isInAlert = true，isFinished = false，alertEndTime 被设定；targetPlayer 被锁定，Alert 期间不更换目标。
    /// </summary>
    public void EnterState(Transform playerTransform)
    {
        nextTickDebugTime = Time.time;
        targetPlayer = playerTransform;
        isInAlert    = true;
        isFinished   = false;
        alertEndTime = Time.time + alertDuration;

        navAgent.isStopped = true;
        animator.SetBool("Walk", false);

        TriggerAlertPresentation();

        LogAlert("[Enter] Enter AlertState");
        LogAlert("[Enter] targetPlayer = " + (targetPlayer != null ? targetPlayer.name : "NULL")
            + " | position = " + (targetPlayer != null ? targetPlayer.position.ToString() : "N/A"));
        LogAlert("[Enter] alertDuration = " + alertDuration + "s | alertEndTime = " + alertEndTime.ToString("F2"));
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Alert 状态每帧更新入口，处理面向玩家与计时逻辑。
    /// 功能：若 isInAlert == false 则直接返回（容错）；每帧调用 UpdateFacingToPlayer 缓慢旋转朝向玩家；检查计时是否到达 alertEndTime，到达时将 isFinished 置为 true。
    /// 工作链路：由状态总控脚本在当前主状态为 Alert 时每帧调用；IsFinished 查询在 Tick 之后执行。
    /// 对下游影响：isFinished 在时间到达后被置为 true，总控在下一次 IsFinished 查询时获得 true 并切入 Summoning。
    /// </summary>
    public void Tick()
    {
        if (!isInAlert)
            return;

        UpdateFacingToPlayer();

        if (Time.time >= alertEndTime)
        {
            isFinished = true;

            LogAlert("[Tick] Alert finished."
                + " | Time = "         + Time.time.ToString("F2")
                + " | alertEndTime = " + alertEndTime.ToString("F2"));
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前 Alert 是否已完成。
    /// 功能：纯布尔查询，返回 isFinished 的当前值；不修改任何内部状态。
    /// 工作链路：由状态总控脚本在 Alert 状态下每帧调用 Tick 之后查询；若返回 true，总控负责调用 ExitState 并切入 Summoning。
    /// 对下游影响：只读查询，无副作用。
    /// </summary>
    public bool IsFinished()
    {
        return isFinished;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 退出 Alert 状态，清理内部运行时标记。
    /// 功能：将 isInAlert、isFinished 重置为 false；清空 targetPlayer 引用；输出退出日志。
    /// 工作链路：由状态总控脚本在 Alert 完成后切出时调用（切入 Summoning 之前）；调用后本模块进入休眠，等待下次 EnterState。
    /// 对下游影响：模块停止驱动，Tick 若仍被调用会因 isInAlert == false 直接返回，不产生错误。
    /// </summary>
    public void ExitState()
    {
        LogAlert("[Exit] Exit AlertState"
            + " | targetPlayer was = " + (targetPlayer != null ? targetPlayer.name : "NULL"));

        isInAlert    = false;
        isFinished   = false;
        targetPlayer = null;

        LogAlert("[Exit] Cleared all runtime flags.");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前 Alert 模块内部的简要运行状态摘要字符串。
    /// 功能：汇总 isInAlert、isFinished、targetPlayer、剩余 Alert 时间等信息，供调试面板与日志使用。
    /// 工作链路：可由总控脚本或外部 Debug 面板随时调用；不改变任何内部状态。
    /// 对下游影响：只读，无副作用。
    /// </summary>
    public string GetDebugStateSummary()
    {
        float remaining = Mathf.Max(0f, alertEndTime - Time.time);
        return $"[SummonerAlert Summary] isInAlert={isInAlert} | isFinished={isFinished}"
            + $" | target={(targetPlayer != null ? targetPlayer.name : "NULL")}"
            + $" | remaining={remaining:F2}s";
    }

    // ══════════════════════════════════════════════
    // 内部方法
    // ══════════════════════════════════════════════

    /// <summary>
    /// Alert 期间缓慢将自身朝向旋转至面向目标玩家。
    /// 功能：计算自身到玩家的水平方向向量，使用 Quaternion.Slerp 平滑旋转；忽略 Y 轴高度差，仅在 XZ 平面旋转。
    /// 算法：基于球面线性插值（Slerp）实现角速度均匀的朝向过渡，alertTurnSpeed * deltaTime 控制每帧旋转量。
    /// 工作链路：由 Tick 每帧调用；targetPlayer 为 null 时安全返回。
    /// 对下游影响：selfTransform.rotation 持续向玩家方向收敛，直至 Alert 结束或被 ExitState 打断。
    /// </summary>
    private void UpdateFacingToPlayer()
    {
        if (targetPlayer == null)
            return;

        Vector3 lookDir = targetPlayer.position - selfTransform.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDir.normalized);
        selfTransform.rotation    = Quaternion.Slerp(
            selfTransform.rotation,
            targetRotation,
            alertTurnSpeed * Time.deltaTime);

        // 节流 debug 输出，每 TickDebugInterval 秒最多输出一次
        if (enableAlertDebug && Time.time >= nextTickDebugTime)
        {
            nextTickDebugTime = Time.time + TickDebugInterval;
            float remaining = Mathf.Max(0f, alertEndTime - Time.time);
            LogAlert("[Tick] Facing player | TargetDir = " + lookDir.normalized
                + " | RemainingAlert = " + remaining.ToString("F2") + "s");
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 触发 Alert 状态的全部嚎叫表现（音效 / 动画 / 特效）。
    /// 功能：根据配置开关依次触发音效播放、动画 Trigger 设置、特效播放；每项触发均输出 debug 日志；缺少资产时安全跳过不报错。
    /// 工作链路：由 EnterState 在进入 Alert 时调用一次；不会被 Tick 重复调用。
    /// 对下游影响：音效通过 AudioSource 播放；动画通过 Animator.SetTrigger 触发；特效为预留接口，当前版本仅输出日志占位。
    /// </summary>
    private void TriggerAlertPresentation()
    {
        // 音效触发
        if (playAlertSound && audioSource != null)
        {
            audioSource.Play();
            LogAlert("[Action] Play alert sound");
        }
        else if (playAlertSound && audioSource == null)
        {
            LogAlert("[Action] playAlertSound = true but AudioSource is null. Skipped.");
        }

        // 动画触发
        if (useAlertTrigger && animator != null)
        {
            animator.SetTrigger(alertTriggerName);
            LogAlert("[Action] Trigger alert animation = " + alertTriggerName);
        }
        else if (useAlertTrigger && animator == null)
        {
            LogAlert("[Action] useAlertTrigger = true but Animator is null. Skipped.");
        }

        // 特效触发（预留接口，当前版本仅输出日志占位）
        if (playAlertEffect)
        {
            // TODO: 在此处实例化或播放警戒特效
            LogAlert("[Action] Play alert effect (placeholder — no VFX asset assigned yet)");
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 统一的 Alert 模块 Debug 输出方法，受 enableAlertDebug 开关控制。
    /// 功能：在 enableAlertDebug == true 时输出带固定前缀 [SummonerAlert] 的日志，否则不产生任何输出。
    /// 工作链路：被本模块所有内部函数调用；总控脚本无需调用此方法。
    /// </summary>
    private void LogAlert(string message)
    {
        if (!enableAlertDebug)
            return;
        Debug.Log("[SummonerAlert]" + message);
    }
}
