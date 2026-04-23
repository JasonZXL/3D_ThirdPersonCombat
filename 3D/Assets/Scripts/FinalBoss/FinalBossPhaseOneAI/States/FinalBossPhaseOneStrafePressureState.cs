using UnityEngine;

public class FinalBossPhaseOneStrafePressureState : FinalBossPhaseOneState
{
    public override string StateName => "StrafePressure";

    private const string LOG_PREFIX = "[StrafePressure_DEBUG]";

    // ══════════════════════════════════════════════════════════════
    #region 枚举
    // ══════════════════════════════════════════════════════════════

    private enum PressureMoveMode
    {
        Left = -1,
        Forward = 0,
        Right = 1
    }

    /// <summary>
    /// 特殊攻击类型（突袭 / 三连冲击波共享冷却，只触发一种）
    /// </summary>
    private enum SpecialAttackType
    {
        None = 0,
        SurpriseAttack = 1,
        TripleShockwave = 2
    }

    private enum SurpriseAttackPhase
    {
        None = 0,
        Casting = 1,
        AnchorPause = 2,
        Finished = 3
    }

    private enum ShockwavePhase
    {
        None = 0,
        Casting = 1,
        Firing = 2,
        Recover = 3
    }

    private enum RangedShotPhase
    {
        None = 0,
        Windup = 1,
        Recover = 2
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 字段
    // ══════════════════════════════════════════════════════════════

    private FinalBossPhaseOneStrafePressureStateData Data => controller.GetConfig().strafePressure;

    // --- 基础移动 ---
    private float localStateTimer;
    private float strafeDirection;
    private bool isWaiting;
    private PressureMoveMode currentMoveMode;
    private int consecutiveSideMoveCount;

    // --- 特殊攻击共享冷却 ---
    private float specialCooldownRemaining;
    private float specialRollTimer;
    private SpecialAttackType activeSpecialAttack;

    // --- SurpriseAttack ---
    private SurpriseAttackPhase surprisePhase;
    private float surprisePhaseTimer;
    private Vector3 surpriseAnchorPoint;
    private Vector3 surpriseBehindPoint;

    // --- TripleShockwave ---
    private ShockwavePhase shockwavePhase;
    private float shockwavePhaseTimer;
    private int shockwavesFired;

    // --- RangedShot ---
    private RangedShotPhase rangedShotPhase;
    private float rangedShotTimer;

    #endregion


    public FinalBossPhaseOneStrafePressureState(FinalBossPhaseOneAIController owner, FinalBossPhaseOneContext phaseContext) : base(owner, phaseContext)
    {
    }


    // ══════════════════════════════════════════════════════════════
    #region Enter / Exit
    // ══════════════════════════════════════════════════════════════

    public override void Enter()
    {
        base.Enter();
        context.SetBossVisualVisible(true);
        consecutiveSideMoveCount = 0;
        PickNextPressureMove();
        isWaiting = false;
        localStateTimer = Random.Range(Data.strafeDurationMin, Data.strafeDurationMax);

        // 特殊攻击状态清零（冷却保留跨 Enter）
        activeSpecialAttack = SpecialAttackType.None;
        surprisePhase = SurpriseAttackPhase.None;
        surprisePhaseTimer = 0f;
        shockwavePhase = ShockwavePhase.None;
        shockwavePhaseTimer = 0f;
        shockwavesFired = 0;
        rangedShotPhase = RangedShotPhase.None;
        rangedShotTimer = 0f;

        specialRollTimer = Mathf.Min(specialRollTimer, Data.specialAttackRollInterval);
        controller.SetAttackAnimationType(FinalBossPhaseOneAIController.AttackAnimationType.None);
        controller.SetStunnedAnimationActive(false);

        Debug.Log($"{LOG_PREFIX} [Enter] StrafePressure entered. cooldownRemaining={specialCooldownRemaining:F2}, rollTimer={specialRollTimer:F2}");
    }

