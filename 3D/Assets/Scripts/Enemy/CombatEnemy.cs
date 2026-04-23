using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 近战小怪行为逻辑：环形区域漫游 → 突进攻击 → 后退 ↔ 位置调整
/// 状态流转：Pressure（压迫接近）→ Probe（中距试探）→ CommitAttack（预警前压）
///           → Attack（正式攻击）→ Recover（攻后恢复）→ ResetSpacing（位置纠偏）
///           特殊状态：Stunned（眩晕）/ Knockback（击退）
/// </summary>
public class CombatEnemy : BaseEnemy, IKnockbackReceiver
{
    // ══════════════════════════════════════════════════════════════
    #region 枚举与状态定义
    // ══════════════════════════════════════════════════════════════

    public enum CombatEnemyState
    {
        Pressure,      // 远距离压迫式接近
        Probe,         // 中距离试探
        CommitAttack,  // 攻击预警 / 短前压
        Attack,        // 正式攻击
        Recover,       // 攻后恢复 / 短退 / 重整
        ResetSpacing,  // 仅纠偏
        Stunned,
        Knockback
    }

    private enum RecoverMode
    {
        Pause,      // 原地短暂停顿
        StepBack    // 小幅后退
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region Inspector - 基础战斗参数
    // ══════════════════════════════════════════════════════════════

    [Header("环形区域")]
    [SerializeField] private float innerRadius = 3f;               // 内环半径
    [SerializeField] private float outerRadius = 7f;               // 外环半径

    [Header("移动")]
    [SerializeField] private float chargeSpeed = 7f;               // 突进速度
    [SerializeField] private float rotationSpeed = 8f;             // 旋转速度
    [SerializeField] private float repositionTargetMargin = 0.5f;  // 归位目标距玩家的额外安全边距

    [Header("攻击")]
    [SerializeField] private float attackRange = 1.5f;             // 近战触发攻击的距离
    [SerializeField] private float attackAnimDuration = 0.5f;      // 攻击动画等待时长（秒）
    [SerializeField] private float giveUpChaseDistance = 20f;      // 追击放弃距离（超出则放弃本次攻击）

    [Header("状态持续时间")]
    [SerializeField] private float pressureMinDuration = 0.8f;
    [SerializeField] private float probeMinDuration = 1.2f;
    [SerializeField] private float commitDuration = 0.25f;
    [SerializeField] private float recoverDuration = 0.5f;

    [Header("重置位置")]
    [SerializeField] private float recoverStepBackDistance = 1.2f;
    [SerializeField] [Range(0f, 1f)] private float recoverStepBackChance = 0.6f;
    [SerializeField] private float recoverRepathDistance = 0.2f;

    [Header("战斗移动")]
    [SerializeField] private float pressureSpeed = 2.8f;
    [SerializeField] private float probeSpeed = 1.8f;
    [SerializeField] private float recoverMoveSpeed = 2.0f;
    [SerializeField] private float resetSpacingSpeed = 3.5f;

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region Inspector - 攻击意图系统
    // ══════════════════════════════════════════════════════════════

    [Header("准备攻击")]
    [SerializeField] private GameObject commitFlashFx;
    [SerializeField] private Transform commitFlashSpawnPoint;
    [SerializeField] private float commitForwardStep = 0.8f;
    [SerializeField] private float commitCancelDistance = 2.5f;

    [Header("攻击意图设置")]
    [SerializeField] private float commitAttackIntentThreshold = 60f;
    [SerializeField] private float standoffIntentGainPerSecond = 16f;
    [SerializeField] private float retaliationDecayPerSecond = 20f;
    [SerializeField] private float retaliationGainOnPlayerHit = 35f;
    [SerializeField] private float retaliationConsumeOnAttack = 30f;
    [SerializeField] private float decisionCheckIntervalMin = 0.18f;
    [SerializeField] private float decisionCheckIntervalMax = 0.32f;

    [Header("攻击意图权重")]
    [SerializeField] private float pressureDistanceIntentWeight = 16f;
    [SerializeField] private float probeDistanceIntentWeight = 22f;
    [SerializeField] private float closeRangeStayIntentPerSecond = 12f;
    [SerializeField] private float playerRetreatIntentPenaltyPerSecond = 8f;
    [SerializeField] private float playerApproachIntentBonus = 12f;
    [SerializeField] private float playerApproachSpeedThreshold = 2.5f;
    [SerializeField] private float playerBlindSideIntentBonus = 15f;
    [SerializeField] private float playerBlindSideDotThreshold = 0.15f;

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region Inspector - 个体差异参数
    // ══════════════════════════════════════════════════════════════

    [Header("个体攻击倾向")]
    [SerializeField] private float aggressionBiasMin = -0.25f;
    [SerializeField] private float aggressionBiasMax = 0.35f;
    [SerializeField] private float aggressionPressureWeight = 18f;
    [SerializeField] private float aggressionProbeWeight = 22f;
    [SerializeField] private float aggressionPlayerInputWeight = 0.6f;

    [Header("个体观察耐心")]
    [SerializeField] private float patienceBiasMin = -0.3f;
    [SerializeField] private float patienceBiasMax = 0.4f;
    [SerializeField] private float patienceProbeDurationWeight = 0.8f;
    [SerializeField] private float patienceStopChanceWeight = 0.18f;
    [SerializeField] private float patienceStopDurationWeight = 0.2f;
    [SerializeField] private float patienceStopIntervalWeight = 0.2f;

    [Header("个体交战距离偏好")]
    [SerializeField] private float spacingBiasMin = -0.6f;
    [SerializeField] private float spacingBiasMax = 0.6f;
    [SerializeField] private float spacingPressureOffsetWeight = 0.8f;
    [SerializeField] private float spacingProbeOffsetWeight = 1.0f;
    [SerializeField] private float spacingResetOffsetWeight = 0.6f;

    [Header("个体横移倾向")]
    [SerializeField] private float strafeBiasMin = -0.35f;
    [SerializeField] private float strafeBiasMax = 0.4f;
    [SerializeField] private float strafePressureWeight = 0.2f;
    [SerializeField] private float strafeProbeWeight = 0.25f;
    [SerializeField] private float strafePressureStepWeight = 0.35f;
    [SerializeField] private float strafeProbeStepWeight = 0.25f;

