using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;

public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("基本设置")]
    [SerializeField] private GameObject playerPrefab;        // 玩家预制体
    [SerializeField] private float respawnDelay = 2f;       // 重生延迟时间
    [SerializeField] private bool showGizmos = true;        // 显示调试图标

    [Header("重生模式")]
    [Tooltip("重生模式：重载场景 或 由GameOver管理")]
    [SerializeField] private RespawnMode respawnMode = RespawnMode.GameOverManagement;
    
    [Header("UI设置")]
    [SerializeField] private Transform playerUIContainer;   // 玩家UI容器
    
    // 枚举：重生模式
    public enum RespawnMode
    {
        ReloadScene,          // 重载场景（传统模式）
        GameOverManagement    // 由GameOver管理（显示UI）
    }
    
    // 事件：玩家死亡
    public event Action OnPlayerDeath;
    
    // 当前玩家引用
    private GameObject currentPlayer;
    private PlayerHealthSystem playerHealth;
    private HeartUIController heartUIController;
    
    // GameOver引用
    private GameOver gameOverManager;
    
    void Start()
    {
        // 查找GameOver管理器
        FindGameOverManager();
        
        // 游戏开始时生成玩家
        SpawnPlayer();
    }
    
    /// <summary>
    /// 查找GameOver管理器
    /// </summary>
    private void FindGameOverManager()
    {
        gameOverManager = GameOver.Instance;
        if (gameOverManager == null)
        {
            Debug.LogWarning("⚠️ 未找到GameOver管理器，将创建新的实例");
            GameObject gameOverGO = new GameObject("GameOverManager");
            gameOverManager = gameOverGO.AddComponent<GameOver>();
        }
        
        Debug.Log($"✅ GameOver管理器: {gameOverManager.gameObject.name}");
    }
    
    /// <summary>
    /// 生成玩家（首次生成）
    /// </summary>
    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("❌ 没有设置玩家预制体！");
            return;
        }
        
        // 创建玩家实例
        currentPlayer = Instantiate(playerPrefab, transform.position, transform.rotation);

        // 初始化玩家UI
        SetupPlayerUI();
        
        // 获取并设置玩家生命系统
        SetupPlayerHealthSystem();
        
        Debug.Log($"👤 玩家已生成在出生点");
    }

    /// <summary>
    /// 初始化玩家UI
    /// </summary>
    private void SetupPlayerUI()
    {
        if (currentPlayer == null) return;
        
        // 获取HeartUIController组件
        heartUIController = currentPlayer.GetComponent<HeartUIController>();
        
        if (heartUIController == null)
        {
            // 尝试在子对象中查找
            heartUIController = currentPlayer.GetComponentInChildren<HeartUIController>();
            
            if (heartUIController == null)
            {
                Debug.LogWarning("⚠️ 玩家没有HeartUIController组件");
                return;
            }
        }
        
        // 如果提供了UI容器，则设置它
        if (playerUIContainer != null)
        {
            heartUIController.SetHeartsContainer(playerUIContainer);
            Debug.Log($"✅ 使用自定义UI容器: {playerUIContainer.name}");
        }
        
        // 获取玩家生命系统以初始化UI
        PlayerHealthSystem healthSystem = currentPlayer.GetComponent<PlayerHealthSystem>();
        if (healthSystem != null && heartUIController != null)
        {
            // 手动初始化UI
            heartUIController.ManualInitialize(healthSystem);
        }
        else
        {
            Debug.LogWarning("⚠️ 无法初始化玩家UI，缺少HealthSystem或HeartUIController");
        }
    }
    
    /// <summary>
    /// 设置玩家生命系统
    /// </summary>
    private void SetupPlayerHealthSystem()
    {
        if (currentPlayer == null) return;
        
        // 获取玩家生命系统组件
        playerHealth = currentPlayer.GetComponent<PlayerHealthSystem>();
        
        if (playerHealth != null)
        {
            // 订阅死亡事件
            playerHealth.OnDeath += HandlePlayerDeath;
            Debug.Log("❤️ 已连接玩家生命系统");
        }
        else
        {
            Debug.LogWarning("⚠️ 玩家没有PlayerHealthSystem组件");
        }
    }
    
    /// <summary>
    /// 处理玩家死亡
    /// </summary>
    private void HandlePlayerDeath()
    {
        if (currentPlayer == null) return;
        
        Debug.Log($"💀 玩家死亡，重生模式: {respawnMode}");
        
        // 触发玩家死亡事件
        OnPlayerDeath?.Invoke();
        
        // 根据重生模式选择不同的处理方式
        switch (respawnMode)
        {
            case RespawnMode.ReloadScene:
                StartCoroutine(ReloadSceneAfterDelay());
                break;
                
            case RespawnMode.GameOverManagement:
                // 延迟后触发游戏结束UI
                StartCoroutine(TriggerGameOverAfterDelay());
                break;
        }
    }
    
    /// <summary>
    /// 延迟后触发游戏结束UI
    /// </summary>
    private IEnumerator TriggerGameOverAfterDelay()
    {
        Debug.Log($"⏱️ 等待 {respawnDelay} 秒后触发游戏结束UI...");
        
        // 等待延迟时间
        yield return new WaitForSeconds(respawnDelay);
        
        // 检查GameOver管理器是否存在
        if (gameOverManager != null && !gameOverManager.IsGameOver())
        {
            Debug.Log($"🎮 触发游戏结束UI");
            // GameOver会暂停游戏并显示UI
        }
        else
        {
            Debug.LogWarning($"⚠️ GameOver管理器不存在或游戏已结束，使用备用方案");
            StartCoroutine(ReloadSceneAfterDelay());
        }
    }
    
    /// <summary>
    /// 延迟后重载场景（协程）
    /// </summary>
    private IEnumerator ReloadSceneAfterDelay()
    {
        Debug.Log($"⏱️ 等待 {respawnDelay} 秒后重载场景...");
        
        // 等待延迟时间
        yield return new WaitForSeconds(respawnDelay);
        
        // 获取当前场景名称
        string currentSceneName = SceneManager.GetActiveScene().name;
        
        Debug.Log($"🔄 正在重载场景: {currentSceneName}");
        
        // 重载当前场景
        SceneManager.LoadScene(currentSceneName);
    }
    
    /// <summary>
    /// 重置玩家位置和旋转
    /// </summary>
    private void ResetPlayerTransform()
    {
        if (currentPlayer == null) return;
        
        // 处理物理组件
        CharacterController controller = currentPlayer.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
            currentPlayer.transform.position = transform.position;
            currentPlayer.transform.rotation = transform.rotation;
            controller.enabled = true;
        }
        else
        {
            // 如果没有CharacterController，直接设置位置
            currentPlayer.transform.position = transform.position;
            currentPlayer.transform.rotation = transform.rotation;
        }
    }
    
    /// <summary>
    /// 手动触发重载场景（用于调试）
    /// </summary>
    [ContextMenu("手动重载场景")]
    public void ManualReloadScene()
    {
        StartCoroutine(ReloadSceneAfterDelay());
    }
    
    /// <summary>
    /// 调试绘图
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        // 根据重生模式设置颜色
        Color gizmoColor = Color.green;
        
        switch (respawnMode)
        {
            case RespawnMode.ReloadScene:
                gizmoColor = Color.cyan;
                break;

            case RespawnMode.GameOverManagement:
                gizmoColor = Color.red;
                break;
        }
        
        // 绘制出生点图标
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // 绘制方向箭头
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        
        // 显示标签
        #if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = gizmoColor;
        
        string label = "";
        switch (respawnMode)
        {
            case RespawnMode.ReloadScene:
                label = "出生点(重载场景)";
                break;

            case RespawnMode.GameOverManagement:
                label = "出生点(GameOver管理)";
                break;
        }
        
        UnityEditor.Handles.Label(transform.position + Vector3.up, label, style);
        #endif
    }
}