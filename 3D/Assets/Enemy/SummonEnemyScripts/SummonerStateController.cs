using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 召唤师主状态枚举。
/// 功能：定义召唤师 AI 当前版本的 5 个主状态标识，用于总控 Update 中的状态分发。
/// </summary>
public enum SummonerMainState
{
    Idle,
    Alert,
    Summoning,
    Commanding,
    SelfDefense
}

/// <summary>
/// Summoner 敌人的主状态总控脚本。
/// 功能：作为召唤师 AI 的唯一主入口，统一持有并初始化所有状态模块，维护当前主状态，在每帧中只驱动当前状态对应的模块，统一处理主状态切换与杂兵归属数据中转。
/// 工作链路：挂载在召唤师 GameObject 上；Awake 阶段初始化所有模块并进入 Idle；Update 中按 currentState 分发到对应的 UpdateXState 函数；主状态切换统一通过 EnterXState 方法执行。
/// 设计原则：所有主状态切换只发生在总控；子模块不允许自己切主状态；currentControlledMinions 由总控统一中转维护；总控 debug 与模块 debug 分层独立。
/// </summary>
public class SummonerStateController : MonoBehaviour
{
    // ══════════════════════════════════════════════
    // Inspector 字段
    // ══════════════════════════════════════════════

    // ──────────────────────────────────────────────
    // 自身组件
    // ──────────────────────────────────────────────

    [Header("Components")]
    [Tooltip("召唤师 NavMeshAgent（若为空则 Awake 时自动获取）")]
    [SerializeField] private NavMeshAgent navAgent;

    [Tooltip("召唤师 Animator（若为空则 Awake 时自动获取）")]
    [SerializeField] private Animator animator;

    [Tooltip("召唤师 AudioSource（若为空则 Awake 时自动获取）")]
    [SerializeField] private AudioSource audioSource;

    // ──────────────────────────────────────────────
    // 场景引用
    // ──────────────────────────────────────────────

    [Header("References")]
    [Tooltip("玩家 Transform（若为空则 Awake 时通过 Tag 'Player' 查找）")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("固定召唤点数组（场景引用，传给 SummoningModule）")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("实现了 ISummonSpawnExecutor 的组件宿主（Inspector 拖入）")]
    [SerializeField] private MonoBehaviour summonSpawnExecutorBehaviour;

    // ──────────────────────────────────────────────
    // 配置
    // ──────────────────────────────────────────────

    [Header("Configs")]
    [SerializeField] private IdleConfig         idleConfig;
    [SerializeField] private DetectionConfig    detectionConfig;
    [SerializeField] private AlertConfig        alertConfig;
    [SerializeField] private SummoningConfig    summoningConfig;
    [SerializeField] private CommandingConfig   commandingConfig;
    [SerializeField] private SelfDefenseConfig  selfDefenseConfig;

    // ──────────────────────────────────────────────
    // Debug
    // ──────────────────────────────────────────────

    [Header("Debug")]
    [Tooltip("是否开启总控脚本自身的 Debug 输出（与各模块 Debug 开关分开）")]
    [SerializeField] private bool enableControllerDebug = true;

    // ══════════════════════════════════════════════
    // 模块实例（运行时创建）
    // ══════════════════════════════════════════════

    private SummonerIdleModule        idleModule;
    private SummonerAlertModule       alertModule;
    private SummonerSummoningModule   summoningModule;
    private SummonerCommandingModule  commandingModule;
    private SummonerSelfDefenseModule selfDefenseModule;

    // ══════════════════════════════════════════════
    // 运行时数据
    // ══════════════════════════════════════════════

    private SummonerMainState  currentState;
    private List<GameObject>   currentControlledMinions;
    private ISummonSpawnExecutor summonExecutor;
    private EnemyAttackDetector attackDetector;

    // ══════════════════════════════════════════════
    // 生命周期
    // ══════════════════════════════════════════════

    /// <summary>
    /// Unity Awake 回调，初始化总控与所有模块。
    /// 功能：缓存/获取自身组件引用；解析 summonExecutor；创建并初始化所有 5 个状态模块；初始化 currentControlledMinions；进入 Idle 状态。
    /// 工作链路：由 Unity 引擎在对象激活时自动调用一次；完成后所有模块可被正常驱动，主状态机进入 Idle。
    /// 对下游影响：所有模块 Initialize 完成；currentState = Idle；idleModule.EnterState() 已被调用。
    /// </summary>
    private void Awake()
    {
        // Step 1：解析自身组件引用
        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Step 2：解析玩家引用
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;
            else
                LogController("[Error] Player not found by Tag 'Player'. playerTransform is null.");
        }

