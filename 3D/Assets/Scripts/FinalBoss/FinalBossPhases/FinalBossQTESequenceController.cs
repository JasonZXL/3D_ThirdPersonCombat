using UnityEngine;
using UnityEngine.InputSystem;

public class FinalBossQTESequenceController : MonoBehaviour
{
    [Header("QTE Setup")]
    [SerializeField] private float fallbackStepTimeLimit = 1.5f;
    [SerializeField] private float successAdvanceDelay = 0.15f;
    [SerializeField] private bool showDebugLogs = true;

    private FinalBossStageThreeQTEController owner;
    private FinalBossPlayerQTEInputRouter inputRouter;
    private FinalBossQTESequenceData sequenceData;
    private FinalBossQTEPromptUI promptUI;
    private FinalBossQTEStepData currentStep;
    private int currentStepIndex = -1;
    private float stepTimer;
    private float transitionTimer;
    private bool isRunning;
    private bool isPendingAdvance;
    private bool pendingComplete;
    private bool hasResolved;

    public void BeginSequence(
        FinalBossStageThreeQTEController stageOwner,
        FinalBossPlayerQTEInputRouter router,
        FinalBossQTESequenceData data,
        FinalBossQTEPromptUI promptController)
    {
        StopSequence();

        owner = stageOwner;
        inputRouter = router;
        sequenceData = data;
        promptUI = promptController;
        hasResolved = false;

        if (sequenceData == null || !sequenceData.IsValid)
        {
            FailSequence("QTE sequence data is missing or empty");
            return;
        }

        if (inputRouter == null)
        {
            FailSequence("QTE input router is missing");
            return;
        }

        SubscribeInput();
        isRunning = true;
        AdvanceToStep(0);
    }

    public void StopSequence()
    {
        isRunning = false;
        isPendingAdvance = false;
        pendingComplete = false;
        currentStep = null;
        currentStepIndex = -1;
        stepTimer = 0f;
        transitionTimer = 0f;
        hasResolved = false;
        UnsubscribeInput();
        promptUI?.HidePrompt();
    }

    private void Update()
    {
        if (!isRunning)
            return;

        if (isPendingAdvance)
        {
            transitionTimer -= Time.deltaTime;
            if (transitionTimer <= 0f)
            {
                isPendingAdvance = false;

                if (pendingComplete)
                {
                    CompleteSequence();
                    return;
                }

                AdvanceToStep(currentStepIndex + 1);
            }

            return;
        }

        if (currentStep == null)
            return;

        PollDirectInputFallback();

        stepTimer -= Time.deltaTime;
        promptUI?.SetTimerNormalized(GetCurrentStepNormalizedTimer());

        if (stepTimer <= 0f)
            FailSequence($"QTE timeout at step {currentStepIndex + 1}");
    }

    private void OnInputReceived(FinalBossQTEInputType inputType)
    {
        if (!isRunning || isPendingAdvance || currentStep == null)
            return;

        if (inputType == currentStep.RequiredInput)
        {
            Log($"StepSuccess -> stepIndex={currentStepIndex} input={inputType}");
            promptUI?.ShowStepSuccess();

            bool completed = currentStepIndex >= sequenceData.StepCount - 1;
            QueueNextStep(completed);
            return;
        }

        Log($"StepWrongInput -> expected={currentStep.RequiredInput} actual={inputType}");
        if (currentStep.FailOnWrongInput)
            FailSequence($"Wrong input at step {currentStepIndex + 1}: {inputType}");
    }

    private void PollDirectInputFallback()
    {
        if (!isRunning || isPendingAdvance || currentStep == null)
            return;

        switch (currentStep.RequiredInput)
        {
            case FinalBossQTEInputType.Space:
                if (WasSpacePressedThisFrame())
                    OnInputReceived(FinalBossQTEInputType.Space);
                break;

            case FinalBossQTEInputType.E:
                if (WasEPressedThisFrame())
                    OnInputReceived(FinalBossQTEInputType.E);
                break;

            case FinalBossQTEInputType.Q:
                if (WasQPressedThisFrame())
                    OnInputReceived(FinalBossQTEInputType.Q);
                break;

            case FinalBossQTEInputType.LeftMouse:
                if (WasLeftMousePressedThisFrame())
                    OnInputReceived(FinalBossQTEInputType.LeftMouse);
                break;

            case FinalBossQTEInputType.RightMouse:
                if (WasRightMousePressedThisFrame())
                    OnInputReceived(FinalBossQTEInputType.RightMouse);
                break;
        }
    }

