using UnityEngine;

[CreateAssetMenu(menuName = "FinalBoss/Phase One/Approach Data", fileName = "FB_PhaseOne_Approach")]
public class FinalBossPhaseOneApproachStateData : FinalBossPhaseOneStateData
{
    public float moveSpeed = 4.5f;
    public float stopDistanceBuffer = 0.5f;
    public float faceRotationSpeed = 720f;
    public float maxApproachDuration = 2f;
    [Range(0f, 1f)] public float quickstepChance = 0.3f;
    public float quickstepMinDistance = 4f;
    public float quickstepDuration = 0.45f;
    public float quickstepSpeed = 8f;
}
