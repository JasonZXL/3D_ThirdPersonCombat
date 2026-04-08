using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

/// <summary>
/// 游戏状态UI管理器：处理游戏结束面板和暂停面板的显示/隐藏，以及相关逻辑。
/// 按ESC呼出/隐藏暂停菜单；玩家死亡时自动显示游戏结束菜单。
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("游戏结束UI组件")]
    [SerializeField] private GameObject gameOverPanel;           // 游戏结束面板（根物体）
    [SerializeField] private Button gameOverRestartButton;       // 游戏结束面板上的“重新开始”按钮
    [SerializeField] private Button gameOverQuitButton;          // 游戏结束面板上的“退出”按钮

    [Header("暂停UI组件")]
    [SerializeField] private GameObject pausePanel;              // 暂停面板（根物体）
    [SerializeField] private Button pauseResumeButton;           // 暂停面板上的“继续游戏”按钮
    [SerializeField] private Button pauseRestartButton;          // 暂停面板上的“重新开始”按钮
    [SerializeField] private Button pauseQuitButton;             // 暂停面板上的“退出”按钮

    [Header("通用设置")]
    [SerializeField] private string menuSceneName = "MenuScene"; // 退出时加载的菜单场景名称

    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;          // 是否显示调试日志

   
    // 单例实例
    public static UIManager Instance { get; private set; }

    // 公开事件
    public static event Action OnGameOver;          // 游戏结束事件
    public static event Action OnGameRestart;       // 游戏重新开始事件（点击重启按钮时触发）
    public static event Action OnGamePaused;        // 游戏暂停事件
    public static event Action OnGameResumed;       // 游戏恢复事件

    // 内部状态枚举
    private enum UIState { None, GameOver, Pause }
    private UIState currentState = UIState.None;
    public bool IsAnyUIActive => currentState != UIState.None;
    // 时间缩放相关
    private float originalTimeScale = 1f;

    // 光标状态保存（用于暂停时恢复）
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    // PlayerSpawnPoint引用
    private PlayerSpawnPoint playerSpawnPoint;

    void Awake()
    {
        // 单例初始化
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Log("✅ GameOver 单例已创建（合并暂停菜单版）");
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 确保引用的UI面板也跨场景不销毁（如果不是此物体的子物体）
        MakePersistent(gameOverPanel);
        MakePersistent(pausePanel);

        // 初始化UI（设置按钮事件、初始隐藏）
        InitializeUI();

        // 监听场景加载事件，以便重新查找PlayerSpawnPoint并重置状态
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        // 检测ESC键：仅在非游戏结束状态下可切换暂停
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentState == UIState.GameOver)
            {
                Log("游戏结束中，忽略ESC");
                return;
            }

            TogglePause();
        }
    }

    /// <summary>
    /// 确保目标物体跨场景不销毁（如果不是预设体或已在DontDestroyOnLoad中）
    /// </summary>
    private void MakePersistent(GameObject obj)
    {
        if (obj != null && obj.scene.name != null) // 场景中的物体才有scene名称，预制体为null
        {
            DontDestroyOnLoad(obj);
            Log($"🔄 已将 {obj.name} 设为DontDestroyOnLoad");
        }
    }

    /// <summary>
    /// 初始化UI：设置按钮监听、初始隐藏
    /// </summary>
    private void InitializeUI()
    {
        // 游戏结束面板按钮事件
        if (gameOverRestartButton != null)
        {
            gameOverRestartButton.onClick.RemoveAllListeners();
            gameOverRestartButton.onClick.AddListener(OnRestartButtonClicked);
        }
        else
        {
            LogError("❌ gameOverRestartButton 未赋值");
        }

        if (gameOverQuitButton != null)
        {
            gameOverQuitButton.onClick.RemoveAllListeners();
            gameOverQuitButton.onClick.AddListener(OnQuitButtonClicked);
        }
        else
        {
            LogError("❌ gameOverQuitButton 未赋值");
        }

        // 暂停面板按钮事件
        if (pauseResumeButton != null)
        {
            pauseResumeButton.onClick.RemoveAllListeners();
            pauseResumeButton.onClick.AddListener(OnResumeButtonClicked);
        }
        else
        {
            LogError("❌ pauseResumeButton 未赋值");
        }

        if (pauseRestartButton != null)
        {
            pauseRestartButton.onClick.RemoveAllListeners();
            pauseRestartButton.onClick.AddListener(OnRestartButtonClicked);
        }
        else
        {
            LogError("❌ pauseRestartButton 未赋值");
        }

        if (pauseQuitButton != null)
        {
            pauseQuitButton.onClick.RemoveAllListeners();
            pauseQuitButton.onClick.AddListener(OnQuitButtonClicked);
        }
        else
        {
            LogError("❌ pauseQuitButton 未赋值");
        }

        // 初始隐藏所有UI
        HideAllPanels();

        Log("✅ UI初始化完成");
    }

    /// <summary>
    /// 场景加载完成时的回调：重新查找PlayerSpawnPoint，重置UI状态
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Log($"🔄 场景加载完成: {scene.name}，重置状态");

        // 查找PlayerSpawnPoint并订阅事件
        FindPlayerSpawnPoint();

        // 重置所有UI状态（隐藏面板、恢复时间、解锁光标？）
        ResetToNoneState();
    }

    /// <summary>
    /// 查找场景中的PlayerSpawnPoint，并订阅其死亡事件
    /// </summary>
    private void FindPlayerSpawnPoint()
    {
        // 取消旧订阅（如果存在）
        if (playerSpawnPoint != null)
        {
            playerSpawnPoint.OnPlayerDeath -= HandlePlayerDeath;
        }

        playerSpawnPoint = FindAnyObjectByType<PlayerSpawnPoint>();
        if (playerSpawnPoint != null)
        {
            playerSpawnPoint.OnPlayerDeath += HandlePlayerDeath;
            Log($"✅ 找到PlayerSpawnPoint: {playerSpawnPoint.gameObject.name}，已订阅死亡事件");
        }
        else
        {
            LogWarning("⚠️ 未找到PlayerSpawnPoint");
        }
    }

    /// <summary>
    /// 处理玩家死亡事件：显示游戏结束UI
    /// </summary>
    private void HandlePlayerDeath()
    {
        Log("💀 玩家死亡，触发游戏结束");
        ShowGameOverUI();
    }

    /// <summary>
    /// 显示游戏结束UI（优先级最高）
    /// </summary>
    private void ShowGameOverUI()
    {
        if (currentState == UIState.GameOver)
            return;

        Log("🎮 显示游戏结束UI");

        // 如果当前是暂停状态，先关闭暂停面板
        if (currentState == UIState.Pause)
        {
            HidePausePanel();
        }

        // 设置状态
        currentState = UIState.GameOver;

        // 暂停游戏时间（保存当前时间缩放）
        PauseTime();

        // 解锁光标并显示
        UnlockCursor();

        // 显示游戏结束面板
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // 触发事件
        OnGameOver?.Invoke();
    }

    /// <summary>
    /// 隐藏游戏结束UI（通常在重启或退出时由按钮触发，但这里通过重置状态处理）
    /// </summary>
    private void HideGameOverUI()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    /// <summary>
    /// 切换暂停状态
    /// </summary>
    private void TogglePause()
    {
        if (currentState == UIState.Pause)
        {
            ResumeGame();
        }
        else if (currentState == UIState.None)
        {
            PauseGame();
        }
        // 其他状态（GameOver）不处理
    }

    /// <summary>
    /// 暂停游戏并显示暂停面板
    /// </summary>
    private void PauseGame()
    {
        if (currentState != UIState.None) return;

        Log("⏸️ 暂停游戏");

        currentState = UIState.Pause;

        // 暂停时间
        PauseTime();

        // 保存当前光标状态并解锁
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        UnlockCursor();

        // 显示暂停面板
        if (pausePanel != null)
            pausePanel.SetActive(true);

        // 触发事件
        OnGamePaused?.Invoke();
    }

    /// <summary>
    /// 恢复游戏并隐藏暂停面板
    /// </summary>
    public void ResumeGame()
    {
        if (currentState != UIState.Pause) return;

        Log("▶️ 恢复游戏");

        // 恢复时间
        ResumeTime();

        // 恢复之前的光标状态
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;

        // 隐藏暂停面板
        HidePausePanel();

        currentState = UIState.None;

        // 触发事件
        OnGameResumed?.Invoke();
    }

    /// <summary>
    /// 暂停时间（保存原始缩放）
    /// </summary>
    private void PauseTime()
    {
        originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 恢复时间到原始缩放
    /// </summary>
    private void ResumeTime()
    {
        Time.timeScale = originalTimeScale;
    }

    /// <summary>
    /// 解锁光标并显示
    /// </summary>
    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// 隐藏暂停面板（不改变时间状态）
    /// </summary>
    private void HidePausePanel()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    /// <summary>
    /// 隐藏所有UI面板，恢复时间，重置状态为None
    /// </summary>
    private void ResetToNoneState()
    {
        Log("🔄 重置UI状态为None");

        // 隐藏所有面板
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (pausePanel != null)
            pausePanel.SetActive(false);

        // 恢复时间缩放（如果当前是暂停状态，但场景加载时一般不会处于暂停，以防万一）
        Time.timeScale = 1f; // 强制设为1，因为新场景开始通常是正常运行

        // 重置状态
        currentState = UIState.None;

        // 光标状态可以根据游戏需求设置，这里不做强制，由具体游戏逻辑决定
    }

    /// <summary>
    /// 重启当前场景（供按钮调用）
    /// </summary>
    private void RestartCurrentScene()
    {
        Log("🔄 重启当前场景");

        // 先恢复时间（确保新场景时间正常）
        Time.timeScale = 1f;

        // 隐藏所有UI（将在场景加载后由OnSceneLoaded再次隐藏，但提前隐藏也无妨）
        HideAllPanels();

        // 触发重启事件
        OnGameRestart?.Invoke();

        // 重新加载当前场景
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }

    /// <summary>
    /// 退出到菜单场景（供按钮调用）
    /// </summary>
    private void QuitToMenu()
    {
        Log("🚪 退出到菜单");

        // 恢复时间
        Time.timeScale = 1f;

        // 隐藏所有UI
        HideAllPanels();

        // 加载菜单场景
        SceneManager.LoadScene(menuSceneName);
    }

    /// <summary>
    /// 隐藏所有UI面板
    /// </summary>
    private void HideAllPanels()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    // ========== 按钮事件处理方法 ==========
    private void OnRestartButtonClicked()
    {
        Log("🔁 重启按钮被点击");
        RestartCurrentScene();
    }

    private void OnQuitButtonClicked()
    {
        Log("🚪 退出按钮被点击");
        QuitToMenu();
    }

    private void OnResumeButtonClicked()
    {
        Log("▶️ 继续游戏按钮被点击");
        ResumeGame();
    }

    // ========== 公开方法（供外部调用或调试） ==========
    /// <summary>
    /// 获取游戏是否处于结束状态
    /// </summary>
    public bool IsGameOver() => currentState == UIState.GameOver;

    /// <summary>
    /// 手动触发游戏结束（用于调试）
    /// </summary>
    [ContextMenu("手动触发游戏结束")]
    public void TriggerGameOver()
    {
        ShowGameOverUI();
    }

    /// <summary>
    /// 手动触发重启（用于调试）
    /// </summary>
    [ContextMenu("手动重启")]
    public void TriggerRestart()
    {
        RestartCurrentScene();
    }

    /// <summary>
    /// 手动触发退出（用于调试）
    /// </summary>
    [ContextMenu("手动退出")]
    public void TriggerQuit()
    {
        QuitToMenu();
    }

    /// <summary>
    /// 手动触发暂停（用于调试）
    /// </summary>
    [ContextMenu("手动暂停")]
    public void TriggerPause()
    {
        PauseGame();
    }

    /// <summary>
    /// 手动触发恢复（用于调试）
    /// </summary>
    [ContextMenu("手动恢复")]
    public void TriggerResume()
    {
        ResumeGame();
    }

    // ========== 日志辅助 ==========
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