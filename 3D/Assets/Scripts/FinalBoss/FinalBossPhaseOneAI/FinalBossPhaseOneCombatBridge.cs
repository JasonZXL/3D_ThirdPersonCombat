using UnityEngine;

public class FinalBossPhaseOneCombatBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FinalBossPhaseOneAIController phaseOneAI;
    [SerializeField] private GameObject bossRoot;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        phaseOneAI ??= GetComponent<FinalBossPhaseOneAIController>();
        bossRoot ??= gameObject;
        enabled = false;
    }

    private void OnEnable()
    {
        ColorEventBus.OnColorInteraction += HandleColorInteraction;
    }

    private void OnDisable()
    {
        ColorEventBus.OnColorInteraction -= HandleColorInteraction;
    }

    private void HandleColorInteraction(ColorInteractionEvent interaction)
    {
        if (phaseOneAI == null) return;
        if (interaction.Source == null || interaction.Target == null) return;

        if (interaction.Type == ColorInteractionType.PlayerAttackEnemy && IsBossTarget(interaction.Target))
        {
            phaseOneAI.NotifyPlayerAggroHit();

            if (showDebugLogs)
                Debug.Log($"[FinalBossPhaseOneCombatBridge] Player aggro hit -> {interaction.Target.name}");
        }
    }

    private bool IsBossTarget(GameObject target)
    {
        if (target == null || bossRoot == null) return false;
        if (target == bossRoot) return true;
        return target.transform.IsChildOf(bossRoot.transform);
    }
}
