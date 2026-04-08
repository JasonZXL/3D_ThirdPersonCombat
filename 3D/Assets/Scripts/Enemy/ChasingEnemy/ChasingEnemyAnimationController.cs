using UnityEngine;

public class ChasingEnemyAnimationController : MonoBehaviour
{
    [Header("动画参数")]
    [SerializeField] private string isAttackingParam = "isAttacking";
    [SerializeField] private string isMovingParam = "isMoving";
    [SerializeField] private string speedParam = "speed";
    [SerializeField] private string isStunningParam = "isStunning";
    
    [Header("动画引用")]
    [SerializeField] private Animator animator;
    
    [Header("移动速度设置")]
    [SerializeField] private float maxMoveSpeed = 3f;
    
    private float currentSpeed = 0f;
    private bool isMoving = false;
    
    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
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
            Debug.LogWarning("ChasingEnemyAnimationController: Animator is null!");
        }
    }
    
    public void SetAttackAnimation(bool attacking)
    {
        if (animator == null) return;
        animator.SetBool(isAttackingParam, attacking);
    }
    
    public void SetMovingAnimation(bool moving)
    {
        if (animator == null) return;
        
        isMoving = moving;
        animator.SetBool(isMovingParam, moving);
        
        // 更新速度参数
        float normalizedSpeed = moving ? Mathf.Clamp01(currentSpeed / maxMoveSpeed) : 0f;
        animator.SetFloat(speedParam, normalizedSpeed);
    }
    
    public void SetStunAnimation(bool stunning)
    {
        if (animator == null) return;
        animator.SetBool(isStunningParam, stunning);
    }
    
    public void SetMovementSpeed(float speed)
    {
        currentSpeed = speed;
        
        // 如果正在移动，更新动画速度
        if (isMoving && animator != null)
        {
            float normalizedSpeed = Mathf.Clamp01(speed / maxMoveSpeed);
            animator.SetFloat(speedParam, normalizedSpeed);
        }
    }
    
    public void SetMaxMoveSpeed(float maxSpeed)
    {
        maxMoveSpeed = maxSpeed;
    }
    
    // 为动画事件提供的方法
    public void OnAttackAnimationStart()
    {
        Debug.Log("Attack animation started");
        // 可以在这里触发攻击效果
    }
    
    public void OnAttackAnimationEnd()
    {
        Debug.Log("Attack animation ended");
        // 可以在这里结束攻击状态
    }
    
    public void OnStunAnimationStart()
    {
        Debug.Log("Stun animation started");
    }
    
    public void OnStunAnimationEnd()
    {
        Debug.Log("Stun animation ended");
    }
    
    // 获取动画状态的方法
    public bool IsAttackingAnimationPlaying()
    {
        if (animator == null) return false;
        return animator.GetBool(isAttackingParam);
    }
    
    public bool IsStunningAnimationPlaying()
    {
        if (animator == null) return false;
        return animator.GetBool(isStunningParam);
    }
    
    public bool IsMovingAnimationPlaying()
    {
        if (animator == null) return false;
        return animator.GetBool(isMovingParam);
    }
    
    // 设置动画参数名称（如果需要动态修改）
    public void SetAnimationParameterNames(string attacking = null, string moving = null, string speed = null, string stunning = null)
    {
        if (attacking != null) isAttackingParam = attacking;
        if (moving != null) isMovingParam = moving;
        if (speed != null) speedParam = speed;
        if (stunning != null) isStunningParam = stunning;
    }
}