    public override void Exit()
    {
        // 特殊攻击被打断时的警告
        if (activeSpecialAttack == SpecialAttackType.SurpriseAttack &&
            surprisePhase != SurpriseAttackPhase.None && surprisePhase != SurpriseAttackPhase.Finished)
        {
            Debug.LogWarning($"{LOG_PREFIX} [Exit] SurpriseAttack INTERRUPTED at phase={surprisePhase}");
        }
        else if (activeSpecialAttack == SpecialAttackType.TripleShockwave &&
                 shockwavePhase != ShockwavePhase.None)
        {
            Debug.LogWarning($"{LOG_PREFIX} [Exit] TripleShockwave INTERRUPTED at phase={shockwavePhase}, fired={shockwavesFired}");
        }

        context.SetBossVisualVisible(true);
        base.Exit();
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region Tick 主循环
    // ══════════════════════════════════════════════════════════════

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

        UpdateSpecialAttackTimers();

        // --- 特殊攻击进行中：最高优先级，不被距离检测打断 ---
        if (activeSpecialAttack != SpecialAttackType.None)
        {
            if (activeSpecialAttack == SpecialAttackType.SurpriseAttack)
                TickSurpriseAttack();
            else if (activeSpecialAttack == SpecialAttackType.TripleShockwave)
                TickTripleShockwave();
            return;
        }

        // --- 远程射击进行中 ---
        if (rangedShotPhase != RangedShotPhase.None)
        {
            TickRangedShot();
            return;
        }

        // --- 距离检测 ---
        FinalBossPhaseOneAIController.DistanceBand currentBand = controller.EvaluateDistanceBand();

        if (currentBand == FinalBossPhaseOneAIController.DistanceBand.Far)
        {
            controller.EnterApproachPlayer();
            return;
        }

        if (currentBand == FinalBossPhaseOneAIController.DistanceBand.DirectAttack)
        {
            controller.EnterPrepareAttack();
            return;
        }

        // --- 尝试触发特殊攻击 ---
        if (CanRollSpecialAttack() && TryStartSpecialAttack())
            return;

        // --- 常规施压移动 ---
        localStateTimer -= Time.deltaTime;

        if (!isWaiting)
        {
            ExecutePressureMove();
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
            // 从移动切换到等待
            isWaiting = true;
            localStateTimer = Random.Range(Data.waitDurationMin, Data.waitDurationMax);
            context.StopMovement();
            controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);

            // 等待开始时尝试远程射击
            if (TryStartRangedShot())
                return;

            return;
        }

        // 等待结束后，再次检测距离
        currentBand = controller.EvaluateDistanceBand();
        if (currentBand == FinalBossPhaseOneAIController.DistanceBand.DirectAttack)
        {
            controller.EnterPrepareAttack();
            return;
        }

        if (CanRollSpecialAttack() && TryStartSpecialAttack())
            return;

        PickNextPressureMove();
        isWaiting = false;
        localStateTimer = Random.Range(Data.strafeDurationMin, Data.strafeDurationMax);
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 基础移动
    // ══════════════════════════════════════════════════════════════

    private void ExecutePressureMove()
    {
        if (currentMoveMode == PressureMoveMode.Forward)
        {
            context.MoveTowardsPlayer(Data.forwardMoveSpeed, controller.GetDirectAttackEnterDistance());
            return;
        }

        context.MoveSidewaysFacingPlayer(strafeDirection, Data.strafeSpeed);
    }

    private void PickNextPressureMove()
    {
        float forwardChance = GetAdjustedForwardMoveChance();
        if (Random.value <= forwardChance)
        {
            currentMoveMode = PressureMoveMode.Forward;
            strafeDirection = 0f;
            consecutiveSideMoveCount = 0;
            return;
        }

        strafeDirection = Random.value > 0.5f ? 1f : -1f;
        currentMoveMode = strafeDirection > 0f
            ? PressureMoveMode.Right
            : PressureMoveMode.Left;
        consecutiveSideMoveCount++;
    }

