using UnityEngine;
using System;

public class ChasingEnemyAttackController : MonoBehaviour
{
    [Header("攻击设置")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDuration = 1f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private bool useBuiltInAttack = true;
    
    [Header("组件引用")]
    [SerializeField] private ChasingEnemyAnimationController animationController;
    
    private Transform player;
    private bool canAttack = true;
    private float cooldownTimer = 0f;
    
    // 事件
    public event Action OnAttackStarted;
    public event Action OnAttackEnded;
    
    // 公共属性
    public float AttackDuration => attackDuration;
    public bool IsInAttackRange => CheckIfInAttackRange();
    public bool CanAttack => canAttack;
    
    public void Initialize(Transform playerTransform, ChasingEnemyAnimationController animController = null)
    {
        player = playerTransform;
        
        if (animController != null)
        {
            animationController = animController;
        }
        
        if (animationController == null)
        {
            animationController = GetComponent<ChasingEnemyAnimationController>();
        }
    }
    
    public void UpdateAttackCooldown()
    {
        if (!canAttack)
        {
            cooldownTimer += Time.deltaTime;
            if (cooldownTimer >= attackCooldown)
            {
                canAttack = true;
                cooldownTimer = 0f;
            }
        }
    }
    
    public bool CheckIfInAttackRange()
    {
        if (player == null || !canAttack) return false;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        return distanceToPlayer <= attackRange;
    }
    
    public void StartAttack()
    {
        if (!canAttack) return;
        
        canAttack = false;
        
        // 触发事件
        OnAttackStarted?.Invoke();
        
        // 设置动画
        if (animationController != null)
        {
            animationController.SetAttackAnimation(true);
        }
        
        // 触发攻击事件
        if (player != null)
        {
            ColorEventBus.PublishEnemyAttack(gameObject, player.gameObject);
        }
        
        Debug.Log($"⚔️ 敌人开始攻击，将持续 {attackDuration} 秒");
    }
    
    public void EndAttack()
    {
        // 触发事件
        OnAttackEnded?.Invoke();
        
        // 重置动画
        if (animationController != null)
        {
            animationController.SetAttackAnimation(false);
        }
        
        Debug.Log("✅ 敌人攻击结束");
    }
    
    public void SetAttackParameters(float range, float duration, float cooldown)
    {
        attackRange = range;
        attackDuration = duration;
        attackCooldown = cooldown;
    }
    
    public void SetAttackEnabled(bool enabled)
    {
        useBuiltInAttack = enabled;
    }
    
    public void ResetAttackCooldown()
    {
        canAttack = true;
        cooldownTimer = 0f;
    }

    public bool CanAttackPlayer()
    {
        return CheckIfInAttackRange();
    }
}
