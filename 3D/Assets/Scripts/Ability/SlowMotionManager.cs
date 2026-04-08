using UnityEngine;

/// <summary>
/// 慢动作管理器
/// 使用 Time.timeScale 实现全局慢动作，同时补偿玩家和指定对象
/// </summary>
public class SlowMotionManager : MonoBehaviour
{
    #region 单例
    public static SlowMotionManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeSlowMotion();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion
    
    #region 慢动作设置
    [Header("慢动作参数")]
    [SerializeField] private float slowMotionScale = 0.5f;      // 慢动作时间缩放（50%速度）
    [SerializeField] private float transitionSpeed = 5f;        // 过渡速度
    
    [Header("调试")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion
    
    #region 状态变量
    private bool isSlowMotionActive = false;
    private float targetTimeScale = 1f;
    private float currentTimeScale = 1f;
    private float baseFixedDeltaTime;
    #endregion
    
    #region 初始化
    private void InitializeSlowMotion()
    {
        // 保存默认的 FixedDeltaTime
        baseFixedDeltaTime = Time.fixedDeltaTime;
        Log($"✅ SlowMotionManager 初始化完成 (慢动作缩放: {slowMotionScale})");
    }
    #endregion
    
    #region Update
    private void Update()
    {
        // 平滑过渡时间缩放
        if (Mathf.Abs(currentTimeScale - targetTimeScale) > 0.01f)
        {
            currentTimeScale = Mathf.Lerp(currentTimeScale, targetTimeScale, Time.unscaledDeltaTime * transitionSpeed);
            Time.timeScale = currentTimeScale;
            
            // 同步调整 FixedDeltaTime 以保持物理稳定
            Time.fixedDeltaTime = baseFixedDeltaTime * currentTimeScale;
        }
    }
    #endregion
    
    #region 公共接口
    /// <summary>
    /// 启动慢动作
    /// </summary>
    public void StartSlowMotion()
    {
        if (isSlowMotionActive)
        {
            Log("⚠️ 慢动作已经激活");
            return;
        }
        
        isSlowMotionActive = true;
        targetTimeScale = slowMotionScale;
        
        Log($"🐌 启动慢动作 (时间缩放: {slowMotionScale})");
    }
    
    /// <summary>
    /// 停止慢动作
    /// </summary>
    public void StopSlowMotion()
    {
        if (!isSlowMotionActive)
        {
            return;
        }
        
        isSlowMotionActive = false;
        targetTimeScale = 1f;
        
        Log("⚡ 停止慢动作，恢复正常速度");
    }
    
    /// <summary>
    /// 检查慢动作是否激活
    /// </summary>
    public bool IsSlowMotionActive()
    {
        return isSlowMotionActive;
    }
    
    /// <summary>
    /// 获取当前时间缩放
    /// </summary>
    public float GetCurrentTimeScale()
    {
        return currentTimeScale;
    }
    
    /// <summary>
    /// 获取慢动作缩放值
    /// </summary>
    public float GetSlowMotionScale()
    {
        return slowMotionScale;
    }
    
    /// <summary>
    /// 设置慢动作缩放值
    /// </summary>
    public void SetSlowMotionScale(float scale)
    {
        slowMotionScale = Mathf.Clamp(scale, 0.1f, 1f);
        if (isSlowMotionActive)
        {
            targetTimeScale = slowMotionScale;
        }
    }
    #endregion
    
    #region 工具方法
    /// <summary>
    /// 获取未缩放的 DeltaTime（用于玩家和UI）
    /// </summary>
    public static float GetUnscaledDeltaTime()
    {
        return Time.unscaledDeltaTime;
    }
    
    /// <summary>
    /// 获取补偿系数（用于保持正常速度）
    /// </summary>
    public float GetCompensationFactor()
    {
        if (currentTimeScale <= 0.01f) return 1f;
        return 1f / currentTimeScale;
    }
    #endregion
    
    #region 调试
    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SlowMotionManager] {message}");
        }
    }
    
    private void OnDestroy()
    {
        // 恢复正常时间
        if (Instance == this)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDeltaTime;
        }
    }
    #endregion
}
