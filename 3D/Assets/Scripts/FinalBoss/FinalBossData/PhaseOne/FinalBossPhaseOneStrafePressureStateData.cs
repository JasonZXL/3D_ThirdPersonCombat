using UnityEngine;

[CreateAssetMenu(menuName = "FinalBoss/Phase One/Strafe Pressure Data", fileName = "FB_PhaseOne_StrafePressure")]
public class FinalBossPhaseOneStrafePressureStateData : FinalBossPhaseOneStateData
{
    [Header("Pressure Movement")]
    public float strafeDurationMin = 0.8f;
    public float strafeDurationMax = 1.6f;
    public float waitDurationMin = 1f;
    public float waitDurationMax = 1.5f;
    public float strafeSpeed = 3.25f;
    public float forwardMoveSpeed = 3.8f;
    [Range(0f, 1f)] public float forwardMoveChance = 0.34f;
    [Range(0f, 1f)] public float closeRangeForwardChanceMultiplier = 0.35f;
    public int sideMovesBeforeForwardBonus = 2;
    [Range(0f, 1f)] public float forwardChanceBonusAfterSideMoves = 0.3f;
    public float preferredRadius = 3.2f;
    public float faceRotationSpeed = 720f;

    [Header("Special Attack (Shared Cooldown)")]
    [Tooltip("突袭和三连冲击波共享此冷却时间")]
    public float specialAttackCooldown = 8f;
    [Tooltip("冷却结束后每隔多久滚动一次概率")]
    public float specialAttackRollInterval = 1f;

    [Header("Surprise Attack")]
    [Range(0f, 1f)] public float surpriseAttackChance = 0.22f;
    public float surpriseCastingDuration = 0.45f;
    public float surpriseFrontRingRadius = 2.25f;
    public float surpriseFrontArcDegrees = 120f;
    public float surpriseFrontOffsetFromPlayer = 1.2f;
    public float surpriseAnchorPauseDuration = 0.55f;
    public float surpriseBehindDistance = 1.35f;
    [Tooltip("背后点的散布半径（独立于前方 ringRadius），控制闪现到玩家身后的横向偏移")]
    public float surpriseBehindSpreadRadius = 0.5f;

    [Header("Triple Shockwave")]
    [Range(0f, 1f)] public float tripleShockwaveChance = 0.22f;
    [Tooltip("蓄力阶段时长")]
    public float shockwaveCastingDuration = 0.5f;
    [Tooltip("冲击波发射次数")]
    public int shockwaveCount = 3;
    [Tooltip("每次冲击波之间的间隔")]
    public float shockwaveInterval = 0.8f;
    [Tooltip("最后一次冲击波发射后的恢复停顿")]
    public float shockwaveRecoverDuration = 0.4f;

    [Header("Ranged Shot (During Wait)")]
    [Tooltip("等待停顿期间执行射击的概率")]
    [Range(0f, 1f)] public float rangedShotChance = 0.3f;
    [Tooltip("射击动画播放的前摇时长（触发动画后等待这段时间再发射弹体）")]
    public float rangedShotWindup = 0.35f;
    [Tooltip("射击发射后的恢复停顿")]
    public float rangedShotRecoverDuration = 0.3f;

}
