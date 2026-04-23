using UnityEngine;
using System.Collections.Generic;

public class FinalBossShockwaveProjectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float lifeTime = 2f;
    [SerializeField] private float startScaleXZ = 0.25f;
    [SerializeField] private float endScaleXZ = 10f;
    [SerializeField] private bool preserveYScale = true;
    [SerializeField] private ColorComponent colorComponent;
    [SerializeField] private MeshCollider meshCollider;
    [SerializeField] private Rigidbody physicsBody;
    [SerializeField] private bool showDebugLogs = true;

    private ColorType currentColor = ColorType.Red;
    private readonly HashSet<int> hitPlayerInstanceIds = new HashSet<int>();
    private Vector3 baseScale;
    private float elapsedLifetime;
    public ColorType CurrentColor => currentColor;

    private void Awake()
    {
        colorComponent ??= GetComponent<ColorComponent>();
        meshCollider ??= GetComponent<MeshCollider>();
        physicsBody ??= GetComponent<Rigidbody>();

        if (meshCollider != null)
        {
            meshCollider.convex = true;
            meshCollider.isTrigger = true;
        }

        if (physicsBody != null)
        {
            physicsBody.isKinematic = true;
            physicsBody.useGravity = false;
        }

        baseScale = transform.localScale;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        elapsedLifetime += Time.deltaTime;
        float normalizedLifetime = lifeTime > 0f ? Mathf.Clamp01(elapsedLifetime / lifeTime) : 1f;
        float currentScaleXZ = Mathf.Lerp(startScaleXZ, endScaleXZ, normalizedLifetime);

        float yScale = preserveYScale ? baseScale.y : currentScaleXZ;
        transform.localScale = new Vector3(
            baseScale.x * currentScaleXZ,
            yScale,
            baseScale.z * currentScaleXZ);
    }

    public void SetColor(ColorType color)
    {
        currentColor = color;
        if (colorComponent != null)
            colorComponent.CurrentColor = color;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        GameObject playerObject = other.gameObject;
        int playerId = playerObject.GetInstanceID();
        if (hitPlayerInstanceIds.Contains(playerId))
            return;

        hitPlayerInstanceIds.Add(playerId);
        ColorEventBus.PublishEnemyAttack(gameObject, playerObject);

        if (showDebugLogs)
            Debug.Log($"[FinalBossShockwaveProjectile] PublishEnemyAttack -> player={playerObject.name}, waveColor={currentColor}");
    }
}
