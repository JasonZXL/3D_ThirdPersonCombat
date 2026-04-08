using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// 鼠标控制器：根据场景和 UI 状态自动隐藏/显示鼠标，并与 UIManager 脚本协同。
/// 挂载在 DontDestroyOnLoad 物体上（建议与 UIManager 同一物体）。
/// </summary>
public class CursorController : MonoBehaviour
{
    [Header("游戏场景设置")]
    [SerializeField] private string[] gameSceneNames;          // 需要隐藏鼠标的场景名称列表（如 "GameLevel1", "GameLevel2"）
    [SerializeField] private bool cursorVisibleInGame = false; // 游戏场景中鼠标是否可见（通常为 false）
    [SerializeField] private CursorLockMode lockModeInGame = CursorLockMode.Locked; // 游戏场景中光标锁定模式

    [Header("菜单场景设置")]
    [SerializeField] private bool cursorVisibleInMenu = true;  // 菜单场景中鼠标是否可见
    [SerializeField] private CursorLockMode lockModeInMenu = CursorLockMode.None; // 菜单场景中光标锁定模式

    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;

    // 当前场景是否属于游戏场景（需要隐藏鼠标）
    private bool isCurrentSceneGame = false;

    // 对 UIManager 脚本的引用
    private UIManager uiManager;

    private void Awake()
    {
        // 订阅场景加载事件
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void Start()
    {
        // 尝试获取 UIManager 实例（可能在同一个物体或已存在）
        uiManager = UIManager.Instance;

        if (uiManager == null)
        {
            Debug.LogError("[CursorController] UIManager.Instance 未找到，请确保 UIManager 脚本已初始化。");
            enabled = false;
            return;
        }
        // 订阅 UIManager 的事件
        SubscribeToGameOverEvents();
        // 初始设置（根据当前场景和 UI 状态）
        UpdateCursorState();
    }
    private void OnDestroy()
    {
        // 取消事件订阅
        if (uiManager != null)
        {
            UIManager.OnGamePaused -= HandleGamePaused;
            UIManager.OnGameResumed -= HandleGameResumed;
            UIManager.OnGameOver -= HandleGameOver;
            UIManager.OnGameRestart -= HandleGameRestart;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 订阅 UIManager 的相关事件
    /// </summary>
    private void SubscribeToGameOverEvents()
    {
        // UIManager 的静态事件可能为空，需要确保在使用前有订阅者
        UIManager.OnGamePaused += HandleGamePaused;
        UIManager.OnGameResumed += HandleGameResumed;
        UIManager.OnGameOver += HandleGameOver;
        UIManager.OnGameRestart += HandleGameRestart;

        Log("已订阅 UIManager 事件");
    }

    // 事件处理函数
    private void HandleGamePaused()
    {
        Log("⏸️ 游戏暂停，强制显示鼠标");
        // 暂停时无条件显示鼠标
        SetCursorVisible(true, CursorLockMode.None);
    }

    private void HandleGameResumed()
    {
        Log("▶️ 游戏恢复，根据场景重新计算鼠标状态");
        // 恢复时根据当前场景和 UI 状态更新
        UpdateCursorState();
    }

    private void HandleGameOver()
    {
        Log("💀 游戏结束，强制显示鼠标");
        // 游戏结束时显示鼠标
        SetCursorVisible(true, CursorLockMode.None);
    }

    private void HandleGameRestart()
    {
        Log("🔄 游戏重启，将在场景加载后由 OnSceneLoaded 处理");
        // 重启后场景会重载，光标在 OnSceneLoaded 中更新
        // 这里可以不做处理，但为了立即响应，可以暂时保持显示
        SetCursorVisible(true, CursorLockMode.None);
    }

    /// <summary>
    /// 场景加载完成时的回调
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Log($"🌍 场景加载: {scene.name}，重新评估鼠标状态");

        // 判断当前场景是否为游戏场景
        isCurrentSceneGame = IsGameScene(scene.name);

        // 更新光标状态
        UpdateCursorState();
    }

    /// <summary>
    /// 判断指定场景名称是否属于游戏场景列表
    /// </summary>
    private bool IsGameScene(string sceneName)
    {
        if (gameSceneNames == null) return false;
        foreach (string name in gameSceneNames)
        {
            if (name == sceneName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 根据当前场景和 GameOver UI 状态设置鼠标
    /// </summary>
    private void UpdateCursorState()
    {
        if (uiManager == null) return;

        // 如果 UIManager 有 UI 激活（暂停或游戏结束），则无条件显示鼠标
        if (uiManager.IsAnyUIActive)
        {
            Log("🎮 GameOver UI 激活中，显示鼠标");
            SetCursorVisible(true, CursorLockMode.None);
            return;
        }

        // 否则根据场景类型设置
        if (isCurrentSceneGame)
        {
            // 游戏场景：隐藏鼠标
            SetCursorVisible(cursorVisibleInGame, lockModeInGame);
        }
        else
        {
            // 菜单场景：显示鼠标
            SetCursorVisible(cursorVisibleInMenu, lockModeInMenu);
        }
    }

    /// <summary>
    /// 设置鼠标可见性和锁定模式
    /// </summary>
    private void SetCursorVisible(bool visible, CursorLockMode lockMode)
    {
        Cursor.visible = visible;
        Cursor.lockState = lockMode;
        Log($"🖱️ 鼠标设置: 可见={visible}, 锁定模式={lockMode}");
    }

    // ========== 日志辅助 ==========
    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[CursorController] {message}");
    }
}
