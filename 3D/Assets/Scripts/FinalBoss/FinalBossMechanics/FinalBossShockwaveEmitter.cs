using System.Collections;
using UnityEngine;

public class FinalBossShockwaveEmitter : MonoBehaviour
{
    [Header("Emitter")]
    [SerializeField] private FinalBossShockwaveProjectile projectilePrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float interval = 2f;
    [SerializeField] private ColorComponent sourceColorComponent;
    [SerializeField] private ColorType fallbackColor = ColorType.Red;
    [SerializeField] private bool showDebugLogs = true;

    private Coroutine emitRoutine;

    public void SetSourceColorComponent(ColorComponent sourceColor)
    {
        sourceColorComponent = sourceColor;
    }

    public void StartEmitter()
    {
        if (emitRoutine != null) return;
        emitRoutine = StartCoroutine(EmitRoutine());
    }

    public void StopEmitter()
    {
        if (emitRoutine == null) return;
        StopCoroutine(emitRoutine);
        emitRoutine = null;
    }

    /// <summary>
    /// 单次发射一枚冲击波（不启动循环）。
    /// </summary>
    public void FireOnce()
    {
        SpawnProjectile();
    }

    private IEnumerator EmitRoutine()
    {
        while (true)
        {
            SpawnProjectile();
            yield return new WaitForSeconds(interval);
        }
    }

    private void SpawnProjectile()
    {
        if (projectilePrefab == null || spawnPoint == null) return;

        ColorType waveColor = sourceColorComponent != null ? sourceColorComponent.CurrentColor : fallbackColor;
        FinalBossShockwaveProjectile projectile = Instantiate(projectilePrefab, spawnPoint.position, spawnPoint.rotation);
        projectile.SetColor(waveColor);

        if (showDebugLogs)
            Debug.Log($"[FinalBossShockwaveEmitter] SpawnProjectile -> expanding torus color={waveColor}");
    }
}
