using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class FinalBossPlayerQTEInputRouter : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public bool IsEnabled { get; private set; }

    public event Action<FinalBossQTEInputType> OnInputReceived;
    public event Action OnSpacePressed;
    public event Action OnEPressed;
    public event Action OnLeftMousePressed;

    public void EnableRouter(bool enabled)
    {
        IsEnabled = enabled;

        if (showDebugLogs)
            Debug.Log($"[FinalBossPlayerQTEInputRouter] EnableRouter -> {enabled}");
    }

    private void Update()
    {
        if (!IsEnabled) return;

        if (WasSpacePressedThisFrame())
            RaiseInput(FinalBossQTEInputType.Space);

        if (WasEPressedThisFrame())
            RaiseInput(FinalBossQTEInputType.E);

        if (WasQPressedThisFrame())
            RaiseInput(FinalBossQTEInputType.Q);

        if (WasLeftMousePressedThisFrame())
            RaiseInput(FinalBossQTEInputType.LeftMouse);

        if (WasRightMousePressedThisFrame())
            RaiseInput(FinalBossQTEInputType.RightMouse);
    }

    private void RaiseInput(FinalBossQTEInputType inputType)
    {
        switch (inputType)
        {
            case FinalBossQTEInputType.Space:
                OnSpacePressed?.Invoke();
                break;

            case FinalBossQTEInputType.E:
                OnEPressed?.Invoke();
                break;

            case FinalBossQTEInputType.LeftMouse:
                OnLeftMousePressed?.Invoke();
                break;
        }

        OnInputReceived?.Invoke(inputType);

        if (showDebugLogs)
            Debug.Log($"[FinalBossPlayerQTEInputRouter] Input -> {inputType}");
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