        // Step 3：解析 summonExecutor
        if (summonSpawnExecutorBehaviour != null)
            summonExecutor = summonSpawnExecutorBehaviour as ISummonSpawnExecutor;

        if (summonExecutor == null)
            LogController("[Error] summonSpawnExecutorBehaviour is null or does not implement ISummonSpawnExecutor.");

        // Step 3.5：解析 EnemyAttackDetector（用于 SelfDefense 挥拳命中判定）
        attackDetector = GetComponent<EnemyAttackDetector>();
        if (attackDetector == null)
            attackDetector = GetComponentInChildren<EnemyAttackDetector>();

        LogController("[Init] navAgent = "        + (navAgent != null));
        LogController("[Init] animator = "        + (animator != null));
        LogController("[Init] audioSource = "     + (audioSource != null));
        LogController("[Init] playerTransform = " + (playerTransform != null ? playerTransform.name : "NULL"));
        LogController("[Init] summonExecutor = "  + (summonExecutor != null));
        LogController("[Init] attackDetector = "  + (attackDetector != null));

        // Step 4：创建并初始化所有模块
        Transform self = transform;

        idleModule = new SummonerIdleModule();
        idleModule.Initialize(self, navAgent, animator, idleConfig, detectionConfig);
        LogController("[Init] IdleModule initialized.");

        alertModule = new SummonerAlertModule();
        alertModule.Initialize(self, navAgent, animator, audioSource, alertConfig);
        LogController("[Init] AlertModule initialized.");

        summoningModule = new SummonerSummoningModule();
        summoningModule.Initialize(self, navAgent, animator, audioSource, summonExecutor, summoningConfig, spawnPoints);
        LogController("[Init] SummoningModule initialized.");

        commandingModule = new SummonerCommandingModule();
        commandingModule.Initialize(self, navAgent, animator, commandingConfig);
        LogController("[Init] CommandingModule initialized.");

        selfDefenseModule = new SummonerSelfDefenseModule();
        selfDefenseModule.Initialize(self, navAgent, animator, selfDefenseConfig);
        LogController("[Init] SelfDefenseModule initialized.");

        // Step 5：初始化运行时数据
        currentControlledMinions = new List<GameObject>();

        // Step 6：进入 Idle
        EnterIdleState();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Unity Update 回调，主状态机每帧入口。
    /// 功能：根据 currentState 分发到对应的 UpdateXState 函数；每帧只驱动一个主状态对应的模块。
    /// 工作链路：由 Unity 引擎每帧自动调用；内部调用对应模块的 Tick 与切换判断。
    /// 对下游影响：当前活跃模块被 Tick 驱动；主状态可能在本帧发生切换。
    /// </summary>
    private void Update()
    {
        switch (currentState)
        {
            case SummonerMainState.Idle:
                UpdateIdleState();
                break;
            case SummonerMainState.Alert:
                UpdateAlertState();
                break;
            case SummonerMainState.Summoning:
                UpdateSummoningState();
                break;
            case SummonerMainState.Commanding:
                UpdateCommandingState();
                break;
            case SummonerMainState.SelfDefense:
                UpdateSelfDefenseState();
                break;
            default:
                LogController("[Error] Unknown state: " + currentState);
                break;
        }
    }

    // ══════════════════════════════════════════════
    // 主状态 Update 处理
    // ══════════════════════════════════════════════

