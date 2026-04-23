using UnityEngine;

public class FinalBossStageOneController : FinalBossStageBase
{
    [Header("Stage One")]
    [SerializeField] private HealthSystem bossHealth;
    [SerializeField] private int stageTwoTriggerHp = 10;
    [SerializeField] private FinalBossPhaseOneAIController phaseOneAI;
    [SerializeField] private FinalBossPhaseOneCombatBridge combatBridge;

    public override void Initialize(FinalBossBattleDirector owner, BossController boss)
    {
        base.Initialize(owner, boss);
        if (bossHealth == null && boss != null)
            bossHealth = boss.GetComponent<HealthSystem>();
        phaseOneAI ??= GetComponentInChildren<FinalBossPhaseOneAIController>(true);
        combatBridge ??= GetComponentInChildren<FinalBossPhaseOneCombatBridge>(true);
    }

    public override void EnterStage()
    {
        base.EnterStage();
        if (bossController != null)
            bossController.enabled = false;

        if (combatBridge != null)
            combatBridge.enabled = true;

        phaseOneAI?.BeginStage(this);
    }

    public override void ExitStage()
    {
        phaseOneAI?.EndStage();

        if (combatBridge != null)
            combatBridge.enabled = false;

        base.ExitStage();
    }

    private void Update()
    {
        if (!IsActive || bossHealth == null) return;

        if (bossHealth.CurrentHearts <= stageTwoTriggerHp)
            CompleteStage();
    }

    public override bool CanBossTakeDirectDamage() => true;

    public override bool IsStageComplete()
    {
        return bossHealth != null && bossHealth.CurrentHearts <= stageTwoTriggerHp;
    }
}
