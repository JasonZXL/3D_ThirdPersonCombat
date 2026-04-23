using UnityEngine;

/// <summary>
/// 召唤师专用的血量系统，继承 HealthSystem。
/// 功能：处理召唤师受伤时的短暂 AI 停顿、死亡时的完整清理（禁用 AI、碰撞体、渲染器、攻击检测），以及死亡特效与掉落物品。
/// 工作链路：挂载在召唤师 GameObject 上，与 SummonerStateController 共存；受伤时短暂禁用 SummonerStateController 实现停顿效果；死亡时永久禁用 AI 并触发销毁流程。
/// 设计原则：不侵入 SummonerStateController 的内部逻辑，仅通过 enabled 开关控制 AI 暂停/恢复；与 HeartUIController、EnemyAttackDetector、ColorComponent 等系统自动兼容。
/// </summary>
public class SummonerHealthSystem : HealthSystem
{
    [Header("召唤师特殊设置")]
    [Tooltip("死亡特效 Prefab")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Tooltip("死亡时是否掉落物品")]
    [SerializeField] private bool dropItemOnDeath = false;

    [Tooltip("掉落物品 Prefab")]
    [SerializeField] private GameObject dropItemPrefab;

    [Header("受击停顿")]
    [Tooltip("受伤后 AI 短暂停顿时长（秒），模拟召唤师被打中的硬直")]
    [SerializeField] private float hitStunDuration = 0.3f;

    // ──────────────────────────────────────────────
    // 运行时引用
    // ──────────────────────────────────────────────

    private SummonerStateController summonerController;
    private KnockbackSystem knockbackSystem;

    // ══════════════════════════════════════════════
    // 生命周期
    // ══════════════════════════════════════════════

    /// <summary>
    /// 初始化召唤师血量系统，获取 SummonerStateController 与 KnockbackSystem 引用，订阅死亡事件。
    /// 功能：调用基类 Awake 初始化血量数据；获取同物体上的 SummonerStateController 与 KnockbackSystem；订阅 OnDeath 事件。
    /// 工作链路：在 GameObject 激活时由 Unity 调用；完成后本组件进入待命状态，等待外部调用 TakeDamage。
    /// 对下游影响：summonerController 引用被缓存，后续受伤/死亡逻辑依赖此引用。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        summonerController = GetComponent<SummonerStateController>();
        knockbackSystem    = GetComponent<KnockbackSystem>();

        OnDeath += HandleSummonerDeath;

