using UnityEngine;

public class FinalBossPhaseOneIdleObserveState : FinalBossPhaseOneState
{
    public override string StateName => "IdleObserve";

    private FinalBossPhaseOneIdleObserveStateData Data => controller.GetConfig().idleObserve;
    private float localStateTimer;
    private bool isWaiting;
    private float strafeDirection = 1f;

    public FinalBossPhaseOneIdleObserveState(FinalBossPhaseOneAIController owner, FinalBossPhaseOneContext phaseContext) : base(owner, phaseContext)
    {
    }

    public override void Enter()
    {
        base.Enter();
        PickNewStrafeDirection();
        isWaiting = false;
        localStateTimer = Random.Range(Data.strafeMoveDurationMin, Data.strafeMoveDurationMax);
        controller.SetAttackAnimationType(FinalBossPhaseOneAIController.AttackAnimationType.None);
        controller.SetStunnedAnimationActive(false);
    }

    public override void Tick()
    {
        base.Tick();
        context.FacePlayer(Data.faceRotationSpeed);

        if (controller.ConsumePlayerAggroRequest())
        {
            controller.EnterRetreatReset();
            return;
        }

        if (elapsedTime >= Data.autoEngageDelay)
        {
            controller.EnterCombatStateByDistance();
            return;
        }

        localStateTimer -= Time.deltaTime;

        if (!isWaiting)
        {
            context.StrafeAroundPlayer(strafeDirection, Data.strafeSpeed, Data.preferredRadius);
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.ObserveStrafe);
        }
        else
        {
            context.StopMovement();
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
        }

        if (localStateTimer > 0f)
            return;

        if (!isWaiting)
        {
            isWaiting = true;
            localStateTimer = Data.observeWaitDuration;
            context.StopMovement();
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
            return;
        }

        PickNewStrafeDirection();
        isWaiting = false;
        localStateTimer = Random.Range(Data.strafeMoveDurationMin, Data.strafeMoveDurationMax);
    }

    private void PickNewStrafeDirection()
    {
        strafeDirection = Random.value > 0.5f ? 1f : -1f;
    }
}
