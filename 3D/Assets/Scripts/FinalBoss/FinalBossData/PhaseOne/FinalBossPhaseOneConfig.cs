using UnityEngine;

[CreateAssetMenu(menuName = "FinalBoss/Phase One/Config", fileName = "FinalBossPhaseOneConfig")]
public class FinalBossPhaseOneConfig : ScriptableObject
{
    [Header("Distance Rules")]
    public float directAttackDistance = 2.25f;
    public float farDistanceThreshold = 6f;

    [Header("Color Switch (Approach & StrafePressure)")]
    [Tooltip("颜色切换冷却时间（跨状态共享）")]
    public float colorSwitchCooldown = 6f;
    [Tooltip("冷却结束后每隔多久滚动一次概率")]
    public float colorSwitchRollInterval = 1.5f;
    [Tooltip("每次滚动切换颜色的概率")]
    [Range(0f, 1f)] public float colorSwitchChance = 0.25f;
    [Tooltip("切换动画播放时长（动画结束后才真正切换颜色）")]
    public float colorSwitchAnimDuration = 0.4f;

    [Header("State Data")]
    public FinalBossPhaseOneSpawnIntroStateData spawnIntro;
    public FinalBossPhaseOneIdleObserveStateData idleObserve;
    public FinalBossPhaseOneApproachStateData approach;
    public FinalBossPhaseOneStrafePressureStateData strafePressure;
    public FinalBossPhaseOnePrepareAttackStateData prepareAttack;
    public FinalBossPhaseOneAttackCommitStateData attackCommit;
    public FinalBossPhaseOneAttackRecoverStateData attackRecover;
    public FinalBossPhaseOneStunnedStateData stunned;
    public FinalBossPhaseOneRetreatResetStateData retreatReset;
}
