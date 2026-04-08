using UnityEngine;

public class EnemyHealthSystem : HealthSystem
{
    [Header("敌人特殊设置")]
    [SerializeField] private GameObject deathEffectPrefab;      // 死亡特效
    [SerializeField] private bool dropItemOnDeath = false;      // 死亡时掉落物品
    [SerializeField] private GameObject dropItemPrefab;         // 掉落物品预制体
    
    private KnockbackSystem knockbackSystem;
    private ChasingEnemy chasingEnemy;
    
    protected override void Awake()
    {
        base.Awake();
        
        // 获取敌人相关组件
        knockbackSystem = GetComponent<KnockbackSystem>();
        chasingEnemy = GetComponent<ChasingEnemy>();
        
        // 订阅死亡事件
        OnDeath += HandleEnemyDeath;
        
        if (showDebugLogs)
            Debug.Log($"👹 敌人血量系统初始化完成: {gameObject.name}, {maxHearts}颗心");
    }
    
    public override void TakeDamage(int damage = 1, GameObject damageSource = null)
    {
        // 调用基类的伤害处理
        base.TakeDamage(damage, damageSource);
        
        // 敌人受到伤害时的额外逻辑
        if (IsAlive && damage > 0)
        {
            // 可以添加受伤动画、音效等
            if (knockbackSystem != null && damageSource != null)
            {
                // 被攻击时可能会有额外的击退效果
                // 这里可以根据需要添加
            }
            
            // 敌人受伤后短暂停顿（可选）
            if (chasingEnemy != null)
            {
                StartCoroutine(EnemyStunEffect());
            }
        }
    }
    
    private System.Collections.IEnumerator EnemyStunEffect()
    {
        // 短暂暂停敌人的AI
        if (chasingEnemy != null)
        {
            chasingEnemy.enabled = false;
            yield return new WaitForSeconds(0.3f);
            chasingEnemy.enabled = true;
        }
    }
    
    protected override void Die()
    {
        if (showDebugLogs)
            Debug.Log($"🎉 敌人 {gameObject.name} 被击败!");
        
        // 触发基类的死亡事件
        base.Die();
    }
    
    private void HandleEnemyDeath()
    {
        // 敌人死亡时的特殊处理
        if (showDebugLogs)
            Debug.Log($"💀 敌人 {gameObject.name} 死亡处理开始");
        
        // 播放死亡效果
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }
        
        // 掉落物品
        if (dropItemOnDeath && dropItemPrefab != null)
        {
            Instantiate(dropItemPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }
        
        // 禁用碰撞体和渲染器，但暂时不销毁（为了视觉效果）
        Collider collider = GetComponent<Collider>();
        if (collider != null) 
        {
            collider.enabled = false;
            Debug.Log("🔒 禁用敌人碰撞体");
        }
        
        // 禁用渲染器
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null) 
        {
            renderer.enabled = false;
            Debug.Log("👻 隐藏敌人模型");
        }
        
        // 禁用敌人的AI脚本
        if (chasingEnemy != null) 
        {
            chasingEnemy.enabled = false;
            Debug.Log("🛑 禁用敌人AI");
        }
        
        // 禁用攻击检测
        EnemyAttackDetector attackDetector = GetComponent<EnemyAttackDetector>();
        if (attackDetector != null) 
        {
            attackDetector.enabled = false;
            Debug.Log("⚔️ 禁用敌人攻击");
        }
        
        // 延迟销毁（为了播放死亡动画或效果）
        if (destroyOnDeath)
        {
            Debug.Log($"⏳ 敌人将在 {deathDelay} 秒后销毁");
            Destroy(gameObject, deathDelay);
        }
    }
    
    // 敌人血量系统的特殊方法
    public void SetEnemyDifficulty(float difficultyMultiplier)
    {
        // 根据难度调整心数
        int newMaxHearts = Mathf.RoundToInt(maxHearts * difficultyMultiplier);
        SetMaxHearts(newMaxHearts, false);
        
        if (showDebugLogs)
            Debug.Log($"🎮 敌人难度设置: 乘数={difficultyMultiplier}, 心数={currentHearts}/{maxHearts}");
    }
}