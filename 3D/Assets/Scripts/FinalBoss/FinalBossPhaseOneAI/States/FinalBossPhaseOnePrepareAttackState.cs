using UnityEngine;

public class FinalBossPhaseOnePrepareAttackState : FinalBossPhaseOneState
{
    public override string StateName => "PrepareAttack";

    private const string LOG = "[PrepareAttack_DEBUG]";

    private FinalBossPhaseOnePrepareAttackStateData Data => controller.GetConfig().prepareAttack;
    private FinalBossPhaseOneAIController.AttackAnimationType plannedAttackType;
    private float plannedAttackRange;
    private float plannedRushDistance;
    private Vector3 rushTargetPoint;

    private FinalBossPhaseOneHitbox activeHitbox;
    private bool hitTriggered;

    public FinalBossPhaseOnePrepareAttackState(FinalBossPhaseOneAIController owner, FinalBossPhaseOneContext phaseContext) : base(owner, phaseContext)
    {
    }

    public override void Enter()
    {
        base.Enter();
        hitTriggered = false;

        plannedAttackType = controller.RollNextAttackAnimationType();
        plannedAttackRange = controller.GetAttackRangeForType(plannedAttackType);
        plannedRushDistance = Mathf.Max(0f, controller.GetDirectAttackEnterDistance() - plannedAttackRange);
        rushTargetPoint = context.BossTransform != null
            ? context.BossTransform.position
            : Vector3.zero;

        if (context.BossTransform != null && context.PlayerTransform != null)
        {
            Vector3 toPlayer = context.PlayerTransform.position - context.BossTransform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
                rushTargetPoint = context.BossTransform.position + toPlayer.normalized * plannedRushDistance;
        }

        controller.QueueAttackAnimationType(plannedAttackType);
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Approach);
        controller.SetAttackAnimationType(FinalBossPhaseOneAIController.AttackAnimationType.None);
        controller.SetStunnedAnimationActive(false);
        if (!controller.ConsumePrepareTelegraphSuppression())
            context.PlayPrepareAttackTelegraph();

        // 在 PrepareAttack 阶段就订阅 Hitbox，因为动画事件可能在 AttackCommit.Enter 之前触发
        activeHitbox = controller.GetHitboxForType(plannedAttackType);
        if (activeHitbox != null)
        {
            activeHitbox.ForceReset();
            activeHitbox.OnHit += HandleHitboxHit;
            Debug.Log($"{LOG} [Enter] Subscribed to hitbox '{activeHitbox.gameObject.name}' for {plannedAttackType}. " +
                $"dist={context.DistanceToPlayer:F2}");
        }
        else
        {
            Debug.LogWarning($"{LOG} [Enter] NO HITBOX for {plannedAttackType}!");
        }
    }

    public override void Tick()
    {
        base.Tick();
        context.FacePlayer(Data.faceRotationSpeed);

        if (elapsedTime <= Data.preRushDuration && plannedRushDistance > 0f)
        {
            context.MoveTowardsWorldPoint(rushTargetPoint, Data.preRushSpeed, Data.stopDistanceBuffer);
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Approach);
        }
        else
        {
            context.StopMovement();
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
        }

        if (elapsedTime >= Data.duration)
            controller.EnterAttackCommit();
    }

    public override void Exit()
    {
        // 如果还没命中，保持订阅传递给 AttackCommitState；如果已命中则清理
        if (activeHitbox != null)
        {
            if (hitTriggered)
            {
                activeHitbox.OnHit -= HandleHitboxHit;
                activeHitbox.ForceReset();
                Debug.Log($"{LOG} [Exit] Hit already triggered in PrepareAttack phase. Cleaned up hitbox.");
            }
            else
            {
                // 不取消订阅——AttackCommitState 会接管清理
                Debug.Log($"{LOG} [Exit] No hit yet. Hitbox subscription stays active for AttackCommit.");
            }
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
        Debug.Log($"{LOG} [HandleHit] === HIT in PrepareAttack phase === attack={plannedAttackType}, " +
            $"hitObject='{playerCollider.gameObject.name}', sameColor={sameColor}");

        context.PublishBossAttack();

        if (sameColor)
        {
            Debug.Log($"{LOG} [HandleHit] Same color — Boss entering Stunned.");
            controller.EnterStunned();
        }
    }
}
