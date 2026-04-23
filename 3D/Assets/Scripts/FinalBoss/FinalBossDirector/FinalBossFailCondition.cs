using System;
using UnityEngine;

public class FinalBossFailCondition : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HealthSystem playerHealthSystem;

    [Header("Failure Rules")]
    [SerializeField] private bool failOnPlayerDeath = true;
    [SerializeField] private bool showDebugLogs = true;

    private FinalBossBattleDirector director;
    private bool hasFailed;

    public event Action<string> OnFailed;

    public void Initialize(FinalBossBattleDirector owner)
    {
        director = owner;

        if (playerHealthSystem == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerHealthSystem = player.GetComponent<HealthSystem>();
        }
    }

    private void Update()
    {
        if (hasFailed || director == null || !director.IsBattleRunning)
            return;

        if (failOnPlayerDeath && playerHealthSystem != null && !playerHealthSystem.IsAlive)
        {
            RaiseFailure("Player died");
        }
    }

    public void FailImmediately(string reason)
    {
        if (hasFailed) return;
        RaiseFailure(reason);
    }

    private void RaiseFailure(string reason)
    {
        hasFailed = true;

        if (showDebugLogs)
            Debug.LogWarning($"[FinalBossFailCondition] {reason}");

        OnFailed?.Invoke(reason);
    }
}
