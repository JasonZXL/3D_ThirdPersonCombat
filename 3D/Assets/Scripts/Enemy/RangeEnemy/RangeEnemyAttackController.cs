using UnityEngine;
using System;

/// <summary>
/// 远程敌人攻击控制器：负责射击攻击逻辑
/// </summary>
public class RangeEnemyAttackController : MonoBehaviour
{
    [Header("攻击设置")]
    [SerializeField] private float attackRange = 10f;
    [SerializeField] private float attackInterval = 3f;
    [SerializeField] private float attackDuration = 0.6f; // 攻击（射击）动作持续时间，用于事件驱动计时
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 10f;
    
    [Header("组件引用")]
    [SerializeField] private RangeEnemyAnimationController animationController;
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = false;
    
    // 组件引用
    private Transform player;
    private ColorComponent colorComponent;
    
    // 攻击状态
    private float lastAttackTime = -999f;
    private bool hasLineOfSight = false;
    
    // 事件
    public event Action OnAttackStarted;
    public event Action OnAttackPerformed;
    public event Action OnAttackEnded;
    
    // 公共属性
    public float AttackRange => attackRange;
    public float AttackInterval => attackInterval;
    public float AttackDuration => attackDuration;
    public bool HasLineOfSight => hasLineOfSight;
    
    private void Awake()
    {
        colorComponent = GetComponent<ColorComponent>();
        
        if (showDebugLogs)
        {
            Debug.Log($"🎯 [RangeAttack] Awake: {gameObject.name}");
        }
    }
    
    public void Initialize(Transform playerTransform, RangeEnemyAnimationController animController = null)
    {
        player = playerTransform;
        
        if (animController != null)
        {
            animationController = animController;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"✅ [RangeAttack] 初始化完成");
            Debug.Log($"   - 攻击范围: {attackRange}");
            Debug.Log($"   - 攻击间隔: {attackInterval}");
            Debug.Log($"   - 子弹预制体: {(projectilePrefab != null ? "已设置" : "⚠️ 未设置")}");
        }
    }
    
    private void FixedUpdate()
    {
        UpdateLineOfSight();
    }
    
    /// <summary>
    /// 更新视线检测
    /// </summary>
    private void UpdateLineOfSight()
    {
        if (player == null)
        {
            hasLineOfSight = false;
            return;
        }
        
        Vector3 directionToPlayer = player.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;
        
        RaycastHit hit;
        bool obstacle = Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            directionToPlayer.normalized,
            out hit,
            distanceToPlayer
        );
        
        hasLineOfSight = !obstacle || (obstacle && hit.transform == player);
    }
    
    /// <summary>
    /// 检查是否在攻击范围内
    /// </summary>
    public bool IsInAttackRange()
    {
        if (player == null) return false;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        return distanceToPlayer <= attackRange && hasLineOfSight;
    }
    
    /// <summary>
    /// 检查是否可以攻击（独立于状态的冷却系统）
    /// </summary>
    public bool CanAttackNow()
    {
        float timeSinceLastAttack = Time.time - lastAttackTime;
        return timeSinceLastAttack >= attackInterval;
    }
    
    /// <summary>
    /// 获取剩余攻击冷却时间
    /// </summary>
    public float GetAttackCooldownRemaining()
    {
        float timeSinceLastAttack = Time.time - lastAttackTime;
        return Mathf.Max(0f, attackInterval - timeSinceLastAttack);
    }
    
    /// <summary>
    /// 执行攻击
    /// </summary>
    
    /// <summary>
    /// 开始攻击（事件驱动入口）
    /// </summary>
    public void StartAttack()
    {
        if (player == null) return;

        // 冷却未结束则不攻击
        if (!CanAttackNow()) return;

        // 触发攻击开始事件
        OnAttackStarted?.Invoke();

        // 触发动画（进入攻击态 + 一次性射击触发）
        if (animationController != null)
        {
            animationController.SetAttackingAnimation(true);
            animationController.TriggerShootAnimation();
        }

        // 实际射击（生成子弹）
        PerformShot();

        // 触发攻击执行事件
        OnAttackPerformed?.Invoke();
    }

    /// <summary>
    /// 攻击结束（由主控计时触发）
    /// </summary>
    public void EndAttack()
    {
        OnAttackEnded?.Invoke();

        if (animationController != null)
        {
            animationController.SetAttackingAnimation(false);
        }
    }

    /// <summary>
    /// 兼容旧接口：执行攻击
    /// </summary>
    public void PerformAttack()
    {
        StartAttack();
    }

    /// <summary>
    /// 实际射击（生成子弹，不触发Start/End事件）
    /// </summary>
    private void PerformShot()
{
        if (projectilePrefab == null || firePoint == null || player == null)
        {
            Debug.LogWarning($"⚠️ 远程敌人无法攻击: 缺少预制体或射击点");
            return;
        }
        // 创建子弹
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        
        // 设置子弹的颜色(继承敌人的颜色)
        ColorComponent projectileColor = projectile.GetComponent<ColorComponent>();
        if (projectileColor != null && colorComponent != null)
        {
            projectileColor.CurrentColor = colorComponent.CurrentColor;
        }
        
        // 获取子弹的ProjectileController组件
        ProjectileController projectileController = projectile.GetComponent<ProjectileController>();
        
        if (projectileController != null)
        {
            projectileController.InitializeProjectile(
                player.position,
                projectileSpeed,
                this.gameObject
            );
        }
        
        // 更新最后攻击时间
        lastAttackTime = Time.time;
        
        
        if (showDebugLogs)
        {
            Debug.Log($"💥 远程敌人射击: {gameObject.name} (颜色: {colorComponent?.CurrentColor}, 下次攻击: {attackInterval}秒后)");
        }
    }
    
    /// <summary>
    /// 重置攻击冷却
    /// </summary>
    public void ResetAttackCooldown()
    {
        lastAttackTime = -999f;
    }
    
    /// <summary>
    /// 设置攻击参数
    /// </summary>
    public void SetAttackParameters(float range, float interval, float speed)
    {
        attackRange = range;
        attackInterval = interval;
        projectileSpeed = speed;
        
        if (showDebugLogs)
        {
            Debug.Log($"🔧 [RangeAttack] 设置攻击参数: 范围={range}, 间隔={interval}, 速度={speed}");
        }
    }
    
    /// <summary>
    /// 设置子弹预制体
    /// </summary>
    public void SetProjectilePrefab(GameObject prefab)
    {
        projectilePrefab = prefab;
    }
    
    /// <summary>
    /// 设置射击点
    /// </summary>
    public void SetFirePoint(Transform point)
    {
        firePoint = point;
    }
    
    
    /// <summary>
    /// 检查玩家是否在攻击范围内
    /// </summary>
    public bool CheckIfInAttackRange()
    {
        if (player == null) return false;
        return DistanceToPlayer() <= attackRange;
    }

    /// <summary>
    /// 是否允许对玩家发起攻击（范围 + 视线 + 冷却）
    /// </summary>
    public bool CanAttackPlayer()
    {
        return player != null && CheckIfInAttackRange() && hasLineOfSight && CanAttackNow();
    }

/// <summary>
    /// 获取到玩家的距离
    /// </summary>
    public float DistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Vector3.Distance(transform.position, player.position);
    }
}