    private float GetAdjustedForwardMoveChance()
    {
        float attackDistance = controller.GetDirectAttackEnterDistance();
        float farDistance = controller.GetConfig().farDistanceThreshold;
        float currentDistance = context.DistanceToPlayer;

        float distanceWindow = Mathf.Max(0.01f, farDistance - attackDistance);
        float normalizedDistance = Mathf.Clamp01((currentDistance - attackDistance) / distanceWindow);

        float closeRangeMultiplier = Mathf.Lerp(
            Data.closeRangeForwardChanceMultiplier,
            1f,
            normalizedDistance);

        float adjustedChance = Data.forwardMoveChance * closeRangeMultiplier;

        if (consecutiveSideMoveCount >= Mathf.Max(1, Data.sideMovesBeforeForwardBonus))
            adjustedChance += Data.forwardChanceBonusAfterSideMoves;

        return Mathf.Clamp01(adjustedChance);
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 特殊攻击共享冷却
    // ══════════════════════════════════════════════════════════════

    private void UpdateSpecialAttackTimers()
    {
        if (specialCooldownRemaining > 0f)
        {
            specialCooldownRemaining = Mathf.Max(0f, specialCooldownRemaining - Time.deltaTime);
            if (specialCooldownRemaining > 0f)
                return;
            Debug.Log($"{LOG_PREFIX} [Timer] Special attack cooldown finished. Ready to roll.");
        }

        specialRollTimer += Time.deltaTime;
    }

    private bool CanRollSpecialAttack()
    {
        return specialCooldownRemaining <= 0f &&
               activeSpecialAttack == SpecialAttackType.None &&
               specialRollTimer >= Data.specialAttackRollInterval;
    }

    /// <summary>
    /// 滚动概率决定触发突袭还是三连冲击波。
    /// 两者各自有独立概率，按先突袭后冲击波的顺序判定。
    /// 如果两个都没中，等下一个 rollInterval 再试。
    /// </summary>
    private bool TryStartSpecialAttack()
    {
        specialRollTimer = 0f;

        // 先尝试突袭
        float surpriseRoll = Random.value;
        if (surpriseRoll <= Data.surpriseAttackChance)
        {
            Debug.Log($"{LOG_PREFIX} [SpecialRoll] SurpriseAttack won: roll={surpriseRoll:F3} <= {Data.surpriseAttackChance:F3}");
            return TryStartSurpriseAttack();
        }

        // 再尝试三连冲击波
        float shockwaveRoll = Random.value;
        if (shockwaveRoll <= Data.tripleShockwaveChance)
        {
            Debug.Log($"{LOG_PREFIX} [SpecialRoll] TripleShockwave won: roll={shockwaveRoll:F3} <= {Data.tripleShockwaveChance:F3}");
            StartTripleShockwave();
            return true;
        }

        Debug.Log($"{LOG_PREFIX} [SpecialRoll] Both failed: surprise={surpriseRoll:F3}>{Data.surpriseAttackChance:F3}, shockwave={shockwaveRoll:F3}>{Data.tripleShockwaveChance:F3}");
        return false;
    }

    private void ConsumeSpecialAttackCooldown()
    {
        specialCooldownRemaining = Data.specialAttackCooldown;
        specialRollTimer = 0f;
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region SurpriseAttack（突袭）
    // ══════════════════════════════════════════════════════════════

    private bool TryStartSurpriseAttack()
    {
        if (!BuildSurpriseAttackPoints())
        {
            Debug.LogWarning($"{LOG_PREFIX} [Surprise] BuildPoints FAILED");
            return false;
        }

        ConsumeSpecialAttackCooldown();
        activeSpecialAttack = SpecialAttackType.SurpriseAttack;
        surprisePhase = SurpriseAttackPhase.Casting;
        surprisePhaseTimer = 0f;
        isWaiting = false;
        context.StopMovement();
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
        controller.SetAttackAnimationType(FinalBossPhaseOneAIController.AttackAnimationType.None);
        controller.TriggerSurpriseAttackAnimation();

        float anchorDistToPlayer = Vector3.Distance(surpriseAnchorPoint, context.PlayerTransform.position);
        float behindDistToPlayer = Vector3.Distance(surpriseBehindPoint, context.PlayerTransform.position);
        Debug.Log($"{LOG_PREFIX} [Surprise] === INITIATED ===\n" +
            $"  bossPos={GetBossPos()}, anchorPoint={surpriseAnchorPoint} (distPlayer={anchorDistToPlayer:F2})\n" +
            $"  behindPoint={surpriseBehindPoint} (distPlayer={behindDistToPlayer:F2})");
        return true;
    }

    private void TickSurpriseAttack()
    {
        surprisePhaseTimer += Time.deltaTime;
        context.StopMovement();
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);

        switch (surprisePhase)
        {
            case SurpriseAttackPhase.Casting:
                if (surprisePhaseTimer < Data.surpriseCastingDuration)
                    return;

                context.SetBossVisualVisible(false);
                context.TeleportToWorldPoint(surpriseAnchorPoint);
                context.SnapFacePlayer();
                context.SetBossVisualVisible(true);
                context.PlayPrepareAttackTelegraph();
                controller.TriggerSurpriseAttackAnimation();

                Debug.Log($"{LOG_PREFIX} [Surprise] Casting->Anchor. POST bossPos={GetBossPos()}, distPlayer={context.DistanceToPlayer:F2}");

                surprisePhase = SurpriseAttackPhase.AnchorPause;
                surprisePhaseTimer = 0f;
                return;

            case SurpriseAttackPhase.AnchorPause:
                if (surprisePhaseTimer < Data.surpriseAnchorPauseDuration)
                    return;

                context.SetBossVisualVisible(false);
                context.TeleportToWorldPoint(surpriseBehindPoint);
                context.SnapFacePlayer();
                context.SetBossVisualVisible(true);

                Debug.Log($"{LOG_PREFIX} [Surprise] AnchorPause->Behind. POST bossPos={GetBossPos()}, distPlayer={context.DistanceToPlayer:F2}");

                controller.SuppressNextPrepareTelegraph();
                surprisePhase = SurpriseAttackPhase.Finished;
                activeSpecialAttack = SpecialAttackType.None;
                controller.EnterPrepareAttack();
                return;
        }
    }

    private bool BuildSurpriseAttackPoints()
    {
        if (context.PlayerTransform == null || context.BossTransform == null)
            return false;

        Vector3 playerPosition = context.PlayerTransform.position;
        Vector3 anchorReferenceForward = context.GetReferenceForwardOnPlane();
        Vector3 oppositeReferenceForward = -anchorReferenceForward;

        float anchorAngle = Random.Range(-Data.surpriseFrontArcDegrees * 0.5f, Data.surpriseFrontArcDegrees * 0.5f);
        Vector3 rotatedForward = Quaternion.Euler(0f, anchorAngle, 0f) * anchorReferenceForward;
        Vector3 frontCenter = playerPosition + anchorReferenceForward * Data.surpriseFrontOffsetFromPlayer;
        surpriseAnchorPoint = frontCenter + rotatedForward * Data.surpriseFrontRingRadius;

        float behindAngle = Random.Range(-30f, 30f);
        Vector3 rotatedBehind = Quaternion.Euler(0f, behindAngle, 0f) * oppositeReferenceForward;
        surpriseBehindPoint = playerPosition + rotatedBehind * Data.surpriseBehindDistance
                            + Quaternion.Euler(0f, behindAngle + 90f, 0f) * oppositeReferenceForward * Data.surpriseBehindSpreadRadius;

        Debug.Log($"{LOG_PREFIX} [BuildPoints] anchor={surpriseAnchorPoint}, behind={surpriseBehindPoint}");
        return true;
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region TripleShockwave（三连冲击波）
    // ══════════════════════════════════════════════════════════════

    private void StartTripleShockwave()
    {
        ConsumeSpecialAttackCooldown();
        activeSpecialAttack = SpecialAttackType.TripleShockwave;
        shockwavePhase = ShockwavePhase.Casting;
        shockwavePhaseTimer = 0f;
        shockwavesFired = 0;
        isWaiting = false;

        context.StopMovement();
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);
        controller.SetAttackAnimationType(FinalBossPhaseOneAIController.AttackAnimationType.None);
        controller.TriggerShockwaveCastAnimation();

        Debug.Log($"{LOG_PREFIX} [Shockwave] === INITIATED === castDuration={Data.shockwaveCastingDuration:F2}, " +
            $"count={Data.shockwaveCount}, interval={Data.shockwaveInterval:F2}");
    }

    private void TickTripleShockwave()
    {
        shockwavePhaseTimer += Time.deltaTime;
        context.StopMovement();
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);

        switch (shockwavePhase)
        {
            case ShockwavePhase.Casting:
                if (shockwavePhaseTimer < Data.shockwaveCastingDuration)
                    return;

                // 蓄力结束，发射第一波
                FireShockwaveAndAdvance();
                return;

            case ShockwavePhase.Firing:
                if (shockwavePhaseTimer < Data.shockwaveInterval)
                    return;

                if (shockwavesFired < Data.shockwaveCount)
                {
                    // 还没发完，继续发射
                    FireShockwaveAndAdvance();
                }
                else
                {
                    // 全部发完，进入恢复
                    shockwavePhase = ShockwavePhase.Recover;
                    shockwavePhaseTimer = 0f;
                    Debug.Log($"{LOG_PREFIX} [Shockwave] All {Data.shockwaveCount} waves fired. Recovering...");
                }
                return;

            case ShockwavePhase.Recover:
                if (shockwavePhaseTimer < Data.shockwaveRecoverDuration)
                    return;

                Debug.Log($"{LOG_PREFIX} [Shockwave] === COMPLETE ===");
                shockwavePhase = ShockwavePhase.None;
                activeSpecialAttack = SpecialAttackType.None;
                return;
        }
    }

