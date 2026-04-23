using UnityEngine;

public class FinalBossPhaseOneApproachState : FinalBossPhaseOneState
{
    public override string StateName => "ApproachPlayer";

    private FinalBossPhaseOneApproachStateData Data => controller.GetConfig().approach;
    private float quickstepRemaining;
    private bool quickstepTriggered;

    public FinalBossPhaseOneApproachState(FinalBossPhaseOneAIController owner, FinalBossPhaseOneContext phaseContext) : base(owner, phaseContext)
    {
    }

    public override void Enter()
    {
        base.Enter();
        controller.SetAttackAnimationType(FinalBossPhaseOneAIController.AttackAnimationType.None);
        controller.SetStunnedAnimationActive(false);

        bool canQuickstep =
            context.DistanceToPlayer >= Data.quickstepMinDistance &&
            Random.value <= Data.quickstepChance;

        if (canQuickstep)
        {
            quickstepRemaining = Data.quickstepDuration;
            quickstepTriggered = true;
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.QuickstepForward);
            controller.TriggerQuickstepForwardAnimation();
        }
        else
        {
            quickstepRemaining = 0f;
            quickstepTriggered = false;
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Approach);
        }
    }

    public override void Tick()
    {
        base.Tick();
        context.FacePlayer(Data.faceRotationSpeed);

        // 颜色切换动画播放中：停止移动，跳过所有其他逻辑
        if (controller.TickColorSwitch())
        {
            context.StopMovement();
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
            return;
        }

        if (quickstepRemaining > 0f)
        {
            quickstepRemaining -= Time.deltaTime;
            context.MoveTowardsPlayer(Data.quickstepSpeed, controller.GetDirectAttackEnterDistance() + Data.stopDistanceBuffer);
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.QuickstepForward);
        }
        else
        {
            if (quickstepTriggered)
                quickstepTriggered = false;

            context.MoveTowardsPlayer(Data.moveSpeed, controller.GetDirectAttackEnterDistance() + Data.stopDistanceBuffer);
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Approach);
        }

        if (controller.EvaluateDistanceBand() != FinalBossPhaseOneAIController.DistanceBand.Far)
        {
            controller.EnterCombatStateByDistance();
            return;
        }

        if (elapsedTime >= Data.maxApproachDuration)
            controller.EnterStrafePressure();
    }
}
