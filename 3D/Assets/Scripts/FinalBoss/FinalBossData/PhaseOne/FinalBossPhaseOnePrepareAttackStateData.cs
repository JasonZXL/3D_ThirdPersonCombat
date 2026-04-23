using UnityEngine;

[CreateAssetMenu(menuName = "FinalBoss/Phase One/Prepare Attack Data", fileName = "FB_PhaseOne_PrepareAttack")]
public class FinalBossPhaseOnePrepareAttackStateData : FinalBossPhaseOneStateData
{
    public float duration = 0.65f;
    public float faceRotationSpeed = 900f;
    public float preRushDuration = 0.3f;
    public float preRushSpeed = 6.25f;
    public float stopDistanceBuffer = 0.2f;
}
