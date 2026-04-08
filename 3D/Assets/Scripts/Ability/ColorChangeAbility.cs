using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 两阶段颜色变换能力
/// 阶段1：准备 - 按E选择目标并显示Indicator
/// 阶段2：释放 - 再按E对准目标释放能力
/// 必须在 LateUpdate 中执行，确保使用相机最终位置
/// </summary>
public class ColorChangeAbility : MonoBehaviour
{
    #region 能力状态枚举
    public enum AbilityState
    {
        Ready,      // 就绪状态，可以进入准备
        Preparing,  // 准备状态，已选中目标，显示Indicator
        Cooldown    // 冷却状态
    }
    #endregion
    
    #region 组件引用
    [Header("必需组件")]
    [SerializeField] private ThirdPersonCam2 cameraController;   // 相机控制器
    
    private TargetSelector targetSelector;
    private SelectionVisualizer visualizer;
    private SlowMotionManager slowMotionManager;
    private GameObject playerObject;
    #endregion
    
    #region 能力设置
    [Header("能力设置")]
    [SerializeField] private KeyCode abilityKey = KeyCode.E;        // 能力按键
    [SerializeField] private string abilityInputAction = "Ability"; // 新输入系统动作名
    [SerializeField] private bool useNewInputSystem = false;        // 是否使用新输入系统
    
    [Header("准备阶段设置")]
    [SerializeField] private float prepareDuration = 3f;            // 准备阶段有效时间
    [SerializeField] private bool autoReleaseOnSameTarget = true;   // 对准同一目标时自动释放
    
    [Header("反馈设置")]
    [SerializeField] private AudioClip prepareSound;                // 准备音效
    [SerializeField] private AudioClip releaseSuccessSound;         // 释放成功音效
    [SerializeField] private AudioClip releaseFailSound;            // 释放失败音效
    [SerializeField] private AudioClip cancelSound;                 // 取消音效
    [SerializeField] private float feedbackVolume = 0.5f;           // 音量
    
    [Header("冷却设置")]
    [SerializeField] private float cooldownTime = 0.5f;             // 冷却时间
    
    [Header("慢动作设置")]
    [SerializeField] private bool enableSlowMotion = true;          // 是否启用慢动作
    [SerializeField] private float slowMotionScale = 0.3f;          // 慢动作缩放（0.3 = 30%速度）
    
