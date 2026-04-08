using UnityEngine;

public class BaseEnemy : MonoBehaviour, IEnemy
{
    [Header("基础敌人设置")]
    [SerializeField] protected ColorType initialColor = ColorType.Red;
    
    [Header("调试")]
    [SerializeField] protected bool showDebugLogs = true;
    
    protected ColorComponent colorComponent;
    
    public ColorComponent GetColorComponent() => colorComponent;
    public GameObject GetGameObject() => gameObject;
    
    protected virtual void Awake()
    {
        // 确保有颜色组件
        colorComponent = GetComponent<ColorComponent>();
        if (colorComponent == null)
        {
            colorComponent = gameObject.AddComponent<ColorComponent>();
        }
        
        // 设置初始颜色
        colorComponent.CurrentColor = initialColor;
        
        Debug.Log($"👹 基础敌人初始化: {gameObject.name}, 颜色: {initialColor}");
    }
    
    protected virtual void OnDestroy()
    {
        // 清理工作
    }
    
    public virtual void OnColorInteraction(ColorInteractionEvent interaction)
    {
        // 基础敌人对颜色交互的反应
        if (showDebugLogs)
            Debug.Log($"👹 敌人 {gameObject.name} 收到颜色交互: {interaction.Type}");
        
        // 基础版本不做特殊处理，留给子类实现
    }
    
    // 通用的颜色改变方法
    public void ChangeColorImmediately()
    {
        if (colorComponent == null) return;
        
        ColorType newColor = (colorComponent.CurrentColor == ColorType.Red) ? ColorType.Blue : ColorType.Red;
        
        if (showDebugLogs)
            Debug.Log($"🔄 敌人 {gameObject.name} 立即改变颜色: {colorComponent.CurrentColor} -> {newColor}");
        
        colorComponent.CurrentColor = newColor;
    }
    
    // 检查是否为同色交互的通用方法
    protected bool CheckForSameColorInteraction(ColorInteractionEvent interaction)
    {
        if (colorComponent == null) return false;
        
        // 如果是敌人攻击玩家
        if (interaction.Type == ColorInteractionType.EnemyAttackPlayer)
        {
            // 检查是否同色
            return interaction.SourceColor == interaction.TargetColor;
        }
        // 如果是玩家攻击敌人
        else if (interaction.Type == ColorInteractionType.PlayerAttackEnemy)
        {
            // 检查是否同色且目标是当前敌人
            if (interaction.Target == gameObject)
            {
                return interaction.SourceColor == interaction.TargetColor;
            }
        }
        
        return false;
    }
}