    /// <summary>
    /// 驱动 Idle 状态，并在发现玩家时切换到 Alert。
    /// 功能：优先调用 CanInterruptByPlayerDetect 检测中断条件；若发现玩家则 ExitState 并切入 Alert；否则调用 Tick 驱动 Idle 内部 Patrol/Observe 循环。
    /// 工作链路：由 Update 在 currentState == Idle 时每帧调用。
    /// 对下游影响：若检测命中，Idle 模块被 Exit，Alert 模块被 Enter；否则 Idle 模块被 Tick。
    /// </summary>
    private void UpdateIdleState()
    {
        if (idleModule.CanInterruptByPlayerDetect())
        {
            idleModule.ExitState();
            EnterAlertState();
            return;
        }

        idleModule.Tick();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 驱动 Alert 状态，并在完成后切换到 Summoning。
    /// 功能：调用 alertModule.Tick 驱动 Alert 内部计时与面向逻辑；查询 IsFinished，若完成则 ExitState 并切入 Summoning。
    /// 工作链路：由 Update 在 currentState == Alert 时每帧调用。
    /// 对下游影响：若 Alert 完成，Alert 模块被 Exit，Summoning 模块被 Enter。
    /// </summary>
    private void UpdateAlertState()
    {
        alertModule.Tick();

        if (alertModule.IsFinished())
        {
            alertModule.ExitState();
            EnterSummoningState();
            return;
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 驱动 Summoning 状态，并在完成后交接杂兵列表切换到 Commanding。
    /// 功能：调用 summoningModule.Tick 驱动召唤计时与生成逻辑；查询 IsFinished，若完成则先取走 spawnedMinions 写入 currentControlledMinions，再 ExitState 并切入 Commanding。
    /// 工作链路：由 Update 在 currentState == Summoning 时每帧调用。
    /// 对下游影响：若 Summoning 完成，currentControlledMinions 被更新，Summoning 模块被 Exit，Commanding 模块被 Enter 并接收杂兵列表。
    /// </summary>
    private void UpdateSummoningState()
    {
        summoningModule.Tick();

        if (summoningModule.IsFinished())
        {
            currentControlledMinions = new List<GameObject>(summoningModule.GetSpawnedMinions());
            LogController("[Handoff] Summoning -> Commanding | minions count = " + currentControlledMinions.Count);

            summoningModule.ExitState();
            EnterCommandingState();
            return;
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 驱动 Commanding 状态，并在玩家逼近中断时交接杂兵列表切换到 SelfDefense。
    /// 功能：调用 commandingModule.Tick 驱动维持、清理与中断检测；查询 ShouldBreakToSelfDefense，若需中断则先取走 controlledMinions 写入 currentControlledMinions，再 ExitState 并切入 SelfDefense。
    /// 工作链路：由 Update 在 currentState == Commanding 时每帧调用。
    /// 对下游影响：若需中断，currentControlledMinions 被更新，Commanding 模块被 Exit（Buff 被关闭），SelfDefense 模块被 Enter。
    /// </summary>
    private void UpdateCommandingState()
    {
        commandingModule.Tick();

        if (commandingModule.ShouldBreakToSelfDefense())
        {
            currentControlledMinions = new List<GameObject>(commandingModule.GetControlledMinions());
            LogController("[Handoff] Commanding -> SelfDefense | minions count = " + currentControlledMinions.Count);

            commandingModule.ExitState();
            EnterSelfDefenseState();
            return;
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 驱动 SelfDefense 状态，并在玩家远离允许退出时交接杂兵列表恢复到 Commanding。
    /// 功能：调用 selfDefenseModule.Tick 驱动子状态循环；查询 CanExitSelfDefense，若允许退出则先取走 controlledMinions 写入 currentControlledMinions，再 ExitState 并切回 Commanding。
    /// 工作链路：由 Update 在 currentState == SelfDefense 时每帧调用。
    /// 对下游影响：若允许退出，currentControlledMinions 被更新，SelfDefense 模块被 Exit，Commanding 模块被重新 Enter（Buff 重新开启）。
    /// </summary>
    private void UpdateSelfDefenseState()
    {
        selfDefenseModule.Tick();

        if (selfDefenseModule.CanExitSelfDefense())
        {
            currentControlledMinions = new List<GameObject>(selfDefenseModule.GetControlledMinions());
            LogController("[Handoff] SelfDefense -> Commanding | minions count = " + currentControlledMinions.Count);

            selfDefenseModule.ExitState();
            EnterCommandingState();
            return;
        }
    }

    // ══════════════════════════════════════════════
    // 主状态切换入口
    // ══════════════════════════════════════════════

    /// <summary>
    /// 切入 Idle 主状态。
    /// 功能：设置 currentState 为 Idle；调用 idleModule.EnterState() 启动 Idle 内部 Patrol/Observe 循环。
    /// 工作链路：由 Awake 在初始化完成后调用一次（开局进入 Idle）。
    /// 对下游影响：Idle 模块被激活，开始 Patrol/Observe 循环与玩家检测。
    /// </summary>
    private void EnterIdleState()
    {
        currentState = SummonerMainState.Idle;
        idleModule.EnterState();
        LogController("[State] -> Idle");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 切入 Alert 主状态。
    /// 功能：设置 currentState 为 Alert；调用 alertModule.EnterState 并传入 playerTransform 锁定目标。
    /// 工作链路：由 UpdateIdleState 在发现玩家后调用。
    /// 对下游影响：Alert 模块被激活，开始嚎叫表现与计时。
    /// </summary>
    private void EnterAlertState()
    {
        currentState = SummonerMainState.Alert;
        alertModule.EnterState(playerTransform);
        LogController("[State] -> Alert");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 切入 Summoning 主状态。
    /// 功能：设置 currentState 为 Summoning；调用 summoningModule.EnterState 并传入 playerTransform。
    /// 工作链路：由 UpdateAlertState 在 Alert 完成后调用。
    /// 对下游影响：Summoning 模块被激活，开始召唤计时与表现。
    /// </summary>
    private void EnterSummoningState()
    {
        currentState = SummonerMainState.Summoning;
        summoningModule.EnterState(playerTransform);
        LogController("[State] -> Summoning");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 切入 Commanding 主状态，并交接 currentControlledMinions。
    /// 功能：设置 currentState 为 Commanding；调用 commandingModule.EnterState 并传入 currentControlledMinions 与 playerTransform，使其接管杂兵并开启指挥 Buff。
    /// 工作链路：由 UpdateSummoningState（Summoning 完成后）或 UpdateSelfDefenseState（SelfDefense 可退出后）调用。
    /// 对下游影响：Commanding 模块被激活，受控杂兵的指挥 Buff 被开启。
    /// </summary>
    private void EnterCommandingState()
    {
        currentState = SummonerMainState.Commanding;
        commandingModule.EnterState(currentControlledMinions, playerTransform);
        LogController("[State] -> Commanding | minions = " + currentControlledMinions.Count);
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 切入 SelfDefense 主状态，并交接 currentControlledMinions。
    /// 功能：设置 currentState 为 SelfDefense；调用 selfDefenseModule.EnterState 并传入 currentControlledMinions 与 playerTransform，使其进入弱自卫循环。
    /// 工作链路：由 UpdateCommandingState 在玩家逼近中断时调用。
    /// 对下游影响：SelfDefense 模块被激活，进入 Reposition -> PrepareAttack -> Attack -> Pause 循环。
    /// </summary>
    private void EnterSelfDefenseState()
    {
        currentState = SummonerMainState.SelfDefense;
        selfDefenseModule.EnterState(currentControlledMinions, playerTransform);
        LogController("[State] -> SelfDefense | minions = " + currentControlledMinions.Count);
    }

    // ══════════════════════════════════════════════
    // 动画事件回调
    // ══════════════════════════════════════════════

    /// <summary>
    /// 动画事件回调：在 SelfDefense 挥拳动画的命中帧触发实际攻击判定。
    /// 功能：检查当前是否处于 SelfDefense 主状态；若是，则委托 EnemyAttackDetector.TryPerformAttack 执行距离检测与颜色交互事件发布。
    /// 工作链路：由 Animator 在攻击动画的指定帧通过 Animation Event 调用（函数名 "PerformAttack"）；attackDetector 为 null 时回退为直接发布 ColorEventBus 事件。
    /// 对下游影响：若命中成功，ColorEventBus.PublishEnemyAttack 被触发，颜色交互系统接管后续逻辑（伤害、眩晕等）。
    /// </summary>
    public void PerformAttack()
    {
        if (currentState != SummonerMainState.SelfDefense)
        {
            LogController("[Attack] PerformAttack called but not in SelfDefense. Ignored. | currentState = " + currentState);
            return;
        }

        if (playerTransform == null)
        {
            LogController("[Attack] PerformAttack called but playerTransform is null. Ignored.");
            return;
        }

        LogController("[Attack] PerformAttack — animation hit frame triggered");

        if (attackDetector != null)
        {
            bool hit = attackDetector.TryPerformAttack(gameObject, playerTransform);
            LogController("[Attack] EnemyAttackDetector result = " + (hit ? "HIT" : "MISS"));
        }
        else
        {
            LogController("[Attack] No EnemyAttackDetector found. Fallback to direct ColorEventBus publish.");
            ColorEventBus.PublishEnemyAttack(gameObject, playerTransform.gameObject);
        }
    }

    // ══════════════════════════════════════════════
    // Gizmos 转调
    // ══════════════════════════════════════════════

    /// <summary>
    /// 在 Scene 视图中转调 Detection Gizmos 绘制。
    /// 功能：将 OnDrawGizmosSelected 转发给 idleModule.DrawDetectionGizmos()，绘制球形保底范围与锥形视野可视化。
    /// 工作链路：由 Unity 引擎在 Scene 视图选中该对象时自动调用；idleModule 为 null 时安全跳过。
    /// 对下游影响：纯绘制，无运行时副作用。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (idleModule != null)
            idleModule.DrawDetectionGizmos();
    }

    // ══════════════════════════════════════════════
    // 工具方法
    // ══════════════════════════════════════════════

    /// <summary>
    /// 统一的总控脚本 Debug 输出方法，受 enableControllerDebug 开关控制。
    /// 功能：在 enableControllerDebug == true 时输出带固定前缀 [SummonerController] 的日志，否则不产生任何输出。
    /// 工作链路：被本脚本所有内部函数调用；各子模块有各自独立的 Log 方法，不使用此函数。
    /// </summary>
    private void LogController(string message)
    {
        if (!enableControllerDebug)
            return;
        Debug.Log("[SummonerController]" + message);
    }
}
