using UnityEngine;

public class FinalBossPhaseOneAttackCommitState : FinalBossPhaseOneState
{
    public override string StateName => "AttackCommit";

    private const string LOG = "[AttackCommit_DEBUG]";

    private FinalBossPhaseOneAttackCommitStateData Data => controller.GetConfig().attackCommit;
    private bool hitTriggered;
    private FinalBossPhaseOneAIController.AttackAnimationType selectedAttackType;
    private FinalBossPhaseOneHitbox activeHitbox;

    public FinalBossPhaseOneAttackCommitState(FinalBossPhaseOneAIController owner, FinalBossPhaseOneContext phaseContext) : base(owner, phaseContext)
    {
    }

    public override void Enter()
    {
        base.Enter();
        hitTriggered = false;
        context.StopMovement();
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
        controller.SetStunnedAnimationActive(false);

        selectedAttackType = controller.ConsumeQueuedAttackAnimationType();
        if (selectedAttackType == FinalBossPhaseOneAIController.AttackAnimationType.None)
            selectedAttackType = controller.RollNextAttackAnimationType();

        controller.SetAttackAnimationType(selectedAttackType);

        // 订阅 Hitbox（PrepareAttack 可能已经订阅过，这里确保 AttackCommit 也有自己的订阅）
        activeHitbox = controller.GetHitboxForType(selectedAttackType);
        if (activeHitbox != null)
        {
            // 先移除可能残留的 PrepareAttack 订阅，避免重复
            activeHitbox.OnHit -= HandleHitboxHit;
            activeHitbox.OnHit += HandleHitboxHit;
            Debug.Log($"{LOG} [Enter] Subscribed to hitbox '{activeHitbox.gameObject.name}' for {selectedAttackType}. " +
                $"duration={Data.duration:F2}, dist={context.DistanceToPlayer:F2}");
        }
        else
        {
            Debug.LogWarning($"{LOG} [Enter] NO HITBOX for {selectedAttackType}!");
        }

        if (selectedAttackType == FinalBossPhaseOneAIController.AttackAnimationType.Punch)
        {
            controller.TriggerPunchAnimation();
            Debug.Log($"{LOG} [Enter] Triggered Punch animation.");
        }
        else
        {
            controller.TriggerKickAnimation();
            Debug.Log($"{LOG} [Enter] Triggered Kick animation.");
        }
    }

    public override void Tick()
    {
        base.Tick();
        context.FacePlayer(Data.faceRotationSpeed);
        context.StopMovement();

        if (elapsedTime >= Data.duration)
        {
            Debug.Log($"{LOG} [Tick] Duration elapsed ({Data.duration:F2}s). hitTriggered={hitTriggered}. " +
                (hitTriggered ? "Attack DID hit." : "Attack did NOT hit."));
            controller.EnterAttackRecover();
        }
    }

    public override void Exit()
    {
        if (activeHitbox != null)
        {
            activeHitbox.OnHit -= HandleHitboxHit;
            activeHitbox.ForceReset();
            Debug.Log($"{LOG} [Exit] Cleaned up hitbox '{activeHitbox.gameObject.name}'. hitTriggered={hitTriggered}");
        }
        activeHitbox = null;
        base.Exit();
    }

    private void HandleHitboxHit(Collider playerCollider)
    {
        if (hitTriggered)
            return;

        hitTriggered = true;
        bool sameColor = controller.IsSameColorAsPlayer();
        Debug.Log($"{LOG} [HandleHit] === HIT CONFIRMED === attack={selectedAttackType}, " +
            $"hitObject='{playerCollider.gameObject.name}', sameColor={sameColor}");

        context.PublishBossAttack();

        if (sameColor)
        {
            Debug.Log($"{LOG} [HandleHit] Same color — Boss entering Stunned.");
            controller.EnterStunned();
        }
    }
}
