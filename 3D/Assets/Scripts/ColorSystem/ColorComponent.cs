using UnityEngine;
using System;

public class ColorComponent : MonoBehaviour
{
    [Header("颜色设置")]
    [SerializeField] private ColorType currentColor = ColorType.Red;
    
    [Header("调试视图")]
    [SerializeField] private bool showDebugLogs = true;
    
    // 颜色改变事件
    public event Action<ColorType, ColorType> OnColorChanged; // 旧颜色, 新颜色
    
    public ColorType CurrentColor 
    { 
        get => currentColor; 
        set
        {
            if (currentColor != value)
            {
                ColorType oldColor = currentColor;
                currentColor = value;
                OnColorChanged?.Invoke(oldColor, currentColor);
                
                if (showDebugLogs)
                    Debug.Log($"{gameObject.name} 颜色改变: {oldColor} -> {currentColor}");
            }
        }
    }
    
    // 切换颜色（用于玩家）
    public void ToggleColor()
    {
        CurrentColor = (currentColor == ColorType.Red) ? ColorType.Blue : ColorType.Red;
    }
    
    // 检查颜色关系
    public bool IsSameColor(ColorComponent other)
    {
        return this.currentColor == other.CurrentColor;
    }
    
    public bool IsOppositeColor(ColorComponent other)
    {
        return (this.currentColor == ColorType.Red && other.CurrentColor == ColorType.Blue) ||
               (this.currentColor == ColorType.Blue && other.CurrentColor == ColorType.Red);
    }
}