    [Header("调试")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion
    
    #region 状态变量
    private AbilityState currentState = AbilityState.Ready;
    private Selectable preparedTarget;          // 准备阶段选中的目标
    private float prepareStartTime;             // 准备阶段开始时间
    private float cooldownStartTime;            // 冷却开始时间
    #endregion
    
    #region Unity 生命周期
    private void Awake()
    {
        // 获取或添加 TargetSelector
        targetSelector = GetComponent<TargetSelector>();
        if (targetSelector == null)
        {
            targetSelector = gameObject.AddComponent<TargetSelector>();
            Log("✅ 自动添加 TargetSelector 组件");
        }
        
        // 获取或添加 SelectionVisualizer
        visualizer = GetComponent<SelectionVisualizer>();
        if (visualizer == null)
        {
            visualizer = gameObject.AddComponent<SelectionVisualizer>();
            Log("✅ 自动添加 SelectionVisualizer 组件");
        }
        
        // 获取或创建 SlowMotionManager
        slowMotionManager = SlowMotionManager.Instance;
        if (slowMotionManager == null && enableSlowMotion)
        {
            GameObject slowMotionObj = new GameObject("SlowMotionManager");
            slowMotionManager = slowMotionObj.AddComponent<SlowMotionManager>();
            Log("✅ 自动创建 SlowMotionManager");
        }
        
        playerObject = gameObject;
    }
    
    private void Start()
    {
        ValidateSetup();
    }
    
    /// <summary>
    /// 使用 LateUpdate 确保在相机更新之后执行
    /// </summary>
    private void LateUpdate()
    {
        UpdateAbilityState();
        HandleInput();
    }
    #endregion
    
    #region 状态管理
    /// <summary>
    /// 更新能力状态
    /// </summary>
    private void UpdateAbilityState()
    {
        switch (currentState)
        {
            case AbilityState.Preparing:
                UpdatePreparingState();
                break;
                
            case AbilityState.Cooldown:
                UpdateCooldownState();
                break;
        }
    }
    
    /// <summary>
    /// 更新准备状态
    /// </summary>
    private void UpdatePreparingState()
    {
        // 使用 unscaledDeltaTime 确保准备时间不受慢动作影响
        float elapsedTime = Time.unscaledTime - prepareStartTime;
        if (elapsedTime >= prepareDuration)
        {
            Log($"⏱️ 准备超时 ({prepareDuration}秒)，自动取消");
            CancelPrepare();
            return;
        }
        
        // 如果启用了自动释放，持续检测当前瞄准的目标
        if (autoReleaseOnSameTarget && preparedTarget != null)
        {
            Selectable currentTarget = targetSelector.TrySelectTarget();
            
            // 如果当前瞄准的目标就是准备的目标，显示提示
            if (currentTarget == preparedTarget)
            {
                Log($"🎯 对准准备的目标 {preparedTarget.gameObject.name}，可释放能力");
            }
        }
    }
    
    /// <summary>
    /// 更新冷却状态
    /// </summary>
    private void UpdateCooldownState()
    {
        float elapsedTime = Time.time - cooldownStartTime;
        if (elapsedTime >= cooldownTime)
        {
            currentState = AbilityState.Ready;
            Log("✅ 能力冷却完成，进入就绪状态");
        }
    }
    #endregion
    
    #region 输入处理
    private void HandleInput()
    {
        bool abilityPressed = false;
        
        if (useNewInputSystem)
        {
            // 新输入系统支持（需要配置）
            LogWarning("新输入系统支持需要配置 Input Actions");
        }
        else
        {
            // 旧输入系统
            abilityPressed = Input.GetKeyDown(abilityKey);
        }
        
        if (abilityPressed)
        {
            OnAbilityKeyPressed();
        }
    }
    
    /// <summary>
    /// 新输入系统回调
    /// </summary>
    public void OnAbility(InputValue value)
    {
        if (value.isPressed)
        {
            OnAbilityKeyPressed();
        }
    }
    
    /// <summary>
    /// 能力按键按下处理（根据当前状态决定行为）
    /// </summary>
    private void OnAbilityKeyPressed()
    {
        switch (currentState)
        {
            case AbilityState.Ready:
                // 就绪状态 → 进入准备阶段
                TryPrepareAbility();
                break;
                
            case AbilityState.Preparing:
                // 准备状态 → 尝试释放能力
                TryReleaseAbility();
                break;
                
            case AbilityState.Cooldown:
                // 冷却状态 → 提示冷却中
                float remaining = cooldownTime - (Time.time - cooldownStartTime);
                Log($"⏱️ 能力冷却中 (剩余: {remaining:F1}秒)");
                break;
        }
    }
    #endregion
    
    #region 准备阶段（第一次按E）
    /// <summary>
    /// 尝试进入准备阶段
    /// </summary>
    private void TryPrepareAbility()
    {
        Log("🎯 尝试准备能力（第一次按E）");
        
        // 选择目标
        Selectable target = targetSelector.TrySelectTarget();
        
        if (target == null)
        {
            Log("❌ 准备失败：无有效目标");
            PlaySound(releaseFailSound);
            return;
        }
        
        // 进入准备状态
        currentState = AbilityState.Preparing;
        preparedTarget = target;
        prepareStartTime = Time.unscaledTime; // 使用 unscaledTime
        
        // 显示 Indicator
        visualizer.ShowIndicator(target.transform);
        
        // 启动慢动作
        if (enableSlowMotion && slowMotionManager != null)
        {
            slowMotionManager.SetSlowMotionScale(slowMotionScale);
            slowMotionManager.StartSlowMotion();
            Log($"🐌 启动慢动作 (缩放: {slowMotionScale})");
        }
        
        // 播放准备音效
        PlaySound(prepareSound);
        
        Log($"✅ 准备成功：目标 {target.gameObject.name}，有效时间 {prepareDuration}秒");
    }
    
    /// <summary>
    /// 取消准备状态
    /// </summary>
    private void CancelPrepare()
    {
        if (currentState != AbilityState.Preparing) return;
        
        Log("❌ 取消准备状态");
        
        // 隐藏 Indicator
        visualizer.HideIndicator();
        
        // 停止慢动作
        if (enableSlowMotion && slowMotionManager != null)
        {
            slowMotionManager.StopSlowMotion();
            Log("⚡ 停止慢动作");
        }
        
        // 播放取消音效
        PlaySound(cancelSound);
        
        // 返回就绪状态
        currentState = AbilityState.Ready;
        preparedTarget = null;
    }
    #endregion
    
    #region 释放阶段（第二次按E）
    /// <summary>
    /// 尝试释放能力
    /// </summary>
    private void TryReleaseAbility()
    {
        Log("🚀 尝试释放能力（第二次按E）");
        
        if (preparedTarget == null)
        {
            LogWarning("⚠️ 准备目标丢失，取消能力");
            CancelPrepare();
            return;
        }
        
        // 再次检测当前瞄准的目标
        Selectable currentTarget = targetSelector.TrySelectTarget();
        
        // 必须对准同一个目标才能释放
        if (currentTarget != preparedTarget)
        {
            Log($"❌ 释放失败：当前目标 ({currentTarget?.gameObject.name ?? "无"}) 与准备目标 ({preparedTarget.gameObject.name}) 不一致");
            PlaySound(releaseFailSound);
            return;
        }
        
        // 执行能力效果
        ExecuteAbility(preparedTarget);
        
        // 停止慢动作
        if (enableSlowMotion && slowMotionManager != null)
        {
            slowMotionManager.StopSlowMotion();
            Log("⚡ 停止慢动作");
        }
        
        // 进入冷却状态
        currentState = AbilityState.Cooldown;
        cooldownStartTime = Time.time;
        
        // 清理准备状态
        preparedTarget = null;
        
        Log($"✅ 能力释放成功，进入冷却 ({cooldownTime}秒)");
    }
    
    /// <summary>
    /// 执行能力效果：改变颜色 + 发布事件
    /// </summary>
    private void ExecuteAbility(Selectable target)
    {
        GameObject targetObject = target.gameObject;
        
        // 获取目标的颜色组件
        ColorComponent targetColor = targetObject.GetComponent<ColorComponent>();
        if (targetColor == null)
        {
            LogWarning($"⚠️ 目标 {targetObject.name} 缺少 ColorComponent");
            CancelPrepare();
            return;
        }
        
        // 记录变色前的颜色
        ColorType oldColor = targetColor.CurrentColor;
        
        Log($"🎨 开始变色: {targetObject.name} ({oldColor})");
        
        // Step 1: 切换颜色（红<->蓝）
        targetColor.ToggleColor();
        
        ColorType newColor = targetColor.CurrentColor;
        Log($"✅ 变色完成: {targetObject.name} ({oldColor} -> {newColor})");
        
        // Step 2: 发布颜色交互事件
        PublishAbilityEvent(targetObject, oldColor, newColor);
        
        // Step 3: 播放成功反馈
        PlaySound(releaseSuccessSound);
        
        // Step 4: 延迟隐藏视觉提示
        StartCoroutine(HideIndicatorAfterDelay(0.3f));

        // Step 5: 触发教学任务事件
        string currentRoom = TutorialRoomController.GetCurrentRoomName();
        if (!string.IsNullOrEmpty(currentRoom))
        {
            TutorialTaskManager.TriggerColorChanged(currentRoom);
        }
    }
    
    /// <summary>
    /// 发布能力交互事件
    /// </summary>
    private void PublishAbilityEvent(GameObject target, ColorType oldColor, ColorType newColor)
    {
        // 创建能力类型的交互事件
        ColorInteractionEvent abilityEvent = new ColorInteractionEvent(
            playerObject, 
            target, 
            ColorInteractionType.Ability
        );
        
        Log($"📢 发布能力事件: Player({abilityEvent.SourceColor}) -> {target.name}({abilityEvent.TargetColor})");
        Log($"   变色: {oldColor} -> {newColor}");
        
        // 发布事件
        ColorEventBus.PublishColorInteraction(abilityEvent);
    }
    
    private System.Collections.IEnumerator HideIndicatorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        visualizer.HideIndicator();
    }
    #endregion
    
