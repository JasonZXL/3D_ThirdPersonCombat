using UnityEngine;
using UnityEngine.AI;

public class ChasingEnemyStunController : MonoBehaviour
{
    [Header("眩晕设置")]
    [SerializeField] private float stunDuration = 3f;
    [SerializeField] private bool showStunDebug = true;
    
    [Header("击退设置")]
    [SerializeField] private bool stunOnKnockback = true;
    
    [Header("组件引用")]
    [SerializeField] private ChasingEnemyAnimationController animationController;
    
    // 眩晕状态变量
    private bool isStunning = false;
    private float stunTimer = 0f;
    private Vector3 stunStartPosition;
    private Quaternion stunStartRotation;
    private bool stunJustEnded = false;
    
    // 组件引用
    private NavMeshAgent navAgent;
    private CharacterController charControl;
    private KnockbackSystem knockbackSystem;
    
    public bool IsStunning => isStunning;
    
    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        charControl = GetComponent<CharacterController>();
        knockbackSystem = GetComponent<KnockbackSystem>();
        
        if (animationController == null)
        {
            animationController = GetComponent<ChasingEnemyAnimationController>();
        }
    }
    
    public void Initialize(ChasingEnemyAnimationController animController = null)
    {
        if (animController != null)
        {
            animationController = animController;
        }
        
        if (knockbackSystem != null && stunOnKnockback)
        {
            // 注册击退事件
            knockbackSystem.OnKnockbackStart += HandleKnockbackStart;
            knockbackSystem.OnKnockbackEnd += HandleKnockbackEnd;
        }
    }
    
    private void Update()
    {
        if (isStunning)
        {
            UpdateStunState();
        }
    }
    
    private void UpdateStunState()
    {
        // 更新眩晕计时器
        stunTimer += Time.deltaTime;
        
        // 眩晕期间保持位置
        transform.position = stunStartPosition;
        transform.rotation = stunStartRotation;
        
        // 检查眩晕是否结束
        if (stunTimer >= stunDuration)
        {
            EndStun();
            return;
        }
        
        // 调试日志
        if (showStunDebug && stunTimer % 0.5f < Time.deltaTime)
        {
            Debug.Log($"🌀 敌人眩晕中: {gameObject.name}, 剩余时间: {(stunDuration - stunTimer):F1}秒");
        }
    }
    
    public void StartStun(float customDuration = 0f)
    {
        // 防止重复眩晕
        if (stunJustEnded)
        {
            if (showStunDebug)
            {
                Debug.Log($"⏳ 眩晕刚结束，忽略新的眩晕请求: {gameObject.name}");
            }
            return;
        }
        
        if (isStunning)
        {
            if (showStunDebug)
            {
                Debug.Log($"⚠️ 敌人已在眩晕中，忽略重复调用: {gameObject.name}");
            }
            return;
        }
        
        // 设置眩晕状态
        isStunning = true;
        stunTimer = 0f;
        
        // 记录初始位置和旋转
        stunStartPosition = transform.position;
        stunStartRotation = transform.rotation;
        
        // 设置自定义持续时间
        if (customDuration > 0)
        {
            stunDuration = customDuration;
        }
        
        // 停止所有移动和攻击行为
        StopAllBehaviors();
        
        // 设置动画
        if (animationController != null)
        {
            animationController.SetStunAnimation(true);
            animationController.SetMovingAnimation(false);
            animationController.SetAttackAnimation(false);
        }
        
        if (showStunDebug)
        {
            Debug.Log($"🌀 敌人进入眩晕状态: {gameObject.name}, 持续时间: {stunDuration}秒");
        }
    }
    
    public void EndStun()
    {
        if (!isStunning)
        {
            if (showStunDebug)
            {
                Debug.Log($"⚠️ 敌人不在眩晕中，忽略EndStun: {gameObject.name}");
            }
            return;
        }
        
        // 重置眩晕状态
        isStunning = false;
        stunTimer = 0f;
        stunJustEnded = true;
        
        // 重置动画
        if (animationController != null)
        {
            animationController.SetStunAnimation(false);
        }
        
        // 恢复NavMeshAgent
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.enabled = true;
            navAgent.isStopped = false;
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            navAgent.velocity = Vector3.zero;
            
            // 确保在NavMesh上
            if (!navAgent.isOnNavMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
                {
                    navAgent.Warp(hit.position);
                    transform.position = hit.position;
                }
            }
        }
        
        if (showStunDebug)
        {
            Debug.Log($"✅ 敌人眩晕状态结束: {gameObject.name}");
        }
        
        // 重置stunJustEnded标记（延迟一点时间）
        Invoke(nameof(ResetStunJustEnded), 0.5f);
    }
    
    private void ResetStunJustEnded()
    {
        stunJustEnded = false;
    }
    
    public void ForceEndStun()
    {
        isStunning = false;
        stunTimer = 0f;
        stunJustEnded = false;
        
        // 恢复NavMeshAgent
        if (navAgent != null)
        {
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            navAgent.isStopped = false;
        }
        
        // 重置动画
        if (animationController != null)
        {
            animationController.SetStunAnimation(false);
        }
    }
    
    private void StopAllBehaviors()
    {
        // 停止NavMeshAgent
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
        }
        
        // 停止CharacterController
        if (charControl != null)
        {
            charControl.Move(Vector3.zero);
        }
    }
    
    private void HandleKnockbackStart()
    {
        if (showStunDebug)
        {
            Debug.Log($"💥 击退开始: {gameObject.name}");
        }
        
        // 击退开始时进入眩晕
        if (stunOnKnockback && !isStunning)
        {
            StartStun();
        }
    }
    
    private void HandleKnockbackEnd()
    {
        if (showStunDebug)
        {
            Debug.Log($"💨 击退结束: {gameObject.name}, 眩晕状态: {isStunning}");
        }
        
        // 如果不在眩晕状态，恢复移动
        if (!isStunning && navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            navAgent.isStopped = false;
        }
    }
    
    public void SetStunDuration(float duration)
    {
        stunDuration = duration;
    }
    
    public void SetStunOnKnockback(bool enable)
    {
        stunOnKnockback = enable;
        
        // 重新绑定事件
        if (knockbackSystem != null)
        {
            if (enable)
            {
                knockbackSystem.OnKnockbackStart += HandleKnockbackStart;
                knockbackSystem.OnKnockbackEnd += HandleKnockbackEnd;
            }
            else
            {
                knockbackSystem.OnKnockbackStart -= HandleKnockbackStart;
                knockbackSystem.OnKnockbackEnd -= HandleKnockbackEnd;
            }
        }
    }
    
    private void OnDestroy()
    {
        // 清理事件订阅
        if (knockbackSystem != null)
        {
            knockbackSystem.OnKnockbackStart -= HandleKnockbackStart;
            knockbackSystem.OnKnockbackEnd -= HandleKnockbackEnd;
        }
    }
}
