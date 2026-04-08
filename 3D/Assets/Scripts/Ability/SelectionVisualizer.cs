using UnityEngine;

/// <summary>
/// 选中目标的视觉提示系统
/// 在目标上方显示预制体指示器（圆锥尖端朝下）
/// </summary>
public class SelectionVisualizer : MonoBehaviour
{
    [Header("视觉设置")]
    [SerializeField] private GameObject indicatorPrefab;            // 指示器预制体（必需）
    [SerializeField] private Vector3 indicatorOffset = new Vector3(0, 2f, 0); // 指示器位置偏移
    [SerializeField] private float indicatorScale = 1f;             // 指示器缩放
    
    [Header("旋转设置")]
    [SerializeField] private bool flipIndicator = true;             // 是否翻转指示器（Z轴180度）
    [SerializeField] private Vector3 customRotationOffset = Vector3.zero; // 自定义旋转偏移
    
    [Header("动画设置")]
    [SerializeField] private bool enableAnimation = true;
    [SerializeField] private float rotationSpeed = 90f;             // Y轴旋转速度（度/秒）
    [SerializeField] private float pulseSpeed = 2f;                 // 脉动速度
    [SerializeField] private float pulseAmount = 0.2f;              // 脉动幅度
    
    [Header("调试")]
    [SerializeField] private bool showDebugLogs = true;
    
    private GameObject currentIndicator;
    private Transform currentTarget;
    private float animationTime;
    private Quaternion baseRotation;  // 基础旋转（含翻转）
    
    #region 显示/隐藏指示器
    /// <summary>
    /// 在目标上显示选中指示器
    /// </summary>
    public void ShowIndicator(Transform target)
    {
        if (target == null)
        {
            HideIndicator();
            return;
        }
        
        // 如果已经有指示器且目标相同，不重复创建
        if (currentIndicator != null && currentTarget == target)
        {
            return;
        }
        
        // 清除旧指示器
        HideIndicator();
        
        currentTarget = target;
        
        // 创建新指示器
        CreateIndicator(target);
        
        Log($"✅ 显示选中指示器: {target.name}");
    }
    
    /// <summary>
    /// 隐藏指示器
    /// </summary>
    public void HideIndicator()
    {
        if (currentIndicator != null)
        {
            Destroy(currentIndicator);
            currentIndicator = null;
            Log("❌ 隐藏选中指示器");
        }
        
        currentTarget = null;
        animationTime = 0f;
    }
    
    /// <summary>
    /// 检查是否正在显示指示器
    /// </summary>
    public bool IsShowingIndicator()
    {
        return currentIndicator != null;
    }
    #endregion
    
    #region 创建指示器
    private void CreateIndicator(Transform target)
    {
        if (indicatorPrefab == null)
        {
            LogWarning("⚠️ 未设置 Indicator Prefab！无法创建指示器");
            return;
        }
        
        // 计算位置
        Vector3 position = GetIndicatorPosition(target);
        
        // 计算旋转（翻转180度，让圆锥尖端朝下）
        Quaternion rotation = CalculateIndicatorRotation();
        
        // 实例化预制体
        currentIndicator = Instantiate(indicatorPrefab, position, rotation);
        currentIndicator.name = $"Indicator_{target.name}";
        
        // 设置缩放
        currentIndicator.transform.localScale = Vector3.one * indicatorScale;
        
        // 保存基础旋转
        baseRotation = rotation;
        
        Log($"🎨 创建指示器 - 位置: {position}, 旋转: {rotation.eulerAngles}");
    }
    
    /// <summary>
    /// 获取指示器位置（目标位置 + 偏移）
    /// </summary>
    private Vector3 GetIndicatorPosition(Transform target)
    {
        return target.position + indicatorOffset;
    }
    
    /// <summary>
    /// 计算指示器旋转（Z轴180度翻转 + 自定义偏移）
    /// </summary>
    private Quaternion CalculateIndicatorRotation()
    {
        // 基础旋转
        Vector3 eulerRotation = Vector3.zero;
        
        // 如果启用翻转，Z轴旋转180度
        if (flipIndicator)
        {
            eulerRotation.z = 180f;
        }
        
        // 添加自定义旋转偏移
        eulerRotation += customRotationOffset;
        
        return Quaternion.Euler(eulerRotation);
    }
    #endregion
    
    #region Update
    private void Update()
    {
        if (currentIndicator == null || currentTarget == null) return;
        
        // 更新位置（跟随目标）
        currentIndicator.transform.position = GetIndicatorPosition(currentTarget);
        
        // 动画效果（使用 unscaledDeltaTime 不受慢动作影响）
        if (enableAnimation)
        {
            animationTime += Time.unscaledDeltaTime;
            
            // Y轴旋转动画（保持Z轴180度翻转）
            float yRotation = rotationSpeed * animationTime;
            Vector3 currentEuler = baseRotation.eulerAngles;
            currentEuler.y = yRotation;
            currentIndicator.transform.rotation = Quaternion.Euler(currentEuler);
            
            // 脉动效果
            float pulse = 1f + Mathf.Sin(animationTime * pulseSpeed) * pulseAmount;
            currentIndicator.transform.localScale = Vector3.one * indicatorScale * pulse;
        }
    }
    
    private void OnDestroy()
    {
        // 清理指示器
        if (currentIndicator != null)
        {
            Destroy(currentIndicator);
        }
    }
    #endregion
    
    #region 公共接口
    /// <summary>
    /// 动态更新指示器位置偏移
    /// </summary>
    public void UpdateIndicatorOffset(Vector3 newOffset)
    {
        indicatorOffset = newOffset;
    }
    
    /// <summary>
    /// 动态更新指示器缩放
    /// </summary>
    public void UpdateIndicatorScale(float newScale)
    {
        indicatorScale = newScale;
        if (currentIndicator != null)
        {
            currentIndicator.transform.localScale = Vector3.one * newScale;
        }
    }
    
    /// <summary>
    /// 设置自定义旋转偏移
    /// </summary>
    public void SetCustomRotation(Vector3 rotationOffset)
    {
        customRotationOffset = rotationOffset;
        if (currentIndicator != null)
        {
            baseRotation = CalculateIndicatorRotation();
        }
    }
    
    /// <summary>
    /// 获取当前指示器对象
    /// </summary>
    public GameObject GetCurrentIndicator()
    {
        return currentIndicator;
    }
    #endregion
    
    #region 调试
    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SelectionVisualizer] {message}");
        }
    }
    
    private void LogWarning(string message)
    {
        if (showDebugLogs)
        {
            Debug.LogWarning($"[SelectionVisualizer] {message}");
        }
    }
    
    private void OnValidate()
    {
        // 编辑器中验证设置
        if (indicatorPrefab == null)
        {
            Debug.LogWarning($"[SelectionVisualizer] {gameObject.name}: 未设置 Indicator Prefab！");
        }
    }
    #endregion
}