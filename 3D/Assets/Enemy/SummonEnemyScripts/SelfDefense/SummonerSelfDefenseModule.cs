using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// SelfDefense 子状态枚举。
/// 功能：定义 SelfDefense 内部 5 个固定子状态的类型标识，用于 Tick 中的子状态分发。
/// 固定循环链路：Reposition -> PrepareAttack -> Approach -> Attack -> Pause。
/// </summary>
public enum SelfDefenseSubState
{
    Reposition,
    PrepareAttack,
    Approach,
    Attack,
    Pause
}

/// <summary>
/// Summoner 敌人的 SelfDefense 状态模块。
/// 功能：负责召唤师在被玩家逼近、失去指挥权后的弱自卫循环，包含 Reposition（短距离局部调整）、PrepareAttack（预警特效短前摇）、Approach（短距离压近至攻击距离）、Attack（单次挥拳）、Pause（攻后停顿）五个固定子状态。
/// 工作链路：由状态总控脚本持有；总控负责决定何时进入/退出 SelfDefense，本模块只负责 SelfDefense 内部所有子状态流转与维护。
/// 设计原则：弱自卫而非强攻；Reposition 每轮只执行一次，不允许无限后退；Approach 只做短距离克制压前不做追击；攻击只做单次，不做连段；攻后必须 Pause；退出条件仅由模块报告，不自行切主状态；不包含 Buff 控制、召唤、指挥逻辑。
/// </summary>
public class SummonerSelfDefenseModule
{
    // ──────────────────────────────────────────────
    // 外部引用
    // ──────────────────────────────────────────────

    private Transform    selfTransform;
    private NavMeshAgent navAgent;
    private Animator     animator;

    // ──────────────────────────────────────────────
    // 配置（从 SelfDefenseConfig 拷贝，Initialize 后只读）
    // ──────────────────────────────────────────────

    private bool   enableSelfDefenseDebug;
    private float  pressureDistance;
    private float  attackReadyDistance;
    private float  selfDefenseExitDistance;
    private float  repositionDistance;
    private float  repositionDuration;
    private float  repositionMoveSpeed;
    private float  prepareAttackDuration;
    private bool   useWarningEffect;
    private float  attackDuration;
    private bool   usePunchTrigger;
    private string punchTriggerName;
    private float  pauseDuration;
    private float  selfDefenseTurnSpeed;
    private bool useBackstepBool;
    private string backstepBoolName;
    private float  approachDistance;
    private float  approachDuration;
    private float  approachMoveSpeed;
    private float  attackRange;
    private bool   useApproachMoveAnim;
    private string approachMoveBoolName;

    // ──────────────────────────────────────────────
    // 运行时状态
    // ──────────────────────────────────────────────

    private Transform           targetPlayer;
    private bool                isInSelfDefense;
    private bool                canExit;
    private List<GameObject>    controlledMinions;
    private SelfDefenseSubState currentSubState;
    private float               subStateEndTime;
    private Vector3             repositionStartPosition;
    private Vector3             repositionMoveDirection;
    private bool                hasPerformedRepositionThisCycle;
    private Vector3             approachStartPosition;

    // ──────────────────────────────────────────────
    // Debug 节流
    // ──────────────────────────────────────────────

    private float nextTickDebugTime;
    private const float TickDebugInterval = 1.0f;

    // ══════════════════════════════════════════════
    // 公开接口
    // ══════════════════════════════════════════════

