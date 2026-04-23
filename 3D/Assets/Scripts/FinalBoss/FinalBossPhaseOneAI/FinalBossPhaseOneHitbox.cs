using UnityEngine;
using System;

/// <summary>
/// 挂在 Boss 攻击部位（拳头/脚）的 Collider 所在 GameObject 上。
/// Collider 必须设为 IsTrigger = true。
/// 由动画事件控制启用/禁用，碰到 Tag="Player" 的碰撞体时触发命中回调。
/// </summary>
[RequireComponent(typeof(Collider))]
public class FinalBossPhaseOneHitbox : MonoBehaviour
{
    private const string LOG = "[Hitbox_DEBUG]";

    [Header("Config")]
    [Tooltip("目标 Tag，碰到该 Tag 的碰撞体才算命中")]
    [SerializeField] private string targetTag = "Player";

    /// <summary>
    /// 命中回调。参数为被命中的 Collider。
    /// 由 AttackCommitState 订阅。
    /// </summary>
    public event Action<Collider> OnHit;

    /// <summary>
    /// 当前判定窗口内是否已经命中过（防止同一次攻击多次触发）
    /// </summary>
    private bool hasHitThisWindow;

    private Collider hitboxCollider;
    private Rigidbody hitboxRigidbody;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;

        // Trigger 检测需要至少一方有 Rigidbody，确保 Hitbox 上有
        hitboxRigidbody = GetComponent<Rigidbody>();
        if (hitboxRigidbody == null)
        {
            hitboxRigidbody = gameObject.AddComponent<Rigidbody>();
            Debug.Log($"{LOG} [Awake] Auto-added Rigidbody on {gameObject.name}");
        }
        hitboxRigidbody.isKinematic = true;
        hitboxRigidbody.useGravity = false;

        Debug.Log($"{LOG} [Awake] Initialized on '{gameObject.name}'. " +
            $"Collider type={hitboxCollider.GetType().Name}, isTrigger={hitboxCollider.isTrigger}, " +
            $"Rigidbody isKinematic={hitboxRigidbody.isKinematic}, " +
            $"targetTag='{targetTag}', layer={LayerMask.LayerToName(gameObject.layer)}");
    }

    /// <summary>
    /// 开启判定窗口（由动画事件调用）
    /// </summary>
    public void EnableHitbox()
    {
        hasHitThisWindow = false;
        hitboxCollider.enabled = true;
        int subscriberCount = OnHit != null ? OnHit.GetInvocationList().Length : 0;
        Debug.Log($"{LOG} [Enable] Hitbox ENABLED on '{gameObject.name}'. " +
            $"OnHit subscribers={subscriberCount}, collider.enabled={hitboxCollider.enabled}, " +
            $"worldPos={transform.position:F2}");
    }

    /// <summary>
    /// 关闭判定窗口（由动画事件调用）
    /// </summary>
    public void DisableHitbox()
    {
        hitboxCollider.enabled = false;
        Debug.Log($"{LOG} [Disable] Hitbox DISABLED on '{gameObject.name}'. " +
            $"hasHitThisWindow={hasHitThisWindow} (did it hit anyone this window?)");
    }

    /// <summary>
    /// 强制关闭并重置状态（用于状态切换时清理）
    /// </summary>
    public void ForceReset()
    {
        hitboxCollider.enabled = false;
        hasHitThisWindow = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"{LOG} [OnTriggerEnter] '{gameObject.name}' touched '{other.gameObject.name}' " +
            $"(tag='{other.tag}', layer={LayerMask.LayerToName(other.gameObject.layer)}). " +
            $"hasHitThisWindow={hasHitThisWindow}, targetTag='{targetTag}', " +
            $"tagMatch={other.CompareTag(targetTag)}");

        if (hasHitThisWindow)
        {
            Debug.Log($"{LOG} [OnTriggerEnter] Skipped — already hit this window.");
            return;
        }

        if (!other.CompareTag(targetTag))
        {
            Debug.Log($"{LOG} [OnTriggerEnter] Skipped — tag mismatch: '{other.tag}' != '{targetTag}'");
            return;
        }

        hasHitThisWindow = true;
        int subscriberCount = OnHit != null ? OnHit.GetInvocationList().Length : 0;
        Debug.Log($"{LOG} [OnTriggerEnter] HIT CONFIRMED on '{gameObject.name}' -> '{other.gameObject.name}'. " +
            $"Invoking OnHit (subscribers={subscriberCount})");
        OnHit?.Invoke(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 用于诊断：如果看到 Stay 但没看到 Enter，说明碰撞体在 Enable 之前就已经重叠了
        if (!hasHitThisWindow && other.CompareTag(targetTag))
        {
            Debug.LogWarning($"{LOG} [OnTriggerStay] '{other.gameObject.name}' is overlapping but OnTriggerEnter was not fired! " +
                $"This means the player collider was already inside the hitbox when it was enabled. " +
                $"Consider using a slightly delayed re-enable or OverlapSphere fallback.");
        }
    }

    /// <summary>
    /// 编辑器中可视化 Hitbox 范围
    /// </summary>
    private void OnDrawGizmos()
    {
        if (hitboxCollider == null || !hitboxCollider.enabled)
            return;

        Gizmos.color = hasHitThisWindow ? Color.green : Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (hitboxCollider is BoxCollider box)
            Gizmos.DrawWireCube(box.center, box.size);
        else if (hitboxCollider is SphereCollider sphere)
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        else if (hitboxCollider is CapsuleCollider capsule)
            Gizmos.DrawWireSphere(capsule.center, capsule.radius);
    }
}
