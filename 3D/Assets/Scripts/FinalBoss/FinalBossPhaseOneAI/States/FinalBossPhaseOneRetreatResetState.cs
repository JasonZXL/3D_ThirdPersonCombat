using UnityEngine;

public class FinalBossPhaseOneRetreatResetState : FinalBossPhaseOneState
{
    public override string StateName => "RetreatReset";

    private FinalBossPhaseOneRetreatResetStateData Data => controller.GetConfig().retreatReset;

    public FinalBossPhaseOneRetreatResetState(FinalBossPhaseOneAIController owner, FinalBossPhaseOneContext phaseContext) : base(owner, phaseContext)
    {
    }

    public override void Tick()
    {
        base.Tick();
        context.FacePlayer(Data.faceRotationSpeed);

        if (elapsedTime <= Data.retreatDuration)
        {
            if (elapsedTime <= Time.deltaTime)
            {
                controller.TriggerQuickstepBackwardAnimation();
                controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.QuickstepBackward);
            }
            context.MoveAwayFromPlayer(Data.retreatSpeed);
            return;
        }

        context.StopMovement();
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
        controller.SetAttackAnimationType(FinalBossPhaseOneAIController.AttackAnimationType.None);
        controller.SetStunnedAnimationActive(false);

        if (elapsedTime >= Data.retreatDuration + Data.postRetreatPause)
            controller.EnterCombatStateByDistance();
    }
}
