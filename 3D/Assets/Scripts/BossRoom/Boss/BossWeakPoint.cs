using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// BossWeakPoint：Boss背部弱点组件
/// - 挂在Boss背部子物体上（带Collider且IsTrigger=true）
/// - 只有weakPointActive=true时才允许造成伤害
/// - 通过监听 ColorEventBus.OnColorInteraction 来判断“玩家攻击命中弱点”
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossWeakPoint : MonoBehaviour
{
    [Header("弱点开关")]
    [SerializeField] private bool weakPointActive = false;

    [Tooltip("弱点激活时是否启用Collider")]
    [SerializeField] private bool toggleColliderWithActive = true;
    [Header("视觉反馈")]
    [SerializeField] private GameObject weakPointPrefab;  // 弱点的预制体
    [Tooltip("弱点视觉效果的挂载点")]
    [SerializeField] private Transform weakPointVisual;
    private GameObject weakPointSwitch;  // 实例化的视觉对象

    [Header("伤害设置")]
    [Tooltip("每次命中弱点对Boss造成的伤害")]
    [SerializeField] private int damagePerHit = 2;

    [Tooltip("命中弱点的最小间隔")]
    [SerializeField] private float hitCooldown = 0.1f;

    [Header("引用（可自动寻找）")]
    [SerializeField] private HealthSystem bossHealthSystem; // Boss本体血量
    [SerializeField] private Transform bossRoot;            // Boss根物体（可选）

    [Header("调试")]
    [SerializeField] private bool showDebugLogs = true;

    private Collider weakPointCollider;
    private float lastHitTime = -999f;

    private void Awake()
    {
        weakPointCollider = GetComponent<Collider>();

        if (bossRoot == null)
            bossRoot = transform.root;

        if (bossHealthSystem == null && bossRoot != null)
            bossHealthSystem = bossRoot.GetComponent<HealthSystem>();

        if (weakPointPrefab != null)
        {
            // 如果指定了visualParent，就挂在那里；否则挂在自身
            Transform parent = weakPointVisual != null ? weakPointVisual : transform;
            weakPointSwitch = Instantiate(weakPointPrefab, parent);
            weakPointSwitch.transform.localPosition = Vector3.zero;
            weakPointSwitch.transform.localRotation = Quaternion.identity;
            weakPointSwitch.SetActive(weakPointActive);
        }

        ApplyActiveToCollider();
    }

    private void OnEnable()
    {
        ColorEventBus.OnColorInteraction += HandleColorInteraction;
    }

    private void OnDisable()
    {
        ColorEventBus.OnColorInteraction -= HandleColorInteraction;
    }

    /// <summary>
    /// 外部调用：开启/关闭弱点窗口
    /// </summary>
    public void SetWeakPointActive(bool active)
    {
        weakPointActive = active;
        ApplyActiveToCollider();
        if (weakPointSwitch != null)
        {
            weakPointSwitch.SetActive(active);
        }

        if (showDebugLogs)
            Debug.Log($"🎯 [BossWeakPoint] {(active ? "开启" : "关闭")}弱点窗口: {name}");
    }

    public bool IsWeakPointActive() => weakPointActive;

    private void ApplyActiveToCollider()
    {
        if (!toggleColliderWithActive) return;
        if (weakPointCollider != null)
            weakPointCollider.enabled = weakPointActive;
    }

    /// <summary>
    /// 监听玩家攻击事件：
    /// - interaction.Type == PlayerAttackEnemy
    /// - interaction.Target == 本弱点GameObject
    /// 只有在弱点激活时才对Boss造成伤害
    /// </summary>
    private void HandleColorInteraction(ColorInteractionEvent interaction)
    {
        // ColorInteractionEvent 是 struct，不能判空
        if (!weakPointActive) return;
        if (interaction.Type != ColorInteractionType.PlayerAttackEnemy) return;

        if (interaction.Source == null || interaction.Target == null) return;

        // 只有当玩家攻击“打到弱点Collider”时才触发伤害
        if (interaction.Target != gameObject) return;
        if (bossRoot == null) bossRoot = transform.root;
        if (bossRoot != null)
        {
            Vector3 toAttacker = (interaction.Source.transform.position - bossRoot.position).normalized;
            float dot = Vector3.Dot(bossRoot.forward, toAttacker);
            
            // 点积>0表示攻击来自前方（无效），<0表示来自后方（有效）
            if (dot >= 0)
            {
                if (showDebugLogs)
                    Debug.Log("必须从Boss后方攻击弱点");
                return;
            }
        }
        // 冷却保护
        if (Time.time - lastHitTime < hitCooldown) return;
        lastHitTime = Time.time;

        if (showDebugLogs)
            Debug.Log($"🩸 [BossWeakPoint] 玩家命中弱点 -> 对Boss造成伤害 {damagePerHit}");

        ApplyDamageToBossSafe(damagePerHit);
    }

    /// <summary>
    /// 兼容不同 HealthSystem 实现：优先调用 TakeDamage(int)
    /// </summary>
    private void ApplyDamageToBossSafe(int dmg)
    {
        if (bossHealthSystem == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"⚠️ [BossWeakPoint] bossHealthSystem 未设置，无法造成伤害");
            return;
        }
        if (showDebugLogs)
            Debug.Log($"🔍 [BossWeakPoint] bossHealthSystem 实际类型: {bossHealthSystem.GetType().Name}, 是否为 BossHealthSystem: {bossHealthSystem is BossHealthSystem}");

        // 优先使用强类型 BossHealthSystem 的专用弱点伤害方法
        BossHealthSystem bossHs = bossHealthSystem as BossHealthSystem;
        if (bossHs != null)
        {
            bossHs.TakeDamageFromWeakPoint(dmg);
            if (showDebugLogs)
                Debug.Log($"🩸 [BossWeakPoint] 通过 BossHealthSystem 对 Boss 造成 {dmg} 点弱点伤害");
            return;
        }

        if (showDebugLogs)
            Debug.LogWarning($"⚠️ [BossWeakPoint] 未找到 TakeDamageFromWeakPoint(int)，无法造成弱点伤害");
    }
}

