using UnityEngine;

public class BossHealthSystem : HealthSystem
{
    [Header("Boss特殊设置")]
    [SerializeField] private BossController bossController;

    private bool IsPhase2 => bossController != null && bossController.IsPhase2;

    /// <summary>
    /// 由弱点攻击调用的专用伤害方法（无视阶段，必定扣血）
    /// </summary>
    public void TakeDamageFromWeakPoint(int damage = 1, GameObject damageSource = null)
    {
        Debug.Log($"🎯 [BossHealthSystem] 弱点伤害 {damage} 点，直接扣血");
        base.TakeDamage(damage, damageSource);
    }

    /// <summary>
    /// 重写普通伤害方法：在Phase2时忽略所有非弱点伤害
    /// </summary>
    public override void TakeDamage(int damage = 1, GameObject damageSource = null)
    {
        if (IsPhase2)
        {
            if (showDebugLogs)
                Debug.Log($"🛡️ [BossHealthSystem] Phase2 本体无敌，忽略伤害");
            return;
        }

        base.TakeDamage(damage, damageSource);
    }
}
