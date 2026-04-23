using UnityEngine;

public abstract class FinalBossPhaseOneState
{
    protected readonly FinalBossPhaseOneAIController controller;
    protected readonly FinalBossPhaseOneContext context;
    protected float elapsedTime;

    public abstract string StateName { get; }
    public virtual bool CanBeInterruptedByStun => true;

    protected FinalBossPhaseOneState(FinalBossPhaseOneAIController owner, FinalBossPhaseOneContext phaseContext)
    {
        controller = owner;
        context = phaseContext;
    }

    public virtual void Enter()
    {
        elapsedTime = 0f;
    }

    public virtual void Tick()
    {
        elapsedTime += Time.deltaTime;
    }

    public virtual void Exit()
    {
    }
}
