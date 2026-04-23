using UnityEngine;

/// <summary>
/// Idle 模块配置数据容器。
/// 功能：集中保存 SummonerIdleModule 所需的全部可调参数，以 ScriptableObject 形式存在，方便 Inspector 统一配置与复用。
/// 工作链路：由状态总控脚本持有，在对象初始化阶段传入 SummonerIdleModule.Initialize()；之后 Idle 模块只读不写。
/// 说明：玩家感知相关参数（检测半径、视野角度等）已移至 DetectionConfig，本配置只负责 Patrol 与 Observe 行为参数。
/// </summary>
[CreateAssetMenu(fileName = "IdleConfig", menuName = "Enemy/SummonerIdleConfig")]
public class IdleConfig : ScriptableObject
{
    [Header("Debug")]
    [Tooltip("是否开启 Idle 模块 Debug 输出")]
    public bool enableIdleDebug = true;

    [Header("Patrol 参数")]
    [Tooltip("巡逻圆周半径（世界单位）")]
    public float patrolRadius = 5f;

    [Tooltip("每次推进的角度步长（度）")]
    public float patrolAngleStep = 45f;

    [Tooltip("初始巡逻起始角度（度，0 = +X 轴方向）")]
    public float initialPatrolAngle = 0f;

    [Tooltip("是否顺时针巡逻（true = 顺时针 = +1，false = 逆时针 = -1）")]
    public bool useClockwisePatrol = true;

    [Tooltip("巡逻移动速度")]
    public float patrolMoveSpeed = 2f;

    [Tooltip("判定到达 anchor 的距离阈值")]
    public float anchorReachThreshold = 0.4f;

    [Header("Observe 参数")]
    [Tooltip("每次到达 anchor 后原地观察的停留时长（秒）")]
    public float idleDuration = 2f;

    [Tooltip("观察阶段旋转朝向的速度（角速度系数）")]
    public float observeTurnSpeed = 2f;

    [Header("Idle Animation")]
    [Tooltip("是否在 Idle 状态期间切换为放松待机动画（未发现玩家 / 脱战复位后）")]
    public bool useRelaxIdleBool = true;

    [Tooltip("放松待机动画 Animator Bool 名称，整个 Idle 期间为 true，发现玩家退出 Idle 后为 false（回到战斗警戒姿态）")]
    public string relaxIdleBoolName = "RelaxIdle";
}
