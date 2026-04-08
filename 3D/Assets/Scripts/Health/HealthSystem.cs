
using UnityEngine;
using System;

public class HealthSystem : MonoBehaviour
{
    [Header("心形血量设置")]
    [SerializeField] protected int maxHearts = 5;           // 最大心数
    [SerializeField] protected int currentHearts;           // 当前心数
    
    [Header("通用设置")]
    [SerializeField] protected bool destroyOnDeath = true;  // 死亡时销毁
    [SerializeField] protected float deathDelay = 0f;       // 死亡延迟时间
    [SerializeField] protected bool showDebugLogs = true;   // 是否显示调试日志
    
    public event Action<int, int> OnHeartsChanged;  // 当前心数, 最大心数
    public event Action OnDeath;                    // 死亡事件
    
    public int CurrentHearts => currentHearts;
    public int MaxHearts => maxHearts;
    public bool IsAlive => currentHearts > 0;
    
    protected virtual void Awake()
    {
        // 初始化心数
        currentHearts = maxHearts;
        if (showDebugLogs)
            Debug.Log($"❤️ {gameObject.name} 血量初始化: {currentHearts}/{maxHearts} 心");
    }
    
    // 受到伤害（失去一颗心）
    public virtual void TakeDamage(int damage = 1, GameObject damageSource = null)
    {
        if (!IsAlive) return;
        
        int oldHearts = currentHearts;
        currentHearts = Mathf.Max(0, currentHearts - damage);
        
        if (showDebugLogs)
            Debug.Log($"💔 {gameObject.name} 受到伤害, 失去 {damage} 颗心 ({oldHearts} → {currentHearts})");
        
        // 触发事件
        OnHeartsChanged?.Invoke(currentHearts, maxHearts);
        
        // 检查是否死亡
        if (currentHearts <= 0)
        {
            Die();
        }
    }
    
    // 恢复一颗心
    public virtual void Heal(int amount = 1)
    {
        if (!IsAlive) return;
        
        int oldHearts = currentHearts;
        currentHearts = Mathf.Min(maxHearts, currentHearts + amount);
        
        if (showDebugLogs)
            Debug.Log($"💚 {gameObject.name} 恢复 {amount} 颗心 ({oldHearts} → {currentHearts})");
        
        OnHeartsChanged?.Invoke(currentHearts, maxHearts);
    }
    
    // 死亡处理
    protected virtual void Die()
    {
        if (showDebugLogs)
            Debug.Log($"☠️ {gameObject.name} 死亡!");
        
        OnDeath?.Invoke();
        
        // 根据配置销毁对象
        if (destroyOnDeath)
        {
            if (deathDelay > 0f)
            {
                Destroy(gameObject, deathDelay);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
    
    // 设置最大心数
    public virtual void SetMaxHearts(int newMaxHearts, bool fillHearts = true)
    {
        maxHearts = newMaxHearts;
        if (fillHearts)
        {
            currentHearts = maxHearts;
        }
        else
        {
            currentHearts = Mathf.Min(currentHearts, maxHearts);
        }
        
        OnHeartsChanged?.Invoke(currentHearts, maxHearts);
    }
    
    // 重置血量
    public void ResetHearts()
    {
        currentHearts = maxHearts;
        OnHeartsChanged?.Invoke(currentHearts, maxHearts);
        
        if (showDebugLogs)
            Debug.Log($"🔄 {gameObject.name} 血量重置: {currentHearts}/{maxHearts}");
    }
}
