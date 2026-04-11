using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 近战小怪行为逻辑：环形区域漫游 → 突进攻击 → 后退 ↔ 位置调整
/// 状态：Roam（漫游）/ Reposition（归位）/ Attack（攻击）/ Stunned（眩晕）/ Knockback（击退）
/// </summary>
public class CombatEnemy : BaseEnemy, IKnockbackReceiver
{
    #region 状态枚举
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

    #region 环形区域设置
    [Header("环形区域设置")]
    [SerializeField] private float innerRadius = 3f;           // 内环半径
    [SerializeField] private float outerRadius = 7f;           // 外环半径
    #endregion

    #region 移动设置
    [Header("移动设置")]
    [SerializeField] private float chargeSpeed = 7f;           // 突进速度
    [SerializeField] private float rotationSpeed = 8f;         // 旋转速度
    [SerializeField] private float repositionTargetMargin = 0.5f;  // 归位目标距玩家的额外安全边距
    #endregion

    #region 攻击设置
    [Header("攻击设置")]
    [SerializeField] private float attackRange = 1.5f;         // 近战触发攻击的距离
    [SerializeField] private float attackAnimDuration = 0.5f;  // 攻击动画等待时长（秒）
    [SerializeField] private float retreatMargin = 1f;         // 后退目标 = innerRadius + retreatMargin
    [SerializeField] private float giveUpChaseDistance = 20f;  // 追击放弃距离（超出则放弃本次攻击）
    #endregion
    [Header("状态持续时间设置")]
    [SerializeField] private float pressureMinDuration = 0.8f;
    [SerializeField] private float probeMinDuration = 1.2f;
    [SerializeField] private float commitDuration = 0.25f;
    [SerializeField] private float recoverDuration = 0.5f;
    [Header("重置位置设置")]
    [SerializeField] private float recoverStepBackDistance = 1.2f;
    [SerializeField] [Range(0f, 1f)] private float recoverStepBackChance = 0.6f;
    [SerializeField] private float recoverRepathDistance = 0.2f;
    [Header("战斗移动设置")]
    [SerializeField] private float pressureSpeed = 2.8f;
    [SerializeField] private float probeSpeed = 1.8f;
    [SerializeField] private float recoverMoveSpeed = 2.0f;
    [SerializeField] private float resetSpacingSpeed = 3.5f;

    [Header("准备攻击设置")]
    [SerializeField] private GameObject commitFlashFx;
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
    [Header("试探设置")]
    [SerializeField] private float probeStrafeWeight = 0.65f;
    [SerializeField] private float pressureStrafeWeight = 0.3f;
    [SerializeField] private float probeStopChance = 0.5f;
    [SerializeField] private float probeStopDuration = 1.0f;
    #region 眩晕设置
    [Header("眩晕设置")]
    [SerializeField] private float stunDuration = 2f;          // 眩晕持续时长（秒）
    #endregion

    #region 调试设置
    [Header("调试设置")]
    [SerializeField] private bool showStateDebug = true;
    #endregion

    #region 内部变量
    private EnemyAttackDetector attackDetector;
    private float stateEnterTime = 0f;
    private float nextDecisionTime = 0f;
    private bool commitFxPlayed = false;
    private Vector3 commitTargetPosition;
    private RecoverMode currentRecoverMode = RecoverMode.Pause;
    private Vector3 recoverTargetPosition;
    private static int globalDebugIdCounter = 0;
    private int debugEnemyId;
    private CombatEnemyState currentState;
    private Transform player;
    private NavMeshAgent navAgent;
    private KnockbackSystem knockbackSystem;
    private Animator animator;
    private float standoffIntentScore = 0f;
    private float retaliationIntentScore = 0f;
    private float recoverSuppressionEndTime = -999f;
    private bool isProbeStopping = false;
    private float probeStopEndTime = -999f;
    private float nextProbeStopCheckTime = -999f;
    private float strafeBias = 0f;
    private float recoverBias = 0f;

    // 每只敌人的个体化 Recover 参数
    private float myRecoverDuration = 0f;
    private float myRecoverStepBackChance = 0f;
    private float myRecoverStepBackDistance = 0f;