    [Header("个体攻后恢复倾向")]
    [SerializeField] private float recoverBiasMin = -0.3f;
    [SerializeField] private float recoverBiasMax = 0.35f;
    [SerializeField] private float recoverDurationWeight = 0.35f;
    [SerializeField] private float recoverStepBackChanceWeight = 0.25f;
    [SerializeField] private float recoverStepBackDistanceWeight = 0.4f;

    [Header("群体攻击许可")]
    [SerializeField] private float groupAttackCheckRadius = 6f;
    [SerializeField] private int maxNearbyAttackers = 1;
    [SerializeField] private float groupAttackIntentPenalty = 22f;
    [SerializeField] [Range(0f, 1f)] private float rareOverrideAttackChance = 0.08f;

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region Inspector - 调试与表现参数
    // ══════════════════════════════════════════════════════════════

    [Header("试探设置")]
    [SerializeField] private float probeStrafeWeight = 0.65f;
    [SerializeField] private float pressureStrafeWeight = 0.3f;
    [SerializeField] private float probeStopChance = 0.5f;
    [SerializeField] private float probeStopDuration = 1.0f;

    [Header("眩晕设置")]
    [SerializeField] private float stunDuration = 2f;              // 眩晕持续时长（秒）

    [Header("调试设置")]
    [SerializeField] private bool showStateDebug = true;

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 运行时引用
    // ══════════════════════════════════════════════════════════════

    private EnemyAttackDetector attackDetector;
    private Transform player;
    private NavMeshAgent navAgent;
    private KnockbackSystem knockbackSystem;
    private Animator animator;

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 状态缓存
    // ══════════════════════════════════════════════════════════════

    private CombatEnemyState currentState;
    private Coroutine currentStateCoroutine;
    private float stateEnterTime = 0f;
    private float nextDecisionTime = 0f;
    private bool commitFxPlayed = false;
    private Vector3 commitTargetPosition;
    private RecoverMode currentRecoverMode = RecoverMode.Pause;
    private Vector3 recoverTargetPosition;
    private float standoffIntentScore = 0f;
    private float retaliationIntentScore = 0f;
    private float recoverSuppressionEndTime = -999f;
    private bool isProbeStopping = false;
    private float probeStopEndTime = -999f;
    private float nextProbeStopCheckTime = -999f;

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 玩家行为采样
    // ══════════════════════════════════════════════════════════════

    private Vector3 lastPlayerPosition;
    private Vector3 playerVelocity;
    private float playerCloseStayTimer = 0f;

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 个体差异运行时结果
    // ══════════════════════════════════════════════════════════════

    private float aggressionBias = 0f;      // 个体攻击倾向偏差
    private float patienceBias = 0f;
    private float spacingBias = 0f;
    private float strafeBias = 0f;
    private float recoverBias = 0f;

    private float personalAngleOffset;      // 个体角度偏好，避免所有敌人站位整齐
    private int preferredOrbitDirection;    // 个体绕圈方向偏好（+1 或 -1）
    private float personalRadiusOffset;     // 个体对包围半径的微偏移

    // 个体化 Recover 参数
    private float myRecoverDuration = 0f;
    private float myRecoverStepBackChance = 0f;
    private float myRecoverStepBackDistance = 0f;

    // 个体化横移参数
    private float myPressureStrafeWeight = 0f;
    private float myProbeStrafeWeight = 0f;
    private float myPressureStepDistance = 0f;
    private float myProbeStepDistance = 0f;

    // 个体化交战距离
    private float myPressureEnterProbeDistance = 0f;
    private float myPreferredProbeDistance = 0f;
    private float myResetSpacingDistance = 0f;

    // 个体化 Probe 节奏
    private float myProbeMinDuration = 0f;
    private float myProbeStopChance = 0f;
    private float myProbeStopDuration = 0f;
    private float myProbeStopCheckMin = 0f;
    private float myProbeStopCheckMax = 0f;

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 调试编号
    // ══════════════════════════════════════════════════════════════

    private static int globalDebugIdCounter = 0;
    private int debugEnemyId;

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region Unity 生命周期
    // ══════════════════════════════════════════════════════════════

    protected override void Awake()
    {
        base.Awake();
        debugEnemyId = ++globalDebugIdCounter;

        InitializeComponents();
        FindPlayer();
        InitializeEncirclementPersonality();
    }

    private void Start()
    {
        if (player != null)
        {
            lastPlayerPosition = player.position;
            SwitchState(CombatEnemyState.Pressure);
        }
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 初始化
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取并配置所有运行时依赖组件（NavMeshAgent、Animator、KnockbackSystem 等）。
    /// 若组件缺失则尝试自动补充，并在调试模式下输出初始化摘要。
    /// </summary>
    private void InitializeComponents()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        navAgent.angularSpeed = rotationSpeed * 100f;
        navAgent.acceleration = 10f;
        navAgent.autoBraking = true;
        navAgent.updateRotation = false; // 由 FacePlayerHorizontally() 手动控制朝向
        navAgent.updatePosition = true;

        knockbackSystem = GetComponent<KnockbackSystem>();

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        attackDetector = GetComponent<EnemyAttackDetector>();
        if (attackDetector == null)
            attackDetector = GetComponentInChildren<EnemyAttackDetector>();

        if (showStateDebug)
            LogState($"初始化完成，name={gameObject.name}, innerRadius={innerRadius}, outerRadius={outerRadius}");
    }

