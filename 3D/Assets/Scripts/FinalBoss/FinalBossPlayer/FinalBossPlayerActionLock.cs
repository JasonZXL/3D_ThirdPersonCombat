using UnityEngine;
using UnityEngine.InputSystem;

public class FinalBossPlayerActionLock : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MonoBehaviour movementController;
    [SerializeField] private PlayerAttackDetector attackDetector;
    [SerializeField] private PlayerColorController playerColorController;
    [SerializeField] private ColorChangeAbility colorChangeAbility;
    [SerializeField] private ThrowObjectAbility throwObjectAbility;
    [SerializeField] private PlayerInput playerInput;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        movementController ??= GetComponent<ThirdPersonMove2>();
        attackDetector ??= GetComponent<PlayerAttackDetector>();
        playerColorController ??= GetComponent<PlayerColorController>();
        colorChangeAbility ??= GetComponent<ColorChangeAbility>();
        throwObjectAbility ??= GetComponent<ThrowObjectAbility>();
        playerInput ??= GetComponent<PlayerInput>();
    }

    public void LockAllGameplayActions()
    {
        SetComponentState(false);
        Log("LockAllGameplayActions");
    }

    public void UnlockAllGameplayActions()
    {
        SetComponentState(true);
        Log("UnlockAllGameplayActions");
    }

    public void SetQTEActionMap(string actionMapName)
    {
        if (playerInput == null || string.IsNullOrWhiteSpace(actionMapName))
            return;

        playerInput.SwitchCurrentActionMap(actionMapName);
        Log($"SetQTEActionMap -> {actionMapName}");
    }

    private void SetComponentState(bool enabled)
    {
        if (movementController != null) movementController.enabled = enabled;
        if (attackDetector != null) attackDetector.enabled = enabled;
        if (playerColorController != null) playerColorController.enabled = enabled;
        if (colorChangeAbility != null) colorChangeAbility.enabled = enabled;
        if (throwObjectAbility != null) throwObjectAbility.enabled = enabled;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossPlayerActionLock] {message}");
    }
}
