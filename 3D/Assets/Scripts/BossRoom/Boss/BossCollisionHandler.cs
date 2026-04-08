using UnityEngine;

public class BossCollisionHandler : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private ChasingEnemyStunController stunController;
    [SerializeField] private ColorComponent colorComponent;
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        if (stunController == null)
            stunController = GetComponent<ChasingEnemyStunController>();
        if (colorComponent == null)
            colorComponent = GetComponent<ColorComponent>();
    }

    /// <summary>
    /// 由 KnockbackCollisionDetector 调用，处理物体撞击Boss
    /// </summary>
    /// <param name="sourceObject">撞击Boss的物体</param>
    public void HandleObjectCollision(GameObject sourceObject)
    {
        if (stunController == null || colorComponent == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"⚠️ BossCollisionHandler 缺少必要组件 on {gameObject.name}");
            return;
        }

        // 获取物体的颜色组件
        var sourceColorComp = sourceObject.GetComponent<ColorComponent>();
        if (sourceColorComp == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"⚠️ 撞击物体 {sourceObject.name} 没有 ColorComponent");
            return;
        }

        // 异色触发眩晕（与你要求的“异色物体撞向Boss时触发眩晕”一致）
        if (sourceColorComp.CurrentColor != colorComponent.CurrentColor)
        {
            stunController.StartStun();
            if (showDebugLogs)
                Debug.Log($"🌀 [Boss] 异色物体 {sourceObject.name} 撞击 → 触发眩晕！");
        }
        else
        {
            if (showDebugLogs)
                Debug.Log($"🟢 [Boss] 同色物体撞击，无效果（Boss特殊规则）");
        }
    }
}
