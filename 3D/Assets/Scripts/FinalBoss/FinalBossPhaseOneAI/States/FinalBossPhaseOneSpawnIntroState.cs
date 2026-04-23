using UnityEngine;

public class FinalBossPhaseOneSpawnIntroState : FinalBossPhaseOneState
{
    public override string StateName => "SpawnIntro";
    public override bool CanBeInterruptedByStun => false;

    private FinalBossPhaseOneSpawnIntroStateData Data => controller.GetConfig().spawnIntro;

    public FinalBossPhaseOneSpawnIntroState(FinalBossPhaseOneAIController owner, FinalBossPhaseOneContext phaseContext) : base(owner, phaseContext)
    {
    }

    public override void Enter()
    {
        base.Enter();
        context.StopMovement();
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
        controller.SetAttackAnimationType(FinalBossPhaseOneAIController.AttackAnimationType.None);
        controller.SetStunnedAnimationActive(false);
        controller.TriggerIntroAnimation();
    }

    public override void Tick()
    {
        base.Tick();
        context.FacePlayer(Data.faceRotationSpeed);

        if (elapsedTime >= Data.duration)
            controller.EnterIdleObserve();
    }
}