    #region 音效系统
    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, feedbackVolume);
        }
    }
    #endregion
    
    #region 验证与调试
    private void ValidateSetup()
    {
        if (targetSelector == null)
        {
            LogWarning("⚠️ 缺少 TargetSelector 组件！");
        }
        
        if (visualizer == null)
        {
            LogWarning("⚠️ 缺少 SelectionVisualizer 组件！");
        }
        
        if (cameraController == null)
        {
            LogWarning("⚠️ 未设置 ThirdPersonCam 引用！");
        }
        
        ColorComponent playerColor = GetComponent<ColorComponent>();
        if (playerColor == null)
        {
            LogWarning("⚠️ 玩家缺少 ColorComponent！");
        }
        
        Log($"✅ ColorChangeAbility 初始化完成");
        Log($"   按键: {abilityKey}");
        Log($"   准备时间: {prepareDuration}秒");
        Log($"   冷却时间: {cooldownTime}秒");
        Log($"   自动释放: {autoReleaseOnSameTarget}");
    }
    
    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ColorChangeAbility] {message}");
        }
    }
    
    private void LogWarning(string message)
    {
        if (showDebugLogs)
        {
            Debug.LogWarning($"[ColorChangeAbility] {message}");
        }
    }
    #endregion
    
    #region 公共接口
    /// <summary>
    /// 获取当前能力状态
    /// </summary>
    public AbilityState GetCurrentState()
    {
        return currentState;
    }
    
    /// <summary>
    /// 获取准备剩余时间
    /// </summary>
    public float GetPrepareRemainingTime()
    {
        if (currentState != AbilityState.Preparing) return 0f;
        float elapsed = Time.unscaledTime - prepareStartTime; // 使用 unscaledTime
        return Mathf.Max(0f, prepareDuration - elapsed);
    }
    
    /// <summary>
    /// 获取冷却剩余时间
    /// </summary>
    public float GetCooldownRemainingTime()
    {
        if (currentState != AbilityState.Cooldown) return 0f;
        float elapsed = Time.time - cooldownStartTime;
        return Mathf.Max(0f, cooldownTime - elapsed);
    }
    
    /// <summary>
    /// 手动取消准备（例如被打断）
    /// </summary>
    public void CancelAbility()
    {
        if (currentState == AbilityState.Preparing)
        {
            CancelPrepare();
        }
    }
    
    /// <summary>
    /// 获取准备的目标
    /// </summary>
    public Selectable GetPreparedTarget()
    {
        return preparedTarget;
    }
    
    public void SetPrepareDuration(float duration) => prepareDuration = Mathf.Max(0.5f, duration);
    public void SetCooldownTime(float time) => cooldownTime = Mathf.Max(0f, time);
    public void SetAutoRelease(bool enable) => autoReleaseOnSameTarget = enable;
    #endregion
}
