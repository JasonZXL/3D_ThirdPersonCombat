using UnityEngine;

/// <summary>
/// 基于相机视线的目标选择系统
/// 使用 SphereCast 实现"隐藏准星"的容错选择
/// </summary>
public class TargetSelector : MonoBehaviour
{
    #region 配置参数
    [Header("选择参数")]
    [SerializeField] private float sphereCastRadius = 0.45f;        // SphereCast 半径
    [SerializeField] private float maxRayDistance = 15f;            // 相机射线最大距离
    [SerializeField] private float maxPlayerDistance = 12f;         // 玩家到目标最大有效距离
    
    [Header("引用")]
    [SerializeField] private Transform playerAnchor;                // 玩家位置参考点
    [SerializeField] private Camera mainCamera;                     // 主相机
    [SerializeField] private LayerMask occluderMask;                // 遮挡检测层（包含所有可能挡住视线的对象）
    
    [Header("调试")]
    [SerializeField] private bool showDebugVisuals = true;
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private Color debugRayColor = Color.yellow;
    [SerializeField] private Color debugHitColor = Color.green;
    [SerializeField] private Color debugNoHitColor = Color.red;
    #endregion
    
    #region 选择结果
    private Selectable currentTarget;
    private Vector3 lastHitPoint;
    private bool lastSelectionSuccess;
    #endregion
    
    #region Unity 生命周期
    private void Awake()
    {
        // 自动获取引用
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        if (playerAnchor == null)
        {
            playerAnchor = transform;
            LogWarning("未设置 PlayerAnchor，使用自身 Transform");
        }
    }
    
    private void Start()
    {
        ValidateSetup();
    }
    #endregion
    
    #region 目标选择核心逻辑
    /// <summary>
    /// 尝试选择当前视线中心附近的目标
    /// 必须在相机更新完成后调用（LateUpdate 或之后）
    /// </summary>
    public Selectable TrySelectTarget()
    {
        if (mainCamera == null || playerAnchor == null)
        {
            LogWarning("缺少必要引用，无法进行目标选择");
            return null;
        }
        
        // Step 1: 构建视线
        Vector3 origin = mainCamera.transform.position;
        Vector3 direction = mainCamera.transform.forward;
        
        Log($"🎯 开始目标选择 - 原点: {origin}, 方向: {direction}");
        
        // Step 2: SphereCast 检测
        RaycastHit hit;
        bool hasHit = Physics.SphereCast(
            origin, 
            sphereCastRadius, 
            direction, 
            out hit, 
            maxRayDistance, 
            occluderMask
        );
        
        // 记录用于调试绘制
        lastHitPoint = hasHit ? hit.point : origin + direction * maxRayDistance;
        
        // Step 3: 未命中任何东西
        if (!hasHit)
        {
            Log("❌ SphereCast 未命中任何对象");
            lastSelectionSuccess = false;
            currentTarget = null;
            return null;
        }
        
        Log($"✅ SphereCast 命中: {hit.collider.gameObject.name} (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");
        
        // Step 4: 检查命中对象是否可选择
        Selectable selectable = hit.collider.GetComponent<Selectable>();
        
        if (selectable == null)
        {
            Log($"⚠️ 命中对象 {hit.collider.gameObject.name} 不是可选择目标（无 Selectable 组件）");
            lastSelectionSuccess = false;
            currentTarget = null;
            return null;
        }
        
        // Step 5: 检查是否当前可被选择
        if (!selectable.CanBeSelected())
        {
            Log($"⚠️ 目标 {selectable.gameObject.name} 当前不可选择: {selectable.SelectableReason}");
            lastSelectionSuccess = false;
            currentTarget = null;
            return null;
        }
        
        // Step 6: 距离检查
        Vector3 targetPosition = selectable.GetAimPoint();
        float distance = Vector3.Distance(playerAnchor.position, targetPosition);
        
        if (distance > maxPlayerDistance)
        {
            Log($"⚠️ 目标 {selectable.gameObject.name} 距离过远: {distance:F2}m (最大: {maxPlayerDistance}m)");
            lastSelectionSuccess = false;
            currentTarget = null;
            return null;
        }
        
        // Step 7: 选择成功
        Log($"✅ 成功选择目标: {selectable.gameObject.name} (距离: {distance:F2}m)");
        lastSelectionSuccess = true;
        currentTarget = selectable;
        return selectable;
    }
    
    /// <summary>
    /// 获取当前选中的目标
    /// </summary>
    public Selectable GetCurrentTarget()
    {
        return currentTarget;
    }
    
    /// <summary>
    /// 清除当前目标
    /// </summary>
    public void ClearTarget()
    {
        currentTarget = null;
    }
    #endregion
    
    #region 调试工具
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals || mainCamera == null) return;
        
        Vector3 origin = mainCamera.transform.position;
        Vector3 direction = mainCamera.transform.forward;
        Vector3 endPoint = origin + direction * maxRayDistance;
        
        // 绘制射线
        Gizmos.color = lastSelectionSuccess ? debugHitColor : debugNoHitColor;
        Gizmos.DrawLine(origin, lastHitPoint);
        
        // 绘制 SphereCast 半径（在射线起点和命中点）
        Gizmos.color = debugRayColor;
        Gizmos.DrawWireSphere(origin, sphereCastRadius);
        
        if (lastSelectionSuccess && currentTarget != null)
        {
            Gizmos.color = debugHitColor;
            Gizmos.DrawWireSphere(lastHitPoint, sphereCastRadius);
            
            // 绘制到目标的连线
            Vector3 targetPos = currentTarget.GetAimPoint();
            Gizmos.DrawLine(lastHitPoint, targetPos);
            Gizmos.DrawWireSphere(targetPos, 0.3f);
        }
        else
        {
            Gizmos.color = debugNoHitColor;
            Gizmos.DrawWireSphere(lastHitPoint, sphereCastRadius * 0.5f);
        }
        
        // 绘制最大距离范围（从玩家）
        if (playerAnchor != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(playerAnchor.position, maxPlayerDistance);
        }
    }
    
    private void ValidateSetup()
    {
        if (occluderMask == 0)
        {
            LogWarning("⚠️ OccluderMask 未设置！请在 Inspector 中配置需要检测的层");
        }
        
        if (playerAnchor == null)
        {
            LogWarning("⚠️ PlayerAnchor 未设置！");
        }
    }
    
    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[TargetSelector] {message}");
        }
    }
    
    private void LogWarning(string message)
    {
        if (showDebugLogs)
        {
            Debug.LogWarning($"[TargetSelector] {message}");
        }
    }
    #endregion
    
    #region 公共接口
    public float GetSphereCastRadius() => sphereCastRadius;
    public float GetMaxRayDistance() => maxRayDistance;
    public float GetMaxPlayerDistance() => maxPlayerDistance;
    
    public void SetSphereCastRadius(float radius) => sphereCastRadius = Mathf.Max(0.1f, radius);
    public void SetMaxRayDistance(float distance) => maxRayDistance = Mathf.Max(1f, distance);
    public void SetMaxPlayerDistance(float distance) => maxPlayerDistance = Mathf.Max(1f, distance);
    #endregion
}