    /// <summary>
    /// 通过标签查找场景中的玩家对象，缓存其 Transform 引用。
    /// 找不到时输出警告，后续逻辑中所有 player == null 判断均依赖此步骤。
    /// </summary>
    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            if (showStateDebug)
                LogState($"找到玩家: {player.name}");
        }
        else
        {
            Debug.LogWarning($"⚠️ CombatEnemy [{gameObject.name}] 未找到Player标签对象！");
        }
    }

    /// <summary>
    /// 为每只敌人随机生成个体差异参数，包括攻击倾向、耐心、交战距离、横移习惯和恢复风格。
    /// 所有"my"前缀的运行时变量均在此处由 Inspector 参数 + 随机偏差计算得出。
    /// </summary>
    private void InitializeEncirclementPersonality()
    {
        personalAngleOffset = Random.Range(-45f, 45f);
        preferredOrbitDirection = Random.value < 0.5f ? -1 : 1;
        personalRadiusOffset = Random.Range(-1.2f, 1.2f);

        aggressionBias = Random.Range(aggressionBiasMin, aggressionBiasMax);
        patienceBias   = Random.Range(patienceBiasMin, patienceBiasMax);
        spacingBias    = Random.Range(spacingBiasMin, spacingBiasMax);
        strafeBias     = Random.Range(strafeBiasMin, strafeBiasMax);
        recoverBias    = Random.Range(recoverBiasMin, recoverBiasMax);

        // Probe 个体节奏
        myProbeMinDuration  = Mathf.Max(0.25f, probeMinDuration + patienceBias * patienceProbeDurationWeight);
        myProbeStopChance   = Mathf.Clamp01(probeStopChance + patienceBias * patienceStopChanceWeight);
        myProbeStopDuration = Mathf.Max(0.05f, probeStopDuration + patienceBias * patienceStopDurationWeight);
        myProbeStopCheckMin = Mathf.Max(0.1f, 0.25f + patienceBias * patienceStopIntervalWeight);
        myProbeStopCheckMax = Mathf.Max(myProbeStopCheckMin + 0.05f, 0.55f + patienceBias * patienceStopIntervalWeight);

        // 交战距离个体化
        myPressureEnterProbeDistance = Mathf.Clamp(
            outerRadius + spacingBias * spacingPressureOffsetWeight,
            innerRadius + 0.5f,
            outerRadius + 1.2f
        );
        myPreferredProbeDistance = Mathf.Clamp(
            ((innerRadius + outerRadius) * 0.5f) + spacingBias * spacingProbeOffsetWeight,
            innerRadius + 0.25f,
            outerRadius - 0.25f
        );
        myResetSpacingDistance = Mathf.Clamp(
            attackRange + repositionTargetMargin + spacingBias * spacingResetOffsetWeight,
            attackRange + 0.2f,
            innerRadius + 0.8f
        );

        // 横移倾向个体化
        myPressureStrafeWeight = Mathf.Clamp01(pressureStrafeWeight + strafeBias * strafePressureWeight);
        myProbeStrafeWeight    = Mathf.Clamp01(probeStrafeWeight + strafeBias * strafeProbeWeight);
        myPressureStepDistance = Mathf.Max(0.8f, 1.6f + strafeBias * strafePressureStepWeight);
        myProbeStepDistance    = Mathf.Max(0.5f, 1.0f + strafeBias * strafeProbeStepWeight);

        // Recover 个体化
        myRecoverDuration         = Mathf.Max(0.15f, recoverDuration + recoverBias * recoverDurationWeight);
        myRecoverStepBackChance   = Mathf.Clamp01(recoverStepBackChance + recoverBias * recoverStepBackChanceWeight);
        myRecoverStepBackDistance = Mathf.Max(0.2f, recoverStepBackDistance + recoverBias * recoverStepBackDistanceWeight);

        if (showStateDebug)
        {
            LogState(
                $"个体参数: aggr={aggressionBias:F2}, patience={patienceBias:F2}, spacing={spacingBias:F2}, " +
                $"strafe={strafeBias:F2}, recover={recoverBias:F2}, probeMin={myProbeMinDuration:F2}, " +
                $"probeDist={myPreferredProbeDistance:F2}, recoverDur={myRecoverDuration:F2}, " +
                $"recoverBackChance={myRecoverStepBackChance:F2}"
            );
        }
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 外部接口 - 击退 / 眩晕 / 颜色交互
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// IKnockbackReceiver：击退开始时调用。
    /// 停止当前状态协程，切换至 Knockback 状态，并触发击退动画。
    /// 同时累积反击意图，使敌人更容易在恢复后主动出手。
    /// </summary>
    public void OnKnockbackStart()
    {
        retaliationIntentScore += retaliationGainOnPlayerHit;
        retaliationIntentScore = Mathf.Clamp(retaliationIntentScore, 0f, 100f);

        if (showStateDebug)
            Debug.Log($"💨 [CombatEnemy] 击退开始");

        StopCurrentCoroutine();
        currentState = CombatEnemyState.Knockback;

        // NavMeshAgent 由 KnockbackSystem 通过 isStopped 控制，此处仅做保险
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        SetAnimatorTrigger("Knockback");
    }

    /// <summary>
    /// IKnockbackReceiver：击退结束时调用。
    /// 恢复 NavMeshAgent 并根据当前与玩家的距离重新进入合适的战斗状态。
    /// </summary>
    public void OnKnockbackEnd()
    {
        if (showStateDebug)
            Debug.Log($"✅ [CombatEnemy] 击退结束");

        if (navAgent != null)
        {
            navAgent.enabled = true;
            if (!navAgent.isOnNavMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
                    navAgent.Warp(hit.position);
            }
            navAgent.isStopped = false;
            navAgent.nextPosition = transform.position;
        }

        float dist = GetHorizontalDistanceToPlayer();
        if (dist > outerRadius)
            SwitchState(CombatEnemyState.Pressure);
        else if (dist > attackRange)
            SwitchState(CombatEnemyState.Probe);
        else
            SwitchState(CombatEnemyState.ResetSpacing);

        SetAnimatorTrigger("KnockbackDone");
    }

    /// <summary>
    /// 由外部（如 ColorInteractionManager）调用，触发眩晕状态。
    /// 传入 duration <= 0 时使用 Inspector 中配置的默认眩晕时长。
    /// </summary>
    public void Stun(float duration = -1f)
    {
        if (currentState == CombatEnemyState.Stunned) return;

        float actualDuration = duration > 0f ? duration : stunDuration;

        if (showStateDebug)
            Debug.Log($"😵 [CombatEnemy] 进入眩晕，时长: {actualDuration:F1}s");

        StopCurrentCoroutine();
        StopAllCoroutines();

        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        currentState = CombatEnemyState.Stunned;
        SetAnimatorTrigger("Stun");

        StartCoroutine(CoroutineStunned(actualDuration));
    }

    /// <summary>
    /// 颜色交互事件响应（override BaseEnemy）。
    /// 当敌人被玩家的同色攻击命中时，触发眩晕。
    /// </summary>
    public override void OnColorInteraction(ColorInteractionEvent interaction)
    {
        base.OnColorInteraction(interaction);

        if (interaction.Source == gameObject &&
            interaction.Type == ColorInteractionType.EnemyAttackPlayer)
        {
            if (showStateDebug)
                Debug.Log($"🎨 [CombatEnemy] 被玩家攻击，触发眩晕");
            Stun();
        }
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 状态切换与调试
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 统一状态切换入口：停止当前协程，更新状态枚举，并启动对应状态协程。
    /// 所有状态跳转必须经过此函数，禁止直接修改 currentState。
    /// </summary>
    private void SwitchState(CombatEnemyState newState)
    {
        StopCurrentCoroutine();
        CombatEnemyState oldState = currentState;
        currentState = newState;

        if (showStateDebug)
            LogState($"状态切换: {oldState} -> {newState}");

        switch (newState)
        {
            case CombatEnemyState.Pressure:
                currentStateCoroutine = StartCoroutine(CoroutinePressure());
                break;
            case CombatEnemyState.Probe:
                currentStateCoroutine = StartCoroutine(CoroutineProbe());
                break;
            case CombatEnemyState.CommitAttack:
                currentStateCoroutine = StartCoroutine(CoroutineCommitAttack());
                break;
            case CombatEnemyState.Attack:
                currentStateCoroutine = StartCoroutine(CoroutineAttack());
                break;
            case CombatEnemyState.Recover:
                currentStateCoroutine = StartCoroutine(CoroutineRecover());
                break;
            case CombatEnemyState.ResetSpacing:
                currentStateCoroutine = StartCoroutine(CoroutineResetSpacing());
                break;
        }
    }

    /// <summary>
    /// 安全停止当前正在运行的状态协程，并将引用置空。
    /// </summary>
    private void StopCurrentCoroutine()
    {
        if (currentStateCoroutine != null)
        {
            StopCoroutine(currentStateCoroutine);
            currentStateCoroutine = null;
        }
    }

    private string GetDebugPrefix()
    {
        return $"[CombatEnemy#{debugEnemyId:00}|{currentState}]";
    }

    private void LogState(string message)
    {
        if (!showStateDebug) return;
        Debug.Log($"{GetDebugPrefix()} {message}", this);
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 状态协程 - Pressure
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Pressure：远距离压迫接近阶段。
    /// 敌人持续向玩家逼近，满足距离条件后切换至 Probe；
    /// 若过于接近则直接跳入 CommitAttack。
    /// </summary>
    private IEnumerator CoroutinePressure()
    {
        stateEnterTime = Time.time;
        ResetDecisionTimer();

        navAgent.isStopped = false;
        navAgent.speed = pressureSpeed;
        SetAnimatorBool("isMoving", true);

        while (currentState == CombatEnemyState.Pressure)
        {
            UpdatePlayerBehaviorReadings();
            UpdateIntentScores();

            float dist = GetHorizontalDistanceToPlayer();
            UpdateFacingByState();

            // 极近距离：允许直接准备攻击
            if (dist <= commitCancelDistance)
            {
                SwitchState(CombatEnemyState.CommitAttack);
                yield break;
            }

            // 进入中距离且满足最短停留时间：切换至试探
            if (dist <= myPressureEnterProbeDistance && Time.time - stateEnterTime >= pressureMinDuration)
            {
                SwitchState(CombatEnemyState.Probe);
                yield break;
            }

            Vector3 target = CalculatePressureTarget();
            SetDestinationSafely(target);

            yield return null;
        }
    }

    /// <summary>
    /// 计算 Pressure 阶段的移动目标点。
    /// 综合朝向玩家的直线冲压方向与切线横移方向，生成带个体差异的接近路径。
    /// </summary>
    private Vector3 CalculatePressureTarget()
    {
        Vector3 dirToPlayer = (player.position - transform.position);
        dirToPlayer.y = 0f;

        if (dirToPlayer.sqrMagnitude < 0.001f)
            return transform.position;

        dirToPlayer.Normalize();

        Vector3 tangentDir = Vector3.Cross(Vector3.up, dirToPlayer).normalized;
        tangentDir *= preferredOrbitDirection;

        Vector3 moveDir = dirToPlayer * (1f - myPressureStrafeWeight) + tangentDir * myPressureStrafeWeight;
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude < 0.001f)
            return transform.position;

        moveDir.Normalize();

        Vector3 target = transform.position + moveDir * myPressureStepDistance;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 状态协程 - Probe
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Probe：中距离试探阶段。
    /// 敌人围绕玩家横移，定期评估意图分数决定是否发起 CommitAttack。
    /// 具有个体化的停顿节奏，模拟"等待时机"的行为。
    /// </summary>
    private IEnumerator CoroutineProbe()
    {
        stateEnterTime = Time.time;
        isProbeStopping = false;
        probeStopEndTime = -999f;
        nextProbeStopCheckTime = Time.time + Random.Range(myProbeStopCheckMin, myProbeStopCheckMax);
        ResetDecisionTimer();

        navAgent.isStopped = false;
        navAgent.speed = probeSpeed;
        SetAnimatorBool("isMoving", true);

        while (currentState == CombatEnemyState.Probe)
        {
            UpdatePlayerBehaviorReadings();
            UpdateIntentScores();

            float dist = GetHorizontalDistanceToPlayer();
            UpdateFacingByState();

            // 玩家拉开距离：重新进入 Pressure
            if (dist > outerRadius)
            {
                SwitchState(CombatEnemyState.Pressure);
                yield break;
            }

            // 定时意图决策
            if (Time.time >= nextDecisionTime)
            {
                if (Time.time - stateEnterTime >= myProbeMinDuration)
                {
                    float intentScore = GetProbeAttackIntentScore();
                    if (TryEnterCommitAttackFromIntent(intentScore))
                        yield break;
                }
                ResetDecisionTimer();
            }

            // 过近时也触发一次意图判断
            if (dist <= innerRadius && Time.time - stateEnterTime >= myProbeMinDuration)
            {
                float intentScore = GetProbeAttackIntentScore();
                if (TryEnterCommitAttackFromIntent(intentScore))
                    yield break;
            }

            // 处理个体化停顿行为
            if (isProbeStopping)
            {
                navAgent.isStopped = true;
                SetAnimatorBool("isMoving", false);

                if (Time.time >= probeStopEndTime)
                {
                    isProbeStopping = false;
                    navAgent.isStopped = false;
                    navAgent.speed = probeSpeed;
                    SetAnimatorBool("isMoving", true);
                    nextProbeStopCheckTime = Time.time + Random.Range(myProbeStopCheckMin, myProbeStopCheckMax);
                }

                yield return null;
                continue;
            }

            // 随机触发停顿
            if (Time.time >= nextProbeStopCheckTime)
            {
                if (Random.value <= myProbeStopChance)
                {
                    isProbeStopping = true;
                    probeStopEndTime = Time.time + myProbeStopDuration;
                    yield return null;
                    continue;
                }
                nextProbeStopCheckTime = Time.time + Random.Range(myProbeStopCheckMin, myProbeStopCheckMax);
            }

            Vector3 target = CalculateProbeTarget();
            SetDestinationSafely(target);

            yield return null;
        }
    }

    /// <summary>
    /// 计算 Probe 阶段的移动目标点。
    /// 以切线横移为主，同时根据与玩家的距离误差做径向微调，维持在偏好交战距离附近。
    /// </summary>
    private Vector3 CalculateProbeTarget()
    {
        Vector3 toPlayer = (player.position - transform.position);
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 0.001f)
            return transform.position;

        float dist = toPlayer.magnitude;
        Vector3 dirToPlayer = toPlayer.normalized;

        Vector3 tangentDir = Vector3.Cross(Vector3.up, dirToPlayer).normalized;
        tangentDir *= preferredOrbitDirection;

        float distanceError = myPreferredProbeDistance - dist;
        Vector3 radialAdjust = Vector3.zero;

        if (distanceError < -0.15f)
            radialAdjust = -dirToPlayer * Mathf.Clamp01(Mathf.Abs(distanceError));
        else if (distanceError > 0.15f)
            radialAdjust = dirToPlayer * Mathf.Clamp01(Mathf.Abs(distanceError));

        Vector3 moveDir = tangentDir * myProbeStrafeWeight + radialAdjust * (1f - myProbeStrafeWeight);
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude < 0.001f)
            moveDir = tangentDir;

        moveDir.Normalize();

        Vector3 target = transform.position + moveDir * myProbeStepDistance;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 状态协程 - CommitAttack
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// CommitAttack：攻击预警前压阶段。
    /// 敌人短暂向玩家前冲，持续 commitDuration 后进入正式 Attack。
    /// 此阶段给玩家留出反应窗口，同时确认敌人有足够距离发动攻击。
    /// </summary>
    private IEnumerator CoroutineCommitAttack()
    {
        stateEnterTime = Time.time;
        commitFxPlayed = false;
        navAgent.isStopped = false;
        navAgent.speed = pressureSpeed;
        SetAnimatorBool("isMoving", true);

        while (currentState == CombatEnemyState.CommitAttack)
        {
            float dist = GetHorizontalDistanceToPlayer();
            UpdateFacingByState();

            Vector3 target = CalculateCommitTarget();
            SetDestinationSafely(target);

            if (Time.time - stateEnterTime >= commitDuration)
            {
                SwitchState(CombatEnemyState.Attack);
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// 计算 CommitAttack 阶段的冲压目标点。
    /// 沿朝向玩家的方向前进 commitForwardStep，并做 NavMesh 采样修正。
    /// </summary>
    private Vector3 CalculateCommitTarget()
    {
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        commitTargetPosition = transform.position + dirToPlayer * commitForwardStep;

        if (NavMesh.SamplePosition(commitTargetPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            commitTargetPosition = hit.position;

        return commitTargetPosition;
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 状态协程 - Attack
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Attack：正式攻击阶段。
    /// 分两阶段执行：① 以 chargeSpeed 冲向玩家锁定位置；② 停止并播放攻击动画。
    /// 实际伤害由动画事件回调 PerformAttack() 触发。攻击完成后进入 Recover。
    /// </summary>
    private IEnumerator CoroutineAttack()
    {
        SetAnimatorBool("isMoving", true);
        navAgent.isStopped = false;
        navAgent.speed = chargeSpeed;

        if (showStateDebug)
            LogState("Attack阶段1：冲向玩家");

        // ── 阶段1：锁定目标位置，向玩家冲刺 ──
        Vector3 chargeTarget = player.position;
        chargeTarget.y = transform.position.y;

        if (showStateDebug)
            Debug.Log($"⚔️ [Attack] 阶段1：冲向锁定位置 {chargeTarget}");

        if (NavMesh.SamplePosition(chargeTarget, out NavMeshHit chargeHit, 2f, NavMesh.AllAreas))
            navAgent.SetDestination(chargeHit.position);
        else
            navAgent.SetDestination(chargeTarget);

        if (!commitFxPlayed)
        {
            PlayCommitFlashFx();
            commitFxPlayed = true;
        }

        while (navAgent.pathPending || navAgent.remainingDistance > attackRange)
        {
            // 实际距离已在攻击范围内：直接进入攻击
            if (GetHorizontalDistanceToPlayer() <= attackRange)
                break;

            // 已到达锁定点但玩家已离开：放弃
            if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.05f)
            {
                if (showStateDebug)
                    Debug.Log($"🏳️ [Attack] 已到达锁定位置但玩家已离开，放弃攻击 → Recover");
                SwitchState(CombatEnemyState.Recover);
                yield break;
            }

            // 玩家逃离过远：放弃追击
            if (GetHorizontalDistanceToPlayer() > giveUpChaseDistance)
            {
                if (showStateDebug)
                    Debug.Log($"🏳️ [Attack] 玩家逃离过远，放弃攻击 → Recover");
                SwitchState(CombatEnemyState.Recover);
                yield break;
            }

            UpdateFacingByState();
            yield return null;
        }

        // ── 阶段2：停止，执行攻击动画 ──
        if (showStateDebug)
            LogState("Attack阶段2：攻击");

        navAgent.isStopped = true;
        SetAnimatorBool("isMoving", false);
        FaceTarget(player.position);

        // 触发攻击动画；实际伤害在动画事件 PerformAttack() 中执行
        SetAnimatorTrigger("Attacking");
        yield return new WaitForSeconds(attackAnimDuration);
        SetAnimatorTrigger("AttackDone");

        if (showStateDebug)
            LogState("Attack完成 -> Recover");

        SwitchState(CombatEnemyState.Recover);
    }

    /// <summary>
    /// 动画事件回调：在攻击动画的命中帧触发实际伤害与颜色交互事件。
    /// 命中成功后消耗部分反击意图和对峙意图，重置攻击欲望。
    /// </summary>
    public void PerformAttack()
    {
        if (currentState != CombatEnemyState.Attack) return;
        if (player == null) return;

        if (showStateDebug)
            Debug.Log($"💥 [CombatEnemy] PerformAttack() 动画命中帧触发");

        bool hitSuccess = false;

        if (attackDetector != null)
        {
            hitSuccess = attackDetector.TryPerformAttack(gameObject, player);
        }
        else
        {
            if (showStateDebug)
                Debug.LogWarning($"⚠️ [CombatEnemy] 未找到 EnemyAttackDetector，退回直接攻击事件");

            ColorEventBus.PublishEnemyAttack(gameObject, player.gameObject);
            hitSuccess = true;
        }

        if (hitSuccess)
        {
            retaliationIntentScore = Mathf.Max(0f, retaliationIntentScore - retaliationConsumeOnAttack);
            standoffIntentScore    = Mathf.Max(0f, standoffIntentScore - 20f);
        }
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 状态协程 - Recover
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Recover：攻击后恢复阶段。
    /// 根据个体差异决定原地停顿或小幅后退，持续 myRecoverDuration 后重新评估距离进入下一状态。
    /// 同时设置 recoverSuppressionEndTime，在恢复期内压低攻击意图分数，避免连续出手。
    /// </summary>
    private IEnumerator CoroutineRecover()
    {
        recoverSuppressionEndTime = Time.time + myRecoverDuration + 0.2f;
        stateEnterTime = Time.time;
        PrepareRecoverMode();

        while (currentState == CombatEnemyState.Recover)
        {
            UpdateFacingByState();
            HandleRecoverMovement();

            if (Time.time - stateEnterTime >= myRecoverDuration)
            {
                navAgent.isStopped = false;

                float dist = GetHorizontalDistanceToPlayer();
                if (dist > outerRadius)
                    SwitchState(CombatEnemyState.Pressure);
                else if (dist > attackRange)
                    SwitchState(CombatEnemyState.Probe);
                else
                    SwitchState(CombatEnemyState.ResetSpacing);

                yield break;
            }

            yield return null;
        }
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 状态协程 - ResetSpacing
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// ResetSpacing：位置纠偏阶段。
    /// 当敌人与玩家距离过近（处于攻击范围内）时，后退至合适的交战距离。
    /// 不做意图判断，仅专注于距离修正。
    /// </summary>
    private IEnumerator CoroutineResetSpacing()
    {
        navAgent.isStopped = false;
        navAgent.speed = resetSpacingSpeed;
        SetAnimatorBool("isMoving", true);

        while (currentState == CombatEnemyState.ResetSpacing)
        {
            float dist = GetHorizontalDistanceToPlayer();
            UpdateFacingByState();

            if (dist > outerRadius)
            {
                SwitchState(CombatEnemyState.Pressure);
                yield break;
            }
            else if (dist > attackRange)
            {
                SwitchState(CombatEnemyState.Probe);
                yield break;
            }

            Vector3 target = CalculateResetSpacingTarget(dist);
            SetDestinationSafely(target);

            yield return null;
        }
    }

    /// <summary>
    /// 计算 ResetSpacing 阶段的目标点。
    /// 沿"远离玩家"方向后退至 myResetSpacingDistance，并做 NavMesh 采样修正。
    /// </summary>
    private Vector3 CalculateResetSpacingTarget(float currentDist)
    {
        Vector3 dirFromPlayer = (transform.position - player.position);
        dirFromPlayer.y = 0f;

        if (dirFromPlayer.sqrMagnitude < 0.001f)
            dirFromPlayer = transform.forward;

        dirFromPlayer.Normalize();

        float desiredDist = myResetSpacingDistance;
        Vector3 target = player.position + dirFromPlayer * desiredDist;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 状态协程 - Stunned
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Stunned：眩晕状态协程。
    /// 等待眩晕时长结束后恢复 NavMeshAgent，并根据当前距离重新进入战斗状态。
    /// </summary>
    private IEnumerator CoroutineStunned(float duration)
    {
        yield return new WaitForSeconds(duration);
        SetAnimatorTrigger("StunDone");

        if (showStateDebug)
            Debug.Log($"✅ [CombatEnemy] 眩晕结束，重新评估状态");

        if (navAgent != null)
            navAgent.isStopped = false;

        float dist = GetHorizontalDistanceToPlayer();
        if (dist > outerRadius)
            SwitchState(CombatEnemyState.Pressure);
        else if (dist > attackRange)
            SwitchState(CombatEnemyState.Probe);
        else
            SwitchState(CombatEnemyState.ResetSpacing);
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 战斗意图计算
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 重置下一次意图决策的触发时间，引入随机间隔避免所有敌人同步决策。
    /// </summary>
    private void ResetDecisionTimer()
    {
        nextDecisionTime = Time.time + Random.Range(decisionCheckIntervalMin, decisionCheckIntervalMax);
    }

    /// <summary>
    /// 每帧采样玩家位置变化，计算玩家速度向量与近距离停留时长。
    /// 这些数据用于修正意图分数（玩家接近加分、玩家远离减分）。
    /// </summary>
    private void UpdatePlayerBehaviorReadings()
    {
        if (player == null) return;

        Vector3 currentPlayerPos = player.position;
        playerVelocity = (currentPlayerPos - lastPlayerPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPlayerPosition = currentPlayerPos;

        float dist = GetHorizontalDistanceToPlayer();
        if (dist <= innerRadius + 0.8f)
            playerCloseStayTimer += Time.deltaTime;
        else
            playerCloseStayTimer = 0f;
    }

    /// <summary>
    /// 每帧更新对峙意图分（standoff）和反击意图分（retaliation）。
    /// 对峙分随时间自然升温；反击分随时间衰减；玩家主动后退时小幅降低对峙分。
    /// </summary>
    private void UpdateIntentScores()
    {
        if (player == null) return;

        // 1. 对峙升温：处于战斗状态时缓慢升温
        standoffIntentScore += standoffIntentGainPerSecond * Time.deltaTime;

        // 2. 玩家近距离停留：越贴近越该打破僵局
        if (playerCloseStayTimer > 0f)
            standoffIntentScore += closeRangeStayIntentPerSecond * Time.deltaTime;

        // 3. 玩家主动远离：降低进攻欲望
        Vector3 toEnemy = (transform.position - player.position);
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude > 0.001f)
        {
            Vector3 playerMoveDir = playerVelocity;
            playerMoveDir.y = 0f;
            if (playerMoveDir.sqrMagnitude > 0.001f)
            {
                playerMoveDir.Normalize();
                toEnemy.Normalize();
                float retreatDot = Vector3.Dot(playerMoveDir, -toEnemy);
                if (retreatDot > 0.6f)
                    standoffIntentScore -= playerRetreatIntentPenaltyPerSecond * Time.deltaTime;
            }
        }

        // 4. 反击分随时间衰减
        retaliationIntentScore -= retaliationDecayPerSecond * Time.deltaTime;
        retaliationIntentScore  = Mathf.Max(0f, retaliationIntentScore);

        // 5. Clamp，防止无限累积
        standoffIntentScore = Mathf.Clamp(standoffIntentScore, 0f, 100f);
    }

    /// <summary>
    /// 计算 Pressure 状态下的攻击意图总分。
    /// 综合对峙温度、距离系数、玩家接近行为、盲侧加成、反击分和群体压制惩罚。
    /// </summary>
    private float GetPressureAttackIntentScore()
    {
        float dist = GetHorizontalDistanceToPlayer();

        float distanceFactor = Mathf.InverseLerp(outerRadius + 2f, innerRadius + 1.2f, dist);
        float score = standoffIntentScore;
        score += distanceFactor * pressureDistanceIntentWeight;

        // 玩家朝敌人接近时加分
        Vector3 toEnemy = (transform.position - player.position);
        toEnemy.y = 0f;
        Vector3 moveDir = playerVelocity;
        moveDir.y = 0f;

        float playerInputBonus = 0f;
        if (toEnemy.sqrMagnitude > 0.001f && moveDir.sqrMagnitude > 0.001f)
        {
            toEnemy.Normalize();
            moveDir.Normalize();
            float approachDot = Vector3.Dot(moveDir, toEnemy);
            if (approachDot > 0.6f && playerVelocity.magnitude >= playerApproachSpeedThreshold)
                playerInputBonus += playerApproachIntentBonus;
        }

        // aggressionBias 放大玩家输入带来的压迫感
        score += playerInputBonus * (1f + aggressionBias * aggressionPlayerInputWeight);
        score += GetBlindSideIntentBonus();
        score += retaliationIntentScore;
        score += aggressionBias * aggressionPressureWeight;

        if (Time.time < recoverSuppressionEndTime)
            score -= 18f;

        score -= GetGroupAttackPressurePenalty();
        return score;
    }

    /// <summary>
    /// 计算 Probe 状态下的攻击意图总分。
    /// 在 Pressure 意图分基础上，额外考虑 Probe 持续时间（越久越急迫）和个体侵略性偏差。
    /// </summary>
    private float GetProbeAttackIntentScore()
    {
        float dist = GetHorizontalDistanceToPlayer();

        float distanceFactor = Mathf.InverseLerp(outerRadius, attackRange + 0.6f, dist);
        float score = standoffIntentScore;
        score += distanceFactor * probeDistanceIntentWeight;

        // Probe 持续时间越久，主动出手意愿越强
        score += Mathf.Clamp((Time.time - stateEnterTime) * 8f, 0f, 24f);

        score += GetBlindSideIntentBonus();
        score += retaliationIntentScore;
        score += aggressionBias * aggressionProbeWeight;

        if (Time.time < recoverSuppressionEndTime)
            score -= 18f;

        score -= GetGroupAttackPressurePenalty();
        return score;
    }

    /// <summary>
    /// 计算盲侧加成：若敌人位于玩家视野背面或侧面，给予额外意图加分。
    /// 利用玩家 forward 与"玩家→敌人"方向的点积判断是否处于盲侧。
    /// </summary>
    private float GetBlindSideIntentBonus()
    {
        if (player == null) return 0f;

        Vector3 playerForward = player.forward;
        playerForward.y = 0f;
        Vector3 toEnemy = (transform.position - player.position);
        toEnemy.y = 0f;

        if (playerForward.sqrMagnitude < 0.001f || toEnemy.sqrMagnitude < 0.001f)
            return 0f;

        playerForward.Normalize();
        toEnemy.Normalize();

        float dot = Vector3.Dot(playerForward, toEnemy);

        // dot 越低，表示敌人越不在玩家正面
        if (dot < playerBlindSideDotThreshold)
            return playerBlindSideIntentBonus;

        return 0f;
    }

    /// <summary>
    /// 根据意图分数判断是否应进入 CommitAttack。
    /// 同时检查群体攻击许可，避免多敌同时发动攻击。
    /// </summary>
    private bool TryEnterCommitAttackFromIntent(float score)
    {
        if (score < commitAttackIntentThreshold)
            return false;

        if (!CanAttemptCommitAttack())
            return false;

        SwitchState(CombatEnemyState.CommitAttack);
        return true;
    }

    /// <summary>
    /// 统计附近处于 CommitAttack 或 Attack 状态的其他敌人数量，用于群体攻击协调。
    /// </summary>
    private int CountNearbyEnemiesInAttackStates()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, groupAttackCheckRadius);
        int count = 0;

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            CombatEnemy other = hit.GetComponentInParent<CombatEnemy>();
            if (other == null || other == this) continue;

            if (other.currentState == CombatEnemyState.CommitAttack ||
                other.currentState == CombatEnemyState.Attack)
                count++;
        }

        return count;
    }

    /// <summary>
    /// 判断当前敌人是否允许发动攻击。
    /// 若附近已有足够多的攻击者，仅保留极小概率"抢招"，避免行为过于死板。
    /// </summary>
    private bool CanAttemptCommitAttack()
    {
        int nearbyAttackers = CountNearbyEnemiesInAttackStates();

        if (nearbyAttackers < maxNearbyAttackers)
            return true;

        // 极低概率允许"抢招"，打破规律感
        if (Random.value <= rareOverrideAttackChance)
            return true;

        return false;
    }

    /// <summary>
    /// 获取群体攻击压制惩罚值：附近已有攻击者时，对当前敌人的意图分额外扣分。
    /// </summary>
    private float GetGroupAttackPressurePenalty()
    {
        int nearbyAttackers = CountNearbyEnemiesInAttackStates();

        if (nearbyAttackers < maxNearbyAttackers)
            return 0f;

        return groupAttackIntentPenalty;
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 恢复与移动辅助
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 进入 Recover 状态时调用，根据个体概率决定本次采用"停顿"还是"后退"模式，
    /// 并预先计算后退目标点。
    /// </summary>
    private void PrepareRecoverMode()
    {
        if (Random.value <= myRecoverStepBackChance)
        {
            currentRecoverMode = RecoverMode.StepBack;

            Vector3 awayFromPlayer = (transform.position - player.position);
            awayFromPlayer.y = 0f;

            if (awayFromPlayer.sqrMagnitude < 0.001f)
                awayFromPlayer = -transform.forward;

            awayFromPlayer.Normalize();
            recoverTargetPosition = transform.position + awayFromPlayer * myRecoverStepBackDistance;

            if (NavMesh.SamplePosition(recoverTargetPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                recoverTargetPosition = hit.position;
        }
        else
        {
            currentRecoverMode = RecoverMode.Pause;
        }
    }

    /// <summary>
    /// Recover 状态每帧调用，根据当前 RecoverMode 决定原地等待还是驱动 NavAgent 后退。
    /// </summary>
    private void HandleRecoverMovement()
    {
        if (currentRecoverMode == RecoverMode.Pause)
        {
            navAgent.isStopped = true;
            SetAnimatorBool("isMoving", false);
            return;
        }

        if (currentRecoverMode == RecoverMode.StepBack)
        {
            navAgent.isStopped = false;
            navAgent.speed = recoverMoveSpeed;
            SetAnimatorBool("isMoving", true);

            if (Vector3.Distance(navAgent.destination, recoverTargetPosition) > recoverRepathDistance)
                SetDestinationSafely(recoverTargetPosition);
        }
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 动画与表现辅助
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 在 Attack 阶段冲刺前播放 CommitFlash 特效，给玩家攻击预警视觉提示。
    /// 特效会跟随敌人根节点，并在 1 秒后自动销毁。
    /// </summary>
    private void PlayCommitFlashFx()
    {
        if (commitFlashFx == null) return;

        Transform spawnRef = commitFlashSpawnPoint != null ? commitFlashSpawnPoint : transform;
        GameObject fx = Instantiate(commitFlashFx, spawnRef.position, spawnRef.rotation);
        fx.transform.SetParent(transform);
        Destroy(fx, 1f);
    }

    /// <summary>
    /// 根据当前状态决定是否驱动朝向更新。
    /// 只在主动交战状态（Pressure / Probe / CommitAttack / Attack / Recover / ResetSpacing）下保持面向玩家。
    /// </summary>
    private void UpdateFacingByState()
    {
        if (player == null) return;

        switch (currentState)
        {
            case CombatEnemyState.Pressure:
            case CombatEnemyState.Probe:
            case CombatEnemyState.CommitAttack:
            case CombatEnemyState.Attack:
            case CombatEnemyState.Recover:
            case CombatEnemyState.ResetSpacing:
                FacePlayerHorizontally();
                break;
        }
    }

    /// <summary>
    /// 平滑转向玩家（忽略 Y 轴），供移动状态帧更新使用。
    /// </summary>
    private void FacePlayerHorizontally()
    {
        if (player == null) return;

        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0f;
        if (lookDir == Vector3.zero) return;

        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 立即朝向目标点（忽略 Y 轴），用于攻击命中瞬间的精确对齐。
    /// </summary>
    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private void SetAnimatorTrigger(string triggerName)
    {
        if (animator != null)
            animator.SetTrigger(triggerName);
    }

    private void SetAnimatorBool(string paramName, bool value)
    {
        if (animator != null)
            animator.SetBool(paramName, value);
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 通用工具函数
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取与玩家的水平距离（忽略 Y 轴）。
    /// 若玩家引用为空，返回 float.MaxValue 以确保所有距离判断安全退出。
    /// </summary>
    private float GetHorizontalDistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        Vector3 selfFlat   = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerFlat = new Vector3(player.position.x, 0f, player.position.z);
        return Vector3.Distance(selfFlat, playerFlat);
    }

    /// <summary>
    /// 在以 center 为圆心的环形区域 [minR, maxR] 内随机取一个点（保留，暂未使用）。
    /// </summary>
    private Vector3 GetRandomPointInRing(Vector3 center, float minR, float maxR)
    {
        float angle  = Random.Range(0f, Mathf.PI * 2f);
        float radius = Random.Range(minR, maxR);
        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        Vector3 target = center + offset;
        target.y = center.y;
        return target;
    }

    /// <summary>
    /// 安全地为 NavMeshAgent 设置目标点。
    /// 对 rawTarget 做 NavMesh 采样修正；若采样失败则原地停留，避免 Agent 错误。
    /// </summary>
    private void SetDestinationSafely(Vector3 rawTarget)
    {
        if (navAgent == null || !navAgent.isActiveAndEnabled) return;

        if (NavMesh.SamplePosition(rawTarget, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            navAgent.SetDestination(hit.position);
        else
            navAgent.SetDestination(transform.position);
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 公共接口
    // ══════════════════════════════════════════════════════════════

    /// <summary>获取当前状态（供外部查询）</summary>
    public CombatEnemyState GetCurrentState() => currentState;

    /// <summary>运行时修改内环半径</summary>
    public void SetInnerRadius(float r) => innerRadius = Mathf.Max(0.5f, r);

    /// <summary>运行时修改外环半径</summary>
    public void SetOuterRadius(float r) => outerRadius = Mathf.Max(innerRadius + 1f, r);

    #endregion
}