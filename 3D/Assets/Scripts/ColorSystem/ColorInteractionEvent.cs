using UnityEngine;

public struct ColorInteractionEvent
{
    public GameObject Source;          // 事件发起者
    public GameObject Target;          // 事件目标
    public ColorType SourceColor;      // 发起者颜色
    public ColorType TargetColor;      // 目标颜色
    public ColorInteractionType Type;  // 交互类型
    public Vector3 InteractionPoint;   // 交互发生位置
    
    public ColorInteractionEvent(GameObject source, GameObject target, ColorInteractionType type, Vector3 point = default)
    {
        Source = source;
        Target = target;
        SourceColor = source?.GetComponent<ColorComponent>()?.CurrentColor ?? ColorType.Neutral;
        TargetColor = target?.GetComponent<ColorComponent>()?.CurrentColor ?? ColorType.Neutral;
        Type = type;
        InteractionPoint = point == default ? target.transform.position : point;
    }
}