    private void AdvanceToStep(int stepIndex)
    {
        if (sequenceData == null)
        {
            FailSequence("QTE sequence data became null");
            return;
        }

        FinalBossQTEStepData nextStep = sequenceData.GetStep(stepIndex);
        if (nextStep == null)
        {
            FailSequence($"Missing QTE step at index {stepIndex}");
            return;
        }

        currentStepIndex = stepIndex;
        currentStep = nextStep;
        stepTimer = Mathf.Max(0.1f, nextStep.TimeLimit > 0f ? nextStep.TimeLimit : fallbackStepTimeLimit);

        promptUI?.ShowStep(nextStep, currentStepIndex + 1, sequenceData.StepCount);
        promptUI?.SetTimerNormalized(1f);
        Log($"AdvanceToStep -> index={currentStepIndex} id={nextStep.StepId} time={stepTimer}");
    }

    private void QueueNextStep(bool completed)
    {
        isPendingAdvance = true;
        pendingComplete = completed;
        transitionTimer = Mathf.Max(0f, successAdvanceDelay);

        if (transitionTimer <= 0f)
        {
            isPendingAdvance = false;

            if (pendingComplete)
                CompleteSequence();
            else
                AdvanceToStep(currentStepIndex + 1);
        }
    }

    private void CompleteSequence()
    {
        if (!isRunning)
            return;

        isRunning = false;
        hasResolved = true;
        currentStep = null;
        promptUI?.HidePrompt();
        Log("CompleteSequence");
        owner?.NotifyQTESuccess();
    }

    private float GetCurrentStepNormalizedTimer()
    {
        if (currentStep == null)
            return 0f;

        float duration = Mathf.Max(0.1f, currentStep.TimeLimit > 0f ? currentStep.TimeLimit : fallbackStepTimeLimit);
        return Mathf.Clamp01(stepTimer / duration);
    }

    private void SubscribeInput()
    {
        if (inputRouter == null)
            return;

        inputRouter.OnInputReceived -= OnInputReceived;
        inputRouter.OnInputReceived += OnInputReceived;
    }

    private void UnsubscribeInput()
    {
        if (inputRouter == null)
            return;

        inputRouter.OnInputReceived -= OnInputReceived;
    }

    private void FailSequence(string reason)
    {
        if (hasResolved)
            return;

        isRunning = false;
        isPendingAdvance = false;
        pendingComplete = false;
        hasResolved = true;
        promptUI?.ShowStepFailure(reason);
        Log($"FailSequence -> {reason}");
        owner?.NotifyQTEFailure(reason);
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossQTESequenceController] {message}");
    }

    private bool WasSpacePressedThisFrame()
    {
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) || LegacyGetKeyDown(KeyCode.Space);
    }

    private bool WasEPressedThisFrame()
    {
        return (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) || LegacyGetKeyDown(KeyCode.E);
    }

    private bool WasQPressedThisFrame()
    {
        return (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) || LegacyGetKeyDown(KeyCode.Q);
    }

    private bool WasLeftMousePressedThisFrame()
    {
        return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || LegacyGetMouseButtonDown(0);
    }

    private bool WasRightMousePressedThisFrame()
    {
        return (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) || LegacyGetMouseButtonDown(1);
    }

    private bool LegacyGetKeyDown(KeyCode keyCode)
    {
        try
        {
            return Input.GetKeyDown(keyCode);
        }
        catch
        {
            return false;
        }
    }

    private bool LegacyGetMouseButtonDown(int button)
    {
        try
        {
            return Input.GetMouseButtonDown(button);
        }
        catch
        {
            return false;
        }
    }
}
