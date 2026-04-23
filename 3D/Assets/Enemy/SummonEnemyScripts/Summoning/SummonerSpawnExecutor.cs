using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最小可用的召唤生成器。
/// 功能：实现 ISummonSpawnExecutor，接收一组世界坐标，在这些坐标实例化杂兵 prefab，并返回生成结果列表。
/// 工作链路：挂在召唤师或场景中的任意对象上；由 SummonerStateController 在 Awake 时解析为 ISummonSpawnExecutor；由 SummonerSummoningModule 在召唤结束时调用 SpawnMinions。
/// </summary>
public class SummonerSpawnExecutor : MonoBehaviour, ISummonSpawnExecutor
{
    [Header("Spawn Prefab")]
    [Tooltip("被召唤的杂兵 prefab")]
    [SerializeField] private GameObject minionPrefab;

    [Header("Spawn Settings")]
    [Tooltip("生成时是否继承 prefab 自身朝向；否则默认朝向与当前执行器一致")]
    [SerializeField] private bool usePrefabRotation = true;

    [Tooltip("生成时在 Y 轴额外抬高一点，避免刷进地面")]
    [SerializeField] private float yOffset = 0f;

    [Header("Debug")]
    [SerializeField] private bool enableSpawnDebug = true;

    /// <summary>
    /// 按给定位置列表生成杂兵，并返回生成结果。
    /// 功能：遍历 positions，在每个位置实例化 minionPrefab；若 prefab 为空则返回空列表并输出错误日志。
    /// 工作链路：由 SummonerSummoningModule.SpawnMinions 调用；返回结果会交给总控，再传给 Commanding。
    /// </summary>
    public List<GameObject> SpawnMinions(List<Vector3> positions)
    {
        List<GameObject> result = new List<GameObject>();

        if (minionPrefab == null)
        {
            LogSpawn("[Error] minionPrefab is null. Cannot spawn.");
            return result;
        }

        if (positions == null || positions.Count == 0)
        {
            LogSpawn("[Warn] positions is null or empty. Nothing spawned.");
            return result;
        }

        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 spawnPos = positions[i];
            spawnPos.y += yOffset;

            Quaternion rot = usePrefabRotation ? minionPrefab.transform.rotation : transform.rotation;

            GameObject spawned = Instantiate(minionPrefab, spawnPos, rot);
            result.Add(spawned);

            LogSpawn("[Spawn] Spawned minion [" + i + "] = " + spawned.name + " | Pos = " + spawnPos);
        }

        LogSpawn("[Spawn] Spawn complete | Count = " + result.Count);
        return result;
    }

    private void LogSpawn(string message)
    {
        if (!enableSpawnDebug)
            return;

        Debug.Log("[SummonerSpawnExecutor]" + message);
    }
}
