using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class HeartUIController : MonoBehaviour
{
    #region 心形设置
    [Header("心形设置")]
    [SerializeField] private GameObject heartPrefab;        // 心形预制体
    [SerializeField] private Transform heartsContainer;     // 心形容器
    [SerializeField] private Sprite fullHeartSprite;       // 实心心形
    [SerializeField] private Sprite emptyHeartSprite;      // 空心心形
    
    [Header("自动查找设置")]
    [SerializeField] private bool autoFindContainer = true;    // 自动查找容器
    [SerializeField] private string containerTag = "HeartUI";  // 容器标签
    [SerializeField] private string containerName = "HeartsContainer"; // 容器名称
    [SerializeField] private bool autoInitialize = true;       // 自动初始化
    
    [Header("布局设置")]
    [SerializeField] private float heartSpacing = 10f;     // 心形间距
    [SerializeField] private Vector2 heartSize = new Vector2(40, 40); // 心形大小
    [SerializeField] private HeartAlignment alignment = HeartAlignment.Center; // 对齐方式
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0, -50);   // 锚定位置
    
    [Header("动画设置")]
    [SerializeField] private bool enablePulseAnimation = true; // 启用脉动动画
    [SerializeField] private float pulseSpeed = 8f;            // 脉动速度
    [SerializeField] private float pulseIntensity = 0.2f;      // 脉动强度
    [SerializeField] private float flashSpeed = 10f;           // 闪烁速度

    #endregion

    #region 调试设置
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showGizmos = false;
    #endregion

    #region 枚举定义
    public enum HeartAlignment
    {
        Left,
        Center,
        Right
    }
    #endregion

    #region 内部变量
    private List<GameObject> hearts = new List<GameObject>();  // 所有心形实例
    private HealthSystem healthSystem;                         // 血量系统
    private bool isInitialized = false;                        // 是否已初始化
    private Canvas uiCanvas;                                   // UI画布
    private GameObject lastHeartPulseTarget;                   // 最后一颗心的脉动目标
    private Coroutine pulseCoroutine;                          // 脉动协程
    private bool isPlayer = false;                             // 是否为玩家
    private bool isDestroying = false;                         // 是否正在销毁
    #endregion

    #region Unity生命周期
    private void Awake()
    {
        // 检查是否为玩家
        isPlayer = gameObject.CompareTag("Player");
        
        if (showDebugLogs)
            Debug.Log($"🔄 HeartUIController Awake: {gameObject.name} (玩家: {isPlayer})");
    }
    
    private void Start()
    {
        if (autoInitialize)
        {
            TryInitialize();
        }
    }
    
    private void OnEnable()
    {
        // 如果之前初始化失败，重新尝试
        if (!isInitialized && autoInitialize)
        {
            TryInitialize();
        }
    }
    
    private void OnDisable()
    {
        // 停止脉动动画
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
    }
    
    private void OnDestroy()
    {
        isDestroying = true;
        
        if (healthSystem != null)
        {
            healthSystem.OnHeartsChanged -= UpdateHeartsDisplay;
        }
        
        // 清理心形对象
        CleanupHearts();
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        // 绘制容器位置
        if (heartsContainer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(heartsContainer.position, 0.1f);
            
            // 绘制心形容器边界
            if (hearts.Count > 0)
            {
                float totalWidth = (hearts.Count - 1) * (heartSize.x + heartSpacing);
                Vector3 centerPos = heartsContainer.position;
                Vector3 leftPos = centerPos + heartsContainer.right * (-totalWidth / 2f);
                Vector3 rightPos = centerPos + heartsContainer.right * (totalWidth / 2f);
                
                Gizmos.DrawLine(leftPos, rightPos);
                Gizmos.DrawSphere(leftPos, 0.05f);
                Gizmos.DrawSphere(rightPos, 0.05f);
            }
        }
    }
    #endregion

    #region 初始化
    /// <summary>
    /// 尝试初始化
    /// </summary>
    private void TryInitialize()
    {
        if (isInitialized || isDestroying) return;
        
        if (showDebugLogs)
            Debug.Log($"🔄 HeartUIController 尝试初始化: {gameObject.name}");
        
        // 查找血量系统
        FindHealthSystem();
        
        if (healthSystem == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"⚠️ HeartUIController 在 {gameObject.name} 上找不到 HealthSystem 组件");
            return;
        }
        
        // 确保UI容器存在
        EnsureUIContainerExists();
        
        if (heartsContainer == null)
        {
            if (showDebugLogs)
                Debug.LogError($"❌ HeartUIController 在 {gameObject.name} 上 heartsContainer 为空");
            return;
        }
        
        if (heartPrefab == null)
        {
            if (showDebugLogs)
                Debug.LogError($"❌ HeartUIController 在 {gameObject.name} 上 heartPrefab 为空");
            return;
        }
        
        // 订阅血量变化事件
        healthSystem.OnHeartsChanged += UpdateHeartsDisplay;
        
        // 初始化心形显示
        InitializeHearts(healthSystem.MaxHearts);
        UpdateHeartsDisplay(healthSystem.CurrentHearts, healthSystem.MaxHearts);
        
        isInitialized = true;
        
        if (showDebugLogs)
            Debug.Log($"✅ HeartUIController 初始化完成: {gameObject.name}, {healthSystem.MaxHearts}颗心");
    }
    
    /// <summary>
    /// 查找血量系统
    /// </summary>
    private void FindHealthSystem()
    {
        // 尝试在父对象上查找
        healthSystem = GetComponentInParent<HealthSystem>();
        
        // 尝试在自己身上查找
        if (healthSystem == null)
        {
            healthSystem = GetComponent<HealthSystem>();
        }
        
        // 如果还没找到，尝试查找BaseEnemy的HealthSystem
        if (healthSystem == null)
        {
            BaseEnemy baseEnemy = GetComponent<BaseEnemy>();
            if (baseEnemy != null)
            {
                healthSystem = baseEnemy.GetComponent<HealthSystem>();
            }
        }
    }
    
    /// <summary>
    /// 确保UI容器存在
    /// </summary>
    private void EnsureUIContainerExists()
    {
        // 如果容器已经设置，直接返回
        if (heartsContainer != null) return;
        
        if (autoFindContainer)
        {
            // 尝试通过标签查找
            if (!string.IsNullOrEmpty(containerTag))
            {
                GameObject taggedContainer = GameObject.FindGameObjectWithTag(containerTag);
                if (taggedContainer != null)
                {
                    heartsContainer = taggedContainer.transform;
                    if (showDebugLogs)
                        Debug.Log($"🏷️ 通过标签找到容器: {containerTag}");
                    return;
                }
            }
            
            // 尝试通过名称查找
            if (!string.IsNullOrEmpty(containerName))
            {
                GameObject namedContainer = GameObject.Find(containerName);
                if (namedContainer != null)
                {
                    heartsContainer = namedContainer.transform;
                    if (showDebugLogs)
                        Debug.Log($"🔍 通过名称找到容器: {containerName}");
                    return;
                }
            }
            
            // 在子对象中查找Canvas
            Canvas childCanvas = GetComponentInChildren<Canvas>();
            if (childCanvas != null)
            {
                // 在Canvas下查找容器
                Transform containerTransform = childCanvas.transform.Find(containerName);
                if (containerTransform != null)
                {
                    heartsContainer = containerTransform;
                    if (showDebugLogs)
                        Debug.Log($"🎨 找到子Canvas中的容器: {containerName}");
                }
                else
                {
                    // 将Canvas作为容器
                    heartsContainer = childCanvas.transform;
                    if (showDebugLogs)
                        Debug.Log($"🎨 使用子Canvas作为容器");
                }
                uiCanvas = childCanvas;
                return;
            }
            
            // 在父对象中查找Canvas
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                // 在父Canvas下查找容器
                Transform containerTransform = parentCanvas.transform.Find(containerName);
                if (containerTransform != null)
                {
                    heartsContainer = containerTransform;
                }
                else
                {
                    // 创建新容器
                    heartsContainer = CreateNewContainer(parentCanvas.transform);
                }
                uiCanvas = parentCanvas;
                return;
            }
            
            // 创建新的Canvas和容器
            CreateNewCanvasAndContainer();
        }
    }
    
    /// <summary>
    /// 创建新的Canvas和容器
    /// </summary>
    private void CreateNewCanvasAndContainer()
    {
        // 创建Canvas
        GameObject canvasObj = new GameObject("HealthCanvas");
        uiCanvas = canvasObj.AddComponent<Canvas>();
        
        // 创建容器
        heartsContainer = CreateNewContainer(uiCanvas.transform);
        
        if (showDebugLogs)
            Debug.Log($"🆕 创建新的Canvas和容器: {gameObject.name}");
    }
    
    /// <summary>
    /// 创建新容器
    /// </summary>
    private Transform CreateNewContainer(Transform parent)
    {
        GameObject containerObj = new GameObject("HeartsContainer");
        containerObj.transform.SetParent(parent);
        containerObj.transform.localPosition = Vector3.zero;
        containerObj.transform.localRotation = Quaternion.identity;
        containerObj.transform.localScale = Vector3.one;
        
        // 添加RectTransform
        RectTransform rect = containerObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        
        return containerObj.transform;
    }
    
    #endregion

    #region 心形UI管理
    /// <summary>
    /// 手动初始化UI
    /// </summary>
    public void ManualInitialize(HealthSystem targetHealthSystem)
    {
        if (isInitialized || isDestroying) return;
        
        healthSystem = targetHealthSystem;
        
        if (healthSystem == null)
        {
            Debug.LogError($"❌ HeartUIController 无法初始化: HealthSystem为空");
            return;
        }
        
        // 确保有容器
        EnsureUIContainerExists();
        
        if (heartsContainer == null)
        {
            Debug.LogError($"❌ HeartUIController 容器为空，无法初始化");
            return;
        }
        
        if (heartPrefab == null)
        {
            Debug.LogError($"❌ HeartUIController heartPrefab为空，无法初始化");
            return;
        }
        
        // 订阅事件
        healthSystem.OnHeartsChanged += UpdateHeartsDisplay;
        
        // 初始化心形
        InitializeHearts(healthSystem.MaxHearts);
        UpdateHeartsDisplay(healthSystem.CurrentHearts, healthSystem.MaxHearts);
        
        isInitialized = true;
        
        if (showDebugLogs)
            Debug.Log($"🎮 HeartUIController 手动初始化完成: {gameObject.name}, {healthSystem.MaxHearts}颗心");
    }
    
    /// <summary>
    /// 初始化心形UI
    /// </summary>
    public void InitializeHearts(int heartCount)
    {
        if (heartsContainer == null)
        {
            Debug.LogError($"❌ 无法初始化心形: heartsContainer为空");
            return;
        }
        
        if (heartPrefab == null)
        {
            Debug.LogError($"❌ 无法初始化心形: heartPrefab为空");
            return;
        }
        
        // 清空现有心形
        CleanupHearts();
        
        // 创建新心形
        for (int i = 0; i < heartCount; i++)
        {
            GameObject heartObj = Instantiate(heartPrefab, heartsContainer);
            
            if (heartObj == null)
            {
                Debug.LogError($"❌ 创建心形失败: 第{i+1}个");
                continue;
            }
            
            // 设置名称
            heartObj.name = $"Heart_{i}";
            
            // 设置大小
            RectTransform rect = heartObj.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = heartSize;
            }
            
            hearts.Add(heartObj);
        }
        
        UpdateHeartPositions();
    }
    
    /// <summary>
    /// 清理心形对象
    /// </summary>
    private void CleanupHearts()
    {
        foreach (GameObject heart in hearts)
        {
            if (heart != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(heart);
                }
                else
                {
                    DestroyImmediate(heart);
                }
            }
        }
        hearts.Clear();
    }
    
    /// <summary>
    /// 更新心形位置
    /// </summary>
    private void UpdateHeartPositions()
    {
        if (hearts.Count == 0 || heartsContainer == null) return;
        
        float totalWidth = (hearts.Count - 1) * (heartSize.x + heartSpacing);
        float startX = 0f;
        
        // 根据对齐方式计算起始位置
        switch (alignment)
        {
            case HeartAlignment.Left:
                startX = 0f;
                break;
            case HeartAlignment.Center:
                startX = -totalWidth / 2f;
                break;
            case HeartAlignment.Right:
                startX = -totalWidth;
                break;
        }
        
        for (int i = 0; i < hearts.Count; i++)
        {
            if (hearts[i] != null)
            {
                RectTransform rect = hearts[i].GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(
                        startX + i * (heartSize.x + heartSpacing),
                        0
                    );
                }
            }
        }
    }
    
    /// <summary>
    /// 更新心形显示
    /// </summary>
    public void UpdateHeartsDisplay(int currentHearts, int maxHearts)
    {
        if (!isInitialized || heartsContainer == null) return;
        
        // 检查心形数量是否匹配
        if (hearts.Count != maxHearts)
        {
            InitializeHearts(maxHearts);
        }
        
        // 更新每颗心的状态
        for (int i = 0; i < hearts.Count; i++)
        {
            Image heartImage = hearts[i].GetComponent<Image>();
            if (heartImage != null)
            {
                // 前面的心是实心，后面的是空心
                if (i < currentHearts)
                {
                    if (fullHeartSprite != null)
                    {
                        heartImage.sprite = fullHeartSprite;
                        heartImage.color = Color.white;
                    }
                }
                else
                {
                    if (emptyHeartSprite != null)
                    {
                        heartImage.sprite = emptyHeartSprite;
                        heartImage.color = Color.white;
                    }
                }
            }
        }
        
        // 低血量警告效果
        if (enablePulseAnimation && currentHearts <= 1 && hearts.Count > 0)
        {
            StartLastHeartPulse();
        }
        else
        {
            StopLastHeartPulse();
        }
    }
    
    /// <summary>
    /// 开始最后一颗心脉动效果
    /// </summary>
    private void StartLastHeartPulse()
    {
        if (hearts.Count == 0) return;
        
        GameObject lastHeart = hearts[hearts.Count - 1];
        if (lastHeart != lastHeartPulseTarget)
        {
            StopLastHeartPulse();
            lastHeartPulseTarget = lastHeart;
            
            if (pulseCoroutine == null)
            {
                pulseCoroutine = StartCoroutine(LastHeartPulseRoutine());
            }
        }
    }
    
    /// <summary>
    /// 停止最后一颗心脉动效果
    /// </summary>
    private void StopLastHeartPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        
        if (lastHeartPulseTarget != null)
        {
            lastHeartPulseTarget.transform.localScale = Vector3.one;
            Image heartImage = lastHeartPulseTarget.GetComponent<Image>();
            if (heartImage != null)
            {
                heartImage.color = Color.white;
            }
            lastHeartPulseTarget = null;
        }
    }
    
    /// <summary>
    /// 最后一颗心脉动协程
    /// </summary>
    private IEnumerator LastHeartPulseRoutine()
    {
        while (lastHeartPulseTarget != null && healthSystem != null && healthSystem.CurrentHearts <= 1)
        {
            if (lastHeartPulseTarget != null)
            {
                Image heartImage = lastHeartPulseTarget.GetComponent<Image>();
                if (heartImage != null && heartImage.sprite == fullHeartSprite)
                {
                    // 脉动动画
                    float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity + 1f;
                    lastHeartPulseTarget.transform.localScale = Vector3.one * pulse;
                    
                    // 闪烁颜色
                    if (Mathf.Sin(Time.time * flashSpeed) > 0)
                    {
                        heartImage.color = Color.red;
                    }
                    else
                    {
                        heartImage.color = Color.white;
                    }
                }
            }
            yield return null;
        }
        
        // 恢复正常
        StopLastHeartPulse();
    }
    #endregion

    #region 公共方法
    /// <summary>
    /// 手动设置容器
    /// </summary>
    public void SetHeartsContainer(Transform container)
    {
        heartsContainer = container;
        
        if (isInitialized)
        {
            // 重新初始化心形
            if (healthSystem != null)
            {
                InitializeHearts(healthSystem.MaxHearts);
                UpdateHeartsDisplay(healthSystem.CurrentHearts, healthSystem.MaxHearts);
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"✅ 手动设置容器: {container?.name}");
    }
    
    /// <summary>
    /// 手动设置心形预制体
    /// </summary>
    public void SetHeartPrefab(GameObject prefab)
    {
        heartPrefab = prefab;
        
        if (isInitialized && healthSystem != null)
        {
            InitializeHearts(healthSystem.MaxHearts);
            UpdateHeartsDisplay(healthSystem.CurrentHearts, healthSystem.MaxHearts);
        }
    }
    
    /// <summary>
    /// 手动设置心形精灵
    /// </summary>
    public void SetHeartSprites(Sprite fullSprite, Sprite emptySprite)
    {
        fullHeartSprite = fullSprite;
        emptyHeartSprite = emptySprite;
        
        if (isInitialized && healthSystem != null)
        {
            UpdateHeartsDisplay(healthSystem.CurrentHearts, healthSystem.MaxHearts);
        }
    }
    
    /// <summary>
    /// 获取当前心形容器
    /// </summary>
    public Transform GetHeartsContainer()
    {
        return heartsContainer;
    }
    
    /// <summary>
    /// 获取血量系统
    /// </summary>
    public HealthSystem GetHealthSystem()
    {
        return healthSystem;
    }
    
    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized;
    }
    
    /// <summary>
    /// 手动刷新UI
    /// </summary>
    [ContextMenu("手动刷新UI")]
    public void RefreshUI()
    {
        if (healthSystem != null && isInitialized)
        {
            UpdateHeartsDisplay(healthSystem.CurrentHearts, healthSystem.MaxHearts);
            
            if (showDebugLogs)
                Debug.Log($"🔄 手动刷新血量UI: {healthSystem.CurrentHearts}/{healthSystem.MaxHearts}");
        }
        else if (!isInitialized)
        {
            TryInitialize();
        }
    }
    
    /// <summary>
    /// 强制重新初始化
    /// </summary>
    [ContextMenu("强制重新初始化")]
    public void ForceReinitialize()
    {
        isInitialized = false;
        TryInitialize();
    }
    
    /// <summary>
    /// 显示/隐藏UI
    /// </summary>
    public void SetUIVisible(bool visible)
    {
        if (heartsContainer != null)
        {
            heartsContainer.gameObject.SetActive(visible);
        }
    }
    #endregion
}

#region 辅助组件
/// <summary>
/// 使UI始终面向摄像机
/// </summary>
public class FaceCamera : MonoBehaviour
{
    private Transform cameraTransform;
    
    void Start()
    {
        cameraTransform = Camera.main?.transform;
    }
    
    void LateUpdate()
    {
        if (cameraTransform != null)
        {
            transform.LookAt(transform.position + cameraTransform.rotation * Vector3.forward,
                            cameraTransform.rotation * Vector3.up);
        }
    }
}
#endregion