using UnityEngine;

public class FinalBossResonancePillarHealth : HealthSystem
{
    protected override void Awake()
    {
        destroyOnDeath = false;
        deathDelay = 0f;
        base.Awake();
    }

    public void Configure(int newMaxHealth)
    {
        destroyOnDeath = false;
        deathDelay = 0f;
        SetMaxHearts(Mathf.Max(1, newMaxHealth), fillHearts: true);
    }

    public void ResetHealth(int newMaxHealth)
    {
        destroyOnDeath = false;
        deathDelay = 0f;
        SetMaxHearts(Mathf.Max(1, newMaxHealth), fillHearts: true);
        ResetHearts();
    }
}
