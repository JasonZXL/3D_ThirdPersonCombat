using UnityEngine;

/// <summary>
/// SelfDefense 模块配置数据容器。
/// 功能：集中保存 SummonerSelfDefenseModule 所需的全部可调参数，以 ScriptableObject 形式存在，方便 Inspector 统一配置与复用。
/// 工作链路：由状态总控脚本持有，在对象初始化阶段传入 SummonerSelfDefenseModule.Initialize()；之后 SelfDefense 模块只读不写。
/// 说明：本配置涵盖距离分层、Reposition / PrepareAttack / Approach / Attack / Pause 五个子状态的时长与表现开关，以及朝向速度。
/// </summary>
[CreateAssetMenu(fileName = "SelfDefenseConfig", menuName = "Enemy/SummonerSelfDefenseConfig")]
public class SelfDefenseConfig : ScriptableObject
{
    [Header("Debug")]
    [Tooltip("是否开启 SelfDefense 模块 Debug 输出")]
    public bool enableSelfDefenseDebug = true;

    [Header("Distance")]
    [Tooltip("强压距离：玩家处于该距离内视为贴脸强压，召唤师认为没有舒适挥拳空间")]
    public float pressureDistance = 2f;

    [Tooltip("攻击就绪距离：Reposition 后若玩家距离介于 PressureDistance 与此值之间，可进入 PrepareAttack")]
    public float attackReadyDistance = 4f;

    [Tooltip("攻击命中距离：Approach 压近至该距离后立即结束 Approach 并进入 Attack，是这一拳真正可以合理挥出的距离")]
    public float attackRange = 2.5f;

    [Tooltip("退出距离：玩家远离到该距离之外时，SelfDefense 可报告允许退出")]
    public float selfDefenseExitDistance = 8f;

    [Header("Reposition")]
    [Tooltip("Reposition 单次调整的目标距离（世界单位）")]
    public float repositionDistance = 2.5f;

    [Tooltip("Reposition 最大允许时长（秒），到时强制结束")]
    public float repositionDuration = 1.0f;

    [Tooltip("Reposition 移动速度")]
    public float repositionMoveSpeed = 3.5f;

    [Header("Reposition Animation")]
    [Tooltip("是否使用后退动画 Bool")]
    public bool useBackstepBool = true;

    [Tooltip("后退动画 Bool 名称")]
    public string backstepBoolName = "Backstep";

    [Header("PrepareAttack")]
    [Tooltip("短前摇持续时间（秒）")]
    public float prepareAttackDuration = 0.5f;

    [Tooltip("是否播放预警特效")]
    public bool useWarningEffect = true;

    [Header("Approach")]
    [Tooltip("Approach 最多允许推进的距离（辅助约束兜底）")]
    public float approachDistance = 3.0f;

    [Tooltip("Approach 最长持续时长（秒），防止卡住")]
    public float approachDuration = 1.2f;

    [Tooltip("Approach 前进速度")]
    public float approachMoveSpeed = 3.0f;

    [Tooltip("是否使用出拳架势前进动画 Bool")]
    public bool useApproachMoveAnim = true;

    [Tooltip("出拳架势前进动画 Bool 名称")]
    public string approachMoveBoolName = "Walk";

    [Header("Attack")]
    [Tooltip("单次挥拳攻击持续时间（秒）")]
    public float attackDuration = 0.6f;

    [Tooltip("是否触发挥拳动画 Trigger")]
    public bool usePunchTrigger = true;

    [Tooltip("挥拳动画 Trigger 名称（例如 \"Punch\"）")]
    public string punchTriggerName = "Punch";

    [Header("Pause")]
    [Tooltip("攻后停顿持续时间（秒）")]
    public float pauseDuration = 0.8f;

    [Header("Facing")]
    [Tooltip("SelfDefense 期间朝向玩家的旋转速度")]
    public float selfDefenseTurnSpeed = 8f;
}
