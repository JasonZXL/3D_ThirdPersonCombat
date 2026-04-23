using System;
using UnityEngine;

[Serializable]
public class FinalBossQTEStepData
{
    [SerializeField] private string stepId = "Step";
    [SerializeField] private string promptTitle = "QTE";
    [SerializeField] private string instructionText = "Press the required key";
    [SerializeField] private string inputDisplayOverride = string.Empty;
    [SerializeField] private FinalBossQTEInputType requiredInput = FinalBossQTEInputType.Space;
    [SerializeField] private float timeLimit = 1.5f;
    [SerializeField] private bool failOnWrongInput = true;

    public string StepId => stepId;
    public string PromptTitle => promptTitle;
    public string InstructionText => instructionText;
    public FinalBossQTEInputType RequiredInput => requiredInput;
    public float TimeLimit => Mathf.Max(0.1f, timeLimit);
    public bool FailOnWrongInput => failOnWrongInput;

    public string GetDisplayInputLabel()
    {
        if (!string.IsNullOrWhiteSpace(inputDisplayOverride))
            return inputDisplayOverride;

        return FinalBossQTEInputTypeUtility.ToDisplayLabel(requiredInput);
    }
}
