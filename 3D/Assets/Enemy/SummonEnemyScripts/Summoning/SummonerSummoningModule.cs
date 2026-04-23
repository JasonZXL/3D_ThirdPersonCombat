using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Summoner 敌人的 Summoning 状态模块。
/// 功能：负责召唤师在 Alert 结束后的原地召唤流程，包含原地锁定、召唤表现播放、计时驱动、结束时一次性生成杂兵四个固定行为。
/// 工作链路：由状态总控脚本持有；总控负责决定何时进入/退出 Summoning，本模块只负责 Summoning 内部所有逻辑的驱动与维护。
/// 设计原则：行为固定、不可分支；召唤期间不移动、不攻击、不自卫；杂兵只生成一次；不包含随机逻辑；模块自治，总控只通过公开接口交互。
/// </summary>
public class SummonerSummoningModule
{
    // ──────────────────────────────────────────────
    // 外部引用
    // ──────────────────────────────────────────────

    private Transform selfTransform;
    private NavMeshAgent navAgent;
    private Animator animator;
    private AudioSource audioSource;
    private ISummonSpawnExecutor summonExecutor;

    // ──────────────────────────────────────────────
    // 配置（Initialize 后只读）
    // ──────────────────────────────────────────────

    private bool enableSummoningDebug;
    private float summoningDuration;
    private float summonEndDuration;

    private bool keepFacingPlayerDuringSummoning;
    private float summoningTurnSpeed;

    private int summonCount;
    private bool useFixedSpawnPoints;
    private float spawnRadius;
    private Transform[] spawnPoints;

    private bool playSummonSound;
    private bool playSummonEffect;

    private bool useSummonStartTrigger;
    private string summonStartTriggerName;

    private bool useSummonLoopBool;
    private string summonLoopBoolName;

    private bool useSummonEndTrigger;
    private string summonEndTriggerName;

    // ──────────────────────────────────────────────
    // 运行时状态
    // ──────────────────────────────────────────────

    private Transform targetPlayer;
    private bool isInSummoning;
    private bool isFinished;
    private bool hasSpawnedMinions;

    private bool hasTriggeredSummonEnd;

    private float summoningEndTime;
    private List<GameObject> spawnedMinions;

    // ──────────────────────────────────────────────
    // Debug 节流
    // ──────────────────────────────────────────────

    private float nextTickDebugTime;
    private const float TickDebugInterval = 1.0f;

    // ══════════════════════════════════════════════
    // 公开接口
    // ══════════════════════════════════════════════