    // 每只敌人的个体化横移参数
    private float myPressureStrafeWeight = 0f;
    private float myProbeStrafeWeight = 0f;
    private float myPressureStepDistance = 0f;
    private float myProbeStepDistance = 0f;
    // 玩家行为采样
    private Vector3 lastPlayerPosition;
    private Vector3 playerVelocity;
    private float playerCloseStayTimer = 0f;
    // 状态协程管理
    private Coroutine currentStateCoroutine;
    private float spacingBias = 0f;
    // 每只敌人的个体化交战距离
    private float myPressureEnterProbeDistance = 0f;
    private float myPreferredProbeDistance = 0f;
    private float myResetSpacingDistance = 0f;
    // 包围意识变量（保留个体差异部分）
    private float personalAngleOffset;      // 个体角度偏好，避免所有敌人站位整齐
    private int preferredOrbitDirection;    // 个体绕圈方向偏好（+1 或 -1，已不再驱动持续切线）
    private float personalRadiusOffset;     // 个体对包围半径的微偏移
    private float aggressionBias = 0f;      // 个体攻击倾向偏差
    private float patienceBias = 0f;
    // Probe 个体化节奏参数
    private float myProbeMinDuration = 0f;
    private float myProbeStopChance = 0f;
    private float myProbeStopDuration = 0f;
    private float myProbeStopCheckMin = 0f;
    private float myProbeStopCheckMax = 0f;
    #endregion

    #region Unity生命周期
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

    #region 初始化
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

