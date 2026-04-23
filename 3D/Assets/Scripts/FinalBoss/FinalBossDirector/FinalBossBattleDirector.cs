using System;
using UnityEngine;

public class FinalBossBattleDirector : MonoBehaviour
{
    public enum BattleState
    {
        Idle = 0,
        Stage1 = 1,
        Stage2 = 2,
        Stage3 = 3,
        Victory = 4,
        Failed = 5
    }

    [Header("References")]
    [SerializeField] private BossController bossController;
    [SerializeField] private BossDamageGate bossDamageGate;
    [SerializeField] private FinalBossDamageGate finalBossDamageGate;
    [SerializeField] private FinalBossFailCondition failCondition;

    [Header("Stage Controllers")]
    [SerializeField] private FinalBossStageOneController stageOne;
    [SerializeField] private FinalBossStageTwoController stageTwo;
    [SerializeField] private FinalBossStageThreeQTEController stageThree;

    [Header("Startup")]
    [SerializeField] private bool autoStartBattle = true;
    [SerializeField] private bool showDebugLogs = true;

    public BattleState CurrentState { get; private set; } = BattleState.Idle;
    public FinalBossStageBase CurrentStage { get; private set; }
    public bool IsBattleRunning => CurrentState == BattleState.Stage1 || CurrentState == BattleState.Stage2 || CurrentState == BattleState.Stage3;

    public event Action<BattleState> OnBattleStateChanged;
    public event Action<int> OnStageIndexChanged;
    public event Action OnBattleVictory;
    public event Action<string> OnBattleFailed;

    private void Awake()
    {
        AutoWireReferences();
        InitializeStage(stageOne);
        InitializeStage(stageTwo);
        InitializeStage(stageThree);

        if (failCondition != null)
        {
            failCondition.Initialize(this);
            failCondition.OnFailed -= HandleBattleFailed;
            failCondition.OnFailed += HandleBattleFailed;
        }
    }

    private void Start()
    {
        if (autoStartBattle)
            StartBattle();
    }

    public void StartBattle()
    {
        if (CurrentState != BattleState.Idle) return;
        Log("StartBattle");
        EnterState(BattleState.Stage1);
        SwitchToStage(stageOne, 1);
    }

    public void NotifyBossBodyDamageWindowChanged()
    {
        Log($"Boss damage window -> {(CanBossTakeDirectDamage() ? "OPEN" : "CLOSED")}");
    }

    public bool CanBossTakeDirectDamage()
    {
        return CurrentStage == null || CurrentStage.CanBossTakeDirectDamage();
    }

    public void AdvanceToNextStage()
    {
        if (CurrentState == BattleState.Stage1)
        {
            EnterState(BattleState.Stage2);
            SwitchToStage(stageTwo, 2);
            return;
        }

        if (CurrentState == BattleState.Stage2)
        {
            EnterState(BattleState.Stage3);
            SwitchToStage(stageThree, 3);
            return;
        }

        if (CurrentState == BattleState.Stage3)
        {
            EnterVictory();
        }
    }

    public void EnterVictory()
    {
        if (CurrentState == BattleState.Victory) return;
        ExitCurrentStage();
        finalBossDamageGate?.ApplyQTEFinisherKill();
        EnterState(BattleState.Victory);
        Log("Battle Victory");
        OnBattleVictory?.Invoke();
    }

    public void HandleBattleFailed(string reason)
    {
        if (CurrentState == BattleState.Failed || CurrentState == BattleState.Victory) return;
        ExitCurrentStage();
        EnterState(BattleState.Failed);
        Log($"Battle Failed -> {reason}");
        OnBattleFailed?.Invoke(reason);
    }

    private void InitializeStage(FinalBossStageBase stage)
    {
        if (stage == null) return;
        stage.Initialize(this, bossController);
        stage.OnStageCompleted -= HandleStageCompleted;
        stage.OnStageCompleted += HandleStageCompleted;
        stage.OnStageFailed -= HandleStageFailed;
        stage.OnStageFailed += HandleStageFailed;
        stage.ExitStage();
    }

    private void HandleStageCompleted(FinalBossStageBase stage)
    {
        if (stage != CurrentStage) return;
        Log($"HandleStageCompleted -> {stage.name}");
        AdvanceToNextStage();
    }

    private void HandleStageFailed(FinalBossStageBase stage, string reason)
    {
        if (stage != CurrentStage) return;
        HandleBattleFailed(reason);
    }

    private void SwitchToStage(FinalBossStageBase nextStage, int stageIndex)
    {
        ExitCurrentStage();
        CurrentStage = nextStage;

        if (CurrentStage == null)
        {
            HandleBattleFailed($"Missing stage controller: {stageIndex}");
            return;
        }

        CurrentStage.EnterStage();
        NotifyBossBodyDamageWindowChanged();
        OnStageIndexChanged?.Invoke(stageIndex);
    }

    private void ExitCurrentStage()
    {
        if (CurrentStage == null) return;
        CurrentStage.ExitStage();
        CurrentStage = null;
    }

    private void EnterState(BattleState newState)
    {
        CurrentState = newState;
        OnBattleStateChanged?.Invoke(newState);
    }

    private void AutoWireReferences()
    {
        bossController ??= GetComponentInChildren<BossController>(true);
        bossDamageGate ??= GetComponentInChildren<BossDamageGate>(true);
        finalBossDamageGate ??= GetComponentInChildren<FinalBossDamageGate>(true);
        failCondition ??= GetComponentInChildren<FinalBossFailCondition>(true);
        stageOne ??= GetComponentInChildren<FinalBossStageOneController>(true);
        stageTwo ??= GetComponentInChildren<FinalBossStageTwoController>(true);
        stageThree ??= GetComponentInChildren<FinalBossStageThreeQTEController>(true);
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossBattleDirector] {message}");
    }
}
