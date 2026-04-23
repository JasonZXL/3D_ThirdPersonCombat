using System;
using UnityEngine;

public abstract class FinalBossStageBase : MonoBehaviour
{
    [Header("Stage Debug")]
    [SerializeField] protected bool showDebugLogs = true;

    protected FinalBossBattleDirector director;
    protected BossController bossController;

    public bool IsActive { get; private set; }

    public event Action<FinalBossStageBase> OnStageCompleted;
    public event Action<FinalBossStageBase, string> OnStageFailed;

    public virtual void Initialize(FinalBossBattleDirector owner, BossController boss)
    {
        director = owner;
        bossController = boss;
    }

    public virtual void EnterStage()
    {
        IsActive = true;
        enabled = true;
        Log($"EnterStage -> {name}");
    }

    public virtual void ExitStage()
    {
        IsActive = false;
        enabled = false;
        Log($"ExitStage -> {name}");
    }

    public virtual bool CanBossTakeDirectDamage() => true;

    public virtual bool IsStageComplete() => false;

    public virtual void ForceFail(string reason)
    {
        if (!IsActive) return;
        Log($"ForceFail -> {reason}");
        OnStageFailed?.Invoke(this, reason);
    }

    protected void CompleteStage()
    {
        if (!IsActive) return;
        Log($"CompleteStage -> {name}");
        OnStageCompleted?.Invoke(this);
    }

    protected void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossStageBase] {message}");
    }
}
