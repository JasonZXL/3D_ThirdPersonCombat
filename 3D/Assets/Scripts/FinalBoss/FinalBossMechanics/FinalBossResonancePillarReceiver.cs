using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FinalBossResonancePillarReceiver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FinalBossResonancePillar pillar;

    [Header("Rules")]
    [SerializeField] private bool requireObjectTag = true;
    [SerializeField] private string objectTag = "Object";
    [SerializeField] private bool requireActiveKnockback = true;
    [SerializeField] private bool stopObjectKnockbackOnValidHit = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        pillar ??= GetComponent<FinalBossResonancePillar>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null) return;
        ProcessHit(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other);
    }

    private void ProcessHit(Collider other)
    {
        if (pillar == null || other == null || !pillar.CanAcceptHits)
            return;

        GameObject sourceRoot = ResolveSourceObject(other);
        if (sourceRoot == null)
            return;

        if (requireObjectTag && !sourceRoot.CompareTag(objectTag))
            return;

        KnockbackSystem knockbackSystem = sourceRoot.GetComponent<KnockbackSystem>();
        if (requireActiveKnockback && (knockbackSystem == null || !knockbackSystem.IsKnockbackActive))
            return;

        if (!pillar.TryRegisterObjectHit(sourceRoot))
            return;

        ColorEventBus.PublishCollision(sourceRoot, pillar.gameObject);

        if (stopObjectKnockbackOnValidHit && knockbackSystem != null)
            knockbackSystem.ForceStopKnockback();

        if (showDebugLogs)
            Debug.Log($"[FinalBossResonancePillarReceiver] Collision event -> {sourceRoot.name} hit {pillar.name}");
    }

    private GameObject ResolveSourceObject(Collider other)
    {
        if (other == null) return null;

        KnockbackSystem knockback = other.GetComponentInParent<KnockbackSystem>();
        if (knockback != null)
            return knockback.gameObject;

        ColorComponent color = other.GetComponentInParent<ColorComponent>();
        if (color != null)
            return color.gameObject;

        return other.gameObject;
    }
}
