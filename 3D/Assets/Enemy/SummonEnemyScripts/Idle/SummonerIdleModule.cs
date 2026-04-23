using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Summoner 敌人的 Idle 状态模块。
/// 功能：负责召唤师在未发现玩家时的全部 Idle 行为，包含固定离散点巡逻（PatrolMove）与原地停驻观察（IdleObserve）两个子流程。
/// 工作链路：由状态总控脚本持有；总控负责决定何时进入/退出 Idle，本模块只负责 Idle 内部所有逻辑的驱动与维护。
/// 设计原则：行为固定可预测，方便玩家学习与利用；不包含随机逻辑；模块自治，总控只通过公开接口交互。
/// </summary>
public class SummonerIdleModule
{
    // ──────────────────────────────────────────────
    // 外部引用
    // ──────────────────────────────────────────────

    private Transform    selfTransform;
    private NavMeshAgent navAgent;
    private Animator     animator;

    // ──────────────────────────────────────────────
    // 配置（从 IdleConfig 拷贝，Initialize 后只读）
    // ──────────────────────────────────────────────

    private bool  enableIdleDebug;
    private float patrolRadius;
    private float patrolAngleStep;
    private float idleDuration;
    private float anchorReachThreshold;
    private float patrolMoveSpeed;
    private int   patrolDirection;      // +1 顺时针 / -1 逆时针
    private float  observeTurnSpeed;
    private bool   useRelaxIdleBool;
    private string relaxIdleBoolName;

    // ──────────────────────────────────────────────
    // 运行时状态
    // ──────────────────────────────────────────────

    private Vector3 guardCenter;
    private float   currentPatrolAngle;
    private Vector3 currentAnchor;
    private bool    hasValidAnchor;
    private bool    isMovingToAnchor;
    private bool    isIdleObserving;
    private float   idleEndTime;

    // ──────────────────────────────────────────────
    // Debug 节流
    // ──────────────────────────────────────────────

    private float nextObserveDebugTime;
    private const float ObserveDebugInterval = 1.0f; // 观察阶段朝向 debug 每秒最多输出一次

    // ──────────────────────────────────────────────
    // 感知检测子模块
    // ──────────────────────────────────────────────

    private SummonerDetectionModule detectionModule;

    // ══════════════════════════════════════════════
    // 公开接口
    // ══════════════════════════════════════════════