    /// <summary>
    /// 初始化 Summoning 模块所需引用与配置。
    /// 功能：将外部依赖、SummoningConfig 配置数据与场景级 spawnPoints 写入模块；重置运行时标记；初始化 spawnedMinions 列表。
    /// 工作链路：由状态总控脚本在对象 Awake/Start 阶段调用一次；完成后模块进入待命状态，等待 EnterState。
    /// 对下游影响：Initialize 完成后，所有配置字段只读；targetPlayer 为 null，isInSummoning = false，isFinished = false，hasSpawnedMinions = false。
    /// </summary>
    public void Initialize(
        Transform selfTransform,
        NavMeshAgent navAgent,
        Animator animator,
        AudioSource audioSource,
        ISummonSpawnExecutor summonExecutor,
        SummoningConfig config,
        Transform[] spawnPoints)
    {
        this.selfTransform = selfTransform;
        this.navAgent = navAgent;
        this.animator = animator;
        this.audioSource = audioSource;
        this.summonExecutor = summonExecutor;

        enableSummoningDebug = config.enableSummoningDebug;
        summoningDuration = config.summoningDuration;
        summonEndDuration = config.summonEndDuration;

        keepFacingPlayerDuringSummoning = config.keepFacingPlayerDuringSummoning;
        summoningTurnSpeed = config.summoningTurnSpeed;

        summonCount = config.summonCount;
        useFixedSpawnPoints = config.useFixedSpawnPoints;
        spawnRadius = config.spawnRadius;
        this.spawnPoints = spawnPoints;

        playSummonSound = config.playSummonSound;
        playSummonEffect = config.playSummonEffect;

        useSummonStartTrigger = config.useSummonStartTrigger;
        summonStartTriggerName = config.summonStartTriggerName;

        useSummonLoopBool = config.useSummonLoopBool;
        summonLoopBoolName = config.summonLoopBoolName;

        useSummonEndTrigger = config.useSummonEndTrigger;
        summonEndTriggerName = config.summonEndTriggerName;

        targetPlayer = null;
        isInSummoning = false;
        isFinished = false;
        hasSpawnedMinions = false;
        hasTriggeredSummonEnd = false;
        spawnedMinions = new List<GameObject>();

        LogSummon("[Init] selfTransform = " + selfTransform.name);
        LogSummon("[Init] summoningDuration = " + summoningDuration);
        LogSummon("[Init] summonEndDuration = " + summonEndDuration);
        LogSummon("[Init] summonCount = " + summonCount);
        LogSummon("[Init] useFixedSpawnPoints = " + useFixedSpawnPoints);
        LogSummon("[Init] spawnRadius = " + spawnRadius);
        LogSummon("[Init] keepFacingPlayer = " + keepFacingPlayerDuringSummoning);
        LogSummon("[Init] summoningTurnSpeed = " + summoningTurnSpeed);
        LogSummon("[Init] playSummonSound = " + playSummonSound);
        LogSummon("[Init] playSummonEffect = " + playSummonEffect);
        LogSummon("[Init] useSummonStartTrigger = " + useSummonStartTrigger);
        LogSummon("[Init] summonStartTriggerName = " + summonStartTriggerName);
        LogSummon("[Init] useSummonLoopBool = " + useSummonLoopBool);
        LogSummon("[Init] summonLoopBoolName = " + summonLoopBoolName);
        LogSummon("[Init] useSummonEndTrigger = " + useSummonEndTrigger);
        LogSummon("[Init] summonEndTriggerName = " + summonEndTriggerName);
        LogSummon("[Init] spawnPoints provided = " + (spawnPoints != null ? spawnPoints.Length.ToString() : "null"));
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 进入 Summoning 状态，锁定目标玩家并启动召唤计时与召唤表现。
    /// 功能：接收当前玩家 Transform 引用（仅用于朝向，可为 null）；停止 NavMeshAgent 移动；关闭 Walk 动画；重置运行时标记；清空上一次的 spawnedMinions；设置计时；触发召唤表现。
    /// 工作链路：由状态总控脚本在从 Alert 切换到 Summoning 时调用；之后由每帧 Tick 驱动剩余计时、朝向与生成逻辑。
    /// 对下游影响：isInSummoning = true，isFinished = false，hasSpawnedMinions = false，summoningEndTime 被设定；spawnedMinions 被清空。
    /// </summary>
    public void EnterState(Transform playerTransform)
    {
        nextTickDebugTime = Time.time;

        targetPlayer = playerTransform;
        isInSummoning = true;
        isFinished = false;
        hasSpawnedMinions = false;
        hasTriggeredSummonEnd = false;
        spawnedMinions.Clear();

        summoningEndTime = Time.time + summoningDuration;

        navAgent.isStopped = true;
        animator.SetBool("Walk", false);

        TriggerSummonStartPresentation();

        LogSummon("[Enter] Enter SummoningState");
        LogSummon("[Enter] targetPlayer = " + (targetPlayer != null ? targetPlayer.name : "NULL")
            + " | position = " + (targetPlayer != null ? targetPlayer.position.ToString() : "N/A"));
        LogSummon("[Enter] summoningDuration = " + summoningDuration + "s | summoningEndTime = " + summoningEndTime.ToString("F2"));
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Summoning 状态每帧更新入口，处理朝向玩家、阶段切换与一次性杂兵生成。
    /// 功能：在总召唤时长内维持朝向；在最后 summonEndDuration 时间窗口触发 Summon_End；到总时长结束后执行一次 SpawnMinions 并结束状态。
    /// 工作链路：由状态总控脚本在当前主状态为 Summoning 时每帧调用；IsFinished 查询在 Tick 之后执行。
    /// 对下游影响：杂兵生成在本函数调用链内完成；isFinished 在生成完毕后被置为 true，总控在下一次 IsFinished 查询时获得 true 并切入 Commanding。
    /// </summary>
    public void Tick()
    {
        if (!isInSummoning)
            return;

        if (keepFacingPlayerDuringSummoning)
            UpdateFacingToPlayer();

        // 在总时长尾声，触发 Summon_End
        if (!hasTriggeredSummonEnd && Time.time >= summoningEndTime - summonEndDuration)
        {
            TriggerSummonEndPresentation();
            hasTriggeredSummonEnd = true;

            LogSummon("[Tick] Trigger Summon_End"
                + " | Time = " + Time.time.ToString("F2")
                + " | EndWindowStart = " + (summoningEndTime - summonEndDuration).ToString("F2"));
        }

        // 到总时长结束时真正生成
        if (!hasSpawnedMinions && Time.time >= summoningEndTime)
        {
            SpawnMinions();
            hasSpawnedMinions = true;
            isFinished = true;

            if (useSummonLoopBool && animator != null)
            {
                animator.SetBool(summonLoopBoolName, false);
                LogSummon("[Action] Set " + summonLoopBoolName + " = false");
            }

            LogSummon("[Tick] Summoning finished."
                + " | Time = " + Time.time.ToString("F2")
                + " | summoningEndTime = " + summoningEndTime.ToString("F2")
                + " | hasSpawnedMinions = true");
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前 Summoning 是否已完成。
    /// 功能：纯布尔查询，返回 isFinished 的当前值；不修改任何内部状态。
    /// 工作链路：由状态总控脚本在 Summoning 状态下每帧调用 Tick 之后查询；若返回 true，总控负责取走 spawnedMinions、调用 ExitState 并切入 Commanding。
    /// 对下游影响：只读查询，无副作用。
    /// </summary>
    public bool IsFinished()
    {
        return isFinished;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 退出 Summoning 状态，清理内部运行时标记。
    /// 功能：将 isInSummoning、isFinished 重置为 false；清空 targetPlayer 引用；关闭 SummonLoop Bool；输出退出日志。
    /// 工作链路：由状态总控脚本在 Summoning 完成后切出时调用（切入 Commanding 之前）；调用后本模块进入休眠，等待下次 EnterState。
    /// 对下游影响：模块停止驱动，Tick 若仍被调用会因 isInSummoning == false 直接返回，不产生错误。
    /// </summary>
    public void ExitState()
    {
        if (useSummonLoopBool && animator != null)
        {
            animator.SetBool(summonLoopBoolName, false);
            LogSummon("[Action] Set " + summonLoopBoolName + " = false (ExitState)");
        }

        LogSummon("[Exit] Exit SummoningState"
            + " | targetPlayer was = " + (targetPlayer != null ? targetPlayer.name : "NULL")
            + " | spawnedMinions count = " + spawnedMinions.Count);

        isInSummoning = false;
        isFinished = false;
        targetPlayer = null;
        hasTriggeredSummonEnd = false;

        LogSummon("[Exit] Cleared runtime flags. spawnedMinions preserved for Commanding handoff.");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回本次 Summoning 生成出来的杂兵 GameObject 列表。
    /// 功能：提供生成结果的只读访问，供总控脚本在切入 Commanding 前取走杂兵引用。
    /// 工作链路：由总控脚本在 IsFinished() 返回 true 后、ExitState() 调用前读取；Commanding 状态依赖此列表接管杂兵。
    /// 对下游影响：只读查询，无副作用；返回的是内部列表的引用，调用方应在取走后自行缓存。
    /// </summary>
    public List<GameObject> GetSpawnedMinions()
    {
        return spawnedMinions;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前 Summoning 模块内部的简要运行状态摘要字符串。
    /// 功能：汇总 isInSummoning、isFinished、hasSpawnedMinions、targetPlayer、剩余时间、已生成杂兵数等信息，供调试面板与日志使用。
    /// 工作链路：可由总控脚本或外部 Debug 面板随时调用；不改变任何内部状态。
    /// 对下游影响：只读，无副作用。
    /// </summary>
    public string GetDebugStateSummary()
    {
        float remaining = Mathf.Max(0f, summoningEndTime - Time.time);
        return $"[SummonerSummon Summary] isInSummoning={isInSummoning} | isFinished={isFinished}"
            + $" | hasSpawned={hasSpawnedMinions}"
            + $" | hasTriggeredEnd={hasTriggeredSummonEnd}"
            + $" | target={(targetPlayer != null ? targetPlayer.name : "NULL")}"
            + $" | remaining={remaining:F2}s"
            + $" | spawnedCount={spawnedMinions.Count}";
    }

    // ══════════════════════════════════════════════
    // 内部方法
    // ══════════════════════════════════════════════

    /// <summary>
    /// Summoning 期间缓慢将自身朝向旋转至面向目标玩家。
    /// 功能：计算自身到玩家的水平方向向量，使用 Quaternion.Slerp 平滑旋转；忽略 Y 轴高度差，仅在 XZ 平面旋转。
    /// 工作链路：由 Tick 在 keepFacingPlayerDuringSummoning == true 时每帧调用；targetPlayer 为 null 时安全返回。
    /// 对下游影响：selfTransform.rotation 持续向玩家方向收敛，直至 Summoning 结束或被 ExitState 打断。
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
        selfTransform.rotation = Quaternion.Slerp(
            selfTransform.rotation,
            targetRotation,
            summoningTurnSpeed * Time.deltaTime);

        if (enableSummoningDebug && Time.time >= nextTickDebugTime)
        {
            nextTickDebugTime = Time.time + TickDebugInterval;
            float remaining = Mathf.Max(0f, summoningEndTime - Time.time);
            LogSummon("[Tick] Facing player | TargetDir = " + lookDir.normalized
                + " | RemainingSummoning = " + remaining.ToString("F2") + "s");
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 触发 Summon_Start 与 Loop 阶段表现。
    /// 功能：播放召唤音效、召唤特效占位、触发 Summon_Start、打开 SummonLoop Bool。
    /// 工作链路：由 EnterState 调用一次。
    /// 对下游影响：Animator 进入 Summon_Start，并准备过渡到 Summon_Loop。
    /// </summary>
    private void TriggerSummonStartPresentation()
    {
        if (playSummonSound && audioSource != null)
        {
            audioSource.Play();
            LogSummon("[Action] Play summon sound");
        }
        else if (playSummonSound && audioSource == null)
        {
            LogSummon("[Action] playSummonSound = true but AudioSource is null. Skipped.");
        }

        if (playSummonEffect)
        {
            // TODO: 在此处实例化或播放召唤特效
            LogSummon("[Action] Play summon effect (placeholder — no VFX asset assigned yet)");
        }

        if (useSummonStartTrigger && animator != null)
        {
            animator.SetTrigger(summonStartTriggerName);
            LogSummon("[Action] Trigger summon start animation = " + summonStartTriggerName);
        }

        if (useSummonLoopBool && animator != null)
        {
            animator.SetBool(summonLoopBoolName, true);
            LogSummon("[Action] Set " + summonLoopBoolName + " = true");
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 触发 Summon_End 阶段表现。
    /// 功能：在总时长末尾切入释放动作。
    /// 工作链路：由 Tick 在 summoningEndTime - summonEndDuration 时触发一次。
    /// 对下游影响：Animator 从 Summon_Loop 进入 Summon_End。
    /// </summary>
    private void TriggerSummonEndPresentation()
    {
        if (useSummonLoopBool && animator != null)
        {
            animator.SetBool(summonLoopBoolName, false);
            LogSummon("[Action] Set " + summonLoopBoolName + " = false");
        }

        if (useSummonEndTrigger && animator != null)
        {
            animator.SetTrigger(summonEndTriggerName);
            LogSummon("[Action] Trigger summon end animation = " + summonEndTriggerName);
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 执行一次性杂兵生成，并将所有生成结果保存到 spawnedMinions 列表。
    /// 功能：调用 GetSpawnPositions 获取出生点列表；委托 summonExecutor.SpawnMinions 执行实际生成；将返回结果写入 spawnedMinions；逐个输出生成日志。
    /// 工作链路：由 Tick 在召唤总时长结束瞬间调用一次（hasSpawnedMinions == false 保证只执行一次）；完成后 hasSpawnedMinions = true，isFinished = true。
    /// 对下游影响：spawnedMinions 列表被填充，总控可通过 GetSpawnedMinions() 取走；生成执行器负责实际 Instantiate，本函数不直接创建 GameObject。
    /// </summary>
    private void SpawnMinions()
    {
        List<Vector3> positions = GetSpawnPositions();

        LogSummon("[Spawn] Start SpawnMinions | RequestedCount = " + summonCount + " | PositionCount = " + positions.Count);

        if (summonExecutor != null)
        {
            List<GameObject> result = summonExecutor.SpawnMinions(positions);
            if (result != null)
            {
                spawnedMinions.AddRange(result);
            }
        }
        else
        {
            LogSummon("[Spawn] summonExecutor is null! Cannot spawn minions. Skipped.");
        }

        if (enableSummoningDebug)
        {
            for (int i = 0; i < spawnedMinions.Count; i++)
            {
                GameObject minion = spawnedMinions[i];
                LogSummon("[Spawn] Spawned minion [" + i + "] = "
                    + (minion != null ? minion.name : "NULL")
                    + " | Pos = " + (minion != null ? minion.transform.position.ToString() : "N/A"));
            }
        }

        LogSummon("[Spawn] Spawn complete | FinalCount = " + spawnedMinions.Count);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 根据配置生成本次召唤的所有出生世界坐标点。
    /// 功能：若 useFixedSpawnPoints == true 且 spawnPoints 有效，则按顺序取固定点的 position；否则使用围绕召唤师等角分布的圆周点。
    /// 工作链路：由 SpawnMinions 在执行生成前调用；返回结果直接传入 summonExecutor.SpawnMinions。
    /// 对下游影响：返回的坐标列表决定杂兵的出生位置；不修改任何模块内部状态。
    /// </summary>
    private List<Vector3> GetSpawnPositions()
    {
        List<Vector3> result = new List<Vector3>();

        if (summonCount <= 0)
        {
            LogSummon("[Spawn] summonCount <= 0. Return empty spawn list.");
            return result;
        }

        if (useFixedSpawnPoints && spawnPoints != null && spawnPoints.Length > 0)
        {
            int count = Mathf.Min(summonCount, spawnPoints.Length);
            for (int i = 0; i < count; i++)
            {
                if (spawnPoints[i] != null)
                {
                    result.Add(spawnPoints[i].position);
                    LogSummon("[Spawn] FixedSpawnPoint [" + i + "] = " + spawnPoints[i].position);
                }
                else
                {
                    LogSummon("[Spawn] FixedSpawnPoint [" + i + "] is null. Skipped.");
                }
            }
            return result;
        }

        if (useFixedSpawnPoints)
        {
            LogSummon("[Spawn] useFixedSpawnPoints = true but no valid spawnPoints provided. Fallback to circle spawn.");
        }

        Vector3 center = selfTransform.position;
        float angleStep = 360f / summonCount;

        for (int i = 0; i < summonCount; i++)
        {
            float angleDeg = angleStep * i;
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                center.x + spawnRadius * Mathf.Cos(rad),
                center.y,
                center.z + spawnRadius * Mathf.Sin(rad));

            result.Add(pos);
            LogSummon("[Spawn] CircleSpawnPoint [" + i + "] = " + pos + " | Angle = " + angleDeg.ToString("F1") + "°");
        }

        return result;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 统一的 Summoning 模块 Debug 输出方法，受 enableSummoningDebug 开关控制。
    /// 功能：在 enableSummoningDebug == true 时输出带固定前缀 [SummonerSummon] 的日志，否则不产生任何输出。
    /// 工作链路：被本模块所有内部函数调用；总控脚本无需调用此方法。
    /// </summary>
    private void LogSummon(string message)
    {
        if (!enableSummoningDebug)
            return;
        Debug.Log("[SummonerSummon]" + message);
    }
}