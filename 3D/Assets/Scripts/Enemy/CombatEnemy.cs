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
        Roam,        // 环形区域内漫游，维护攻击倒计时
        Reposition,  // 调整距离，回归合法环形区域
        Attack,      // 突进攻击 → 攻击动画 → 后退
        Stunned,     // 眩晕（被同色攻击触发）
        Knockback    // 击退（由KnockbackSystem接管移动）
    }
    #endregion

    #region 环形区域设置
    [Header("环形区域设置")]
    [SerializeField] private float innerRadius = 3f;           // 内环半径
    [SerializeField] private float outerRadius = 7f;           // 外环半径
    #endregion

    #region 移动设置
    [Header("移动设置")]
    [SerializeField] private float roamSpeed = 2f;             // 漫游速度
    [SerializeField] private float repositionSpeed = 4f;       // 归位速度
    [SerializeField] private float chargeSpeed = 7f;           // 突进速度
    [SerializeField] private float rotationSpeed = 8f;         // 旋转速度
    [SerializeField] private float roamTargetUpdateInterval = 2f;  // 漫游目标刷新间隔（秒）
    [SerializeField] private float repositionCheckInterval = 0.5f; // 归位距离检查间隔（秒）
    [SerializeField] private float repositionTargetMargin = 0.5f;  // 归位目标距玩家的额外安全边距
    #endregion

    #region 攻击设置
    [Header("攻击设置")]
    [SerializeField] private float minAttackDelay = 2f;        // 攻击倒计时最小值（秒）
    [SerializeField] private float maxAttackDelay = 5f;        // 攻击倒计时最大值（秒）
    [SerializeField] private float attackRange = 1.5f;         // 近战触发攻击的距离
    [SerializeField] private float attackAnimDuration = 0.5f;  // 攻击动画等待时长（秒）
    [SerializeField] private float retreatMargin = 1f;         // 后退目标 = innerRadius + retreatMargin
    [SerializeField] private float giveUpChaseDistance = 20f;  // 追击放弃距离（超出则放弃本次攻击）
    #endregion

    #region 眩晕设置
    [Header("眩晕设置")]
    [SerializeField] private float stunDuration = 2f;          // 眩晕持续时长（秒）
    #endregion

    #region 归位随机延迟
    [Header("归位随机延迟")]
    [SerializeField] private float repositionRandomDelayMin = 0f;   // 归位进入前随机延迟最小值
    [SerializeField] private float repositionRandomDelayMax = 0.3f; // 归位进入前随机延迟最大值
    #endregion

    #region 包围意识设置
    [Header("包围意识设置")]
    [SerializeField] private bool enableEncirclement = true;              // 是否启用包围槽位逻辑
    [SerializeField] private float encircleRadiusRatio = 0.65f;           // 包围点在内外环之间的比例位置
    [SerializeField] private float slotAngleJitter = 15f;                 // 包围角度的随机抖动（度）
    [SerializeField] private float slotRepathInterval = 1.2f;             // 包围目标点刷新间隔（秒）
    [SerializeField] private float allyAvoidanceRadius = 1.8f;            // 同伴排斥检测半径
    [SerializeField] private float allyAvoidanceStrength = 1.2f;          // 同伴排斥推力强度
    [SerializeField] private float attackPermissionCheckInterval = 0.25f; // 攻击许可检查间隔（秒）
    #endregion

    #region 调试设置
    [Header("调试设置")]
    [SerializeField] private bool showStateDebug = true;
    [SerializeField] private bool showRangeGizmos = true;
    [SerializeField] private Color innerRangeColor = new Color(1f, 1f, 0f, 0.15f);
    [SerializeField] private Color outerRangeColor = new Color(1f, 0f, 0f, 0.08f);
    [SerializeField] private Color attackRangeColor = new Color(1f, 0.3f, 0f, 0.2f);
    #endregion

    #region 内部变量
    private CombatEnemyState currentState = CombatEnemyState.Roam;
    private Transform player;
    private NavMeshAgent navAgent;
    private KnockbackSystem knockbackSystem;
    private Animator animator;

    // 状态协程管理
    private Coroutine currentStateCoroutine;

    // 攻击倒计时
    private float attackTimer = 0f;
    private float remainingAttackTimer = 0f; // 玩家靠近时保存的剩余计时

    // 漫游辅助
    private float lastRoamTargetTime = -999f;

    // 状态标志
    private bool resetTimerOnNextRoam = true; // 进入Roam时是否重置倒计时

    // 包围意识变量
    private float personalAngleOffset;      // 个体角度偏好，避免所有敌人站位整齐
    private int preferredOrbitDirection;    // 个体绕圈方向偏好（+1 或 -1）
    private float personalRadiusOffset;     // 个体对包围半径的微偏移
    private float lastEncircleTargetTime = -999f;        // 上次刷新包围目标点的时间
    private float lastAttackPermissionCheckTime = -999f; // 上次检查攻击许可的时间
    private Vector3 currentEncircleTarget;  // 当前选中的包围目标点
    #endregion

    #region Unity生命周期
    protected override void Awake()
    {
        base.Awake();
        InitializeComponents();
        FindPlayer();
        InitializeEncirclementPersonality();
    }

    private void Start()
    {
        if (player != null)
            SwitchState(CombatEnemyState.Roam);
    }
    #endregion

    #region 初始化
    private void InitializeComponents()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
            navAgent = gameObject.AddComponent<NavMeshAgent>();

        navAgent.speed = roamSpeed;
        navAgent.angularSpeed = rotationSpeed * 100f;
        navAgent.acceleration = 10f;
        navAgent.autoBraking = true;
        navAgent.updateRotation = true;
        navAgent.updatePosition = true;

        knockbackSystem = GetComponent<KnockbackSystem>();

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (showStateDebug)
            Debug.Log($"⚔️ CombatEnemy 初始化: {gameObject.name}, 内环: {innerRadius}, 外环: {outerRadius}");
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            if (showStateDebug)
                Debug.Log($"⚔️ CombatEnemy 找到玩家: {player.name}");
        }
        else
        {
            Debug.LogWarning($"⚠️ CombatEnemy [{gameObject.name}] 未找到Player标签对象！");
        }
    }

    /// <summary>初始化每只敌人的包围意识个体差异</summary>
    private void InitializeEncirclementPersonality()
    {
        personalAngleOffset = Random.Range(-20f, 20f);
        preferredOrbitDirection = Random.value < 0.5f ? -1 : 1;
        personalRadiusOffset = Random.Range(-0.4f, 0.4f);

        if (showStateDebug)
            Debug.Log($"⚔️ CombatEnemy 包围个性: angleOffset={personalAngleOffset:F1}, orbitDir={preferredOrbitDirection}, radiusOffset={personalRadiusOffset:F2}");
    }
    #endregion

    #region IKnockbackReceiver 实现
    public void OnKnockbackStart()
    {
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

        // 恢复NavMeshAgent
        if (navAgent != null)
        {
            navAgent.enabled = true;
            if (!navAgent.isOnNavMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
                {
                    navAgent.Warp(hit.position);
                }
            }
            navAgent.isStopped = false;
            navAgent.nextPosition = transform.position;
        }

        resetTimerOnNextRoam = true;

        float dist = GetHorizontalDistanceToPlayer();
        if (dist >= innerRadius && dist <= outerRadius)
            SwitchState(CombatEnemyState.Roam);
        else
            SwitchState(CombatEnemyState.Reposition);
        SetAnimatorTrigger("KnockbackDone");
    }
    #endregion

    #region 状态机切换
    /// <summary>
    /// 统一状态切换入口：停止当前协程，更新状态并启动新协程。
    /// preservedTimer > 0 时表示希望在进入Roam时沿用该剩余计时值。
    /// </summary>
    private void SwitchState(CombatEnemyState newState, float preservedTimer = 0f)
    {
        StopCurrentCoroutine();

        CombatEnemyState oldState = currentState;
        currentState = newState;

        if (showStateDebug)
            Debug.Log($"🔄 CombatEnemy 状态切换: {oldState} → {newState}");

        switch (newState)
        {
            case CombatEnemyState.Roam:
                // 若传入保留计时，则沿用（来自玩家靠近触发的Reposition）
                if (preservedTimer > 0f)
                {
                    attackTimer = preservedTimer;
                    resetTimerOnNextRoam = false;
                }
                currentStateCoroutine = StartCoroutine(CoroutineRoam());
                break;

            case CombatEnemyState.Reposition:
                currentStateCoroutine = StartCoroutine(CoroutineReposition());
                break;

            case CombatEnemyState.Attack:
                currentStateCoroutine = StartCoroutine(CoroutineAttack());
                break;

            // Stunned 与 Knockback 由外部调用 Stun() / OnKnockbackStart() 触发，不走此分支
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
    #endregion

    #region 状态协程 — Roam
    private IEnumerator CoroutineRoam()
    {
        // 初始化或重置倒计时
        if (resetTimerOnNextRoam)
        {
            attackTimer = Random.Range(minAttackDelay, maxAttackDelay);
            resetTimerOnNextRoam = false;
        }
        // 否则沿用 attackTimer 中已保留的剩余时间（由SwitchState传入preservedTimer写入）

        SetAnimatorBool("isMoving", true);
        navAgent.speed = roamSpeed;
        navAgent.isStopped = false;

        if (showStateDebug)
            Debug.Log($"🚶 [Roam] 开始漫游，攻击倒计时: {attackTimer:F1}s");

        while (currentState == CombatEnemyState.Roam)
        {
            // 更新漫游/包围目标点
            if (Time.time - lastEncircleTargetTime > slotRepathInterval)
            {
                Vector3 roamTarget = enableEncirclement
                    ? CalculateEncircleTarget()
                    : GetRandomPointInRing(player.position, innerRadius, outerRadius);

                if (NavMesh.SamplePosition(roamTarget, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    currentEncircleTarget = hit.position;
                    navAgent.SetDestination(hit.position);
                }
                lastEncircleTargetTime = Time.time;
                // 兼容旧字段，保持两者同步
                lastRoamTargetTime = lastEncircleTargetTime;
            }

            // 攻击倒计时递减
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                if (CanStartAttackNow())
                {
                    if (showStateDebug)
                        Debug.Log($"⚔️ [Roam] 攻击倒计时结束，获得攻击许可 → Attack");
                    SwitchState(CombatEnemyState.Attack);
                    yield break;
                }
                else
                {
                    // 没轮到，继续包围，稍作等待再尝试
                    if (showStateDebug)
                        Debug.Log($"⚔️ [Roam] 攻击倒计时到但未获许可，延迟等待");
                    attackTimer = Random.Range(0.4f, 1.0f);
                }
            }

            // 距离越界检查
            float dist = GetHorizontalDistanceToPlayer();
            if (dist < innerRadius)
            {
                // 玩家靠近：保留剩余倒计时
                if (showStateDebug)
                    Debug.Log($"📏 [Roam] 玩家过近(dist={dist:F1}) → Reposition（保留计时: {attackTimer:F1}s）");
                resetTimerOnNextRoam = false;
                remainingAttackTimer = attackTimer;
                SwitchState(CombatEnemyState.Reposition);
                yield break;
            }
            else if (dist > outerRadius)
            {
                // 玩家远离：重置倒计时
                if (showStateDebug)
                    Debug.Log($"📏 [Roam] 玩家过远(dist={dist:F1}) → Reposition（重置计时）");
                resetTimerOnNextRoam = true;
                SwitchState(CombatEnemyState.Reposition);
                yield break;
            }

            yield return null;
        }
    }
    #endregion

    #region 状态协程 — Reposition
    private IEnumerator CoroutineReposition()
    {
        // 进入前随机延迟，避免所有敌人同步行动
        float delay = Random.Range(repositionRandomDelayMin, repositionRandomDelayMax);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        navAgent.speed = repositionSpeed;
        navAgent.isStopped = false;
        SetAnimatorBool("isMoving", true);

        if (showStateDebug)
            Debug.Log($"🔀 [Reposition] 开始归位");

        while (currentState == CombatEnemyState.Reposition)
        {
            float dist = GetHorizontalDistanceToPlayer();

            // 已回到合法区间 → 恢复 Roam
            if (dist >= innerRadius && dist <= outerRadius)
            {
                if (showStateDebug)
                    Debug.Log($"✅ [Reposition] 归位完成(dist={dist:F1}) → Roam");

                // 根据是否需要保留倒计时来决定传入值
                if (!resetTimerOnNextRoam && remainingAttackTimer > 0f)
                    SwitchState(CombatEnemyState.Roam, remainingAttackTimer);
                else
                    SwitchState(CombatEnemyState.Roam);

                yield break;
            }

            // 计算归位目标点
            Vector3 targetPos = CalculateRepositionTarget(dist);
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                navAgent.SetDestination(hit.position);
            }
            else
            {
                // 采样失败时沿方向小步外推再采样
                targetPos = TryFindFallbackNavPoint(targetPos);
            }

            yield return new WaitForSeconds(repositionCheckInterval);
        }
    }

    /// <summary>根据当前距离计算归位目标：优先归回包围槽位，不可用时回退到距离修正</summary>
    private Vector3 CalculateRepositionTarget(float dist)
    {
        // 若包围意识启用，优先尝试归回自己的包围槽位点
        if (enableEncirclement)
        {
            Vector3 encircleTarget = CalculateEncircleTarget();
            if (NavMesh.SamplePosition(encircleTarget, out NavMeshHit encircleHit, 3f, NavMesh.AllAreas))
            {
                if (showStateDebug)
                    Debug.Log($"🔀 [Reposition] 归回包围槽位: {encircleHit.position}");
                return encircleHit.position;
            }
        }

        // 回退：原始距离纠偏逻辑
        Vector3 dirFromPlayer = (transform.position - player.position);
        dirFromPlayer.y = 0f;

        if (dirFromPlayer.sqrMagnitude < 0.001f)
            dirFromPlayer = transform.forward;

        dirFromPlayer.Normalize();

        if (dist < innerRadius)
        {
            // 太近 → 向外推至 innerRadius + margin
            return player.position + dirFromPlayer * (innerRadius + repositionTargetMargin);
        }
        else
        {
            // 太远 → 向内拉至 outerRadius - margin
            return player.position + dirFromPlayer * (outerRadius - repositionTargetMargin);
        }
    }

    /// <summary>在目标点附近沿方向外推，寻找可用NavMesh点</summary>
    private Vector3 TryFindFallbackNavPoint(Vector3 origin)
    {
        Vector3 dir = (origin - transform.position).normalized;
        for (int i = 1; i <= 5; i++)
        {
            Vector3 candidate = origin + dir * (i * 0.5f);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                return hit.position;
        }
        return origin;
    }
    #endregion

    #region 状态协程 — Attack
    private IEnumerator CoroutineAttack()
    {
        SetAnimatorBool("isMoving", true);
        navAgent.isStopped = false;
        navAgent.speed = chargeSpeed;

        if (showStateDebug)
            Debug.Log($"⚔️ [Attack] 阶段1：冲向玩家");

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
                resetTimerOnNextRoam = true;
                SwitchState(CombatEnemyState.Roam);
                yield break;
            }
 
            // 冲刺途中距离超出放弃阈值 → 放弃
            if (GetHorizontalDistanceToPlayer() > giveUpChaseDistance)
            {
                if (showStateDebug)
                    Debug.Log($"🏳️ [Attack] 玩家逃离过远，放弃攻击 → Roam（重置计时）");
                resetTimerOnNextRoam = true;
                SwitchState(CombatEnemyState.Roam);
                yield break;
            }
 
            yield return null;
        }

        // ── 阶段2：停止，执行攻击 ──
        if (showStateDebug)
            Debug.Log($"⚔️ [Attack] 阶段2：攻击");

        navAgent.isStopped = true;
        SetAnimatorBool("isMoving", false);

        // 面向玩家
        FaceTarget(player.position);

        // 触发攻击动画，等待动画窗口（实际伤害在动画事件PerformAttack中执行）
        SetAnimatorTrigger("Attacking");
        yield return new WaitForSeconds(attackAnimDuration);
        SetAnimatorTrigger("AttackDone");

        // ── 阶段3：后退至安全距离 ──
        if (showStateDebug)
            Debug.Log($"⚔️ [Attack] 阶段3：后退");

        Vector3 backDir = (transform.position - player.position);
        backDir.y = 0f;
        if (backDir.sqrMagnitude < 0.001f) backDir = -transform.forward;
        backDir.Normalize();

        float backTargetDist = innerRadius + retreatMargin;
        Vector3 backTarget = player.position + backDir * backTargetDist;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(backTarget, out navHit, 3f, NavMesh.AllAreas))
        {
            navAgent.isStopped = false;
            navAgent.speed = repositionSpeed;
            navAgent.SetDestination(navHit.position);
            SetAnimatorBool("isMoving", true);

            // 等待到达后退目标
            while (navAgent.pathPending || navAgent.remainingDistance > navAgent.stoppingDistance + 0.1f)
            {
                // 安全检查：若被眩晕/击退打断，协程应已被Stop，此处作为保险
                if (currentState != CombatEnemyState.Attack)
                    yield break;
                yield return null;
            }
        }

        // 攻击全流程完成，回到Roam并强制重置计时器
        if (showStateDebug)
            Debug.Log($"✅ [Attack] 攻击完成 → Roam（重置计时）");
        resetTimerOnNextRoam = true;
        SwitchState(CombatEnemyState.Roam);
    }

    /// <summary>
    /// 动画事件调用 — 在攻击动画命中帧触发实际伤害/交互事件
    /// </summary>
    public void PerformAttack()
    {
        if (currentState != CombatEnemyState.Attack) return;

        if (player == null) return;

        if (showStateDebug)
            Debug.Log($"💥 [CombatEnemy] PerformAttack() 触发攻击事件");

        ColorInteractionEvent evt = new ColorInteractionEvent(
            gameObject,
            player.gameObject,
            ColorInteractionType.EnemyAttackPlayer
        );
        ColorEventBus.PublishColorInteraction(evt);
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
        SetAnimatorTrigger("StunDone");
    }

    private IEnumerator CoroutineStunned(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (showStateDebug)
            Debug.Log($"✅ [CombatEnemy] 眩晕结束，重新评估状态");

        // 恢复NavMeshAgent
        if (navAgent != null)
        {
            navAgent.isStopped = false;
        }

        // 眩晕结束后强制重置计时器
        resetTimerOnNextRoam = true;

        float dist = GetHorizontalDistanceToPlayer();
        if (dist >= innerRadius && dist <= outerRadius)
            SwitchState(CombatEnemyState.Roam);
        else
            SwitchState(CombatEnemyState.Reposition);
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

    // ─── 包围意识辅助 ───

    /// <summary>计算当前敌人的包围目标点（考虑个体偏好和同伴排斥）</summary>
    private Vector3 CalculateEncircleTarget()
    {
        if (player == null) return transform.position;

        Vector3 toEnemy = transform.position - player.position;
        toEnemy.y = 0f;
        if (toEnemy == Vector3.zero) toEnemy = transform.forward;

        Vector3 baseDir = toEnemy.normalized;

        // 切线方向：让敌人倾向绕圈而非只前后收缩
        Vector3 tangent = Vector3.Cross(Vector3.up, baseDir) * preferredOrbitDirection;

        float orbitBlend = 0.65f;
        Vector3 desiredDir = Vector3.Slerp(baseDir, tangent.normalized, orbitBlend).normalized;

        // 叠加个体角度偏好和随机抖动
        float angleJitter = Random.Range(-slotAngleJitter, slotAngleJitter);
        desiredDir = Quaternion.Euler(0f, personalAngleOffset + angleJitter, 0f) * desiredDir;

        float targetRadius = Mathf.Lerp(innerRadius, outerRadius, encircleRadiusRatio) + personalRadiusOffset;
        Vector3 rawTarget = player.position + desiredDir * targetRadius;
        rawTarget.y = transform.position.y;

        return ApplyAllyAvoidance(rawTarget);
    }

    /// <summary>对目标点施加同伴排斥，防止多个敌人扎堆到同一方向</summary>
    private Vector3 ApplyAllyAvoidance(Vector3 targetPos)
    {
        Collider[] hits = Physics.OverlapSphere(targetPos, allyAvoidanceRadius);
        Vector3 push = Vector3.zero;

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            CombatEnemy other = hit.GetComponentInParent<CombatEnemy>();
            if (other == null || other == this) continue;

            Vector3 away = targetPos - other.transform.position;
            away.y = 0f;

            float dist = away.magnitude;
            if (dist <= 0.01f) continue;

            float weight = 1f - Mathf.Clamp01(dist / allyAvoidanceRadius);
            push += away.normalized * weight;
        }

        if (push != Vector3.zero)
            targetPos += push.normalized * allyAvoidanceStrength;

        return targetPos;
    }

    /// <summary>检查当前帧是否允许本敌人发起攻击（攻击轮换：仅离玩家最近的1~2只可出手）</summary>
    private bool CanStartAttackNow()
    {
        if (player == null) return true;

        // 节流：避免每帧都做 OverlapSphere
        if (Time.time - lastAttackPermissionCheckTime < attackPermissionCheckInterval)
            return true; // 节流期间默认放行，防止永远卡住

        lastAttackPermissionCheckTime = Time.time;

        Collider[] hits = Physics.OverlapSphere(player.position, outerRadius + 2f);
        int closerCount = 0;
        float myDist = GetHorizontalDistanceToPlayer();

        foreach (Collider hit in hits)
        {
            CombatEnemy other = hit.GetComponentInParent<CombatEnemy>();
            if (other == null || other == this) continue;

            Vector3 otherFlat = new Vector3(other.transform.position.x, 0f, other.transform.position.z);
            Vector3 playerFlat = new Vector3(player.position.x, 0f, player.position.z);
            float otherDist = Vector3.Distance(otherFlat, playerFlat);

            if (otherDist < myDist)
                closerCount++;
        }

        // 比我更近的敌人少于2只时，允许出手
        bool canAttack = closerCount < 2;
        if (showStateDebug)
            Debug.Log($"⚔️ [CanStartAttackNow] myDist={myDist:F1}, closerCount={closerCount}, canAttack={canAttack}");
        return canAttack;
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

    /// <summary>获取当前攻击倒计时剩余时间</summary>
    public float GetAttackTimerRemaining() => attackTimer;

    /// <summary>运行时修改内环半径</summary>
    public void SetInnerRadius(float r) => innerRadius = Mathf.Max(0.5f, r);

    /// <summary>运行时修改外环半径</summary>
    public void SetOuterRadius(float r) => outerRadius = Mathf.Max(innerRadius + 1f, r);

    /// <summary>运行时修改攻击延迟范围</summary>
    public void SetAttackDelay(float min, float max)
    {
        minAttackDelay = Mathf.Max(0.5f, min);
        maxAttackDelay = Mathf.Max(minAttackDelay + 0.1f, max);
    }
    #endregion

    #region 调试可视化
    private void OnDrawGizmosSelected()
    {
        if (!showRangeGizmos) return;

        // 内环
        Gizmos.color = innerRangeColor;
        DrawCircle(transform.position, innerRadius, 32);
        Gizmos.color = Color.yellow;
        DrawWireCircle(transform.position, innerRadius, 32);

        // 外环
        Gizmos.color = outerRangeColor;
        DrawCircle(transform.position, outerRadius, 32);
        Gizmos.color = Color.red;
        DrawWireCircle(transform.position, outerRadius, 32);

        // 攻击范围
        Gizmos.color = attackRangeColor;
        Gizmos.DrawSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.3f, 0f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

#if UNITY_EDITOR
        if (Application.isPlaying && player != null)
        {
            float dist = GetHorizontalDistanceToPlayer();
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (outerRadius + 1.2f),
                $"状态: {currentState}\n" +
                $"距玩家: {dist:F1}m\n" +
                $"攻击倒计时: {attackTimer:F1}s\n" +
                $"resetTimer: {resetTimerOnNextRoam}\n" +
                $"angleOffset: {personalAngleOffset:F1} orbitDir: {preferredOrbitDirection}\n" +
                $"encircle: {enableEncirclement}"
            );
        }
#endif
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float step = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = Mathf.Deg2Rad * (step * i);
            float a2 = Mathf.Deg2Rad * (step * (i + 1));
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
            Vector3 p2 = center + new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius);
            Gizmos.DrawLine(center, p1);
            Gizmos.DrawLine(center, p2);
        }
    }

    private void DrawWireCircle(Vector3 center, float radius, int segments)
    {
        float step = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = Mathf.Deg2Rad * (step * i);
            float a2 = Mathf.Deg2Rad * (step * (i + 1));
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
            Vector3 p2 = center + new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius);
            Gizmos.DrawLine(p1, p2);
        }
    }
    #endregion
}