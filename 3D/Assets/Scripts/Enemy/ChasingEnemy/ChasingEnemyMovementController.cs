using UnityEngine;
using UnityEngine.AI;

public class ChasingEnemyMovementController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float turnSpeed = 3f;
    [SerializeField] private float stoppingDistance = 1.5f;
    
    [Header("路径更新设置")]
    [SerializeField] private float playerMoveThreshold = 1.5f; // 玩家移动超过此距离时更新路径
    
    [Header("组件引用")]
    [SerializeField] private ChasingEnemyAnimationController animationController;
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;
    
    private NavMeshAgent navAgent;
    private CharacterController charControl;
    private Transform Player;
    private Vector3 desVelocity;
    
    // 移动状态
    private Vector3 lastPosition;
    private float currentSpeed = 0f;
    private bool isMoving = false;
    
    // 路径更新追踪
    private Vector3 lastPlayerPosition;
    
    public bool IsMoving => isMoving;
    public float CurrentSpeed => currentSpeed;
    
    private void Awake()
    {
        // 尝试获取组件，如果不存在会在 Initialize 中再次尝试
        navAgent = GetComponent<NavMeshAgent>();
        charControl = GetComponent<CharacterController>();
        
        if (showDebugLogs) 
        {
            Debug.Log($"📄 [MovementController] Awake: {gameObject.name}");
            Debug.Log($"   - NavAgent: {(navAgent != null ? "已找到" : "⚠️ 未找到(将在Initialize中重试)")}");
            Debug.Log($"   - CharacterController: {(charControl != null ? "已找到" : "⚠️ 未找到")}");
            Debug.Log($"   - AnimationController引用: {(animationController != null ? "已分配" : "⚠️ 未分配")}");
        }
    }
    
    public void Initialize(Transform PlayerTransform, ChasingEnemyAnimationController animController = null)
    {
        if (showDebugLogs) Debug.Log($"🎯 [MovementController] 初始化开始: {gameObject.name}");
        
        Player = PlayerTransform;
        
        if (animController != null)
        {
            animationController = animController;
        }
        
        // 如果 Awake 时没找到，这里再尝试一次
        if (navAgent == null)
        {
            navAgent = GetComponent<NavMeshAgent>();
            if (navAgent == null)
            {
                Debug.LogError($"❌ [MovementController] NavMeshAgent 仍未找到！请确保已添加 NavMeshAgent 组件到 {gameObject.name}");
                return;
            }
            else if (showDebugLogs)
            {
                Debug.Log($"✅ [MovementController] Initialize 中成功找到 NavMeshAgent");
            }
        }
        
        if (charControl == null)
        {
            charControl = GetComponent<CharacterController>();
            if (charControl == null)
            {
                Debug.LogError($"❌ [MovementController] CharacterController 未找到！请在敌人上添加 CharacterController 组件");
                return;
            }
            else if (showDebugLogs)
            {
                Debug.Log($"✅ [MovementController] Initialize 中成功找到 CharacterController");
            }
        }
        
        // 配置NavMeshAgent
        navAgent.speed = moveSpeed;
        navAgent.acceleration = 8f;
        navAgent.angularSpeed = 120f;
        navAgent.stoppingDistance = stoppingDistance;
        navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        
        lastPosition = transform.position;
        
        // 初始化玩家位置追踪
        if (Player != null)
        {
            lastPlayerPosition = Player.position;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"✅ [MovementController] 初始化完成");
            Debug.Log($"   - 移动速度: {moveSpeed}");
            Debug.Log($"   - 停止距离: {stoppingDistance}");
            Debug.Log($"   - 玩家移动阈值: {playerMoveThreshold}");
            Debug.Log($"   - 玩家: {(Player != null ? Player.name : "null")}");
            Debug.Log($"   - NavAgent状态: {(navAgent.isOnNavMesh ? "在NavMesh上" : "不在NavMesh上")}");
        }
    }
    
    public void UpdateMovement()
    {
        CalculateMovementSpeed();
        
        if (Player == null)
        {
            if (showDebugLogs && Time.frameCount % 120 == 0) 
                Debug.LogWarning($"⚠️ [MovementController] 玩家引用为null");
            SetMovingAnimation(false);
            return;
        }
        
        if (navAgent == null || !navAgent.isActiveAndEnabled)
        {
            if (showDebugLogs && Time.frameCount % 120 == 0) 
                Debug.LogWarning($"⚠️ [MovementController] NavAgent不可用");
            SetMovingAnimation(false);
            return;
        }
        
        // 检查是否在NavMesh上
        if (!navAgent.isOnNavMesh)
        {
            if (showDebugLogs && Time.frameCount % 120 == 0) 
                Debug.LogWarning($"⚠️ [MovementController] 不在NavMesh上，尝试重新定位");
            
            // 尝试重新定位到NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
                transform.position = hit.position;
                if (showDebugLogs) Debug.Log($"📍 [MovementController] 已重新定位到NavMesh: {hit.position}");
            }
            else
            {
                SetMovingAnimation(false);
                return;
            }
        }
        
        // 检测玩家是否移动了足够距离，如果是则更新路径
        float playerMoveDist = Vector3.Distance(Player.position, lastPlayerPosition);
        bool needsPathUpdate = playerMoveDist > playerMoveThreshold || !navAgent.hasPath;
        
        if (needsPathUpdate)
        {
            navAgent.SetDestination(Player.position);
            lastPlayerPosition = Player.position;
            
            if (showDebugLogs && Time.frameCount % 60 == 0) 
            {
                if (playerMoveDist > playerMoveThreshold)
                {
                    Debug.Log($"🎯 [MovementController] 玩家移动{playerMoveDist:F2}米，更新追击路径");
                }
                else
                {
                    Debug.Log($"🎯 [MovementController] 设置新目标: {Player.position}");
                }
            }
        }
        
        // 处理旋转
        FaceTarget();
        
        // 处理移动
        if (navAgent.hasPath && !navAgent.isStopped)
        {
            desVelocity = navAgent.desiredVelocity;
            
            if (desVelocity.magnitude > 0.1f)
            {
                charControl.Move(desVelocity.normalized * moveSpeed * Time.deltaTime);
                navAgent.velocity = charControl.velocity;
                
                SetMovingAnimation(true);
                
                if (showDebugLogs && Time.frameCount % 120 == 0)
                {
                    float distanceToPlayer = Vector3.Distance(transform.position, Player.position);
                    Debug.Log($"🏃 [MovementController] 移动中, 速度: {currentSpeed:F2}, 到玩家距离: {distanceToPlayer:F2}");
                }
            }
            else
            {
                SetMovingAnimation(false);
            }
        }
        else
        {
            SetMovingAnimation(false);
        }
    }
    
    private void CalculateMovementSpeed()
    {
        Vector3 currentPosition = transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        currentSpeed = distanceMoved / Time.deltaTime;
        lastPosition = currentPosition;
    }
    
    private void FaceTarget()
    {
        if (Player == null) return;
        
        Vector3 lookPos = Player.position - transform.position;
        lookPos.y = 0;
        if (lookPos != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookPos);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);
        }
    }
    
    private void SetMovingAnimation(bool moving)
    {
        isMoving = moving;
        
        if (animationController != null)
        {
            animationController.SetMovingAnimation(moving);
            animationController.SetMovementSpeed(currentSpeed);
        }
    }
    
    public void StopMovement()
    {
        if (showDebugLogs) Debug.Log($"⏹️ [MovementController] 停止移动");
        
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
        }
        
        if (charControl != null)
        {
            charControl.Move(Vector3.zero);
        }
        
        SetMovingAnimation(false);
    }
    
    public void ResumeMovement()
    {
        if (showDebugLogs) Debug.Log($"▶️ [MovementController] 恢复移动");
        
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = false;
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
        }
    }
    
    public void ForceRecalculatePath()
    {
        if (Player != null && navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.SetDestination(Player.position);
            lastPlayerPosition = Player.position;
            navAgent.isStopped = false;
            
            if (showDebugLogs) Debug.Log($"🔄 [MovementController] 强制重新计算路径");
        }
    }
    
    public void SetMovementSpeed(float speed)
    {
        moveSpeed = speed;
        
        if (navAgent != null)
        {
            navAgent.speed = speed;
        }
        
        if (animationController != null)
        {
            animationController.SetMaxMoveSpeed(speed);
        }
        
        if (showDebugLogs) Debug.Log($"🔧 [MovementController] 设置移动速度: {speed}");
    }
    
    public void SetStoppingDistance(float distance)
    {
        stoppingDistance = distance;
        
        if (navAgent != null)
        {
            navAgent.stoppingDistance = distance;
        }
        
        if (showDebugLogs) Debug.Log($"🔧 [MovementController] 设置停止距离: {distance}");
    }
    
    public void SetPlayerMoveThreshold(float threshold)
    {
        playerMoveThreshold = threshold;
        
        if (showDebugLogs) Debug.Log($"🔧 [MovementController] 设置玩家移动阈值: {threshold}");
    }
    
    public bool IsPlayerInRange(float range)
    {
        if (Player == null) return false;
        return Vector3.Distance(transform.position, Player.position) <= range;
    }
    
    public float DistanceToPlayer()
    {
        if (Player == null) return Mathf.Infinity;
        return Vector3.Distance(transform.position, Player.position);
    }
    
    public Vector3 GetMovementDirection()
    {
        return desVelocity;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (navAgent != null && navAgent.hasPath)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < navAgent.path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(navAgent.path.corners[i], navAgent.path.corners[i + 1]);
            }

            if (Player != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, stoppingDistance);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, Player.position);
                
                // 显示玩家移动阈值范围
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(lastPlayerPosition, playerMoveThreshold);
            }
        }
    }
}