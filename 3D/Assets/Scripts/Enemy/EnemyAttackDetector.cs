using UnityEngine;

public class EnemyAttackDetector : MonoBehaviour
{
    [Header("敌人攻击设置")]
    [SerializeField] private float attackRange = 1.5f;

    [Header("攻击检测原点（可选）")]
    [SerializeField] private Transform attackOrigin;

    private Transform player;

    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (attackOrigin == null)
            attackOrigin = transform;

        Debug.Log($"👹 敌人攻击检测器初始化: {gameObject.name}");
    }

    public bool TryPerformAttack(GameObject attacker, Transform targetOverride = null)
    {
        Transform target = targetOverride != null ? targetOverride : player;
        if (target == null) return false;

        Vector3 originPos = attackOrigin != null ? attackOrigin.position : transform.position;
        float distanceToPlayer = Vector3.Distance(originPos, target.position);

        if (distanceToPlayer > attackRange)
        {
            Debug.Log($"❌ 攻击未命中，目标超出攻击范围: {distanceToPlayer:F2} > {attackRange:F2}");
            return false;
        }

        Debug.Log($"✅ 敌人攻击命中: {attacker.name}");
        ColorEventBus.PublishEnemyAttack(attacker, target.gameObject);
        return true;
    }

    public float GetAttackRange()
    {
        return attackRange;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Vector3 originPos = attackOrigin != null ? attackOrigin.position : transform.position;
        Gizmos.DrawWireSphere(originPos, attackRange);
    }
}
