using UnityEngine;

[CreateAssetMenu(menuName = "FinalBoss/Phase One/Attack Recover Data", fileName = "FB_PhaseOne_AttackRecover")]
public class FinalBossPhaseOneAttackRecoverStateData : FinalBossPhaseOneStateData
{
    public float retreatDuration = 0.25f;
    public float retreatSpeed = 7f;
    public float retreatDistance = 1.25f;
    public float faceRotationSpeed = 900f;
    public float duration = 0.2f;
}
