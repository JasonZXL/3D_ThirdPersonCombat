using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Summoner 敌人的 Commanding 状态模块。
/// 功能：负责召唤师在召唤完成后的"维持军势"流程，包含接管受控杂兵、批量开启/关闭指挥 Buff、持续挥旗表现、检测玩家逼近中断条件四个核心行为。
/// 工作链路：由状态总控脚本持有；总控负责决定何时进入/退出 Commanding，本模块只负责 Commanding 内部所有逻辑的驱动与维护。
/// 设计原则：Buff 开关通过 ISummonerCommandBuffTarget 接口委托，不硬编码数值；EnterState 开 Buff，ExitState 关 Buff，Tick 不重复 Apply；不包含移动、攻击、召唤、自卫逻辑；模块自治，总控只通过公开接口交互。
/// </summary>
public class SummonerCommandingModule
{
    // ──────────────────────────────────────────────
    // 外部引用
    // ──────────────────────────────────────────────

    private Transform    selfTransform;
    private NavMeshAgent navAgent;
    private Animator     animator;

    // ──────────────────────────────────────────────
    // 配置（从 CommandingConfig 拷贝，Initialize 后只读）
    // ──────────────────────────────────────────────

    private bool   enableCommandingDebug;
    private float  selfDefenseBreakDistance;
    private bool   keepFacingPlayerDuringCommanding;
    private float  commandingTurnSpeed;
    private bool   useBacklineHold;
    private bool   useCommandingBool;
    private string commandingBoolName;
    private bool   playCommandingEffect;
    private float  reactionDelay;

    // ──────────────────────────────────────────────
    // 运行时状态
    // ──────────────────────────────────────────────

    private Transform        targetPlayer;
    private bool             isInCommanding;
    private bool             shouldBreak;
    private bool             isReacting;
    private float            reactionEndTime;
    private List<GameObject> controlledMinions;

    // ──────────────────────────────────────────────
    // Debug 节流
    // ──────────────────────────────────────────────

    private float nextTickDebugTime;
    private const float TickDebugInterval = 1.0f;

    // ══════════════════════════════════════════════
    // 公开接口
    // ══════════════════════════════════════════════

