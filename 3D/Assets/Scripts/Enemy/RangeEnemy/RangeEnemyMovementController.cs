using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 远程敌人移动控制器：负责追击和撤离移动逻辑
/// </summary>
public class RangeEnemyMovementController : MonoBehaviour
{
    [Header("移动速度设置")]
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float retreatSpeed = 5f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float stopDistance = 8f;
    
    [Header("范围设置")]
    [SerializeField] private float attackRange = 10f;
    [SerializeField] private float retreatRange = 3f;
    
    [Header("组件引用")]
    [SerializeField] private RangeEnemyAnimationController animationController;
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = false;
    
    // 组件引用
    private NavMeshAgent navAgent;
    private CharacterController charController;
    private Transform player;
    
    // 移动状态
    private Vector3 lastPosition;
    private float currentSpeed = 0f;
    private bool isMoving = false;
    private Vector3 retreatTarget;
    
    // 撤离卡住检测
    private Vector3 lastRetreatPosition;
    private float retreatStuckTimer = 0f;
    private const float RETREAT_STUCK_TIMEOUT = 2f;
    private const float RETREAT_POSITION_EPSILON = 0.5f;
    
    // 公共属性
    public bool IsMoving => isMoving;
    public float CurrentSpeed => currentSpeed;
    public float AttackRange => attackRange;
    public float RetreatRange => retreatRange;
    
    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        charController = GetComponent<CharacterController>();
        
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"🔄 [RangeMovement] Awake: {gameObject.name}");
            Debug.Log($"   - NavAgent: {(navAgent != null ? "已找到" : "⚠️ 未找到")}");
            Debug.Log($"   - CharacterController: {(charController != null ? "已找到" : "⚠️ 未找到")}");
        }
    }
    
    public void Initialize(Transform playerTransform, RangeEnemyAnimationController animController = null)
    {
        player = playerTransform;
        
        if (animController != null)
        {
            animationController = animController;
        }
        
        if (navAgent != null)
        {
            navAgent.speed = chaseSpeed;
            navAgent.stoppingDistance = stopDistance;
            navAgent.angularSpeed = rotationSpeed * 100f;
            navAgent.acceleration = 8f;
            navAgent.autoBraking = true;
            navAgent.updateRotation = false;
            navAgent.updatePosition = false;
        }
        
        lastPosition = transform.position;
        lastRetreatPosition = transform.position;
        
        if (showDebugLogs)
        {
            Debug.Log($"✅ [RangeMovement] 初始化完成");
            Debug.Log($"   - 追击速度: {chaseSpeed}");
            Debug.Log($"   - 撤离速度: {retreatSpeed}");
            Debug.Log($"   - 玩家: {(player != null ? player.name : "null")}");
        }
    }
    
    /// <summary>
    /// 追击移动
    /// </summary>
    public void UpdateChaseMovement()
    {
        if (player == null || navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
        {
            isMoving = false;
            UpdateMovementAnimation();
            return;
        }
        
        navAgent.speed = chaseSpeed;
        navAgent.SetDestination(player.position);
        
        ExecuteMovement();
        RotateTowardsPlayer();
    }
    
    /// <summary>
    /// 撤离移动
    /// </summary>
    public void UpdateRetreatMovement()
    {
        if (player == null || navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
        {
            isMoving = false;
            UpdateMovementAnimation();
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float distanceToTarget = Vector3.Distance(transform.position, retreatTarget);
        
        // 检测卡住
        CheckRetreatStuck();
        
        // 重新计算撤离目标的条件
        if (distanceToPlayer <= retreatRange * 1.1f || distanceToTarget < 1f)
        {
            CalculateRetreatTarget();
            navAgent.SetDestination(retreatTarget);
        }
        
        navAgent.speed = retreatSpeed;
        ExecuteMovement();
        RotateAwayFromPlayer();
    }
    
    /// <summary>
    /// 执行移动
    /// </summary>
    private void ExecuteMovement()
    {
        if (navAgent.hasPath && !navAgent.isStopped)
        {
            Vector3 desiredVelocity = navAgent.desiredVelocity;
            
            if (charController != null && charController.enabled)
            {
                charController.Move(desiredVelocity.normalized * navAgent.speed * Time.deltaTime);
                navAgent.velocity = charController.velocity;
            }
            else
            {
                transform.position += desiredVelocity.normalized * navAgent.speed * Time.deltaTime;
            }
            
            // ✅ 手动驱动transform时，同步NavMeshAgent的内部位置，防止路径/速度异常

            
            if (navAgent != null && navAgent.enabled)

            
            {

            
                navAgent.nextPosition = transform.position;

            
            }


            
            isMoving = desiredVelocity.magnitude > 0.1f;
}
        else
        {
            isMoving = false;
        }
        
        CalculateMovementSpeed();
        UpdateMovementAnimation();
    }
    
    /// <summary>
    /// 计算撤离目标位置
    /// </summary>
    private void CalculateRetreatTarget()
    {
        if (player == null) return;
        
        Vector3 playerPosition = player.position;
        Vector3 enemyPosition = transform.position;
        
        // 计算远离方向
        Vector3 retreatDirection = (enemyPosition - playerPosition).normalized;
        
        // 计算目标位置(安全距离)
        float safeDistance = attackRange * 0.8f;
        retreatTarget = playerPosition + retreatDirection * safeDistance;
        
        // 确保目标在NavMesh上
        NavMeshHit hit;
        if (NavMesh.SamplePosition(retreatTarget, out hit, 10f, NavMesh.AllAreas))
        {
            retreatTarget = hit.position;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"🏃 计算撤离目标: {retreatTarget}");
        }
    }
    
    /// <summary>
    /// 检查撤离是否卡住
    /// </summary>
    private void CheckRetreatStuck()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastRetreatPosition);
        
        if (distanceMoved < RETREAT_POSITION_EPSILON)
        {
            retreatStuckTimer += Time.deltaTime;
        }
        else
        {
            lastRetreatPosition = transform.position;
            retreatStuckTimer = 0f;
        }
    }
    
    /// <summary>
    /// 是否撤离卡住
    /// </summary>
    public bool IsRetreatStuck()
    {
        return retreatStuckTimer >= RETREAT_STUCK_TIMEOUT;
    }
    
    /// <summary>
    /// 重置撤离卡住状态
    /// </summary>
    public void ResetRetreatStuck()
    {
        retreatStuckTimer = 0f;
        lastRetreatPosition = transform.position;
    }
    
    /// <summary>
    /// 计算移动速度
    /// </summary>
    private void CalculateMovementSpeed()
    {
        Vector3 currentPosition = transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        currentSpeed = distanceMoved / Time.deltaTime;
        lastPosition = currentPosition;
    }
    
    /// <summary>
    /// 更新移动动画
    /// </summary>
    private void UpdateMovementAnimation()
    {
        if (animationController != null)
        {
            animationController.SetMovingAnimation(isMoving);
            animationController.SetMovementSpeed(currentSpeed);
        }
    }
    
    /// <summary>
    /// 面向玩家
    /// </summary>
    private void RotateTowardsPlayer()
    {
        if (player == null) return;
        
        Vector3 lookDirection = player.position - transform.position;
        lookDirection.y = 0;
        
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
        }
    }
    
    /// <summary>
    /// 背向玩家
    /// </summary>
    private void RotateAwayFromPlayer()
    {
        if (player == null) return;
        
        Vector3 lookDirection = transform.position - player.position;
        lookDirection.y = 0;
        
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
        }
    }
    
    /// <summary>
    /// 停止移动
    /// </summary>
    public void StopMovement()
    {
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }
        
        isMoving = false;
        UpdateMovementAnimation();
        
        if (showDebugLogs)
        {
            Debug.Log($"⏹️ [RangeMovement] 停止移动");
        }
    }
    
    /// <summary>
    /// 恢复移动
    /// </summary>
    public void ResumeMovement()
    {
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            // ✅ 与本控制器的“手动移动”模式保持一致
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;

            navAgent.isStopped = false;
            navAgent.nextPosition = transform.position;
        }
