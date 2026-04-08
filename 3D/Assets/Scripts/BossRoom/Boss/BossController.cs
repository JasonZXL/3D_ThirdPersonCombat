using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BossController : BaseEnemy
{
    public enum BossPhase
    {
        Phase1_Melee = 1,
        Phase2_Ranged = 2
    }

    [Header("Phase Settings")]
    [SerializeField] private int phase2TriggerHP = 10;

    [Header("References")]
    [SerializeField] private HealthSystem bossHealth;
    [SerializeField] private Transform bossSpawnPoint;

    [Header("Modules")]
    [SerializeField] private BossMeleeModule meleeModule;
    [SerializeField] private BossRangedModule rangedModule;

    [Header("Stun (Same-color)")]
    [SerializeField] private ChasingEnemyStunController stunController;

    [Header("Movement (Hybrid Mode)")]
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private CharacterController characterController;

    [Header("Teleport / Corner Fail-safe")]
    [Tooltip("撤退卡住后传送冷却，避免连续触发")]
    [SerializeField] private float teleportCooldown = 1.5f;

    [Tooltip("传送后延迟一帧再恢复远程模块（避免同帧又被Move拉回角落）")]
    [SerializeField] private bool reenableRangedNextFrame = true;

    private Transform player;
    private BossPhase currentPhase = BossPhase.Phase1_Melee;
    public bool IsPhase2 => currentPhase == BossPhase.Phase2_Ranged;
    private bool phase2Entered = false;

    private float lastTeleportTime = -999f;
    private Coroutine teleportRoutine;

    // ✅ 修复：override Awake + base.Awake()
    protected override void Awake()
    {
        base.Awake();
        AutoWireReferences(includeInactive: true);
    }

    private void OnEnable()
    {
        if (rangedModule != null)
        {
            rangedModule.OnRetreatStuck -= HandleRetreatStuck;
            rangedModule.OnRetreatStuck += HandleRetreatStuck;
        }
    }

    private void OnDisable()
    {
        if (rangedModule != null)
            rangedModule.OnRetreatStuck -= HandleRetreatStuck;
    }

    private void Start()
    {
        EnsurePlayerReference();

        meleeModule?.Initialize(player);
        rangedModule?.Initialize(player);

        EnterPhase1("Start");
    }

    private void Update()
    {
        if (bossHealth == null) return;

        if (!bossHealth.IsAlive)
        {
            DisableAllModules("Boss Dead");
            return;
        }

        EnsurePlayerReference();

        // Phase切换判定
        if (!phase2Entered && bossHealth.CurrentHearts <= phase2TriggerHP)
        {
            EnterPhase2($"HP <= {phase2TriggerHP}");
        }

        // 驱动当前模块
        if (currentPhase == BossPhase.Phase1_Melee)
            meleeModule?.Tick();
        else
            rangedModule?.Tick();
    }

    private void LateUpdate()
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null && !cc.enabled)
        {
            cc.enabled = true;
            Debug.LogWarning("[Boss] CharacterController 被意外关闭，已自动恢复 enabled=true");
        }
    }

    #region Phase Control

    private void EnterPhase1(string reason)
    {
        currentPhase = BossPhase.Phase1_Melee;
        phase2Entered = false;

        // 关闭可能存在的传送协程
        StopTeleportRoutineIfAny();

        meleeModule?.SetActive(true);
        rangedModule?.SetActive(false);

        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }

        if (showDebugLogs)
            Debug.Log($"🗡️ [BossController] EnterPhase1 ({reason})");
    }

    private void EnterPhase2(string reason)
    {
        currentPhase = BossPhase.Phase2_Ranged;
        phase2Entered = true;

        // Phase2 开启远程模块，关闭近战模块
        meleeModule?.SetActive(false);
        rangedModule?.SetActive(true);

        // 清一下路径避免残留目标点干扰
        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }

        if (showDebugLogs)
            Debug.Log($"🏹 [BossController] EnterPhase2 ({reason})");
    }

    private void DisableAllModules(string reason)
    {
        StopTeleportRoutineIfAny();

        meleeModule?.SetActive(false);
        rangedModule?.SetActive(false);

        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        if (showDebugLogs)
            Debug.Log($"🛑 [BossController] DisableAllModules ({reason})");
    }

    #endregion

    #region Player Binding

    private void EnsurePlayerReference()
    {
        if (player != null) return;

        var pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null)
        {
            player = pObj.transform;

            meleeModule?.Initialize(player);
            rangedModule?.Initialize(player);

            if (showDebugLogs)
                Debug.Log($"🎯 [BossController] Player Found -> {player.name}");
        }
    }

    #endregion

    #region Same-color Interaction -> Stun

    public override void OnColorInteraction(ColorInteractionEvent interaction)
    {
        if (interaction.Source == null || interaction.Target == null) return;

        // 只处理：Boss攻击玩家
        if (interaction.Type != ColorInteractionType.EnemyAttackPlayer) return;
        if (interaction.Source != gameObject) return;

        bool sameColor = interaction.SourceColor == interaction.TargetColor;
        if (!sameColor) return;

        // 这里只负责触发眩晕（Phase1核心）
        if (stunController != null)
        {
            stunController.StartStun();

            if (showDebugLogs)
                Debug.Log("🌀 [BossController] SameColor EnemyAttackPlayer -> StartStun()");
        }
    }

    #endregion

    #region Corner Fail-safe -> Teleport

    private void HandleRetreatStuck()
    {
        if (currentPhase != BossPhase.Phase2_Ranged) return;

        // 冷却，避免连续触发
        if (Time.time - lastTeleportTime < teleportCooldown)
            return;

        lastTeleportTime = Time.time;

        if (showDebugLogs)
            Debug.LogWarning("🧯 [BossController] Phase2 Retreat Stuck -> Teleport To SpawnPoint");

        if (teleportRoutine != null)
            StopCoroutine(teleportRoutine);

        teleportRoutine = StartCoroutine(TeleportToSpawnPointRoutine());
    }

    /// <summary>
    /// 保留混合模式的“硬传送同步”：
    /// - rangedModule 暂停，避免同帧又Move回角落
    /// - 临时禁用 CharacterController，强制写 transform.position
    /// - NavMeshAgent.Warp + nextPosition 同步（避免 agent 与 transform 分裂）
    /// - 下一帧恢复 rangedModule
    /// </summary>
    private IEnumerator TeleportToSpawnPointRoutine()
    {
        if (bossSpawnPoint == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("⚠️ [BossController] bossSpawnPoint 未设置，无法传送");
            yield break;
        }

        Vector3 targetPos = bossSpawnPoint.position;
        Quaternion targetRot = bossSpawnPoint.rotation;

        // 1) 暂停远程模块，防止它在同一帧里继续推进移动（把你又推回角落）
        rangedModule?.SetActive(false);

        // 2) 停掉 navAgent 路径（避免内部状态残留）
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        // 3) 临时禁用 CC，避免位置设置被CC干扰
        bool ccWasEnabled = false;
        if (characterController != null)
        {
            ccWasEnabled = characterController.enabled;
            characterController.enabled = false;
        }

        // 4) 硬写 Transform（✅ 即使 updatePosition=false 也必定生效）
        transform.position = targetPos;
        transform.rotation = targetRot;

        // 5) 同步 NavMeshAgent（关键：Warp + nextPosition）
        if (navAgent != null)
        {
            if (!navAgent.enabled)
                navAgent.enabled = true;

            // Warp 主要用于同步 agent 内部位置
            navAgent.Warp(targetPos);

            // ✅ 必须同步 nextPosition，防止 agent/transform 分裂
            navAgent.nextPosition = targetPos;

            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        // 6) 恢复 CC
        if (characterController != null)
            characterController.enabled = ccWasEnabled;

        if (showDebugLogs)
            Debug.Log($"✅ [BossController] Teleport Done -> {targetPos}");

        // 7) 下一帧再恢复 rangedModule（强烈推荐）
        if (reenableRangedNextFrame)
            yield return null;

        EnsurePlayerReference();
        rangedModule?.Initialize(player);
        rangedModule?.SetActive(true);

        teleportRoutine = null;
    }

    private void StopTeleportRoutineIfAny()
    {
        if (teleportRoutine != null)
        {
            StopCoroutine(teleportRoutine);
            teleportRoutine = null;
        }
    }

    #endregion

    #region Auto Wire

    private void AutoWireReferences(bool includeInactive)
    {
        bossHealth ??= GetComponent<HealthSystem>();
        navAgent ??= GetComponent<NavMeshAgent>();
        characterController ??= GetComponent<CharacterController>();

        stunController ??= GetComponentInChildren<ChasingEnemyStunController>(includeInactive);
        meleeModule ??= GetComponentInChildren<BossMeleeModule>(includeInactive);
        rangedModule ??= GetComponentInChildren<BossRangedModule>(includeInactive);

        if (bossSpawnPoint == null)
        {
            var sp = GameObject.Find("BossSpawnPoint");
            if (sp != null) bossSpawnPoint = sp.transform;
        }

        if (showDebugLogs)
        {
            Debug.Log($"🧩 [BossController] AutoWire -> " +
                      $"Health={(bossHealth != null ? "OK" : "NULL")}, " +
                      $"Agent={(navAgent != null ? "OK" : "NULL")}, " +
                      $"CC={(characterController != null ? "OK" : "NULL")}, " +
                      $"Stun={(stunController != null ? "OK" : "NULL")}, " +
                      $"MeleeModule={(meleeModule != null ? "OK" : "NULL")}, " +
                      $"RangedModule={(rangedModule != null ? "OK" : "NULL")}, " +
                      $"SpawnPoint={(bossSpawnPoint != null ? bossSpawnPoint.name : "NULL")}");
        }
    }

    #endregion

    #region Debug

    [ContextMenu("Debug/Force Enter Phase1")]
    private void DebugForcePhase1() => EnterPhase1("Debug");

    [ContextMenu("Debug/Force Enter Phase2")]
    private void DebugForcePhase2() => EnterPhase2("Debug");

    #endregion
}



