using UnityEngine;

public class PlayerHealthSystem : HealthSystem
{
    #region 玩家特殊设置
    [Header("玩家特殊设置")]
    [SerializeField] private float invincibilityTime = 1f;  // 无敌时间
    #endregion

    #region 内部变量
    private float invincibilityTimer = 0f;
    private bool isInvincible = false;
    #endregion

    #region Unity生命周期
    protected override void Awake()
    {
        base.Awake();
        
        OnDeath += HandlePlayerDeath;
        
        if (showDebugLogs)
            Debug.Log($"🎮 玩家血量系统初始化完成: {gameObject.name}, {maxHearts}颗心");
    }
    
    private void Update()
    {
        // 更新无敌时间
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0f)
            {
                isInvincible = false;
                if (showDebugLogs)
                    Debug.Log("🛡️ 玩家无敌时间结束");
            }
        }
    }
    #endregion

    #region 伤害处理
    /// <summary>玩家受到伤害</summary>
    public override void TakeDamage(int damage = 1, GameObject damageSource = null)
    {
        // 检查无敌状态
        if (isInvincible)
        {
            if (showDebugLogs)
                Debug.Log("🛡️ 玩家处于无敌状态，免疫伤害");
            return;
        }
        
        // 调用基类的伤害处理
        base.TakeDamage(damage, damageSource);
        
        // 如果还活着，进入无敌状态
        if (IsAlive && damage > 0)
        {
            isInvincible = true;
            invincibilityTimer = invincibilityTime;
            
            if (showDebugLogs)
                Debug.Log("🩸 玩家受伤，进入无敌状态");
        }
    }
    #endregion

    #region 死亡处理
    /// <summary>处理玩家死亡</summary>
    private void HandlePlayerDeath()
    {
        if (showDebugLogs)
            Debug.Log("💀 玩家死亡!");
        
        // 这里可以添加玩家死亡的特殊逻辑
        // 例如：显示游戏失败UI、暂停游戏等
    }
    #endregion

    #region 玩家特殊方法
    /// <summary>重置玩家状态</summary>
    public void ResetPlayer()
    {
        ResetHearts();
        isInvincible = false;
        invincibilityTimer = 0f;
        
        if (showDebugLogs)
            Debug.Log("🔄 玩家状态重置");
    }
    
    /// <summary>检查是否无敌</summary>
    public bool IsInvincible()
    {
        return isInvincible;
    }
    #endregion
}