    /// <summary>
    /// 初始化 SelfDefense 模块所需引用与配置。
    /// 功能：将外部依赖与 SelfDefenseConfig 配置数据写入模块；重置运行时标记；初始化 controlledMinions 列表。
    /// 工作链路：由状态总控脚本在对象 Awake/Start 阶段调用一次；完成后模块进入待命状态，等待 EnterState。
    /// 对下游影响：Initialize 完成后，所有配置字段只读；targetPlayer 为 null，isInSelfDefense = false，canExit = false。
    /// </summary>
    public void Initialize(
        Transform         selfTransform,
        NavMeshAgent      navAgent,
        Animator          animator,
        SelfDefenseConfig config)
    {
        this.selfTransform = selfTransform;
        this.navAgent      = navAgent;
        this.animator      = animator;

        enableSelfDefenseDebug  = config.enableSelfDefenseDebug;
        pressureDistance        = config.pressureDistance;
        attackReadyDistance     = config.attackReadyDistance;
        selfDefenseExitDistance = config.selfDefenseExitDistance;
        repositionDistance      = config.repositionDistance;
        repositionDuration      = config.repositionDuration;
        repositionMoveSpeed     = config.repositionMoveSpeed;
        prepareAttackDuration   = config.prepareAttackDuration;
        useWarningEffect        = config.useWarningEffect;
        attackDuration          = config.attackDuration;
        usePunchTrigger         = config.usePunchTrigger;
        punchTriggerName        = config.punchTriggerName;
        pauseDuration           = config.pauseDuration;
        selfDefenseTurnSpeed    = config.selfDefenseTurnSpeed;
        useBackstepBool         = config.useBackstepBool;
        backstepBoolName        = config.backstepBoolName;
        approachDistance         = config.approachDistance;
        approachDuration         = config.approachDuration;
        approachMoveSpeed        = config.approachMoveSpeed;
        attackRange              = config.attackRange;
        useApproachMoveAnim      = config.useApproachMoveAnim;
        approachMoveBoolName     = config.approachMoveBoolName;

        targetPlayer      = null;
        isInSelfDefense   = false;
        canExit           = false;
        controlledMinions = new List<GameObject>();

        LogSelfDefense("[Init] selfTransform = "         + selfTransform.name);
        LogSelfDefense("[Init] pressureDistance = "       + pressureDistance);
        LogSelfDefense("[Init] attackReadyDistance = "    + attackReadyDistance);
        LogSelfDefense("[Init] selfDefenseExitDistance = " + selfDefenseExitDistance);
        LogSelfDefense("[Init] repositionDistance = "     + repositionDistance);
        LogSelfDefense("[Init] repositionDuration = "     + repositionDuration);
        LogSelfDefense("[Init] repositionMoveSpeed = "    + repositionMoveSpeed);
        LogSelfDefense("[Init] prepareAttackDuration = "  + prepareAttackDuration);
        LogSelfDefense("[Init] useWarningEffect = "       + useWarningEffect);
        LogSelfDefense("[Init] attackDuration = "         + attackDuration);
        LogSelfDefense("[Init] usePunchTrigger = "        + usePunchTrigger);
        LogSelfDefense("[Init] punchTriggerName = "       + punchTriggerName);
        LogSelfDefense("[Init] pauseDuration = "          + pauseDuration);
        LogSelfDefense("[Init] selfDefenseTurnSpeed = "   + selfDefenseTurnSpeed);
        LogSelfDefense("[Init] attackRange = "           + attackRange);
        LogSelfDefense("[Init] approachDistance = "      + approachDistance);
        LogSelfDefense("[Init] approachDuration = "      + approachDuration);
        LogSelfDefense("[Init] approachMoveSpeed = "     + approachMoveSpeed);
        LogSelfDefense("[Init] useApproachMoveAnim = "   + useApproachMoveAnim);
        LogSelfDefense("[Init] approachMoveBoolName = "  + approachMoveBoolName);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 进入 SelfDefense 状态，接收杂兵列表与玩家引用，初始化第一轮子状态循环（Reposition -> PrepareAttack -> Approach -> Attack -> Pause）。
    /// 功能：接收当前仍存活的受控杂兵列表并清洗有效引用；锁定玩家引用；重置运行时标记；停止移动；关闭 Walk 动画；启动第一轮 Reposition。
    /// 工作链路：由状态总控脚本在从 Commanding 切换到 SelfDefense 时调用；之后由每帧 Tick 驱动子状态流转。
    /// 对下游影响：isInSelfDefense = true，canExit = false；controlledMinions 被填充；hasPerformedRepositionThisCycle = false；立即进入 Reposition 子状态。
    /// </summary>
    public void EnterState(List<GameObject> minions, Transform playerTransform)
    {
        nextTickDebugTime = Time.time;
        targetPlayer      = playerTransform;
        isInSelfDefense   = true;
        canExit           = false;

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

        hasPerformedRepositionThisCycle = false;

        LogSelfDefense("[Enter] Enter SelfDefenseState");
        LogSelfDefense("[Enter] targetPlayer = " + (targetPlayer != null ? targetPlayer.name : "NULL"));
        LogSelfDefense("[Enter] input minions count = " + inputCount
            + " | valid controlledMinions count = " + controlledMinions.Count);

        EnterReposition();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// SelfDefense 状态每帧更新入口，驱动 5 个子状态的内部流转。
    /// 功能：若 isInSelfDefense == false 则直接返回（容错）；优先检查退出条件（玩家远离到 selfDefenseExitDistance 之外）；未退出则根据 currentSubState 分发到对应 Update 函数。
    /// 工作链路：由状态总控脚本在当前主状态为 SelfDefense 时每帧调用；CanExitSelfDefense 查询在 Tick 之后执行。
    /// 对下游影响：子状态之间的切换完全在此函数调用链内完成；canExit 可能在本帧被置为 true。
    /// </summary>
    public void Tick()
    {
        if (!isInSelfDefense)
            return;

        // 优先检查退出条件
        float distance = GetHorizontalDistanceToPlayer();
        if (distance >= selfDefenseExitDistance)
        {
            canExit = true;
            LogSelfDefense("[Exit] Player retreated beyond selfDefenseExitDistance."
                + " | Distance = " + distance.ToString("F2")
                + " | Threshold = " + selfDefenseExitDistance
                + " | canExitSelfDefense = true");
            return;
        }

        // 子状态分发
        switch (currentSubState)
        {
            case SelfDefenseSubState.Reposition:
                UpdateReposition();
                break;
            case SelfDefenseSubState.PrepareAttack:
                UpdatePrepareAttack();
                break;
            case SelfDefenseSubState.Approach:
                UpdateApproach();
                break;
            case SelfDefenseSubState.Attack:
                UpdateAttack();
                break;
            case SelfDefenseSubState.Pause:
                UpdatePause();
                break;
        }

        // 节流 debug
        if (enableSelfDefenseDebug && Time.time >= nextTickDebugTime)
        {
            nextTickDebugTime = Time.time + TickDebugInterval;
            LogSelfDefense("[Tick] SubState = " + currentSubState
                + " | playerDistance = " + distance.ToString("F2")
                + " | canExit = " + canExit);
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前是否允许退出 SelfDefense。
    /// 功能：纯布尔查询，返回 canExit 的当前值；不修改任何内部状态。
    /// 工作链路：由状态总控脚本在 SelfDefense 状态下每帧调用 Tick 之后查询；若返回 true，总控负责取走 controlledMinions、调用 ExitState 并决定后续状态。
    /// 对下游影响：只读查询，无副作用。
    /// </summary>
    public bool CanExitSelfDefense()
    {
        return canExit;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 退出 SelfDefense 状态，清理内部运行时标记。
    /// 功能：停止 NavMeshAgent 移动；关闭 Walk 动画；重置 isInSelfDefense、canExit、targetPlayer；输出退出日志。注意：不清空 controlledMinions，确保总控在 ExitState 之前已通过 GetControlledMinions 取走列表。
    /// 工作链路：由状态总控脚本在 SelfDefense 可退出后切出时调用；调用后本模块进入休眠，等待下次 EnterState。
    /// 对下游影响：模块停止驱动，Tick 若仍被调用会因 isInSelfDefense == false 直接返回。
    /// </summary>
    public void ExitState()
    {
        navAgent.isStopped = true;
        animator.SetBool("Walk", false);

        if (useBackstepBool && animator != null)
        {
            animator.SetBool(backstepBoolName, false);
            LogSelfDefense("[Exit] Reset " + backstepBoolName + " = false");
        }

        if (useApproachMoveAnim && animator != null)
        {
            animator.SetBool(approachMoveBoolName, false);
            LogSelfDefense("[Exit] Reset " + approachMoveBoolName + " = false");
        }

        LogSelfDefense("[Exit] Exit SelfDefenseState"
            + " | lastSubState = " + currentSubState
            + " | controlledMinions count = " + controlledMinions.Count);

        isInSelfDefense = false;
        canExit         = false;
        targetPlayer    = null;

        LogSelfDefense("[Exit] Cleared runtime flags. controlledMinions preserved for handoff.");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前仍然归属于这只召唤师的杂兵 GameObject 列表。
    /// 功能：提供受控杂兵列表的只读访问，供总控在退出 SelfDefense 后切回 Commanding 或其他状态时继续使用。
    /// 工作链路：由总控脚本在 CanExitSelfDefense() 返回 true 后、ExitState() 调用前读取。
    /// 对下游影响：只读查询，无副作用；返回的是内部列表的引用，调用方应在取走后自行缓存。
    /// </summary>
    public List<GameObject> GetControlledMinions()
    {
        return controlledMinions;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前 SelfDefense 模块内部的简要运行状态摘要字符串。
    /// 功能：汇总 isInSelfDefense、canExit、currentSubState、玩家距离、controlledMinions 数量、是否已执行本轮 Reposition 等信息，供调试面板与日志使用。
    /// 工作链路：可由总控脚本或外部 Debug 面板随时调用；不改变任何内部状态。
    /// 对下游影响：只读，无副作用。
    /// </summary>
    public string GetDebugStateSummary()
    {
        float distance = GetHorizontalDistanceToPlayer();
        float remaining = Mathf.Max(0f, subStateEndTime - Time.time);
        return $"[SummonerSelfDefense Summary] isInSelfDefense={isInSelfDefense}"
            + $" | canExit={canExit}"
            + $" | subState={currentSubState}"
            + $" | playerDistance={distance:F2}"
            + $" | subStateRemaining={remaining:F2}s"
            + $" | repositionDone={hasPerformedRepositionThisCycle}"
            + $" | controlledMinions={controlledMinions.Count}";
    }

    // ══════════════════════════════════════════════
    // 子状态 Enter / Update
    // ══════════════════════════════════════════════

    /// <summary>
    /// 进入 Reposition 子状态，初始化一次“面朝玩家后撤步”。
    /// 功能：记录后撤起点；计算后撤方向为当前朝向的反方向；开启 Walk 动画；启动计时；标记本轮已执行 Reposition。
    /// 工作链路：由 EnterState（首轮）或 UpdatePause（下一轮循环开始）调用；调用后由 UpdateReposition 每帧驱动。
    /// 对下游影响：本轮 Reposition 改为“面朝玩家 + 后撤位移”，不再导航到斜后空间点。
    /// </summary>
    private void EnterReposition()
    {
        currentSubState = SelfDefenseSubState.Reposition;
        subStateEndTime = Time.time + repositionDuration;
        hasPerformedRepositionThisCycle = true;

        // 先面对玩家，再记录后撤方向
        UpdateFacingToPlayer();

        repositionStartPosition = selfTransform.position;
        repositionMoveDirection = -selfTransform.forward;
        repositionMoveDirection.y = 0f;

        if (repositionMoveDirection.sqrMagnitude <= 0.001f)
            repositionMoveDirection = -selfTransform.forward;

        repositionMoveDirection.Normalize();

        // Reposition 改为手动后撤位移，不再使用 NavMeshAgent 寻路目标
        navAgent.isStopped = true;
        animator.SetBool("Walk", true);

        if (useBackstepBool && animator != null)
        {
            animator.SetBool(backstepBoolName, true);
            LogSelfDefense("[Reposition] Set " + backstepBoolName + " = true");
        }

        LogSelfDefense("[Reposition] Enter Reposition"
            + " | StartPos = " + repositionStartPosition
            + " | BackstepDir = " + repositionMoveDirection
            + " | Distance = " + repositionDistance
            + " | Duration = " + repositionDuration + "s"
            + " | MoveSpeed = " + repositionMoveSpeed);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Reposition 子状态每帧更新，驱动“面朝玩家后撤步”，结束后切入 PrepareAttack。
    /// 功能：持续面向玩家；沿进入时记录的 backward 方向手动后撤；到达目标距离或计时结束后结束 Reposition。
    /// 工作链路：由 Tick 在 currentSubState == Reposition 时每帧调用。
    /// 对下游影响：Reposition 结束后无论是否拉开成功，都进入 PrepareAttack；不允许同一轮循环中再次 Reposition。
    /// </summary>
    private void UpdateReposition()
    {
        // 始终面向玩家，形成“边看边退”的效果
        UpdateFacingToPlayer();

        // 手动后撤位移：沿进入时记录的 backward 方向退一步
        float step = repositionMoveSpeed * Time.deltaTime;
        selfTransform.position += repositionMoveDirection * step;

        float movedDistance = Vector3.Distance(selfTransform.position, repositionStartPosition);
        bool reached = movedDistance >= repositionDistance;
        bool timedOut = Time.time >= subStateEndTime;

        if (!reached && !timedOut)
            return;

        // Reposition 结束
        navAgent.isStopped = true;
        animator.SetBool("Walk", false);
        navAgent.Warp(selfTransform.position);
        LogSelfDefense("[Reposition] Warp NavMeshAgent to current position = " + selfTransform.position);
        
        if (useBackstepBool && animator != null)
        {
            animator.SetBool(backstepBoolName, false);
            LogSelfDefense("[Reposition] Set " + backstepBoolName + " = false");
        }

        float distToPlayer = GetHorizontalDistanceToPlayer();

        if (distToPlayer <= pressureDistance)
        {
            LogSelfDefense("[Reposition] Finished | NOT fully separated"
                + " | MovedDistance = " + movedDistance.ToString("F2")
                + " | PlayerDistance = " + distToPlayer.ToString("F2")
                + " | PressureDistance = " + pressureDistance
                + " | -> PrepareAttack (emergency)");
        }
        else
        {
            LogSelfDefense("[Reposition] Finished | Space created"
                + " | MovedDistance = " + movedDistance.ToString("F2")
                + " | PlayerDistance = " + distToPlayer.ToString("F2")
                + " | -> PrepareAttack (stable)");
        }

        EnterPrepareAttack();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 进入 PrepareAttack 子状态，触发预警特效并启动短前摇计时。
    /// 功能：设置 currentSubState 为 PrepareAttack；停止移动；触发预警特效（若配置启用）；启动前摇计时。
    /// 工作链路：由 UpdateReposition 在 Reposition 结束后调用；调用后由 UpdatePrepareAttack 每帧驱动。
    /// 对下游影响：预警特效被播放（或占位日志输出）；前摇结束后进入 Attack。
    /// </summary>
    private void EnterPrepareAttack()
    {
        currentSubState = SelfDefenseSubState.PrepareAttack;
        subStateEndTime = Time.time + prepareAttackDuration;

        navAgent.isStopped = true;
        animator.SetBool("Walk", false);

        if (useWarningEffect)
        {
            // TODO: 在此处实例化或播放预警特效
            LogSelfDefense("[Prepare] Warning effect triggered (placeholder — assign VFX here)");
        }

        LogSelfDefense("[Prepare] Enter PrepareAttack"
            + " | Duration = " + prepareAttackDuration + "s");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// PrepareAttack 子状态每帧更新，维持朝向玩家并在前摇结束后切入 Approach。
    /// 功能：每帧调用 UpdateFacingToPlayer 保持面向玩家；检查计时是否到达 subStateEndTime；到达后进入 Approach。
    /// 工作链路：由 Tick 在 currentSubState == PrepareAttack 时每帧调用。
    /// 对下游影响：前摇结束后立即进入 Approach 子状态，由 Approach 压近后再进入 Attack。
    /// </summary>
    private void UpdatePrepareAttack()
    {
        UpdateFacingToPlayer();

        if (Time.time >= subStateEndTime)
            EnterApproach();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 进入 Approach 子状态，向玩家方向做一次短距离、短时间、克制的压前移动。
    /// 功能：记录 Approach 起点；启动 Approach 计时；开启"出拳架势前进"动画；面向玩家方向前进。
    /// 工作链路：由 UpdatePrepareAttack 在前摇结束后调用；调用后由 UpdateApproach 每帧驱动。
    /// 对下游影响：Approach 结束后固定进入 Attack，不允许回退到 Reposition 或跳过。
    /// </summary>
    private void EnterApproach()
    {
        currentSubState = SelfDefenseSubState.Approach;
        subStateEndTime = Time.time + approachDuration;

        approachStartPosition = selfTransform.position;

        navAgent.isStopped = true;

        if (useApproachMoveAnim && animator != null)
        {
            animator.SetBool(approachMoveBoolName, true);
            LogSelfDefense("[Approach] Set " + approachMoveBoolName + " = true");
        }

        float distToPlayer = GetHorizontalDistanceToPlayer();

        LogSelfDefense("[Approach] Enter Approach"
            + " | PlayerDistance = " + distToPlayer.ToString("F2")
            + " | attackRange = " + attackRange
            + " | approachDuration = " + approachDuration + "s"
            + " | approachMoveSpeed = " + approachMoveSpeed);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Approach 子状态每帧更新，保持面向玩家并向玩家方向前进，达到攻击距离或超时后进入 Attack。
    /// 功能：每帧面向玩家；沿自身 +forward 方向手动位移；检查水平距离是否 <= attackRange（主退出条件）或超时（兜底退出条件）。
    /// 工作链路：由 Tick 在 currentSubState == Approach 时每帧调用。
    /// 对下游影响：Approach 结束后固定进入 Attack；关闭"出拳架势前进"动画。
    /// </summary>
    private void UpdateApproach()
    {
        // 1. 保持面向玩家
        UpdateFacingToPlayer();

        // 2. 向玩家方向前进（沿自身 +forward）
        if (targetPlayer != null)
        {
            float step = approachMoveSpeed * Time.deltaTime;
            selfTransform.position += selfTransform.forward * step;
        }

        // 3. 检查退出条件
        float distToPlayer = GetHorizontalDistanceToPlayer();
        bool reachedAttackRange = distToPlayer <= attackRange;
        bool timedOut = Time.time >= subStateEndTime;

        // 节流 debug
        if (enableSelfDefenseDebug && Time.time >= nextTickDebugTime)
        {
            float movedDist = Vector3.Distance(selfTransform.position, approachStartPosition);
            nextTickDebugTime = Time.time + TickDebugInterval;
            LogSelfDefense("[Approach] Tick"
                + " | PlayerDistance = " + distToPlayer.ToString("F2")
                + " | movedDistance = " + movedDist.ToString("F2")
                + " | reachedAttackRange = " + reachedAttackRange);
        }

        if (!reachedAttackRange && !timedOut)
            return;

        // Approach 结束 —— 关闭动画
        if (useApproachMoveAnim && animator != null)
        {
            animator.SetBool(approachMoveBoolName, false);
            LogSelfDefense("[Approach] Set " + approachMoveBoolName + " = false");
        }

        // 同步 NavMeshAgent 位置
        navAgent.Warp(selfTransform.position);

        float finalMovedDist = Vector3.Distance(selfTransform.position, approachStartPosition);

        if (reachedAttackRange)
        {
            LogSelfDefense("[Approach] Exit — reached attackRange"
                + " | PlayerDistance = " + distToPlayer.ToString("F2")
                + " | attackRange = " + attackRange
                + " | movedDistance = " + finalMovedDist.ToString("F2")
                + " | -> Attack");
        }
        else
        {
            LogSelfDefense("[Approach] Exit — timed out"
                + " | PlayerDistance = " + distToPlayer.ToString("F2")
                + " | attackRange = " + attackRange
                + " | movedDistance = " + finalMovedDist.ToString("F2")
                + " | -> Attack (forced)");
        }

        EnterAttack();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 进入 Attack 子状态，触发单次挥拳动画并启动攻击计时。
    /// 功能：设置 currentSubState 为 Attack；触发挥拳 Animator Trigger（若配置启用）；启动攻击持续时间计时。
    /// 工作链路：由 UpdatePrepareAttack 在前摇结束后调用；调用后由 UpdateAttack 每帧驱动。
    /// 对下游影响：挥拳动画被触发；攻击结束后进入 Pause。
    /// </summary>
    private void EnterAttack()
    {
        currentSubState = SelfDefenseSubState.Attack;
        subStateEndTime = Time.time + attackDuration;

        if (usePunchTrigger && animator != null)
        {
            animator.SetTrigger(punchTriggerName);
            LogSelfDefense("[Attack] Trigger punch animation = " + punchTriggerName);
        }

        LogSelfDefense("[Attack] Enter Attack"
            + " | Duration = " + attackDuration + "s");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Attack 子状态每帧更新，维持单次攻击直到计时结束后切入 Pause。
    /// 功能：检查计时是否到达 subStateEndTime；到达后进入 Pause。不做连段、不追打、不移动。
    /// 工作链路：由 Tick 在 currentSubState == Attack 时每帧调用。
    /// 对下游影响：攻击结束后立即进入 Pause 子状态。
    /// </summary>
    private void UpdateAttack()
    {
        if (Time.time >= subStateEndTime)
            EnterPause();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 进入 Pause 子状态，切入攻后停顿并启动计时。
    /// 功能：设置 currentSubState 为 Pause；停止移动；关闭 Walk 动画（保持待机/出拳架势待机）；启动 Pause 计时。
    /// 工作链路：由 UpdateAttack 在攻击结束后调用；调用后由 UpdatePause 每帧驱动。
    /// 对下游影响：召唤师进入明显破绽窗口；Pause 结束后重新判断下一轮。
    /// </summary>
    private void EnterPause()
    {
        currentSubState = SelfDefenseSubState.Pause;
        subStateEndTime = Time.time + pauseDuration;

        navAgent.isStopped = true;
        animator.SetBool("Walk", false);

        LogSelfDefense("[Pause] Enter Pause"
            + " | Duration = " + pauseDuration + "s");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Pause 子状态每帧更新，维持朝向并在结束后判断下一轮流转。
    /// 功能：每帧维持面向玩家；计时到达后检查玩家距离——若远离到退出阈值则置位 canExit，否则重置 hasPerformedRepositionThisCycle 并启动下一轮 Reposition。
    /// 工作链路：由 Tick 在 currentSubState == Pause 时每帧调用。
    /// 对下游影响：Pause 结束后要么 canExit = true（等待总控退出），要么开始新一轮 Reposition -> PrepareAttack -> Approach -> Attack -> Pause 循环。
    /// </summary>
    private void UpdatePause()
    {
        UpdateFacingToPlayer();

        if (Time.time < subStateEndTime)
            return;

        // Pause 结束，判断下一步
        float distance = GetHorizontalDistanceToPlayer();

        if (distance >= selfDefenseExitDistance)
        {
            canExit = true;
            LogSelfDefense("[Pause] Pause ended | Player retreated beyond exit distance"
                + " | Distance = " + distance.ToString("F2")
                + " | Threshold = " + selfDefenseExitDistance
                + " | canExitSelfDefense = true");
        }
        else
        {
            LogSelfDefense("[Pause] Pause ended | Starting next cycle"
                + " | PlayerDistance = " + distance.ToString("F2"));
            hasPerformedRepositionThisCycle = false;
            EnterReposition();
        }
    }

    // ══════════════════════════════════════════════
    // 工具方法
    // ══════════════════════════════════════════════

    /// <summary>
    /// SelfDefense 期间缓慢将自身朝向旋转至面向目标玩家。
    /// 功能：计算自身到玩家的水平方向向量，使用 Quaternion.Slerp 平滑旋转；忽略 Y 轴高度差，仅在 XZ 平面旋转。
    /// 算法：基于球面线性插值（Slerp）实现角速度均匀的朝向过渡，selfDefenseTurnSpeed * deltaTime 控制每帧旋转量。
    /// 工作链路：由 UpdateReposition、UpdatePrepareAttack、UpdatePause 每帧调用；targetPlayer 为 null 时安全返回。
    /// 对下游影响：selfTransform.rotation 持续向玩家方向收敛。
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
            selfDefenseTurnSpeed * Time.deltaTime);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 计算自身与目标玩家之间的 XZ 水平面距离（忽略 Y 轴高度差）。
    /// 功能：将 selfTransform 与 targetPlayer 的 position 投影到 XZ 平面后计算距离；targetPlayer 为 null 时返回 float.MaxValue（视为极远）。
    /// 工作链路：被 Tick、UpdateReposition、UpdatePause 等多处调用，是所有距离判断的唯一来源。
    /// 对下游影响：只读计算，无副作用。
    /// </summary>
    private float GetHorizontalDistanceToPlayer()
    {
        if (targetPlayer == null)
            return float.MaxValue;

        Vector3 selfPos   = selfTransform.position;
        Vector3 playerPos = targetPlayer.position;
        selfPos.y   = 0f;
        playerPos.y = 0f;

        return Vector3.Distance(selfPos, playerPos);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 统一的 SelfDefense 模块 Debug 输出方法，受 enableSelfDefenseDebug 开关控制。
    /// 功能：在 enableSelfDefenseDebug == true 时输出带固定前缀 [SummonerSelfDefense] 的日志，否则不产生任何输出。
    /// 工作链路：被本模块所有内部函数调用；总控脚本无需调用此方法。
    /// </summary>
    private void LogSelfDefense(string message)
    {
        if (!enableSelfDefenseDebug)
            return;
        Debug.Log("[SummonerSelfDefense]" + message);
    }
}