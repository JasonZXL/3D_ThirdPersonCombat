using UnityEngine;

/// <summary>
/// Summoning 模块配置数据容器。
/// 功能：集中保存 SummonerSummoningModule 所需的全部可调参数，以 ScriptableObject 形式存在，方便 Inspector 统一配置与复用。
/// 工作链路：由状态总控脚本持有，在对象初始化阶段传入 SummonerSummoningModule.Initialize()；之后 Summoning 模块只读不写。
/// 说明：spawnPoints（固定召唤点 Transform[]）因为是场景对象引用，无法存放在 ScriptableObject 中，需由总控脚本通过 Initialize 参数传入。
/// </summary>
[CreateAssetMenu(fileName = "SummoningConfig", menuName = "Enemy/SummonerSummoningConfig")]
public class SummoningConfig : ScriptableObject
{
    [Header("Debug")]
    [Tooltip("是否开启 Summoning 模块 Debug 输出")]
    public bool enableSummoningDebug = true;

    [Header("Timing")]
    [Tooltip("整个 Summoning 主状态的总持续时间（秒）")]
    public float summoningDuration = 3f;

    [Tooltip("释放阶段（Summon_End）的时长（秒），代码会在总时长剩余该时间时触发 SummonEnd")]
    public float summonEndDuration = 0.5f;

    [Header("Facing")]
    [Tooltip("是否在 Summoning 期间持续缓慢朝向玩家")]
    public bool keepFacingPlayerDuringSummoning = true;

    [Tooltip("Summoning 状态下朝向玩家的旋转速度（角速度系数）")]
    public float summoningTurnSpeed = 2f;

    [Header("Spawn")]
    [Tooltip("本次固定召唤数量")]
    public int summonCount = 3;

    [Tooltip("是否使用固定召唤点（若为 true，需由总控通过 Initialize 传入 spawnPoints）")]
    public bool useFixedSpawnPoints = false;

    [Tooltip("若不使用固定召唤点，围绕召唤师生成的半径（世界单位）")]
    public float spawnRadius = 4f;

    [Header("Presentation")]
    [Tooltip("是否播放召唤音效")]
    public bool playSummonSound = true;

    [Tooltip("是否播放召唤特效")]
    public bool playSummonEffect = false;

    [Header("Animator - Summon Phases")]
    [Tooltip("是否触发 Summon_Start")]
    public bool useSummonStartTrigger = true;

    [Tooltip("Summon_Start 动画 Trigger 名称")]
    public string summonStartTriggerName = "SummonStart";

    [Tooltip("是否在 Summoning 期间维持 Summon_Loop 布尔")]
    public bool useSummonLoopBool = true;

    [Tooltip("Summon_Loop Bool 名称")]
    public string summonLoopBoolName = "SummonLoop";

    [Tooltip("是否触发 Summon_End")]
    public bool useSummonEndTrigger = true;

    [Tooltip("Summon_End 动画 Trigger 名称")]
    public string summonEndTriggerName = "SummonEnd";
}