    /// <summary>
    /// 初始化 Commanding 模块所需引用与配置。
    /// 功能：将外部依赖与 CommandingConfig 配置数据写入模块；重置运行时标记；初始化 controlledMinions 列表。
    /// 工作链路：由状态总控脚本在对象 Awake/Start 阶段调用一次；完成后模块进入待命状态，等待 EnterState。
    /// 对下游影响：Initialize 完成后，所有配置字段只读；targetPlayer 为 null，isInCommanding = false，shouldBreak = false。
    /// </summary>
    public void Initialize(
        Transform        selfTransform,
        NavMeshAgent     navAgent,
        Animator         animator,
        CommandingConfig config)
    {
        this.selfTransform = selfTransform;
        this.navAgent      = navAgent;
        this.animator      = animator;

        enableCommandingDebug           = config.enableCommandingDebug;
        selfDefenseBreakDistance         = config.selfDefenseBreakDistance;
        keepFacingPlayerDuringCommanding = config.keepFacingPlayerDuringCommanding;
        commandingTurnSpeed             = config.commandingTurnSpeed;
        useBacklineHold                 = config.useBacklineHold;
        useCommandingBool               = config.useCommandingBool;
        commandingBoolName              = config.commandingBoolName;
        playCommandingEffect            = config.playCommandingEffect;
        reactionDelay                   = config.reactionDelay;

        targetPlayer      = null;
        isInCommanding    = false;
        shouldBreak       = false;
        isReacting        = false;
        reactionEndTime   = 0f;
        controlledMinions = new List<GameObject>();

        LogCommand("[Init] selfTransform = "                   + selfTransform.name);
        LogCommand("[Init] selfDefenseBreakDistance = "         + selfDefenseBreakDistance);
        LogCommand("[Init] keepFacingPlayerDuringCommanding = " + keepFacingPlayerDuringCommanding);
        LogCommand("[Init] commandingTurnSpeed = "             + commandingTurnSpeed);
        LogCommand("[Init] useBacklineHold = "                 + useBacklineHold);
        LogCommand("[Init] useCommandingBool = "               + useCommandingBool);
        LogCommand("[Init] commandingBoolName = "              + commandingBoolName);
        LogCommand("[Init] playCommandingEffect = "            + playCommandingEffect);
        LogCommand("[Init] reactionDelay = "                  + reactionDelay);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 进入 Commanding 状态，接管受控杂兵列表并批量开启指挥 Buff。
    /// 功能：接收外部 minions 列表并清洗有效引用写入 controlledMinions；锁定玩家引用（用于中断检测与朝向）；停止移动；开启挥旗表现；对有效杂兵批量调用 ApplyCommandBuff。
    /// 工作链路：由状态总控脚本在从 Summoning 切换到 Commanding 时调用；之后由每帧 Tick 驱动维持、清理与中断检测。
    /// 对下游影响：isInCommanding = true，shouldBreak = false；controlledMinions 被填充；所有有效杂兵进入"被指挥"增益状态。
    /// </summary>
    public void EnterState(List<GameObject> minions, Transform playerTransform)
    {
        nextTickDebugTime = Time.time;
        targetPlayer      = playerTransform;
        isInCommanding    = true;
        shouldBreak       = false;
        isReacting        = false;
        reactionEndTime   = 0f;

        // 清洗有效引用
        controlledMinions.Clear();
        int inputCount = minions != null ? minions.Count : 0;
        if (minions != null)
        {
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i] != null)
                    controlledMinions.Add(minions[i]);
            }
        }

        navAgent.isStopped = true;
        animator.SetBool("Walk", false);

        SetCommandingPresentation(true);
        ApplyCommandBuffToMinions();

        LogCommand("[Enter] Enter CommandingState");
        LogCommand("[Enter] input minions count = " + inputCount);
        LogCommand("[Enter] valid controlledMinions count = " + controlledMinions.Count);
        LogCommand("[Enter] targetPlayer = " + (targetPlayer != null ? targetPlayer.name : "NULL"));
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Commanding 状态每帧更新入口，驱动维持、清理与中断检测。
    /// 功能：若 isInCommanding == false 则直接返回（容错）；清理失效/死亡杂兵引用；若配置启用面向玩家且未处于反应停顿中则缓慢旋转（反应停顿期间保持当前姿态不动）；检测玩家是否逼近中断距离并处理反应延迟；输出节流 debug。
    /// 工作链路：由状态总控脚本在当前主状态为 Commanding 时每帧调用；ShouldBreakToSelfDefense 查询在 Tick 之后执行。
    /// 对下游影响：shouldBreak 可能在本帧被置为 true；controlledMinions 中的失效引用被移除；不重复开关 Buff。
    /// </summary>
    public void Tick()
    {
        if (!isInCommanding)
            return;

        CleanupInvalidMinions();

        if (keepFacingPlayerDuringCommanding && !isReacting)
            UpdateFacingToPlayer();

        CheckBreakCondition();

        // 节流 debug
        if (enableCommandingDebug && Time.time >= nextTickDebugTime)
        {
            nextTickDebugTime = Time.time + TickDebugInterval;
            float distance = targetPlayer != null
                ? Vector3.Distance(selfTransform.position, targetPlayer.position)
                : -1f;
            LogCommand("[Tick] controlledMinions = " + controlledMinions.Count
                + " | playerDistance = " + distance.ToString("F2")
                + " | isReacting = " + isReacting
                + " | shouldBreak = " + shouldBreak);
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前是否应中断指挥并切入 SelfDefense。
    /// 功能：纯布尔查询，返回 shouldBreak 的当前值；不修改任何内部状态。
    /// 工作链路：由状态总控脚本在 Commanding 状态下每帧调用 Tick 之后查询；若返回 true，总控负责取走 controlledMinions、调用 ExitState 并切入 SelfDefense。
    /// 对下游影响：只读查询，无副作用。
    /// </summary>
    public bool ShouldBreakToSelfDefense()
    {
        return shouldBreak;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 退出 Commanding 状态，批量关闭受控杂兵的指挥 Buff 并清理运行时标记。
    /// 功能：对当前 controlledMinions 批量调用 RemoveCommandBuff；关闭挥旗表现；重置 isInCommanding、shouldBreak、targetPlayer。注意：不清空 controlledMinions，确保总控在 ExitState 之前已通过 GetControlledMinions 取走列表。
    /// 工作链路：由状态总控脚本在 Commanding 被中断后切出时调用（切入 SelfDefense 之前）；调用后本模块进入休眠，等待下次 EnterState。
    /// 对下游影响：所有受控杂兵的指挥 Buff 被移除；模块停止驱动，Tick 若仍被调用会因 isInCommanding == false 直接返回。
    /// </summary>
    public void ExitState()
    {
        RemoveCommandBuffFromMinions();
        SetCommandingPresentation(false);

        LogCommand("[Exit] Exit CommandingState | controlledMinions count = " + controlledMinions.Count);

        isInCommanding  = false;
        shouldBreak     = false;
        isReacting      = false;
        reactionEndTime = 0f;
        targetPlayer    = null;

        LogCommand("[Exit] Cleared runtime flags. controlledMinions preserved for handoff.");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前仍被这只召唤师控制的杂兵 GameObject 列表。
    /// 功能：提供受控杂兵列表的只读访问，供总控在切入 SelfDefense 或恢复 Commanding 时继续使用。
    /// 工作链路：由总控脚本在 ShouldBreakToSelfDefense() 返回 true 后、ExitState() 调用前读取；SelfDefense 状态依赖此列表。
    /// 对下游影响：只读查询，无副作用；返回的是内部列表的引用，调用方应在取走后自行缓存。
    /// </summary>
    public List<GameObject> GetControlledMinions()
    {
        return controlledMinions;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前 Commanding 模块内部的简要运行状态摘要字符串。
    /// 功能：汇总 isInCommanding、shouldBreak、controlledMinions 数量、玩家距离等信息，供调试面板与日志使用。
    /// 工作链路：可由总控脚本或外部 Debug 面板随时调用；不改变任何内部状态。
    /// 对下游影响：只读，无副作用。
    /// </summary>
    public string GetDebugStateSummary()
    {
        float distance = targetPlayer != null
            ? Vector3.Distance(selfTransform.position, targetPlayer.position)
            : -1f;
        float reactionRemaining = isReacting ? Mathf.Max(0f, reactionEndTime - Time.time) : 0f;
        return $"[SummonerCommand Summary] isInCommanding={isInCommanding}"
            + $" | isReacting={isReacting}"
            + $" | reactionRemaining={reactionRemaining:F2}s"
            + $" | shouldBreak={shouldBreak}"
            + $" | controlledMinions={controlledMinions.Count}"
            + $" | playerDistance={distance:F2}";
    }

    // ══════════════════════════════════════════════
    // 内部方法
    // ══════════════════════════════════════════════

    /// <summary>
    /// 对当前 controlledMinions 中所有有效杂兵批量开启指挥 Buff。
    /// 功能：遍历 controlledMinions，对每个有效 GameObject 尝试获取 ISummonerCommandBuffTarget 组件；若存在则调用 ApplyCommandBuff，否则仅输出 debug 跳过。
    /// 工作链路：仅由 EnterState 调用一次；不在 Tick 中重复调用，保证 Buff 生命周期清晰（Commanding 开始 = Buff 开）。
    /// 对下游影响：所有挂载了 ISummonerCommandBuffTarget 的杂兵进入"被指挥"增益状态；未挂载的杂兵不受影响，不阻断流程。
    /// </summary>
    private void ApplyCommandBuffToMinions()
    {
        for (int i = 0; i < controlledMinions.Count; i++)
        {
            GameObject minion = controlledMinions[i];
            if (minion == null)
                continue;

            ISummonerCommandBuffTarget receiver = minion.GetComponent<ISummonerCommandBuffTarget>();
            if (receiver != null)
            {
                receiver.ApplyCommandBuff(selfTransform.gameObject);
                LogCommand("[BuffOn] Apply command buff to " + minion.name);
            }
            else
            {
                LogCommand("[BuffOn] No ISummonerCommandBuffTarget on " + minion.name + ". Skipped.");
            }
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 对当前 controlledMinions 中所有仍有效的杂兵批量关闭指挥 Buff。
    /// 功能：遍历 controlledMinions，对每个仍有效的 GameObject 尝试获取 ISummonerCommandBuffTarget 组件；若存在则调用 RemoveCommandBuff，否则仅输出 debug 跳过。
    /// 工作链路：仅由 ExitState 调用一次；保证 Buff 生命周期清晰（Commanding 结束 = Buff 关）。
    /// 对下游影响：所有挂载了 ISummonerCommandBuffTarget 的杂兵退出"被指挥"增益状态；未挂载或已销毁的杂兵安全跳过。
    /// </summary>
    private void RemoveCommandBuffFromMinions()
    {
        for (int i = 0; i < controlledMinions.Count; i++)
        {
            GameObject minion = controlledMinions[i];
            if (minion == null)
                continue;

            ISummonerCommandBuffTarget receiver = minion.GetComponent<ISummonerCommandBuffTarget>();
            if (receiver != null)
            {
                receiver.RemoveCommandBuff(selfTransform.gameObject);
                LogCommand("[BuffOff] Remove command buff from " + minion.name);
            }
            else
            {
                LogCommand("[BuffOff] No ISummonerCommandBuffTarget on " + minion.name + ". Skipped.");
            }
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 清理 controlledMinions 中所有空引用或已被销毁的杂兵条目。
    /// 功能：使用反向遍历移除 null 条目，避免遍历过程中的索引偏移问题；移除时输出 debug 日志。
    /// 工作链路：由 Tick 每帧调用；在 UpdateFacingToPlayer 和 CheckBreakCondition 之前执行，确保后续逻辑基于干净列表。
    /// 对下游影响：controlledMinions 中的无效条目被移除；列表长度可能减少。
    /// </summary>
    private void CleanupInvalidMinions()
    {
        for (int i = controlledMinions.Count - 1; i >= 0; i--)
        {
            if (controlledMinions[i] == null)
            {
                LogCommand("[Tick] Cleanup invalid minion at index " + i);
                controlledMinions.RemoveAt(i);
            }
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Commanding 期间缓慢将自身朝向旋转至面向目标玩家。
    /// 功能：计算自身到玩家的水平方向向量，使用 Quaternion.Slerp 平滑旋转；忽略 Y 轴高度差，仅在 XZ 平面旋转。
    /// 算法：基于球面线性插值（Slerp）实现角速度均匀的朝向过渡，commandingTurnSpeed * deltaTime 控制每帧旋转量。
    /// 工作链路：由 Tick 在 keepFacingPlayerDuringCommanding == true 时每帧调用；targetPlayer 为 null 时安全返回。
    /// 对下游影响：selfTransform.rotation 持续向玩家方向收敛，直至 Commanding 结束或被 ExitState 打断。
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
            commandingTurnSpeed * Time.deltaTime);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 检测玩家是否逼近到中断距离以内；首次触发时进入反应停顿阶段（isReacting），停顿期间召唤师保持不动，延迟结束后才置位 shouldBreak。
    /// 功能：计算自身与 targetPlayer 的距离，与 selfDefenseBreakDistance 比较；首次达到阈值时记录 reactionEndTime 并进入停顿；停顿期间每帧检查时间，到达后将 shouldBreak 设为 true。
    /// 工作链路：由 Tick 每帧在 CleanupInvalidMinions 和 UpdateFacingToPlayer 之后调用；shouldBreak 被置位后，总控在 ShouldBreakToSelfDefense 查询时获得 true。
    /// 对下游影响：isReacting 期间 Tick 跳过面向旋转（保持当前姿态冻结）；shouldBreak = true 后，总控将在本帧或下帧调用 ExitState 并切入 SelfDefense。
    /// </summary>
    private void CheckBreakCondition()
    {
        if (targetPlayer == null)
            return;

        if (shouldBreak)
            return;

        float distance = Vector3.Distance(selfTransform.position, targetPlayer.position);

        // 尚未进入反应阶段：检测是否首次触发
        if (!isReacting)
        {
            if (distance <= selfDefenseBreakDistance)
            {
                isReacting      = true;
                reactionEndTime = Time.time + reactionDelay;

                LogCommand("[Reaction] Player entered break distance — reaction started."
                    + " | Distance = " + distance.ToString("F2")
                    + " | Threshold = " + selfDefenseBreakDistance
                    + " | reactionDelay = " + reactionDelay + "s"
                    + " | reactionEndTime = " + reactionEndTime.ToString("F2"));
            }
            return;
        }

        // 已在反应停顿中：检查玩家是否撤退到安全距离（取消反应）
        if (distance > selfDefenseBreakDistance)
        {
            isReacting = false;
            LogCommand("[Reaction] Player retreated beyond break distance — reaction cancelled."
                + " | Distance = " + distance.ToString("F2")
                + " | Threshold = " + selfDefenseBreakDistance);
            return;
        }

        // 反应倒计时完成：真正置位 shouldBreak
        if (Time.time >= reactionEndTime)
        {
            shouldBreak = true;
            LogCommand("[Break] Reaction delay elapsed — shouldBreak = true."
                + " | Distance = " + distance.ToString("F2")
                + " | Threshold = " + selfDefenseBreakDistance);
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 开启或关闭挥旗表现（动画 Bool / 持续特效）。
    /// 功能：根据 enabled 参数设置 Animator Bool 与特效状态；当前版本特效为预留接口，仅输出日志占位。
    /// 工作链路：由 EnterState（enabled = true）和 ExitState（enabled = false）各调用一次；不被 Tick 调用。
    /// 对下游影响：Animator 的挥旗 Bool 被设置/清除，驱动对应动画状态切换。
    /// </summary>
    private void SetCommandingPresentation(bool enabled)
    {
        if (useCommandingBool && animator != null)
        {
            animator.SetBool(commandingBoolName, enabled);
            LogCommand("[Action] Set " + commandingBoolName + " = " + enabled);
        }

        if (playCommandingEffect)
        {
            // TODO: 在此处开启/关闭挥旗持续特效
            LogCommand("[Action] Commanding effect " + (enabled ? "ON" : "OFF") + " (placeholder — no VFX asset assigned yet)");
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 统一的 Commanding 模块 Debug 输出方法，受 enableCommandingDebug 开关控制。
    /// 功能：在 enableCommandingDebug == true 时输出带固定前缀 [SummonerCommand] 的日志，否则不产生任何输出。
    /// 工作链路：被本模块所有内部函数调用；总控脚本无需调用此方法。
    /// </summary>
    private void LogCommand(string message)
    {
        if (!enableCommandingDebug)
            return;
        Debug.Log("[SummonCommand]" + message);
    }
}