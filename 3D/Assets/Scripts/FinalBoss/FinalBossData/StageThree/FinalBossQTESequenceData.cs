using UnityEngine;

[CreateAssetMenu(fileName = "FB_QTE_Sequence", menuName = "FinalBoss/StageThree/QTE Sequence Data")]
public class FinalBossQTESequenceData : ScriptableObject
{
    [Header("Sequence")]
    [SerializeField] private string sequenceId = "FinalBoss_StageThree_QTE";
    [SerializeField] private FinalBossQTEStepData[] steps;

    public string SequenceId => sequenceId;
    public FinalBossQTEStepData[] Steps => steps;
    public int StepCount => steps != null ? steps.Length : 0;
    public bool IsValid => steps != null && steps.Length > 0;

    public FinalBossQTEStepData GetStep(int index)
    {
        if (steps == null || index < 0 || index >= steps.Length)
            return null;

        return steps[index];
    }
}
