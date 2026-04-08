using UnityEngine;

public class ColorAutoChangeBehavior : MonoBehaviour
{
    [Header("自动颜色转换设置")]
    [SerializeField] private float autoColorChangeInterval = 5f;
    [SerializeField] private bool startOnAwake = true;
    
    [Header("同色交互触发")]
    [SerializeField] private bool changeOnSameColorAttack = true;
    [SerializeField] private bool changeOnSameColorHit = true;
    
    [Header("调试")]
    [SerializeField] private bool showDebugLogs = true;
    
    private ColorComponent colorComponent;
    private IEnemy enemyInterface;
    private float colorChangeTimer;
    private bool isEnabled = true;
    
    public float CurrentTimer => colorChangeTimer;
    public bool IsEnabled => isEnabled;
    
    private void Awake()
    {
        // 获取必要的组件
        colorComponent = GetComponent<ColorComponent>();
        enemyInterface = GetComponent<IEnemy>();
        
        if (colorComponent == null)
        {
            Debug.LogError($"❌ ColorAutoChangeBehavior 需要 ColorComponent 组件: {gameObject.name}");
            enabled = false;
            return;
        }
        
        // 重置计时器
        ResetColorChangeTimer();
        
        // 订阅颜色改变事件
        colorComponent.OnColorChanged += OnColorComponentChanged;
        
        // 如果实现了IEnemy，订阅交互事件
        if (enemyInterface != null)
        {
            // 创建一个包装器来拦截OnColorInteraction调用
            StartCoroutine(SetupEnemyInteraction());
        }
        
        Debug.Log($"🔄 自动颜色转换行为初始化: {gameObject.name}, 间隔: {autoColorChangeInterval}s");
    }
    
    private System.Collections.IEnumerator SetupEnemyInteraction()
    {
        // 等待一帧确保BaseEnemy已经初始化
        yield return null;
        
        // 使用反射或事件系统来拦截IEnemy的OnColorInteraction调用
        // 这里我们使用一个简单的解决方案：添加一个监听器到事件总线
        // 或者让敌人手动调用这个组件的方法
    }
    
    private void Start()
    {
        isEnabled = startOnAwake;
    }
    
    private void Update()
    {
        if (!isEnabled) return;
        
        UpdateColorChangeTimer();
    }
    
    private void OnDestroy()
    {
        if (colorComponent != null)
        {
            colorComponent.OnColorChanged -= OnColorComponentChanged;
        }
    }
    
    // 处理敌人的颜色交互（需要从敌人的OnColorInteraction中调用）
    public void HandleEnemyColorInteraction(ColorInteractionEvent interaction)
    {
        if (!isEnabled) return;
        
        // 检查是否应该触发立即颜色转换
        bool shouldChangeImmediately = false;
        
        if (changeOnSameColorAttack && 
            interaction.Type == ColorInteractionType.EnemyAttackPlayer &&
            CheckForSameColorInteraction(interaction))
        {
            shouldChangeImmediately = true;
            if (showDebugLogs)
                Debug.Log($"⚡ 同色攻击触发立即颜色转换: {gameObject.name}");
        }
        else if (changeOnSameColorHit && 
                 interaction.Type == ColorInteractionType.PlayerAttackEnemy &&
                 CheckForSameColorInteraction(interaction))
        {
            shouldChangeImmediately = true;
            if (showDebugLogs)
                Debug.Log($"⚡ 被同色攻击触发立即颜色转换: {gameObject.name}");
        }
        
        if (shouldChangeImmediately)
        {
            ChangeColorImmediately();
            ResetColorChangeTimer();
        }
    }
    
    // 检查是否为同色交互
    private bool CheckForSameColorInteraction(ColorInteractionEvent interaction)
    {
        if (colorComponent == null) return false;
        
        // 根据交互类型判断
        switch (interaction.Type)
        {
            case ColorInteractionType.EnemyAttackPlayer:
                return interaction.SourceColor == interaction.TargetColor;
                
            case ColorInteractionType.PlayerAttackEnemy:
                return interaction.SourceColor == interaction.TargetColor && 
                       interaction.Target == gameObject;
                
            default:
                return false;
        }
    }
    
    // 立即改变颜色
    public void ChangeColorImmediately()
    {
        if (colorComponent == null) return;
        
        ColorType newColor = (colorComponent.CurrentColor == ColorType.Red) ? ColorType.Blue : ColorType.Red;
        
        if (showDebugLogs)
            Debug.Log($"🔄 {gameObject.name} 立即改变颜色: {colorComponent.CurrentColor} -> {newColor}");
        
        colorComponent.CurrentColor = newColor;
    }
    
    // 更新颜色转换计时器
    private void UpdateColorChangeTimer()
    {
        colorChangeTimer -= Time.deltaTime;
        
        if (colorChangeTimer <= 0f)
        {
            // 自动转换颜色
            ChangeColorImmediately();
            
            // 重置计时器
            ResetColorChangeTimer();
        }
    }
    
    // 重置颜色转换计时器
    public void ResetColorChangeTimer()
    {
        colorChangeTimer = autoColorChangeInterval;
    }
    
    // 颜色组件改变时的回调
    private void OnColorComponentChanged(ColorType oldColor, ColorType newColor)
    {
        if (showDebugLogs)
            Debug.Log($"🎨 {gameObject.name} 颜色已改变: {oldColor} -> {newColor}");
    }
    
    // 启用/禁用自动转换
    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        
        if (showDebugLogs)
            Debug.Log($"⏸️ {gameObject.name} 自动颜色转换: {(enabled ? "启用" : "禁用")}");
    }
    
    // 设置自动转换间隔
    public void SetAutoColorChangeInterval(float interval)
    {
        autoColorChangeInterval = Mathf.Max(0.1f, interval);
        ResetColorChangeTimer();
        
        if (showDebugLogs)
            Debug.Log($"⏱️ {gameObject.name} 自动转换间隔设置为: {autoColorChangeInterval}s");
    }
    
    // 获取剩余时间
    public float GetRemainingTime()
    {
        return colorChangeTimer;
    }
}
