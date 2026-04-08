using UnityEngine;

public class PlayerColorController : MonoBehaviour
{
    #region 颜色切换设置
    [Header("颜色切换设置")]
    [SerializeField] private float colorSwitchCooldown = 2f;
    [SerializeField] private KeyCode switchKey = KeyCode.Space;
    #endregion

    #region 调试设置
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion

    #region 内部变量
    private ColorComponent colorComponent;
    private float currentCooldown = 0f;
    private bool canSwitchColor = true;
    
    public float CurrentCooldown => currentCooldown;
    public float CooldownPercentage => Mathf.Clamp01(currentCooldown / colorSwitchCooldown);
    #endregion

    #region Unity生命周期
    private void Awake()
    {
        colorComponent = GetComponent<ColorComponent>();
        if (colorComponent == null)
        {
            Debug.LogError("❌ PlayerColorController 需要 ColorComponent!");
        }
    }
    
    private void Update()
    {
        // 更新冷却时间
        if (!canSwitchColor)
        {
            currentCooldown -= Time.deltaTime;
            if (currentCooldown <= 0f)
            {
                canSwitchColor = true;
                currentCooldown = 0f;
                
                if (showDebugLogs)
                    Debug.Log("✅ 颜色切换冷却完毕");
            }
        }
        
        // 检测切换输入
        if (canSwitchColor && Input.GetKeyDown(switchKey))
        {
            SwitchColor();
        }
    }
    #endregion

    #region 颜色控制方法
    /// <summary>切换颜色</summary>
    public void SwitchColor()
    {
        if (canSwitchColor && colorComponent != null)
        {
            colorComponent.ToggleColor();
            currentCooldown = colorSwitchCooldown;
            canSwitchColor = false;
            
            if (showDebugLogs)
                Debug.Log($"🎨 玩家切换颜色到: {colorComponent.CurrentColor}, 进入冷却: {colorSwitchCooldown}秒");
        }
    }
    
    /// <summary>减少冷却时间</summary>
    public void ReduceCooldown(float reduction)
    {
        currentCooldown = Mathf.Max(0f, currentCooldown - reduction);
        
        if (showDebugLogs)
            Debug.Log($"⏩ 冷却时间减少 {reduction}秒, 剩余冷却: {currentCooldown}秒");
        
        if (currentCooldown <= 0f)
        {
            canSwitchColor = true;
        }
    }
    #endregion
}