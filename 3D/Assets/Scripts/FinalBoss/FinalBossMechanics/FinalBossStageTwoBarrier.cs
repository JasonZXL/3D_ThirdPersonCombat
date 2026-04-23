using UnityEngine;

public class FinalBossStageTwoBarrier : MonoBehaviour
{
    [Header("Barrier")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool followRotation = false;
    [SerializeField] private SphereCollider fallbackSphereCollider;
    [SerializeField] private bool showDebugLogs = true;

    private Collider[] barrierColliders;

    private void Awake()
    {
        barrierColliders = GetComponentsInChildren<Collider>(true);
        fallbackSphereCollider ??= GetComponent<SphereCollider>();
    }

    public void Initialize(Transform target, Collider[] ignoredColliders)
    {
        followTarget = target;
        barrierColliders ??= GetComponentsInChildren<Collider>(true);
        ForceBarrierSolid();

        if (ignoredColliders == null || barrierColliders == null)
            return;

        for (int i = 0; i < barrierColliders.Length; i++)
        {
            Collider barrierCollider = barrierColliders[i];
            if (barrierCollider == null)
                continue;

            for (int j = 0; j < ignoredColliders.Length; j++)
            {
                Collider ignoredCollider = ignoredColliders[j];
                if (ignoredCollider == null)
                    continue;

                Physics.IgnoreCollision(barrierCollider, ignoredCollider, true);
            }
        }

        SnapToTarget();

        if (showDebugLogs)
            Debug.Log($"[FinalBossStageTwoBarrier] Initialize -> target={(followTarget != null ? followTarget.name : "NULL")}");
    }

    public void ConfigureFallbackSphere(float radius)
    {
        if (fallbackSphereCollider == null)
        {
            fallbackSphereCollider = gameObject.GetComponent<SphereCollider>();
            if (fallbackSphereCollider == null)
                fallbackSphereCollider = gameObject.AddComponent<SphereCollider>();
        }

        fallbackSphereCollider.isTrigger = false;
        fallbackSphereCollider.radius = radius;
        fallbackSphereCollider.center = Vector3.zero;
    }

    private void ForceBarrierSolid()
    {
        if (barrierColliders == null)
            return;

        for (int i = 0; i < barrierColliders.Length; i++)
        {
            Collider barrierCollider = barrierColliders[i];
            if (barrierCollider == null)
                continue;

            barrierCollider.isTrigger = false;
        }
    }

    private void LateUpdate()
    {
        SnapToTarget();
    }

    private void SnapToTarget()
    {
        if (followTarget == null)
            return;

        transform.position = followTarget.position;
        if (followRotation)
            transform.rotation = followTarget.rotation;
    }
}
