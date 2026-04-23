using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FinalBossVictoryUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FinalBossBattleDirector battleDirector;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    [Header("Settings")]
    [SerializeField] private string menuSceneName = "MenuScene";
    [SerializeField] private bool pauseTimeOnVictory = true;
    [SerializeField] private bool unlockCursorOnVictory = true;
    [SerializeField] private bool hidePanelOnAwake = true;
    [SerializeField] private bool showDebugLogs = true;

    private float cachedTimeScale = 1f;
    private bool subscribed;

    private void Awake()
    {
        battleDirector ??= GetComponentInParent<FinalBossBattleDirector>();

        if (hidePanelOnAwake && victoryPanel != null)
            victoryPanel.SetActive(false);

        BindButtons();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        if (battleDirector == null)
            battleDirector = FindAnyObjectByType<FinalBossBattleDirector>(FindObjectsInactive.Include);

        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || battleDirector == null)
            return;

        battleDirector.OnBattleVictory -= HandleBattleVictory;
        battleDirector.OnBattleVictory += HandleBattleVictory;
        subscribed = true;
        Log($"Subscribe -> {battleDirector.name}");
    }

    private void Unsubscribe()
    {
        if (!subscribed || battleDirector == null)
            return;

        battleDirector.OnBattleVictory -= HandleBattleVictory;
        subscribed = false;
    }

    private void BindButtons()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartCurrentScene);
            restartButton.onClick.AddListener(RestartCurrentScene);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitToMenu);
            quitButton.onClick.AddListener(QuitToMenu);
        }
    }

    private void HandleBattleVictory()
    {
        ShowVictoryUI();
    }

    public void ShowVictoryUI()
    {
        Log("ShowVictoryUI");

        if (pauseTimeOnVictory)
        {
            cachedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (unlockCursorOnVictory)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (victoryPanel != null)
            victoryPanel.SetActive(true);
    }

    public void HideVictoryUI()
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        if (pauseTimeOnVictory)
            Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
    }

    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    [ContextMenu("Test Victory UI")]
    public void TestVictoryUI()
    {
        ShowVictoryUI();
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossVictoryUIController] {message}");
    }
}
