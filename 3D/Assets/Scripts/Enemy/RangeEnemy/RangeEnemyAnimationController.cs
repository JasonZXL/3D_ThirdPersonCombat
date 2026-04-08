using UnityEngine;

/// <summary>
/// 远程敌人动画控制器：负责所有动画状态的控制
/// </summary>
public class RangeEnemyAnimationController : MonoBehaviour
{
    [Header("动画参数名称")]
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string isAttackingParam = "IsAttacking";
    [SerializeField] private string isRetreatingParam = "IsRetreating";
    [SerializeField] private string walkBackwardsParam = "WalkBackwards";
    [SerializeField] private string isStunningParam = "IsStunning";
    [SerializeField] private string shootTrigger = "Shoot";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string moveDirectionParam = "MoveDirection";
    
    [Header("动画引用")]
    [SerializeField] private Animator animator;
    
    [Header("速度设置")]
    [SerializeField] private float maxMoveSpeed = 5f;
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = false;
    
    // 当前状态
    private float currentSpeed = 0f;
    private bool isMoving = false;
    private bool isRetreating = false;
    private bool isAttacking = false;
    private bool isStunning = false;
    
    // 公共属性
    public bool IsRetreating => isRetreating;
    public bool IsAttacking => isAttacking;
    public bool IsStunning => isStunning;
    
    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"🎬 [RangeAnimation] Awake: {gameObject.name}");
            Debug.Log($"   - Animator: {(animator != null ? animator.name : "⚠️ 未找到")}");
        }
    }
    
    public void Initialize(Animator targetAnimator = null)
    {
        if (targetAnimator != null)
        {
            animator = targetAnimator;
        }
        
        if (animator == null)
        {
            Debug.LogWarning($"⚠️ [RangeAnimation] Animator未找到！");
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"✅ [RangeAnimation] 初始化完成");
        }
    }
    
    /// <summary>
    /// 设置移动动画
    /// </summary>
    public void SetMovingAnimation(bool moving)
    {
        if (animator == null) return;
        
        if (isMoving != moving)
        {
            isMoving = moving;
            animator.SetBool(isMovingParam, moving);
            
            if (showDebugLogs)
            {
                Debug.Log($"🎬 设置移动动画: {moving}");
            }
        }
    }
    
    /// <summary>
    /// 设置移动速度
    /// </summary>
    public void SetMovementSpeed(float speed)
    {
        if (animator == null) return;
        
        currentSpeed = speed;
        float normalizedSpeed = Mathf.Clamp01(speed / maxMoveSpeed);
        animator.SetFloat(speedParam, normalizedSpeed);
    }
    
    /// <summary>
    /// 设置攻击动画状态
    /// </summary>
    public void SetAttackingAnimation(bool attacking)
    {
        if (animator == null) return;
        
        if (isAttacking != attacking)
        {
            isAttacking = attacking;
            animator.SetBool(isAttackingParam, attacking);
            
            if (showDebugLogs)
            {
                Debug.Log($"🎬 设置攻击动画: {attacking}");
            }
        }
    }
    
    /// <summary>
    /// 设置撤离动画状态（后退动画）
    /// </summary>
    public void SetRetreatingAnimation(bool retreating)
    {
        if (animator == null) return;
        
        if (isRetreating != retreating)
        {
            isRetreating = retreating;
            animator.SetBool(isRetreatingParam, retreating);
            animator.SetBool(walkBackwardsParam, retreating);  // 兼容不同的参数名
            
            if (showDebugLogs)
            {
                Debug.Log($"🎬 设置撤离动画: {retreating}");
            }
        }
    }
    
    /// <summary>
    /// 设置眩晕动画状态
    /// </summary>
    public void SetStunningAnimation(bool stunning)
    {
        if (animator == null) return;
        
        if (isStunning != stunning)
        {
            isStunning = stunning;
            animator.SetBool(isStunningParam, stunning);
            
            if (showDebugLogs)
            {
                Debug.Log($"🎬 设置眩晕动画: {stunning}");
            }
        }
    }
    
    /// <summary>
    /// 触发射击动画（一次性触发）
    /// </summary>
    public void TriggerShootAnimation()
    {
        if (animator == null) return;
        
        animator.SetTrigger(shootTrigger);
        animator.SetTrigger(attackTrigger);  // 兼容不同的参数名
        
        if (showDebugLogs)
        {
            Debug.Log($"🎬 触发射击动画");
        }
    }
    
    /// <summary>
    /// 设置移动方向（前进/后退）
    /// </summary>
    public void SetMoveDirection(float direction)
    {
        if (animator == null) return;
        
        animator.SetFloat(moveDirectionParam, direction);
    }
    
    /// <summary>
    /// 更新移动方向动画（基于当前状态）
    /// </summary>
    public void UpdateMovementAnimation()
    {
        if (animator == null) return;
        
        if (isRetreating && isMoving)
        {
            SetMoveDirection(-1f);  // 后退
        }
        else if (isMoving)
        {
            SetMoveDirection(1f);   // 前进
        }
        else
        {
            SetMoveDirection(0f);   // 静止
        }
    }
    
    /// <summary>
    /// 设置最大移动速度
    /// </summary>
    public void SetMaxMoveSpeed(float maxSpeed)
    {
        maxMoveSpeed = maxSpeed;
    }
    
    /// <summary>
    /// 设置Animator
    /// </summary>
    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;
        
        if (showDebugLogs)
        {
            Debug.Log($"🔧 [RangeAnimation] 设置Animator: {(animator != null ? animator.name : "NULL")}");
        }
    }
    
    /// <summary>
    /// 重置所有动画状态
    /// </summary>
    public void ResetAllAnimations()
    {
        if (animator == null) return;
        
        SetMovingAnimation(false);
        SetAttackingAnimation(false);
        SetRetreatingAnimation(false);
        SetStunningAnimation(false);
        SetMoveDirection(0f);
        SetMovementSpeed(0f);
        
        if (showDebugLogs)
        {
            Debug.Log($"🔄 [RangeAnimation] 重置所有动画");
        }
    }
    
    /// <summary>
    /// 检查是否正在播放特定动画
    /// </summary>
    public bool IsPlayingAnimation(string stateName)
    {
        if (animator == null) return false;
        
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(stateName);
    }
    
    /// <summary>
    /// 设置动画参数名称（如果需要动态修改）
    /// </summary>
    public void SetAnimationParameterNames(
        string moving = null,
        string speed = null,
        string attacking = null,
        string retreating = null,
        string stunning = null,
        string shoot = null)
    {
        if (moving != null) isMovingParam = moving;
        if (speed != null) speedParam = speed;
        if (attacking != null) isAttackingParam = attacking;
        if (retreating != null) isRetreatingParam = retreating;
        if (stunning != null) isStunningParam = stunning;
        if (shoot != null) shootTrigger = shoot;
    }
}
