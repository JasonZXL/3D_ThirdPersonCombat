using UnityEngine;

[CreateAssetMenu(menuName = "FinalBoss/Phase One/Retreat Reset Data", fileName = "FB_PhaseOne_RetreatReset")]
public class FinalBossPhaseOneRetreatResetStateData : FinalBossPhaseOneStateData
{
    public float retreatDuration = 0.6f;
    public float retreatSpeed = 4f;
    public float retreatDistance = 3.5f;
    public float postRetreatPause = 0.35f;
    public float faceRotationSpeed = 720f;
}