        if (showDebugLogs)
            Debug.Log($"[SummonerHealth] Init complete: {gameObject.name}, {maxHearts} hearts"
                + " | controller = " + (summonerController != null ? "found" : "MISSING")
                + " | knockback = " + (knockbackSystem != null ? "found" : "none"));
    }

    // ══════════════════════════════════════════════
    // 受伤
    // ══════════════════════════════════════════════

    /// <summary>
    /// 召唤师受伤处理，调用基类伤害逻辑后触发短暂 AI 停顿。
    /// 功能：调用 base.TakeDamage 扣血并触发血量事件；若仍存活，短暂禁用 SummonerStateController 实现"被打中硬直"效果。
    /// 工作链路：由外部攻击系统（玩家攻击、颜色交互等）调用；基类负责扣血与无敌帧判定，本函数负责召唤师特有的受击反馈。
    /// 对下游影响：AI 被短暂禁用期间，所有状态模块的 Tick 停止调用；停顿结束后 AI 恢复正常驱动。
    /// </summary>
    public override void TakeDamage(int damage = 1, GameObject damageSource = null)
    {
        base.TakeDamage(damage, damageSource);

        if (IsAlive && damage > 0)
        {
            if (showDebugLogs)
                Debug.Log($"[SummonerHealth] {gameObject.name} took {damage} damage"
                    + " | remaining = " + currentHearts + "/" + maxHearts);

            // 受伤短暂停顿
            if (summonerController != null)
            {
                StartCoroutine(HitStunEffect());
            }
        }
    }

    /// <summary>
    /// 受击硬直协程：短暂禁用 SummonerStateController 使 AI 停顿，到时后恢复。
    /// 功能：将 SummonerStateController.enabled 置为 false 暂停 Update 驱动；等待 hitStunDuration 秒后恢复为 true。
    /// 工作链路：由 TakeDamage 在确认存活且伤害有效时启动；协程结束后 AI 从暂停处继续运行。
    /// 对下游影响：停顿期间召唤师完全静止（状态机暂停、NavMeshAgent 不更新目标）；恢复后当前状态继续正常 Tick。
    /// </summary>
    private System.Collections.IEnumerator HitStunEffect()
    {
        if (summonerController != null)
        {
            summonerController.enabled = false;

            if (showDebugLogs)
                Debug.Log($"[SummonerHealth] Hit stun START | duration = {hitStunDuration}s");

            yield return new WaitForSeconds(hitStunDuration);

            // 恢复前确认对象仍然存活（可能在停顿期间被击杀）
            if (summonerController != null && IsAlive)
            {
                summonerController.enabled = true;

                if (showDebugLogs)
                    Debug.Log($"[SummonerHealth] Hit stun END | AI resumed");
            }
        }
    }

    // ══════════════════════════════════════════════
    // 死亡
    // ══════════════════════════════════════════════

    /// <summary>
    /// 触发召唤师死亡流程，调用基类 Die 发布死亡事件。
    /// 功能：输出死亡日志，调用 base.Die() 触发 OnDeath 事件链。
    /// 工作链路：由基类 HealthSystem 在血量归零时自动调用；base.Die() 会触发 OnDeath，进而调用 HandleSummonerDeath。
    /// 对下游影响：HandleSummonerDeath 执行完整的死亡清理。
    /// </summary>
    protected override void Die()
    {
        if (showDebugLogs)
            Debug.Log($"[SummonerHealth] {gameObject.name} defeated!");

        base.Die();
    }

    /// <summary>
    /// 召唤师死亡后的完整清理流程。
    /// 功能：播放死亡特效；生成掉落物品；禁用碰撞体、渲染器；永久禁用 SummonerStateController（AI 停止）；禁用 EnemyAttackDetector（停止攻击判定）。
    /// 工作链路：由 OnDeath 事件触发（在 base.Die 中发布）；base.Die 在事件触发后会自行处理 destroyOnDeath / deathDelay 销毁逻辑，因此本函数不重复销毁。
    /// 对下游影响：召唤师 AI 永久停止；所有碰撞与渲染关闭；HeartUIController 会在 OnDestroy 时自动清理。
    /// </summary>
    private void HandleSummonerDeath()
    {
        if (showDebugLogs)
            Debug.Log($"[SummonerHealth] {gameObject.name} death handling started");

        // 播放死亡特效
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }

        // 掉落物品
        if (dropItemOnDeath && dropItemPrefab != null)
        {
            Instantiate(dropItemPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        // 禁用碰撞体
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            if (showDebugLogs)
                Debug.Log($"[SummonerHealth] Collider disabled");
        }

        // 禁用渲染器
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.enabled = false;
            if (showDebugLogs)
                Debug.Log($"[SummonerHealth] Renderer disabled");
        }

        // 禁用子物体渲染器（SkinnedMeshRenderer 等）
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < childRenderers.Length; i++)
        {
            childRenderers[i].enabled = false;
        }

        // 禁用召唤师 AI
        if (summonerController != null)
        {
            summonerController.enabled = false;
            if (showDebugLogs)
                Debug.Log($"[SummonerHealth] SummonerStateController disabled");
        }

        // 禁用攻击检测
        EnemyAttackDetector attackDetector = GetComponent<EnemyAttackDetector>();
        if (attackDetector != null)
        {
            attackDetector.enabled = false;
            if (showDebugLogs)
                Debug.Log($"[SummonerHealth] EnemyAttackDetector disabled");
        }

        // 注意：不在此处调用 Destroy。
        // 基类 HealthSystem.Die() 在触发 OnDeath 事件后会自行处理 destroyOnDeath / deathDelay 销毁逻辑，
        // 本函数由 OnDeath 事件驱动，不重复销毁以避免 double-destroy。
    }
}
