using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 召唤杂兵生成执行器的抽象接口。
/// 功能：定义"如何生成杂兵"的契约，将生成细节（Prefab 选择、Instantiate、初始化等）与 Summoning 模块的计时/状态逻辑解耦。
/// 工作链路：由 SummonerSummoningModule 在 SpawnMinions() 中调用；具体实现类由总控脚本在 Initialize 阶段注入。
/// 设计原则：Summoning 模块只关心"什么时候生成"与"生成在哪里"，生成执行器只关心"怎么生成"；职责单一，互不侵入。
/// </summary>
public interface ISummonSpawnExecutor
{
    /// <summary>
    /// 根据指定的出生位置列表，执行一次性杂兵生成，并返回所有成功生成的杂兵 GameObject 列表。
    /// 功能：遍历 spawnPositions，在每个位置生成一个杂兵实例；生成细节（Prefab 类型、朝向、初始属性等）由实现类决定。
    /// 工作链路：由 SummonerSummoningModule.SpawnMinions() 在召唤计时结束时调用一次；返回结果被记录到 spawnedMinions 列表。
    /// 对下游影响：返回的 GameObject 列表将被 Summoning 模块保存，并通过 GetSpawnedMinions() 提供给总控/Commanding 状态接管。
    /// </summary>
    /// <param name="spawnPositions">本次召唤的所有出生世界坐标点。</param>
    /// <returns>所有成功生成的杂兵 GameObject 列表。</returns>
    List<GameObject> SpawnMinions(List<Vector3> spawnPositions);
}
