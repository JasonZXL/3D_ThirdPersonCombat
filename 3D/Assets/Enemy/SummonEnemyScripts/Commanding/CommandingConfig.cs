using UnityEngine;

/// <summary>
/// Commanding 模块配置数据容器。
/// 功能：集中保存 SummonerCommandingModule 所需的全部可调参数，以 ScriptableObject 形式存在，方便 Inspector 统一配置与复用。
/// 工作链路：由状态总控脚本持有，在对象初始化阶段传入 SummonerCommandingModule.Initialize()；之后 Commanding 模块只读不写。
/// 说明：Buff 的具体数值不在本配置中定义，本配置只负责 Commanding 状态自身的行为参数（中断距离、反应延迟、朝向速度、表现开关等）。
/// </summary>
[CreateAssetMenu(fileName = "CommandingConfig", menuName = "Enemy/SummonerCommandingConfig")]
public class CommandingConfig : ScriptableObject
{
    [Header("Debug")]
    [Tooltip("是否开启 Commanding 模块 Debug 输出")]
    public bool enableCommandingDebug = true;

    [Header("Break Condition")]
    [Tooltip("玩家逼近到该距离时，必须中断指挥并切入 SelfDefense")]
    public float selfDefenseBreakDistance = 3f;

    [Header("Reaction Delay")]
    [Tooltip("检测到玩家逼近 breakDistance 后、真正置位 shouldBreak 前的反应停顿时长（秒），模拟召唤师从指挥姿态切换到自卫的反应时间")]
    public float reactionDelay = 0.6f;

    [Header("Facing")]
    [Tooltip("是否在挥旗时持续缓慢朝向玩家")]
    public bool keepFacingPlayerDuringCommanding = true;

    [Tooltip("Commanding 状态下朝向玩家的旋转速度（角速度系数）")]
    public float commandingTurnSpeed = 2f;

    [Header("Optional Positioning")]
    [Tooltip("是否启用保持后方站位逻辑（第一版可先不实现，仅保留配置位）")]
    public bool useBacklineHold = false;

    [Header("Animation / Presentation")]
    [Tooltip("是否设置挥旗状态 Bool")]
    public bool useCommandingBool = true;

    [Tooltip("挥旗状态 Animator Bool 名称（例如 \"Commanding\"）")]
    public string commandingBoolName = "Commanding";

    [Tooltip("是否播放挥旗中的持续特效")]
    public bool playCommandingEffect = false;
}