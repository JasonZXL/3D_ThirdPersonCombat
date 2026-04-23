using UnityEngine;

/// <summary>
/// Alert 模块配置数据容器。
/// 功能：集中保存 SummonerAlertModule 所需的全部可调参数，以 ScriptableObject 形式存在，方便 Inspector 统一配置与复用。
/// 工作链路：由状态总控脚本持有，在对象初始化阶段传入 SummonerAlertModule.Initialize()；之后 Alert 模块只读不写。
/// 说明：Alert 是发现玩家后的短时过渡状态，本配置只负责 Alert 持续时间、朝向速度与表现触发开关。
/// </summary>
[CreateAssetMenu(fileName = "AlertConfig", menuName = "Enemy/SummonerAlertConfig")]
public class AlertConfig : ScriptableObject
{
    [Header("Debug")]
    [Tooltip("是否开启 Alert 模块 Debug 输出")]
    public bool enableAlertDebug = true;

    [Header("Timing")]
    [Tooltip("Alert 状态持续时间（秒）")]
    public float alertDuration = 2f;

    [Header("Facing")]
    [Tooltip("Alert 状态下朝向玩家的旋转速度（角速度系数）")]
    public float alertTurnSpeed = 3f;

    [Header("Audio / VFX")]
    [Tooltip("是否播放嚎叫音效")]
    public bool playAlertSound = true;

    [Tooltip("是否播放警戒特效")]
    public bool playAlertEffect = false;

    [Header("Animation")]
    [Tooltip("是否触发 Alert 动画 Trigger")]
    public bool useAlertTrigger = true;

    [Tooltip("Alert 动画 Trigger 名称（例如 \"Alert\" / \"Howl\"）")]
    public string alertTriggerName = "Alert";
}
