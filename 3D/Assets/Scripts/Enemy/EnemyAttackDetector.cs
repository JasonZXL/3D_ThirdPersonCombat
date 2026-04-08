using UnityEngine;

public class EnemyAttackDetector : MonoBehaviour
{
    [Header("敌人攻击设置")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 2f;
    
    private float lastAttackTime = 0f;
    private Transform player;
    private ColorComponent enemyColor;
    
    private void Awake()
    {
        enemyColor = GetComponent<ColorComponent>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        Debug.Log($"👹 敌人攻击检测器初始化: {gameObject.name}");
    }
    
    private void Update()
    {
        if (player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            AttackPlayer();
        }
    }
    
    private void AttackPlayer()
    {
        Debug.Log($"👹 敌人攻击玩家: {gameObject.name}");
        ColorEventBus.PublishEnemyAttack(gameObject, player.gameObject);
        lastAttackTime = Time.time;
    }
    
    // 可视化攻击范围
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
