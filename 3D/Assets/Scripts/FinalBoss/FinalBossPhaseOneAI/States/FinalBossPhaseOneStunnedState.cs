using UnityEngine;

public class FinalBossPhaseOneStunnedState : FinalBossPhaseOneState
{
    public override string StateName => "Stunned";

    private FinalBossPhaseOneStunnedStateData Data => controller.GetConfig().stunned;

    public FinalBossPhaseOneStunnedState(FinalBossPhaseOneAIController owner, FinalBossPhaseOneContext phaseContext) : base(owner, phaseContext)
    {
    }

    public override void Enter()
    {
        base.Enter();
        context.StopMovement();
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
        controller.SetAttackAnimationType(FinalBossPhaseOneAIController.AttackAnimationType.None);
        controller.SetStunnedAnimationActive(true);
        controller.TriggerStunnedAnimation();
    }

    public override void Tick()
    {
        base.Tick();

        if (elapsedTime >= Data.duration)
            controller.EnterRetreatReset();
    }
}