    /// <summary>初始化每只敌人的包围意识个体差异</summary>
    private void InitializeEncirclementPersonality()
    {
        personalAngleOffset = Random.Range(-45f, 45f);
        preferredOrbitDirection = Random.value < 0.5f ? -1 : 1;
        personalRadiusOffset = Random.Range(-1.2f, 1.2f);

        aggressionBias = Random.Range(aggressionBiasMin, aggressionBiasMax);
        patienceBias = Random.Range(patienceBiasMin, patienceBiasMax);
        spacingBias = Random.Range(spacingBiasMin, spacingBiasMax);
        strafeBias = Random.Range(strafeBiasMin, strafeBiasMax);
        recoverBias = Random.Range(recoverBiasMin, recoverBiasMax);

        // Probe 个体节奏
        myProbeMinDuration = Mathf.Max(0.25f, probeMinDuration + patienceBias * patienceProbeDurationWeight);
        myProbeStopChance = Mathf.Clamp01(probeStopChance + patienceBias * patienceStopChanceWeight);
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
        myProbeStrafeWeight = Mathf.Clamp01(probeStrafeWeight + strafeBias * strafeProbeWeight);

        myPressureStepDistance = Mathf.Max(0.8f, 1.6f + strafeBias * strafePressureStepWeight);
        myProbeStepDistance = Mathf.Max(0.5f, 1.0f + strafeBias * strafeProbeStepWeight);

        // Recover 个体化
        myRecoverDuration = Mathf.Max(0.15f, recoverDuration + recoverBias * recoverDurationWeight);
        myRecoverStepBackChance = Mathf.Clamp01(recoverStepBackChance + recoverBias * recoverStepBackChanceWeight);
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

    #region IKnockbackReceiver 实现
    public void OnKnockbackStart()
    {
        retaliationIntentScore += retaliationGainOnPlayerHit;
        retaliationIntentScore = Mathf.Clamp(retaliationIntentScore, 0f, 100f);
        if (showStateDebug)
            Debug.Log($"💨 [CombatEnemy] 击退开始");

        // 停止所有状态协程，进入Knockback状态
        StopCurrentCoroutine();
        currentState = CombatEnemyState.Knockback;
        // NavMeshAgent由KnockbackSystem通过isStopped控制，此处仅做保险
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        SetAnimatorTrigger("Knockback");
    }

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
    #endregion

    #region 状态机切换
    /// <summary>
    /// 统一状态切换入口：停止当前协程，更新状态并启动新协程。
    /// preservedTimer > 0 时表示希望在进入Roam时沿用该剩余计时值。
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

    #region 状态协程 — Pressure
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

            // 先保证中距离能真正进入 Probe
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

    #region 状态协程 — Probe
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

            if (dist > outerRadius)
            {
                SwitchState(CombatEnemyState.Pressure);
                yield break;
            }

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

            if (dist <= innerRadius && Time.time - stateEnterTime >= myProbeMinDuration)
            {
                float intentScore = GetProbeAttackIntentScore();
                if (TryEnterCommitAttackFromIntent(intentScore))
                    yield break;
            }

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
        {
            radialAdjust = -dirToPlayer * Mathf.Clamp01(Mathf.Abs(distanceError));
        }
        else if (distanceError > 0.15f)
        {
            radialAdjust = dirToPlayer * Mathf.Clamp01(Mathf.Abs(distanceError));
        }

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
    
    #region 状态协程 — CommitAttack
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

            if (!commitFxPlayed)
            {
                PlayCommitFlashFx();
                commitFxPlayed = true;
            }

            float commitElapsed = Time.time - stateEnterTime;

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
    private void PlayCommitFlashFx()
    {
        if (commitFlashFx != null)
        {
            GameObject fx = Instantiate(commitFlashFx, transform.position, Quaternion.identity);
            fx.transform.SetParent(transform);
            Destroy(fx, 1.5f);
        }
    }
    private Vector3 CalculateCommitTarget()
    {
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        commitTargetPosition = transform.position + dirToPlayer * commitForwardStep;

        if (NavMesh.SamplePosition(commitTargetPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            commitTargetPosition = hit.position;
        return commitTargetPosition;
    }
    #endregion
    #region 状态协程 — ResetSpacing
    
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
    private void SetDestinationSafely(Vector3 rawTarget)
    {
        if (navAgent == null || !navAgent.isActiveAndEnabled) return;

        if (NavMesh.SamplePosition(rawTarget, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
        }
        else
        {
            navAgent.SetDestination(transform.position);
        }
    }
    #endregion

    #region 状态协程 — Attack
    private IEnumerator CoroutineAttack()
    {
        SetAnimatorBool("isMoving", true);
        navAgent.isStopped = false;
        navAgent.speed = chargeSpeed;

        if (showStateDebug)
            LogState("Attack阶段1：冲向玩家");

        // ── 阶段1：冲向玩家当前位置（记录一次，不持续追踪）──
        Vector3 chargeTarget = player.position;
        chargeTarget.y = transform.position.y; // 保持水平冲刺
 
        if (showStateDebug)
            Debug.Log($"⚔️ [Attack] 阶段1：冲向锁定位置 {chargeTarget}");
 
        if (NavMesh.SamplePosition(chargeTarget, out NavMeshHit chargeHit, 2f, NavMesh.AllAreas))
            navAgent.SetDestination(chargeHit.position);
        else
            navAgent.SetDestination(chargeTarget);
 
        while (navAgent.pathPending || navAgent.remainingDistance > attackRange)
        {
            // 到达锁定点后仍未命中：检查与玩家实际距离，若在攻击范围内则直接进入攻击
            if (GetHorizontalDistanceToPlayer() <= attackRange)
                break;
 
            // 锁定点已到达但玩家已不在攻击范围（玩家跑走了）→ 放弃
            if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.05f)
            {
                if (showStateDebug)
                    Debug.Log($"🏳️ [Attack] 已到达锁定位置但玩家已离开，放弃攻击 → Roam（重置计时）");
                SwitchState(CombatEnemyState.Recover);
                yield break;
            }
 
            // 冲刺途中距离超出放弃阈值 → 放弃
            if (GetHorizontalDistanceToPlayer() > giveUpChaseDistance)
            {
                if (showStateDebug)
                    Debug.Log($"🏳️ [Attack] 玩家逃离过远，放弃攻击 → Roam（重置计时）");
                SwitchState(CombatEnemyState.Recover);
                yield break;
            }

            UpdateFacingByState();

            yield return null;
        }

        // ── 阶段2：停止，执行攻击 ──
        if (showStateDebug)
            LogState("Attack阶段2：攻击");

        navAgent.isStopped = true;
        SetAnimatorBool("isMoving", false);

        // 面向玩家
        FaceTarget(player.position);

        // 触发攻击动画，等待动画窗口（实际伤害在动画事件PerformAttack中执行）
        SetAnimatorTrigger("Attacking");
        yield return new WaitForSeconds(attackAnimDuration);
        SetAnimatorTrigger("AttackDone");
        // 攻击全流程完成，回到 Recover 并强制重置计时器
        if (showStateDebug)
            LogState("Attack完成 -> Recover");

        SwitchState(CombatEnemyState.Recover);
    }

    /// <summary>
    /// 动画事件调用 — 在攻击动画命中帧触发实际伤害/交互事件
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
            // 没挂 EnemyAttackDetector 时，退回到直接事件发布
            if (showStateDebug)
                Debug.LogWarning($"⚠️ [CombatEnemy] 未找到 EnemyAttackDetector，退回直接攻击事件");

            ColorEventBus.PublishEnemyAttack(gameObject, player.gameObject);
            hitSuccess = true;
        }

        if (hitSuccess)
        {
            retaliationIntentScore = Mathf.Max(0f, retaliationIntentScore - retaliationConsumeOnAttack);
            standoffIntentScore = Mathf.Max(0f, standoffIntentScore - 20f);
        }
    }
    #endregion
    #region 状态协程 — Recover
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
            {
                SetDestinationSafely(recoverTargetPosition);
            }
        }
    }
    #endregion

    #region 眩晕
    /// <summary>
    /// 由外部（ColorInteractionManager等）检测到同色攻击时调用，触发眩晕状态。
    /// </summary>
    public void Stun(float duration = -1f)
    {
        if (currentState == CombatEnemyState.Stunned) return;

        float actualDuration = duration > 0f ? duration : stunDuration;

        if (showStateDebug)
            Debug.Log($"😵 [CombatEnemy] 进入眩晕，时长: {actualDuration:F1}s");

        // 停止所有协程，接管控制
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

    private IEnumerator CoroutineStunned(float duration)
    {
        yield return new WaitForSeconds(duration);
        SetAnimatorTrigger("StunDone");
        if (showStateDebug)
            Debug.Log($"✅ [CombatEnemy] 眩晕结束，重新评估状态");

        // 恢复NavMeshAgent
        if (navAgent != null)
        {
            navAgent.isStopped = false;
        }

        // 眩晕结束后强制重置计时器
        //resetTimerOnNextRoam = true;

        float dist = GetHorizontalDistanceToPlayer();
        if (dist > outerRadius)
            SwitchState(CombatEnemyState.Pressure);
        else if (dist > attackRange)
            SwitchState(CombatEnemyState.Probe);
        else
            SwitchState(CombatEnemyState.ResetSpacing);
    }
    #endregion

    #region 事件处理
    public override void OnColorInteraction(ColorInteractionEvent interaction)
    {
        base.OnColorInteraction(interaction);

        // 同色攻击命中时触发眩晕
        if (interaction.Source == gameObject &&
            interaction.Type == ColorInteractionType.EnemyAttackPlayer)
        {
            // 颜色匹配判断由ColorInteractionManager负责，
            // 此处仅响应已通过颜色校验的同色交互并触发眩晕
            // 若需区分同色/异色，可在此添加颜色比对逻辑
            if (showStateDebug)
                Debug.Log($"🎨 [CombatEnemy] 被玩家攻击，触发眩晕");
            Stun();
        }
    }
    #endregion

    #region 辅助方法
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
            {
                count++;
            }
        }

        return count;
    }
    private bool CanAttemptCommitAttack()
    {
        int nearbyAttackers = CountNearbyEnemiesInAttackStates();

        if (nearbyAttackers < maxNearbyAttackers)
            return true;

        // 极低概率允许一次“抢招”，避免过于死板
        if (Random.value <= rareOverrideAttackChance)
            return true;

        return false;
    }
    private float GetGroupAttackPressurePenalty()
    {
        int nearbyAttackers = CountNearbyEnemiesInAttackStates();

        if (nearbyAttackers < maxNearbyAttackers)
            return 0f;

        return groupAttackIntentPenalty;
    }
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
    /// <summary>获取与玩家的水平距离（忽略Y轴）</summary>
    private float GetHorizontalDistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        Vector3 selfFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerFlat = new Vector3(player.position.x, 0f, player.position.z);
        return Vector3.Distance(selfFlat, playerFlat);
    }

    /// <summary>在以center为圆心的环形区域 [minR, maxR] 内随机取一个点</summary>
    private Vector3 GetRandomPointInRing(Vector3 center, float minR, float maxR)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Random.Range(minR, maxR);
        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        Vector3 target = center + offset;
        target.y = center.y;
        return target;
    }

    /// <summary>平滑转向目标点（忽略Y轴）</summary>
    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }
  
    /// <summary>平滑转向玩家（忽略Y轴），供 Roam/Reposition 使用</summary>
    private void FacePlayerHorizontally()
    {
        if (player == null) return;

        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0f;
        if (lookDir == Vector3.zero) return;

        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }
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
    private void ResetDecisionTimer()
    {
        nextDecisionTime = Time.time + Random.Range(decisionCheckIntervalMin, decisionCheckIntervalMax);
    }
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
    private void UpdateIntentScores()
    {
        if (player == null) return;

        // 1. 对峙升温：敌人只要还在战斗状态里，就缓慢升温
        standoffIntentScore += standoffIntentGainPerSecond * Time.deltaTime;

        // 2. 玩家近距离停留：越贴近越该打破僵局
        if (playerCloseStayTimer > 0f)
            standoffIntentScore += closeRangeStayIntentPerSecond * Time.deltaTime;

        // 3. 玩家主动远离：降低一点进攻欲望
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
        retaliationIntentScore = Mathf.Max(0f, retaliationIntentScore);

        // 5. Clamp，防止无穷上涨
        standoffIntentScore = Mathf.Clamp(standoffIntentScore, 0f, 100f);
    }
    private float GetPressureAttackIntentScore()
    {
        float dist = GetHorizontalDistanceToPlayer();

        float distanceFactor = Mathf.InverseLerp(outerRadius + 2f, innerRadius + 1.2f, dist);
        float score = standoffIntentScore;
        score += distanceFactor * pressureDistanceIntentWeight;

        // 玩家朝敌人接近
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

        // aggressionBias 会放大玩家输入带来的压迫感
        score += playerInputBonus * (1f + aggressionBias * aggressionPlayerInputWeight);

        score += GetBlindSideIntentBonus();
        score += retaliationIntentScore;

        // aggressionBias 本身也直接抬高 Pressure 中的主动出手倾向
        score += aggressionBias * aggressionPressureWeight;

        if (Time.time < recoverSuppressionEndTime)
            score -= 18f;
        // 同伴已在准备/攻击时，降低当前敌人的主动出手倾向
        score -= GetGroupAttackPressurePenalty();
        return score;
    }
    private float GetProbeAttackIntentScore()
    {
        float dist = GetHorizontalDistanceToPlayer();

        float distanceFactor = Mathf.InverseLerp(outerRadius, attackRange + 0.6f, dist);
        float score = standoffIntentScore;
        score += distanceFactor * probeDistanceIntentWeight;

        // Probe 持续时间越久，越该主动打
        score += Mathf.Clamp((Time.time - stateEnterTime) * 8f, 0f, 24f);

        score += GetBlindSideIntentBonus();
        score += retaliationIntentScore;

        // aggressionBias 在 Probe 中影响更明显
        score += aggressionBias * aggressionProbeWeight;

        if (Time.time < recoverSuppressionEndTime)
            score -= 18f;
        // 同伴已在准备/攻击时，降低当前敌人的主动出手倾向
        score -= GetGroupAttackPressurePenalty();
        return score;
    }
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

        // dot 越低，说明敌人越不在玩家正面视野范围
        if (dot < playerBlindSideDotThreshold)
            return playerBlindSideIntentBonus;

        return 0f;
    }
    private bool TryEnterCommitAttackFromIntent(float score)
    {
        if (score < commitAttackIntentThreshold)
            return false;

        if (!CanAttemptCommitAttack())
            return false;

        SwitchState(CombatEnemyState.CommitAttack);
        return true;
    }
    // ─── 动画辅助 ───
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

    #region 公共接口
    /// <summary>获取当前状态（供外部查询）</summary>
    public CombatEnemyState GetCurrentState() => currentState;

    /// <summary>运行时修改内环半径</summary>
    public void SetInnerRadius(float r) => innerRadius = Mathf.Max(0.5f, r);

    /// <summary>运行时修改外环半径</summary>
    public void SetOuterRadius(float r) => outerRadius = Mathf.Max(innerRadius + 1f, r);

    /// <summary>运行时修改攻击延迟范围</summary>
    #endregion
}