using UnityEngine;
using System.Collections;

public class ObjectSpawnPoint : MonoBehaviour
{
    [Header("基本设置")]
    [SerializeField] private GameObject ObjectPrefab;        // 物体预制体
    [SerializeField] private bool showGizmos = true;        // 显示调试图标

    [SerializeField] private float SpawnDelay;   // 物体生成延迟时间

    private GameObject currentObject;

    void Start()
    {
        StartCoroutine(spawnObjectInDelay());
    }

    private IEnumerator spawnObjectInDelay()
    {
        yield return new WaitForSeconds(SpawnDelay);

        SpawnObject();

    }

    private void SpawnObject()
    {
        if (ObjectPrefab == null)
        {
            Debug.LogError("❌ 没有设置物体预制体！");
            return;
        }

        // 创建物体实例
        currentObject = Instantiate(ObjectPrefab, transform.position, transform.rotation);

        Debug.Log($"📦 物体已生成在出生点");
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