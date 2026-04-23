using UnityEngine;

[CreateAssetMenu(menuName = "FinalBoss/Phase One/Attack Commit Data", fileName = "FB_PhaseOne_AttackCommit")]
public class FinalBossPhaseOneAttackCommitStateData : FinalBossPhaseOneStateData
{
    public float duration = 0.45f;
    [Range(0f, 1f)] public float hitMomentNormalized = 0.4f;
    public float lungeSpeed = 5f;
    public float faceRotationSpeed = 1080f;
    [Range(0f, 1f)] public float punchChance = 0.5f;
    public float punchAttackRange = 2.25f;
    public float kickAttackRange = 2.9f;
}
