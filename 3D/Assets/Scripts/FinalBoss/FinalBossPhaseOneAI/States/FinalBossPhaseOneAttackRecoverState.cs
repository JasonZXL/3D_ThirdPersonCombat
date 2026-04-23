using UnityEngine;

public class FinalBossPhaseOneAttackRecoverState : FinalBossPhaseOneState
{
    public override string StateName => "AttackRecover";

    private FinalBossPhaseOneAttackRecoverStateData Data => controller.GetConfig().attackRecover;
    private bool retreatTriggered;
    private bool retreatCompleted;
    private float retreatElapsed;
    private float pauseElapsed;
    private Vector3 lastPosition;
    private float retreatedDistance;

    public FinalBossPhaseOneAttackRecoverState(FinalBossPhaseOneAIController owner, FinalBossPhaseOneContext phaseContext) : base(owner, phaseContext)
    {
    }

    public override void Enter()
    {
        base.Enter();
        retreatTriggered = false;
        retreatCompleted = false;
        retreatElapsed = 0f;
        pauseElapsed = 0f;
        retreatedDistance = 0f;
        lastPosition = context.BossTransform != null ? context.BossTransform.position : Vector3.zero;
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.QuickstepBackward);
        controller.SetAttackAnimationType(FinalBossPhaseOneAIController.AttackAnimationType.None);
        controller.SetStunnedAnimationActive(false);
    }

    public override void Tick()
    {
        base.Tick();
        context.FacePlayer(Data.faceRotationSpeed);

        if (!retreatTriggered)
        {
            retreatTriggered = true;
            controller.TriggerQuickstepBackwardAnimation();
        }

        if (!retreatCompleted)
        {
            retreatElapsed += Time.deltaTime;
            context.MoveAwayFromPlayer(Data.retreatSpeed);

            if (context.BossTransform != null)
            {
                Vector3 currentPosition = context.BossTransform.position;
                retreatedDistance += Vector3.Distance(lastPosition, currentPosition);
                lastPosition = currentPosition;
            }

            bool reachedNominalRetreat =
                retreatElapsed >= Data.retreatDuration ||
                retreatedDistance >= Data.retreatDistance;

            bool clearedAttackRange =
                context.DistanceToPlayer > controller.GetDirectAttackEnterDistance();

            if (!reachedNominalRetreat || !clearedAttackRange)
                return;

            retreatCompleted = true;
            context.StopMovement();
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
        }

        pauseElapsed += Time.deltaTime;
        if (pauseElapsed >= Data.duration)
            controller.EnterCombatStateByDistance();
    }
}
