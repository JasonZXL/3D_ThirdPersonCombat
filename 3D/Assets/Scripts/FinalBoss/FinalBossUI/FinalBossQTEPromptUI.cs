using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinalBossQTEPromptUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private TMP_Text stepIndexText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Image timerFillImage;

    [Header("Presentation")]
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private string successText = "Success";
    [SerializeField] private string failureText = "Fail";
    [SerializeField] private string completedText = "Completed";
    [SerializeField] private Color normalResultColor = Color.white;
    [SerializeField] private Color successResultColor = Color.green;
    [SerializeField] private Color failureResultColor = Color.red;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        if (hideOnAwake)
            HidePrompt();
    }

    public void ShowStep(FinalBossQTEStepData stepData, int currentStepIndex, int totalSteps)
    {
        if (stepData == null)
        {
            HidePrompt();
            return;
        }

        SetVisible(true);

        if (stepIndexText != null)
            stepIndexText.text = $"{currentStepIndex}/{totalSteps}";

        if (titleText != null)
            titleText.text = stepData.PromptTitle;

        if (instructionText != null)
            instructionText.text = stepData.InstructionText;

        if (keyText != null)
            keyText.text = stepData.GetDisplayInputLabel();

        if (resultText != null)
        {
            resultText.text = string.Empty;
            resultText.color = normalResultColor;
        }

        SetTimerNormalized(1f);
        Log($"ShowStep -> {stepData.StepId} ({currentStepIndex}/{totalSteps})");
    }

    public void SetTimerNormalized(float normalized)
    {
        if (timerFillImage != null)
            timerFillImage.fillAmount = Mathf.Clamp01(normalized);
    }

    public void ShowStepSuccess()
    {
        if (resultText != null)
        {
            resultText.text = successText;
            resultText.color = successResultColor;
        }

        Log("ShowStepSuccess");
    }

    public void ShowStepFailure(string reason)
    {
        if (resultText != null)
        {
            resultText.text = string.IsNullOrWhiteSpace(reason) ? failureText : $"{failureText}: {reason}";
            resultText.color = failureResultColor;
        }

        Log($"ShowStepFailure -> {reason}");
    }

    public void ShowSequenceCompleted()
    {
        if (resultText != null)
        {
            resultText.text = completedText;
            resultText.color = successResultColor;
        }

        SetTimerNormalized(1f);
        Log("ShowSequenceCompleted");
    }

    public void HidePrompt()
    {
        SetVisible(false);

        if (timerFillImage != null)
            timerFillImage.fillAmount = 0f;

        if (resultText != null)
            resultText.text = string.Empty;

        Log("HidePrompt");
    }

    private void SetVisible(bool visible)
    {
        if (rootCanvasGroup == null)
        {
            gameObject.SetActive(visible);
            return;
        }

        rootCanvasGroup.alpha = visible ? 1f : 0f;
        rootCanvasGroup.interactable = visible;
        rootCanvasGroup.blocksRaycasts = visible;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossQTEPromptUI] {message}");
    }
}