    private void FireShockwaveAndAdvance()
    {
        shockwavesFired++;
        controller.FireShockwave();
        shockwavePhaseTimer = 0f;
        shockwavePhase = ShockwavePhase.Firing;
        Debug.Log($"{LOG_PREFIX} [Shockwave] Fired wave {shockwavesFired}/{Data.shockwaveCount}");
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region RangedShot（等待期间远程射击）
    // ══════════════════════════════════════════════════════════════

    private bool TryStartRangedShot()
    {
        if (rangedShotPhase != RangedShotPhase.None)
            return false;

        float roll = Random.value;
        if (roll > Data.rangedShotChance)
            return false;

        rangedShotPhase = RangedShotPhase.Windup;
        rangedShotTimer = 0f;
        controller.TriggerRangedShotAnimation();
        Debug.Log($"{LOG_PREFIX} [RangedShot] Started. roll={roll:F3} <= {Data.rangedShotChance:F3}, windup={Data.rangedShotWindup:F2}s");
        return true;
    }

    private void TickRangedShot()
    {
        rangedShotTimer += Time.deltaTime;
        context.StopMovement();
        controller.SetMoveAnimationState(FinalBossPhaseOneAIController.MoveAnimationState.Idle);

        switch (rangedShotPhase)
        {
            case RangedShotPhase.Windup:
                if (rangedShotTimer < Data.rangedShotWindup)
                    return;

                controller.FireRangedShot();
                rangedShotPhase = RangedShotPhase.Recover;
                rangedShotTimer = 0f;
                Debug.Log($"{LOG_PREFIX} [RangedShot] Fired! Recovering {Data.rangedShotRecoverDuration:F2}s");
                return;

            case RangedShotPhase.Recover:
                if (rangedShotTimer < Data.rangedShotRecoverDuration)
                    return;

                Debug.Log($"{LOG_PREFIX} [RangedShot] Complete.");
                rangedShotPhase = RangedShotPhase.None;
                return;
        }
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 工具方法
    // ══════════════════════════════════════════════════════════════

    private string GetBossPos()
    {
        return context.BossTransform != null ? context.BossTransform.position.ToString("F2") : "NULL";
    }

    private string GetPlayerPos()
    {
        return context.PlayerTransform != null ? context.PlayerTransform.position.ToString("F2") : "NULL";
    }

    #endregion
}
