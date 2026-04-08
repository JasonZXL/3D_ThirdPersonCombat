using UnityEngine;
using System.Collections;

public class EnemySpawn : MonoBehaviour
{
    [Header("基本设置")]
    [SerializeField] private GameObject EnemyPrefab;        // 追击敌人预制体
    [SerializeField] private bool showGizmos = true;        // 显示调试图标

    [SerializeField] private float SpawnDelay;   // 敌人生成延迟时间


    private GameObject currentEnemy;

    void Start()
    {
        StartCoroutine(spawnEnemyInDelay());
    }

    private IEnumerator spawnEnemyInDelay()
    {
        yield return new WaitForSeconds(SpawnDelay);

        SpawnEnemy();

    }

    private void SpawnEnemy()
    {
        if (EnemyPrefab == null)
        {
            Debug.LogError("❌ 没有设置敌人预制体！");
            return;
        }

        // 创建敌人实例
        currentEnemy = Instantiate(EnemyPrefab, transform.position, transform.rotation);

        Debug.Log($"👤 敌人已生成在出生点");
    }

    /// <summary>
    /// 调试绘图
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        // 绘制出生点图标
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // 绘制方向箭头
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        
        // 显示标签
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.green;
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up, "出生点", style);
        #endif
    }
}

    