if (showDebugLogs)
        {
            Debug.Log($"▶️ [RangeMovement] 恢复移动");
        }
    }
    
    /// <summary>
    /// 获取到玩家的距离
    /// </summary>
    public float DistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Vector3.Distance(transform.position, player.position);
    }
    
    /// <summary>
    /// 设置移动参数
    /// </summary>
    public void SetMovementParameters(float chase, float retreat, float rotation, float stop)
    {
        chaseSpeed = chase;
        retreatSpeed = retreat;
        rotationSpeed = rotation;
        stopDistance = stop;
        
        if (navAgent != null)
        {
            navAgent.speed = chaseSpeed;
            navAgent.stoppingDistance = stopDistance;
            navAgent.angularSpeed = rotationSpeed * 100f;
        }
    }
    
    /// <summary>
    /// 设置范围参数
    /// </summary>
    public void SetRangeParameters(float attack, float retreat)
    {
        attackRange = Mathf.Max(retreat + 1f, attack);
        retreatRange = Mathf.Min(attack - 1f, Mathf.Max(1f, retreat));
    }
    
    private void OnDrawGizmosSelected()
    {
        // 绘制攻击范围
        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
        Gizmos.DrawSphere(transform.position, attackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // 绘制撤离范围
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, retreatRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, retreatRange);
        
        // 绘制撤离目标
        if (retreatTarget != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(retreatTarget, 0.5f);
            Gizmos.DrawLine(transform.position, retreatTarget);
        }
    }
}