    /// <summary>
    /// 初始化 Idle 模块所需引用、配置和初始 Patrol 数据。
    /// 功能：将外部依赖与配置数据写入模块；记录 guardCenter 与初始角度；确定巡逻方向；初始化 Detection 子模块。
    /// 工作链路：由状态总控脚本在对象 Awake/Start 阶段调用一次；完成后模块可被 EnterState 正常启用。
    /// 对下游影响：Initialize 完成后，所有配置字段只读；guardCenter 与 patrolDirection 全局固定不变；detectionModule 可立即使用。
    /// </summary>
    public void Initialize(
        Transform       selfTransform,
        NavMeshAgent    navAgent,
        Animator        animator,
        IdleConfig      config,
        DetectionConfig detectionConfig)
    {
        this.selfTransform = selfTransform;
        this.navAgent      = navAgent;
        this.animator      = animator;
        
        patrolRadius         = config.patrolRadius;
        patrolAngleStep      = config.patrolAngleStep;
        idleDuration         = config.idleDuration;
        anchorReachThreshold = config.anchorReachThreshold;
        patrolMoveSpeed      = config.patrolMoveSpeed;
        patrolDirection      = config.useClockwisePatrol ? 1 : -1;
        observeTurnSpeed     = config.observeTurnSpeed;
        useRelaxIdleBool     = config.useRelaxIdleBool;
        relaxIdleBoolName    = config.relaxIdleBoolName;

        guardCenter        = selfTransform.position;
        currentPatrolAngle = config.initialPatrolAngle;

        hasValidAnchor   = false;
        isMovingToAnchor = false;
        isIdleObserving  = false;

        // 初始化感知检测子模块，玩家引用传 null 由 DetectionModule 内部自行查找
        detectionModule = new SummonerDetectionModule();
        detectionModule.Initialize(selfTransform, null, detectionConfig.enableDetectDebug, detectionConfig);

        LogIdle("[Init] guardCenter = "        + guardCenter);
        LogIdle("[Init] patrolDirection = "    + patrolDirection + (patrolDirection > 0 ? " (顺时针)" : " (逆时针)"));
        LogIdle("[Init] currentPatrolAngle = " + currentPatrolAngle);
        LogIdle("[Init] patrolRadius = "       + patrolRadius + " | patrolAngleStep = " + patrolAngleStep);
        LogIdle("[Init] useRelaxIdleBool = "  + useRelaxIdleBool);
        LogIdle("[Init] relaxIdleBoolName = " + relaxIdleBoolName);
        LogIdle("[Init] DetectionModule initialized.");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 进入 Idle 状态并开始第一次 IdleObserve。
    /// 功能：停止移动、关闭 Walk 动画、重置内部流程标记，启动首次原地观察计时。
    /// 工作链路：由状态总控脚本切入 Idle 主状态时调用；之后由每帧 Tick 驱动 Patrol/Observe 循环。
    /// 对下游影响：isIdleObserving = true，Tick 会在本帧末开始驱动 UpdateIdleObserve。
    /// </summary>
    public void EnterState()
    {
        navAgent.isStopped = true;
        animator.SetBool("Walk", false);

        if (useRelaxIdleBool && animator != null)
        {
            animator.SetBool(relaxIdleBoolName, true);
            LogIdle("[State] Set " + relaxIdleBoolName + " = true (entering Idle, relaxed stance)");
        }

        hasValidAnchor   = false;
        isMovingToAnchor = false;
        isIdleObserving  = true;
        idleEndTime      = Time.time + idleDuration;

        LogIdle("[State] Enter IdleState");
        LogIdle("[Observe] Start first IdleObserve, duration = " + idleDuration + "s | idleEndTime = " + idleEndTime.ToString("F2"));
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Idle 状态每帧更新入口，驱动 PatrolMove / IdleObserve 两个子流程。
    /// 功能：根据当前子流程标记分发到对应更新函数；若标记异常则容错重置为 IdleObserve。
    /// 工作链路：由状态总控脚本在当前主状态为 Idle 时、且 CanInterruptByPlayerDetect 返回 false 后每帧调用。
    /// 对下游影响：子流程之间的切换（Observe -> Move -> Observe）完全在此函数调用链内完成，总控无需感知。
    /// </summary>
    public void Tick()
    {
        if (isIdleObserving)
        {
            UpdateIdleObserve();
            return;
        }

        if (isMovingToAnchor)
        {
            UpdatePatrolMove();
            return;
        }

        // 容错：子流程标记均为 false 时强制重置，防止 Idle 卡死
        LogIdle("[State] Invalid subflow detected. Force reset to IdleObserve.");
        isIdleObserving = true;
        idleEndTime     = Time.time + idleDuration;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 退出 Idle 状态，清理内部流程标记并停止所有 Idle 行为。
    /// 功能：停止 NavMeshAgent 移动，关闭 Walk 动画，将所有运行时状态标记重置为 false。
    /// 工作链路：由状态总控脚本从 Idle 切出时调用（无论是因为发现玩家还是其他原因）；调用后本模块进入休眠，等待下次 EnterState。
    /// 对下游影响：模块停止驱动，Tick 若仍被调用会触发容错逻辑，但不会产生错误。
    /// </summary>
    public void ExitState()
    {
        navAgent.isStopped = true;
        animator.SetBool("Walk", false);

        if (useRelaxIdleBool && animator != null)
        {
            animator.SetBool(relaxIdleBoolName, false);
            LogIdle("[State] Set " + relaxIdleBoolName + " = false (exiting Idle)");
        }

        isMovingToAnchor = false;
        isIdleObserving  = false;
        hasValidAnchor   = false;

        LogIdle("[State] Exit IdleState | Cleared all subflow flags.");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 查询当前 Idle 模块是否满足"因发现玩家而中断"的条件。
    /// 功能：委托 SummonerDetectionModule 执行完整检测（球形保底 + 锥形视野）；检测命中时输出包含发现原因的退出日志。
    /// 工作链路：由状态总控脚本在 Idle 状态下每帧优先调用，先于 Tick；若返回 true，总控负责调用 ExitState 并切换到 Alert。
    /// 对下游影响：本函数不自行切换状态，只提供布尔查询结果；所有检测逻辑收口至 detectionModule，本模块不重复实现。
    /// </summary>
    public bool CanInterruptByPlayerDetect()
    {
        if (!detectionModule.CanDetectPlayer())
            return false;

        LogIdle("[Detect] Player detected during Idle!"
            + " | SubFlow = "  + GetCurrentSubflowName()
            + " | Reason = "   + detectionModule.GetLastDetectReason()
            + " | -> Prepare Exit Idle -> Alert");

        return true;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 转调 Detection 子模块的 Gizmos 绘制函数。
    /// 功能：将宿主 MonoBehaviour 的 OnDrawGizmos/OnDrawGizmosSelected 调用透传给 detectionModule.DrawDetectionGizmos()。
    /// 工作链路：由宿主 MonoBehaviour（如 SummonerIdleTester）在 OnDrawGizmosSelected 中调用；detectionModule 为 null 时安全跳过。
    /// 对下游影响：纯绘制，无运行时副作用。
    /// </summary>
    public void DrawDetectionGizmos()
    {
        detectionModule?.DrawDetectionGizmos();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前 Idle 模块内部的简要运行状态摘要字符串（含 Detection 摘要）。
    /// 功能：汇总当前子流程、anchor、巡逻角度、方向、剩余观察时间等信息，供调试与日志使用。
    /// 工作链路：可由总控脚本或外部 Debug 面板随时调用；不改变任何内部状态。
    /// 对下游影响：只读，无副作用。
    /// </summary>
    public string GetDebugStateSummary()
    {
        string subflow   = GetCurrentSubflowName();
        float  remaining = Mathf.Max(0f, idleEndTime - Time.time);
        string idlePart  = $"[SummonerIdle Summary] SubFlow={subflow} | Anchor={currentAnchor} | PatrolAngle={currentPatrolAngle:F1}° | Direction={(patrolDirection > 0 ? "CW" : "CCW")} | ObserveRemaining={remaining:F2}s";
        string detectPart = detectionModule != null ? "\n" + detectionModule.GetDebugDetectSummary() : "";
        return idlePart + detectPart;
    }

    // ══════════════════════════════════════════════
    // 内部子流程
    // ══════════════════════════════════════════════

    /// <summary>
    /// 处理 IdleObserve 子流程的每帧更新。
    /// 功能：维持原地停止状态，关闭 Walk 动画，驱动朝向缓慢转向"下一个 patrol 方向"；观察时间结束后触发 Anchor 生成并进入 PatrolMove。
    /// 工作链路：由 Tick 在 isIdleObserving == true 时每帧调用；观察完毕后依次调用 GenerateNextAnchor 和 BeginPatrolMove，切换子流程。
    /// 对下游影响：时间到达 idleEndTime 后，isIdleObserving 会被 BeginPatrolMove 置为 false，isMovingToAnchor 置为 true。
    /// </summary>
    private void UpdateIdleObserve()
    {
        navAgent.isStopped = true;
        animator.SetBool("Walk", false);

        UpdateObserveRotation();

        if (Time.time < idleEndTime)
            return;

        LogIdle("[Observe] IdleObserve finished. Prepare next patrol anchor.");

        GenerateNextAnchor();
        BeginPatrolMove();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 处理 PatrolMove 子流程的每帧更新。
    /// 功能：检测与目标 anchor 的距离，到达后停止移动，关闭 Walk 动画，切换回 IdleObserve 子流程并重置计时。
    /// 工作链路：由 Tick 在 isMovingToAnchor == true 时每帧调用；NavMeshAgent 的实际导航由 BeginPatrolMove 中的 SetDestination 驱动，本函数只做到达检测与状态切换。
    /// 对下游影响：到达后 isMovingToAnchor = false，isIdleObserving = true，下一帧 Tick 切换到 UpdateIdleObserve。
    /// </summary>
    private void UpdatePatrolMove()
    {
        float dist = Vector3.Distance(selfTransform.position, currentAnchor);
        if (dist > anchorReachThreshold)
            return;

        // 到达 anchor
        navAgent.isStopped = true;
        animator.SetBool("Walk", false);

        isMovingToAnchor = false;
        isIdleObserving  = true;
        idleEndTime      = Time.time + idleDuration;

        LogIdle("[Patrol] Reached anchor = "            + currentAnchor);
        LogIdle("[Patrol] Patrol complete. Remaining distance = " + dist.ToString("F3"));
        LogIdle("[Observe] IdleObserve start | duration = " + idleDuration + "s | idleEndTime = " + idleEndTime.ToString("F2"));
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 根据固定 patrolDirection 和角度推进规则生成下一个 patrol anchor。
    /// 功能：累加 currentPatrolAngle，计算圆周点 rawAnchor，对其进行 NavMesh 采样得到最终 currentAnchor；设置 hasValidAnchor = true。
    /// 工作链路：通常在 UpdateIdleObserve 确认观察时间结束后调用；生成结果由 BeginPatrolMove 使用。
    /// 对下游影响：currentAnchor 与 currentPatrolAngle 被更新；hasValidAnchor = true 使 BeginPatrolMove 可以正常执行。
    /// </summary>
    private void GenerateNextAnchor()
    {
        LogIdle("[Anchor] Generate next patrol anchor.");
        LogIdle("[Anchor] Previous angle = " + currentPatrolAngle.ToString("F1") + "°");

        currentPatrolAngle += patrolAngleStep * patrolDirection;

        Vector3 rawAnchor = CalculateCirclePoint(guardCenter, patrolRadius, currentPatrolAngle);
        currentAnchor     = SampleNavMeshOrFallback(rawAnchor);
        hasValidAnchor    = true;

        LogIdle("[Anchor] New angle = "    + currentPatrolAngle.ToString("F1") + "°");
        LogIdle("[Anchor] Raw anchor = "   + rawAnchor);
        LogIdle("[Anchor] Final anchor = " + currentAnchor);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 启动 PatrolMove 子流程，驱动 NavMeshAgent 前往 currentAnchor。
    /// 功能：检查 hasValidAnchor 有效性，设置 NavMeshAgent 速度与目标点，开启 Walk 动画，切换子流程标记。
    /// 工作链路：由 UpdateIdleObserve 在 GenerateNextAnchor 之后立即调用；调用后 isMovingToAnchor = true，下一帧 Tick 进入 UpdatePatrolMove。
    /// 对下游影响：NavMeshAgent 开始寻路，Animator Walk = true；若 hasValidAnchor = false 则提前返回，模块不移动。
    /// </summary>
    private void BeginPatrolMove()
    {
        if (!hasValidAnchor)
        {
            LogIdle("[Patrol] No valid anchor. Abort BeginPatrolMove.");
            return;
        }

        isIdleObserving  = false;
        isMovingToAnchor = true;

        navAgent.isStopped = false;
        navAgent.speed     = patrolMoveSpeed;
        navAgent.SetDestination(currentAnchor);
        animator.SetBool("Walk", true);

        LogIdle("[Patrol] Start moving to anchor = "  + currentAnchor);
        LogIdle("[Patrol] Current position = "        + selfTransform.position);
        LogIdle("[Patrol] patrolMoveSpeed = "         + patrolMoveSpeed);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 在 IdleObserve 阶段将自身朝向缓慢旋转至"下一个 patrol 方向"。
    /// 功能：预测下一个 patrol 角度对应的 previewAnchor，计算朝向向量，使用 Quaternion.Slerp 平滑旋转；内含节流 debug 输出，避免每帧刷屏。
    /// 算法：基于球面线性插值（Slerp）实现角速度均匀的朝向过渡，observeTurnSpeed * deltaTime 控制每帧旋转量。
    /// 工作链路：由 UpdateIdleObserve 每帧调用；不改变 isIdleObserving 等状态标记，纯旋转副作用。
    /// 对下游影响：selfTransform.rotation 持续向目标方向收敛，直至 Observe 结束或被 ExitState 打断。
    /// </summary>
    private void UpdateObserveRotation()
    {
        float   previewAngle  = currentPatrolAngle + patrolAngleStep * patrolDirection;
        Vector3 previewAnchor = CalculateCirclePoint(guardCenter, patrolRadius, previewAngle);

        Vector3 lookDir = previewAnchor - selfTransform.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDir.normalized);
        selfTransform.rotation    = Quaternion.Slerp(
            selfTransform.rotation,
            targetRotation,
            observeTurnSpeed * Time.deltaTime);

        // 节流 debug 输出，每 ObserveDebugInterval 秒最多输出一次
        if (enableIdleDebug && Time.time >= nextObserveDebugTime)
        {
            nextObserveDebugTime = Time.time + ObserveDebugInterval;
            Debug.Log("[SummonerIdle][Observe] Rotating toward next patrol direction | PreviewAngle = "
                + previewAngle.ToString("F1") + "° | TargetDir = " + lookDir.normalized
                + " | RemainingObserve = " + Mathf.Max(0f, idleEndTime - Time.time).ToString("F2") + "s");
        }
    }

    // ══════════════════════════════════════════════
    // 工具方法
    // ══════════════════════════════════════════════

    /// <summary>
    /// 根据中心点、半径和角度（度）计算圆周上的世界坐标点。
    /// 功能：以 XZ 平面为巡逻平面，Y 轴保持 guardCenter 高度；角度 0 对应 +X 轴方向。
    /// 工作链路：由 GenerateNextAnchor 和 UpdateObserveRotation 调用，是所有 anchor 坐标的唯一来源。
    /// </summary>
    private Vector3 CalculateCirclePoint(Vector3 center, float radius, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(
            center.x + radius * Mathf.Cos(rad),
            center.y,
            center.z + radius * Mathf.Sin(rad));
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 对目标点进行 NavMesh 采样，若采样失败则回退使用原始坐标。
    /// 功能：在 rawAnchor 附近（采样半径 = patrolRadius * 0.5f）查找最近 NavMesh 可达点；采样失败时输出警告并返回 rawAnchor 本身。
    /// 工作链路：由 GenerateNextAnchor 调用，确保生成的 anchor 始终落在可导航区域内。
    /// 对下游影响：返回值直接赋给 currentAnchor，NavMeshAgent.SetDestination 依赖此点的有效性。
    /// </summary>
    private Vector3 SampleNavMeshOrFallback(Vector3 rawAnchor)
    {
        if (NavMesh.SamplePosition(rawAnchor, out NavMeshHit hit, patrolRadius * 0.5f, NavMesh.AllAreas))
            return hit.position;

        if (enableIdleDebug)
            Debug.LogWarning("[SummonerIdle][Anchor] NavMesh sample failed for rawAnchor = " + rawAnchor + ". Using raw position as fallback.");

        return rawAnchor;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 统一的 Idle 模块 Debug 输出方法，受 enableIdleDebug 开关控制。
    /// 功能：在 enableIdleDebug == true 时输出带固定前缀 [SummonerIdle] 的日志，否则不产生任何输出。
    /// 工作链路：被本模块所有内部函数调用；总控脚本无需调用此方法。
    /// </summary>
    private void LogIdle(string message)
    {
        if (!enableIdleDebug)
            return;
        Debug.Log("[SummonerIdle]" + message);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前子流程的可读名称字符串，用于 Debug 输出。
    /// 功能：根据 isIdleObserving / isMovingToAnchor 标记返回对应名称；两者均为 false 时返回"None（异常）"。
    /// 工作链路：被 CanInterruptByPlayerDetect 和 GetDebugStateSummary 调用，纯只读查询。
    /// </summary>
    private string GetCurrentSubflowName()
    {
        if (isIdleObserving)  return "IdleObserve";
        if (isMovingToAnchor) return "PatrolMove";
        return "None（异常）";
    }
}
