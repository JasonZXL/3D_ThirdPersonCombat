using UnityEngine;

/// <summary>
/// 标记对象可被能力选中
/// 挂载在所有可被能力作用的对象上
/// </summary>
public class Selectable : MonoBehaviour
{
    [Header("选择设置")]
    [SerializeField] private bool isSelectableNow = true;
    [SerializeField] private string selectableReason = "可选择";
    
    [Header("调试")]
    [SerializeField] private bool showDebugLogs = false;
    
    public bool IsSelectableNow 
    { 
        get => isSelectableNow;
        set
        {
            if (isSelectableNow != value)
            {
                isSelectableNow = value;
                if (showDebugLogs)
                {
                    Debug.Log($"{gameObject.name} 可选择状态改变: {value} ({selectableReason})");
                }
            }
        }
    }
    
    public string SelectableReason
    {
        get => selectableReason;
        set => selectableReason = value;
    }
    
    /// <summary>
    /// 检查是否可以被选择
    /// </summary>
    public bool CanBeSelected()
    {
        // 检查是否有 ColorComponent
        ColorComponent colorComp = GetComponent<ColorComponent>();
        if (colorComp == null)
        {
            selectableReason = "缺少 ColorComponent";
            return false;
        }
        
        if (!isSelectableNow)
        {
            return false;
        }
        
        selectableReason = "可选择";
        return true;
    }
    
    /// <summary>
    /// 获取瞄准点（使用碰撞体中心）
    /// </summary>
    public Vector3 GetAimPoint()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            return col.bounds.center;
        }
        return transform.position;
    }
    
    private void OnValidate()
    {
        // 编辑器中检查配置
        if (GetComponent<ColorComponent>() == null)
        {
            Debug.LogWarning($"{gameObject.name}: Selectable 需要 ColorComponent 组件！");
        }
        
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"{gameObject.name}: Selectable 需要 Collider 组件用于检测！");
        }
    }
}
