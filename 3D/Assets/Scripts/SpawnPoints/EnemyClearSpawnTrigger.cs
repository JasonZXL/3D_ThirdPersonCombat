using UnityEngine;
using System.Collections;

public class EnemyClearSpawnTrigger : MonoBehaviour
{
    [Header("敌人检测")]
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float checkStartDelay = 3f;

    [Header("生成设置")]
    [SerializeField] private GameObject prefabToSpawn;
    [SerializeField] private Transform spawnPoint;

    [Header("UI 控制")]
    [SerializeField] private GameObject targetUI;
    [SerializeField] private bool hideUIOnStart = true;
    [SerializeField] private float uiShowDuration = 2f;

    [Header("调试")]
    [SerializeField] private bool showDebugLog = true;

    private bool hasSpawned = false;
    private float timer = 0f;
    private bool canStartChecking = false;

    private void Start()
    {
        if (hideUIOnStart && targetUI != null)
        {
            targetUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (hasSpawned) return;

        if (!canStartChecking)
        {
            timer += Time.deltaTime;

            if (timer >= checkStartDelay)
            {
                canStartChecking = true;

                if (showDebugLog)
                {
                    Debug.Log($"[EnemyClearSpawnTrigger] 延迟结束，开始检查场上敌人，延迟时长: {checkStartDelay:F1}s");
                }
            }

            return;
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);

        if (enemies.Length == 0)
        {
            OnEnemiesCleared();
        }
    }

    private void OnEnemiesCleared()
    {
        hasSpawned = true;

        SpawnPrefab();
        ShowUI();

        if (showDebugLog)
        {
            Debug.Log("[EnemyClearSpawnTrigger] 场上敌人已清空，已执行生成与 UI 显示逻辑");
        }
    }

    private void SpawnPrefab()
    {
        if (prefabToSpawn == null || spawnPoint == null) return;

        Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);

        if (showDebugLog)
        {
            Debug.Log($"[EnemyClearSpawnTrigger] 已生成 Prefab: {prefabToSpawn.name}");
        }
    }

    private void ShowUI()
    {
        if (targetUI == null) return;

        targetUI.SetActive(true);

        if (showDebugLog)
        {
            Debug.Log($"[EnemyClearSpawnTrigger] 已打开 UI: {targetUI.name}，持续时间: {uiShowDuration:F1}s");
        }

        if (uiShowDuration > 0f)
        {
            StartCoroutine(HideUIAfterDelay(uiShowDuration));
        }
    }

    private IEnumerator HideUIAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (targetUI != null)
        {
            targetUI.SetActive(false);

            if (showDebugLog)
            {
                Debug.Log($"[EnemyClearSpawnTrigger] 已关闭 UI: {targetUI.name}");
            }
        }
    }

    public void ResetTrigger()
    {
        hasSpawned = false;
        timer = 0f;
        canStartChecking = false;

        if (hideUIOnStart && targetUI != null)
        {
            targetUI.SetActive(false);
        }

        if (showDebugLog)
        {
            Debug.Log("[EnemyClearSpawnTrigger] 触发器已重置");
        }
    }
}
