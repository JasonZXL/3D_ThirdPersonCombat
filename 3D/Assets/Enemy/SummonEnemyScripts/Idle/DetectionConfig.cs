using UnityEngine;

/// <summary>
/// Detection 模块配置数据容器。
/// 功能：集中保存 SummonerDetectionModule 所需的全部可调参数，以 ScriptableObject 形式存在，方便 Inspector 统一配置与复用。
/// 工作链路：由 SummonerIdleModule 持有，在 Initialize 阶段传入 SummonerDetectionModule.Initialize()；之后 Detection 模块只读不写。
/// </summary>
[CreateAssetMenu(fileName = "DetectionConfig", menuName = "Enemy/SummonerDetectionConfig")]
public class DetectionConfig : ScriptableObject
{
    [Header("Debug")]
    [Tooltip("是否开启 Detection 模块 Debug 输出")]
    public bool enableDetectDebug = true;

    [Tooltip("是否绘制 Detection Gizmos（Scene 视图可视化）")]
    public bool showDetectGizmos = true;

    [Tooltip("是否输出未命中原因的节流 Debug（建议开发期开启）")]
    public bool enableMissReasonDebug = true;

    [Header("球形保底发现")]
    [Tooltip("球形保底发现半径：玩家进入此范围内无论朝向均被发现")]
    public float guaranteeDetectRadius = 2f;

    [Header("锥形视野发现")]
    [Tooltip("锥形视野最大检测距离")]
    public float visionDetectRadius = 10f;

    [Tooltip("锥形视野总角度（度），实际判定使用半角，即此值的一半")]
    [Range(10f, 360f)]
    public float visionAngle = 90f;
}
