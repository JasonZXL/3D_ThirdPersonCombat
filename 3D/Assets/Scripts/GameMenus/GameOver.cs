using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

public class GameOver : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private Canvas gameOverCanvas;          // 游戏结束画布
    [SerializeField] private Button restartButton;           // 重新开始按钮
    [SerializeField] private Button quitButton;              // 退出游戏按钮
    
    [Header("游戏结束设置")]
    [SerializeField] private string gameOverMessage = "游戏失败"; // 游戏结束消息
    [SerializeField] private string menuSceneName = "MenuScene";  // 菜单场景名称
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;           // 显示调试日志

    // 单例实例
    public static GameOver Instance { get; private set; }
    
    // 游戏结束事件
    public static event Action OnGameOver;
    public static event Action OnGameRestart;
    
    // 内部状态
    private bool isGameOver = false;
    private PlayerSpawnPoint playerSpawnPoint;
    private float originalTimeScale;
    
    void Awake()
    {
        // 单例初始化
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 初始化UI（不再自动创建）
            InitializeUI();
            Log("✅ GameOver 单例已创建");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // 监听场景加载事件
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // 保存原始时间缩放
        originalTimeScale = Time.timeScale;
    }
    
    void OnDestroy()
    {
        // 取消事件订阅
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // 清理UI事件
        if (restartButton != null)
            restartButton.onClick.RemoveAllListeners();
        
        if (quitButton != null)
            quitButton.onClick.RemoveAllListeners();
    }
    
    /// <summary>
    /// 场景加载完成时的回调
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Log($"🔄 场景加载完成: {scene.name}");
        
        // 查找PlayerSpawnPoint
        FindPlayerSpawnPoint();
        
        // 重置游戏状态
        ResetGameState();
    }
    
    /// <summary>
    /// 初始化UI
    /// </summary>
    private void InitializeUI()
    {
        // 检查必需组件是否已配置
        if (gameOverCanvas == null)
        {
            LogError("❌ GameOverCanvas未配置！请在Inspector中手动设置");
            enabled = false;
            return;
        }
        
        if (restartButton == null)
        {
            LogError("❌ RestartButton未配置！请在Inspector中手动设置");
        }
        
        if (quitButton == null)
        {
            LogError("❌ QuitButton未配置！请在Inspector中手动设置");
        }
        
        // 设置按钮事件
        SetupButtonEvents();
        
        // 默认隐藏UI
        if (gameOverCanvas != null)
            gameOverCanvas.gameObject.SetActive(false);
        
        Log("✅ UI初始化完成");
    }
    
    /// <summary>
    /// 设置按钮事件
    /// </summary>
    private void SetupButtonEvents()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartButtonClicked);
            Log("✅ 重新开始按钮事件已设置");
        }
        
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitButtonClicked);
            Log("✅ 退出游戏按钮事件已设置");
        }
    }
    
    /// <summary>
    /// 查找PlayerSpawnPoint
    /// </summary>
    private void FindPlayerSpawnPoint()
    {
        playerSpawnPoint = FindAnyObjectByType<PlayerSpawnPoint>();
        if (playerSpawnPoint != null)
        {
            Log($"✅ 找到PlayerSpawnPoint: {playerSpawnPoint.gameObject.name}");
            
            // 监听玩家死亡事件
            playerSpawnPoint.OnPlayerDeath += HandlePlayerDeath;
            Log("✅ 已监听玩家死亡事件");
        }
        else
        {
            LogWarning("⚠️ 未找到PlayerSpawnPoint");
        }
    }
    
    /// <summary>
    /// 处理玩家死亡
    /// </summary>
    private void HandlePlayerDeath()
    {
        Log("💀 接收到玩家死亡事件");
        
        // 如果游戏已经结束，不再处理
        if (isGameOver)
            return;
        
        // 显示游戏结束UI
        ShowGameOverUI();
        
        // 触发游戏结束事件
        OnGameOver?.Invoke();
    }
    
    /// <summary>
    /// 显示游戏结束UI
    /// </summary>
    private void ShowGameOverUI()
    {
        Log("🎮 显示游戏结束UI");
        
        // 标记游戏结束
        isGameOver = true;
        
        // 暂停游戏
        PauseGame();
        
        // 显示UI
        if (gameOverCanvas != null)
        {
            gameOverCanvas.gameObject.SetActive(true);
            
            // 确保UI在最上层
            gameOverCanvas.sortingOrder = 999;
        }
        
        // 解锁鼠标
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// 隐藏游戏结束UI
    /// </summary>
    private void HideGameOverUI()
    {
        Log("🎮 隐藏游戏结束UI");
        
        if (gameOverCanvas != null)
            gameOverCanvas.gameObject.SetActive(false);
        
        // 锁定鼠标（如果游戏需要）
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;
    }
    
    /// <summary>
    /// 暂停游戏
    /// </summary>
    private void PauseGame()
    {
        originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Log($"⏸️ 游戏已暂停 (时间缩放: {Time.timeScale})");
    }
    
    /// <summary>
    /// 恢复游戏
    /// </summary>
    private void ResumeGame()
    {
        Time.timeScale = originalTimeScale;
        Log($"▶️ 游戏已恢复 (时间缩放: {Time.timeScale})");
    }
    
    /// <summary>
    /// 重置游戏状态
    /// </summary>
    private void ResetGameState()
    {
        isGameOver = false;
        
        // 确保UI是隐藏的
        if (gameOverCanvas != null)
            gameOverCanvas.gameObject.SetActive(false);
            
        Log("🔄 游戏状态已重置");
    }
    
    /// <summary>
    /// 重新开始按钮点击事件
    /// </summary>
    private void OnRestartButtonClicked()
    {
        Log("🔁 重新开始按钮被点击");
        
        // 恢复游戏
        ResumeGame();
        
        // 隐藏UI
        HideGameOverUI();
        
        // 触发游戏重新开始事件
        OnGameRestart?.Invoke();
        
        // 获取当前场景名称
        string currentSceneName = SceneManager.GetActiveScene().name;
        
        Log($"🔄 重新加载场景: {currentSceneName}");
        
        // 重新加载当前场景
        SceneManager.LoadScene(currentSceneName);
    }
    
    /// <summary>
    /// 退出游戏按钮点击事件
    /// </summary>
    private void OnQuitButtonClicked()
    {
        Log("🚪 退出游戏按钮被点击");
        
        // 恢复游戏
        ResumeGame();
        
        // 隐藏UI
        HideGameOverUI();
        
        Log($"🏠 切换到菜单场景: {menuSceneName}");
        
        // 加载菜单场景
        SceneManager.LoadScene(menuSceneName);
    }
    
    /// <summary>
    /// 手动触发游戏结束（用于调试）
    /// </summary>
    [ContextMenu("手动触发游戏结束")]
    public void TriggerGameOver()
    {
        if (!isGameOver)
        {
            ShowGameOverUI();
            OnGameOver?.Invoke();
        }
    }
    
    /// <summary>
    /// 手动重新开始游戏（用于调试）
    /// </summary>
    [ContextMenu("手动重新开始游戏")]
    public void TriggerRestart()
    {
        OnRestartButtonClicked();
    }
    
    /// <summary>
    /// 手动退出游戏（用于调试）
    /// </summary>
    [ContextMenu("手动退出游戏")]
    public void TriggerQuit()
    {
        OnQuitButtonClicked();
    }
    
    /// <summary>
    /// 获取游戏状态
    /// </summary>
    public bool IsGameOver() => isGameOver;
    
    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[GameOver] {message}");
    }
    
    private void LogWarning(string message)
    {
        if (showDebugLogs)
            Debug.LogWarning($"[GameOver] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[GameOver] {message}");
    }
}