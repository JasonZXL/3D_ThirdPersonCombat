using UnityEngine;

[CreateAssetMenu(menuName = "FinalBoss/Phase One/Idle Observe Data", fileName = "FB_PhaseOne_IdleObserve")]
public class FinalBossPhaseOneIdleObserveStateData : FinalBossPhaseOneStateData
{
    public float strafeMoveDurationMin = 0.8f;
    public float strafeMoveDurationMax = 1.4f;
    public float observeWaitDuration = 0.45f;
    public float strafeSpeed = 2.8f;
    public float preferredRadius = 4.5f;
    public float faceRotationSpeed = 540f;
    public float autoEngageDelay = 4f;
}
