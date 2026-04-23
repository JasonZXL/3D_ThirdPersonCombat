using UnityEngine;

public class FinalBossQTEBeam : MonoBehaviour
{
    [Header("QTE Beam")]
    [SerializeField] private ColorComponent colorComponent;
    [SerializeField] private bool showDebugLogs = true;

    private FinalBossStageThreeQTEController owner;
    public ColorType CurrentColor => colorComponent != null ? colorComponent.CurrentColor : ColorType.Neutral;

    public void Initialize(FinalBossStageThreeQTEController stageOwner)
    {
        owner = stageOwner;
        colorComponent ??= GetComponent<ColorComponent>();
    }

    public void ToggleBeamColor()
    {
        if (colorComponent == null) return;
        colorComponent.ToggleColor();

        if (showDebugLogs)
            Debug.Log($"[FinalBossQTEBeam] ToggleBeamColor -> {colorComponent.CurrentColor}");
    }

    public void SetBeamColor(ColorType color)
    {
        if (colorComponent == null) return;
        colorComponent.CurrentColor = color;

        if (showDebugLogs)
            Debug.Log($"[FinalBossQTEBeam] SetBeamColor -> {color}");
    }

    public void ReflectBackToBoss()
    {
        if (showDebugLogs)
            Debug.Log("[FinalBossQTEBeam] ReflectBackToBoss");

        Destroy(gameObject);
    }
}
