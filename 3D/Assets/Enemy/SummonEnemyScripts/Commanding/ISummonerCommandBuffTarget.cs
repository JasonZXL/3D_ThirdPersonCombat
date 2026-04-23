using UnityEngine;

/// <summary>
/// 召唤师指挥 Buff 接收器的抽象接口。
/// 功能：定义"杂兵如何接收/移除召唤师指挥 Buff"的契约，将 Buff 的数值改动、表现、图标等细节与 Commanding 模块的状态逻辑解耦。
/// 工作链路：由 SummonerCommandingModule 在 ApplyCommandBuffToMinions / RemoveCommandBuffFromMinions 中通过 GetComponent 获取并调用；具体实现类挂在杂兵 GameObject 上。
/// 设计原则：Commanding 模块只负责"何时开 Buff / 何时关 Buff"，Buff 的具体内容（数值、特效、状态图标等）完全由实现类自行处理；职责单一，互不侵入。
/// </summary>
public interface ISummonerCommandBuffTarget
{
    /// <summary>
    /// 接收召唤师的指挥 Buff。
    /// 功能：由 Commanding 模块在 EnterState 时对每个有效杂兵调用一次；实现类在内部自行处理数值增益、特效播放、状态图标等。
    /// 工作链路：由 SummonerCommandingModule.ApplyCommandBuffToMinions() 调用；仅在 EnterState 时触发一次，Tick 中不重复调用。
    /// 对下游影响：杂兵进入"被指挥"增益状态；具体增益内容由实现类决定。
    /// </summary>
    /// <param name="sourceSummoner">发起指挥的召唤师 GameObject，供来源校验或多召唤师场景区分使用。</param>
    void ApplyCommandBuff(GameObject sourceSummoner);

    /// <summary>
    /// 移除召唤师的指挥 Buff。
    /// 功能：由 Commanding 模块在 ExitState 时对每个仍有效的杂兵调用一次；实现类在内部自行还原数值、停止特效、移除状态图标等。
    /// 工作链路：由 SummonerCommandingModule.RemoveCommandBuffFromMinions() 调用；仅在 ExitState 时触发一次。
    /// 对下游影响：杂兵退出"被指挥"增益状态；具体还原逻辑由实现类决定。
    /// </summary>
    /// <param name="sourceSummoner">发起指挥的召唤师 GameObject，供来源校验使用。</param>
    void RemoveCommandBuff(GameObject sourceSummoner);
}
