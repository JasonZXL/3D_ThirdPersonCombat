using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;

public interface IKnockbackReceiver
{
    void OnKnockbackStart();
    void OnKnockbackEnd();
}

public class KnockbackSystem : MonoBehaviour
{
    #region 击退设置
    [Header("击退设置")]
    [SerializeField] private float knockbackSpeed = 15f;
    [SerializeField] private float knockbackDuration = 0.5f;
    [SerializeField] private AnimationCurve knockbackCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("物体击退设置")]
    [SerializeField] private bool canBeKnockbacked = true;
    [SerializeField] private bool isObject = false;
    
    [Header("🔧 优化：碰撞检测配置")]
    [Tooltip("用于检测障碍物的层（Ground, Obstacle）")]
    [SerializeField] private LayerMask obstacleLayerMask = -1;
    
    [Tooltip("用于检测敌人的层")]
    [SerializeField] private LayerMask enemyLayerMask = -1;
    
    [Tooltip("用于检测可击退物体的层")]
    [SerializeField] private LayerMask objectLayerMask = -1;
    
    [Tooltip("碰撞检测球体半径（建议值：0.3-0.5）")]
    [SerializeField] private float collisionRadius = 0.4f;
    
    [Tooltip("碰撞体表面安全距离（防止嵌入，建议值：0.01-0.05）")]
    [SerializeField] private float skinWidth = 0.02f;
    
    [Tooltip("最大穿透修正距离（发生穿模时的最大退出距离）")]
    [SerializeField] private float maxPenetrationCorrection = 0.5f;
    
    [Tooltip("每帧最大移动步长（防止高速穿透，建议值：0.1-0.3）")]
    [SerializeField] private float maxMoveStepPerFrame = 0.2f;

    [Header("🔧 优化：碰撞响应")]
    [Tooltip("启用精确碰撞停止（使用SphereCast）")]
    [SerializeField] private bool enablePreciseCollision = true;
    
    [Tooltip("启用穿透修正（自动推出已嵌入的碰撞体）")]
    [SerializeField] private bool enablePenetrationCorrection = true;

    private bool _isKnockbackActive = false;
    private Vector3 _knockbackStartPosition;
    private Vector3 _knockbackTargetPosition;
    private Vector3 _knockbackDirection;
    private float _knockbackTimer = 0f;
    private CharacterController _characterController;
    private NavMeshAgent _navAgent;
    private Collider _mainCollider; // 新增：缓存主碰撞体

    public bool IsKnockbackActive => _isKnockbackActive;
    public bool CanBeKnockbacked => canBeKnockbacked;

    public event Action OnKnockbackStart;
    public event Action OnKnockbackEnd;

    #endregion
    
    #region 状态检测器集成
    [Header("状态检测器集成")]
    [SerializeField] private bool enableStateCollisionDetection = true;
    private KnockbackCollisionDetector _stateDetector;
    #endregion
    
    #region 调试设置
    [Header("🔧 优化：调试可视化")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color gizmoRayColor = Color.cyan;
    [SerializeField] private Color gizmoHitColor = Color.red;
    [SerializeField] private Color gizmoSafeColor = Color.green;
    
    // 调试数据
    private Vector3 _lastCollisionPoint;
    private Vector3 _lastCollisionNormal;
    private bool _hadCollisionLastFrame;
    #endregion
    
    #region Unity生命周期
    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _navAgent = GetComponent<NavMeshAgent>();
        _mainCollider = GetComponent<Collider>();
        
        // 如果没有Collider，尝试获取子对象的Collider
        if (_mainCollider == null)
        {
            _mainCollider = GetComponentInChildren<Collider>();
        }
        
        if (_mainCollider == null && showDebugLogs)
        {
            Debug.LogWarning($"⚠️ {gameObject.name} 没有找到Collider组件，碰撞检测可能无效！");
        }
        
        isObject = gameObject.CompareTag("Object");
        
        // 修改：无论是否为物体，都尝试获取碰撞检测器
        if (enableStateCollisionDetection)
        {
            KnockbackCollisionDetector collisionDetector = GetComponent<KnockbackCollisionDetector>();
            _stateDetector = collisionDetector;
            if (collisionDetector == null && showDebugLogs)
            {
                Debug.LogWarning($"⚠️ {gameObject.name} 缺少KnockbackCollisionDetector组件");
            }
        }
    }
    
    private void Update()
    {
        if (!_isKnockbackActive) return;
        
        UpdateKnockbackMovement();
        CheckKnockbackCompletion();
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;
        
        // 绘制碰撞检测球体
        if (_isKnockbackActive)
        {
            Gizmos.color = _hadCollisionLastFrame ? gizmoHitColor : gizmoSafeColor;
            Gizmos.DrawWireSphere(transform.position, collisionRadius);
            
            // 绘制击退方向
            Gizmos.color = gizmoRayColor;
            Gizmos.DrawRay(transform.position, _knockbackDirection * 1f);
            
            // 绘制碰撞点
            if (_hadCollisionLastFrame)
            {
                Gizmos.color = gizmoHitColor;
                Gizmos.DrawSphere(_lastCollisionPoint, 0.1f);
                Gizmos.DrawRay(_lastCollisionPoint, _lastCollisionNormal * 0.5f);
            }
        }
    }
    #endregion
    
    #region 击退控制方法
    public void ApplyKnockbackToPosition(Vector3 targetPosition)
    {
        if (!canBeKnockbacked || _isKnockbackActive)
        {
            if (showDebugLogs)
                Debug.Log($"⚠️ {gameObject.name} 已经在击退中或不可击退，跳过新击退");
            return;
        }
        
        _knockbackStartPosition = transform.position;
        float originalY = _knockbackStartPosition.y;
        _knockbackTargetPosition = new Vector3(targetPosition.x, originalY, targetPosition.z);
        
        Vector3 directionFlat = _knockbackTargetPosition - _knockbackStartPosition;
        directionFlat.y = 0;
        _knockbackDirection = directionFlat.normalized;
        
        if (_knockbackDirection == Vector3.zero)
        {
            _knockbackDirection = transform.forward;
            _knockbackDirection.y = 0;
            _knockbackDirection.Normalize();
        }
        
        _knockbackTimer = 0f;
        _isKnockbackActive = true;
        _hadCollisionLastFrame = false;
        
        if (showDebugLogs)
            Debug.Log($"💨 {gameObject.name} 开始水平击退: {_knockbackStartPosition} → {_knockbackTargetPosition}, 方向: {_knockbackDirection}");
        
        if (!isObject)
        {
            NotifyEnemyKnockbackStart();
        }

        OnKnockbackStart?.Invoke();
        
        // 修改：无论是否为物体，都尝试启动碰撞检测器
        if (enableStateCollisionDetection && _stateDetector != null)
        {
            _stateDetector.StartKnockback();
        }
        
        if (!isObject)
        {
            DisableEnemyComponentsDuringKnockback();
        }
    }
    
    public void ApplyKnockbackInDirection(Vector3 direction, float distance)
    {
        Vector3 directionFlat = new Vector3(direction.x, 0, direction.z).normalized;
        Vector3 targetPosition = transform.position + direction.normalized * distance;
        targetPosition.y = transform.position.y;
        ApplyKnockbackToPosition(targetPosition);
    }
    
    /// <summary>🔧 优化：更新击退移动（新增精确碰撞检测）</summary>
    private void UpdateKnockbackMovement()
    {
        _knockbackTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(_knockbackTimer / knockbackDuration);
        float curveValue = knockbackCurve.Evaluate(progress);
        
        Vector3 currentTargetPosition = Vector3.Lerp(_knockbackStartPosition, _knockbackTargetPosition, curveValue);
        Vector3 desiredMove = currentTargetPosition - transform.position;
        
        // 🔧 关键优化：限制每帧最大移动距离（防止高速穿透）
        float moveDistance = desiredMove.magnitude;
        if (moveDistance > maxMoveStepPerFrame)
        {
            desiredMove = desiredMove.normalized * maxMoveStepPerFrame;
            moveDistance = maxMoveStepPerFrame;
        }
        
        // 🔧 核心优化：执行精确碰撞检测
        Vector3 finalMove = desiredMove;
        if (enablePreciseCollision && moveDistance > 0.001f)
        {
            finalMove = PerformSweptCollisionDetection(desiredMove, moveDistance);
        }
        
        // 如果检测到碰撞并且停止了移动，检查是否应该结束击退
        if (finalMove.magnitude < 0.001f)
        {
            // 不立即结束击退，等待下一帧的穿透修正或计时器结束
        }
        
        // 执行移动
        if (_characterController != null && _characterController.enabled)
        {
            _characterController.Move(finalMove);
        }
        else
        {
            transform.position += finalMove;
        }
        
        // 🔧 新增：穿透修正（如果已经陷入碰撞体）
        if (enablePenetrationCorrection)
        {
            CorrectPenetration();
        }
    }
    
    /// <summary>🔧 新增：扫掠碰撞检测（核心优化）</summary>
    private Vector3 PerformSweptCollisionDetection(Vector3 desiredMove, float moveDistance)
    {
        Vector3 moveDirection = desiredMove.normalized;
        Vector3 origin = transform.position;
        
        // 合并所有需要检测的层
        LayerMask combinedMask = obstacleLayerMask | enemyLayerMask | objectLayerMask;
        
        // 🔧 使用SphereCast进行路径检测
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            collisionRadius,
            moveDirection,
            moveDistance,
            combinedMask,
            QueryTriggerInteraction.Ignore
        );
        
        // 过滤自身碰撞
        List<RaycastHit> validHits = new List<RaycastHit>();
        foreach (var hit in hits)
        {
            if (hit.collider.gameObject != gameObject && 
                hit.collider.transform != transform &&
                !hit.collider.transform.IsChildOf(transform))
            {
                validHits.Add(hit);
            }
        }
        
        if (validHits.Count > 0)
        {
            if(showDebugLogs)
                Debug.Log($"🚧🚧🚧 KnockbackSystem碰撞详情:");
                Debug.Log($"  碰撞体数量: {validHits.Count}");
            
            // 找到最近的碰撞点
            RaycastHit nearestHit = validHits[0];
            float nearestDistance = nearestHit.distance;
            
            foreach (var hit in validHits)
            {
                Debug.Log($"  碰撞: {hit.collider.name}, 标签: {hit.collider.tag}, 距离: {hit.distance:F3}");
                if (hit.distance < nearestDistance)
                {
                    nearestHit = hit;
                    nearestDistance = hit.distance;
                }
            }
            
            _lastCollisionPoint = nearestHit.point;
            _lastCollisionNormal = nearestHit.normal;
            _hadCollisionLastFrame = true;
            
            if (showDebugLogs)
                Debug.Log($"🚧 {gameObject.name} 检测到碰撞: {nearestHit.collider.name} 距离={nearestDistance:F3}m");
            
            // 🔧 关键修改：不再直接结束击退，而是触发碰撞事件并返回零移动
            // 计算安全移动距离（停在碰撞点前skinWidth距离）
            float safeDistance = Mathf.Max(0, nearestDistance - skinWidth);
            
            if (safeDistance < 0.001f)
            {
                // 已经非常接近，触发碰撞事件
                TriggerCollisionEvent(nearestHit.collider.gameObject);
                return Vector3.zero;
            }
            
            // 仍然可以移动，但触发碰撞事件
            TriggerCollisionEvent(nearestHit.collider.gameObject);
            
            // 计算实际可以移动的距离
            Vector3 safeMove = moveDirection * safeDistance;
            
            // 检查是否可以继续移动这段安全距离
            if (safeDistance > 0.001f)
            {
                return safeMove;
            }
            else
            {
                return Vector3.zero;
            }
        }
        
        _hadCollisionLastFrame = false;
        return desiredMove;
    }
    
    /// <summary>🔧 新增：触发碰撞事件</summary>
    private void TriggerCollisionEvent(GameObject collisionTarget)
    {
        if (collisionTarget == null) return;
        
        if (showDebugLogs)
            Debug.Log($"🚀 {gameObject.name} 触发碰撞事件 -> {collisionTarget.name}");
        
        // 通过状态检测器触发碰撞事件
        if (_stateDetector != null)
        {
            _stateDetector.ProcessCollisionImmediately(collisionTarget);
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning($"⚠️ {gameObject.name} 没有KnockbackCollisionDetector，无法触发碰撞事件");
        }
    }
    
    /// <summary>🔧 新增：穿透修正系统</summary>
    private void CorrectPenetration()
    {
        LayerMask combinedMask = obstacleLayerMask | enemyLayerMask | objectLayerMask;
        
        // 检测当前位置是否与任何碰撞体重叠
        Collider[] overlaps = Physics.OverlapSphere(
            transform.position,
            collisionRadius,
            combinedMask,
            QueryTriggerInteraction.Ignore
        );
        
        foreach (var overlap in overlaps)
        {
            // 跳过自身
            if (overlap.gameObject == gameObject || 
                overlap.transform == transform ||
                overlap.transform.IsChildOf(transform))
                continue;
            
            // 🔧 计算最近点和推出方向
            Vector3 closestPoint = overlap.ClosestPoint(transform.position);
            Vector3 pushOutDirection = (transform.position - closestPoint).normalized;
            float penetrationDepth = Vector3.Distance(transform.position, closestPoint);
            
            // 如果确实发生了穿透
            if (penetrationDepth < collisionRadius)
            {
                float correctionDistance = collisionRadius - penetrationDepth + skinWidth;
                correctionDistance = Mathf.Min(correctionDistance, maxPenetrationCorrection);
                
                Vector3 correctionMove = pushOutDirection * correctionDistance;
                
                if (showDebugLogs)
                    Debug.LogWarning($"⚠️ {gameObject.name} 穿透修正: {overlap.name} 深度={penetrationDepth:F3}m 修正={correctionDistance:F3}m");
                
                // 触发碰撞事件
                TriggerCollisionEvent(overlap.gameObject);
                
                // 应用修正
                if (_characterController != null && _characterController.enabled)
                {
                    _characterController.Move(correctionMove);
                }
                else
                {
                    transform.position += correctionMove;
                }
                
                // 立即停止击退
                EndKnockback();
                return;
            }
        }
    }
    
    private void CheckKnockbackCompletion()
    {
        if (_knockbackTimer >= knockbackDuration)
        {
            EndKnockback();
        }
        
        float distanceToTarget = Vector3.Distance(transform.position, _knockbackTargetPosition);
        if (distanceToTarget < 0.1f && _knockbackTimer > 0.1f)
        {
            EndKnockback();
        }
    }
    
    private void EndKnockback()
    {
        if (!_isKnockbackActive) return;
        
        _isKnockbackActive = false;
        _knockbackTimer = 0f;
        _hadCollisionLastFrame = false;
        
        if (showDebugLogs)
            Debug.Log($"🛑 {gameObject.name} 击退结束");
        
        if (!isObject)
        {
            NotifyEnemyKnockbackEnd();
        }

        OnKnockbackEnd?.Invoke();
        
        // 修改：无论是否为物体，都尝试停止碰撞检测器
        if (enableStateCollisionDetection && _stateDetector != null)
        {
            _stateDetector.StopKnockback();
        }
        
        if (!isObject)
        {
            EnableEnemyComponentsAfterKnockback();
        }
    }
    #endregion
    
    #region 敌人回调通知
    private void NotifyEnemyKnockbackStart()
    {
        IKnockbackReceiver receiver = GetComponent<IKnockbackReceiver>();
        if (receiver != null)
        {
            receiver.OnKnockbackStart();
            if (showDebugLogs)
                Debug.Log($"📢 通知IKnockbackReceiver击退开始: {gameObject.name}");
            return;
        }

        ChasingEnemy2 chasingEnemy2 = GetComponent<ChasingEnemy2>();
        if (chasingEnemy2 != null)
        {
            chasingEnemy2.OnKnockbackStart();
            if (showDebugLogs)
                Debug.Log($"📢 通知ChasingEnemy2击退开始: {gameObject.name}");
            return;
        }
    }

    private void NotifyEnemyKnockbackEnd()
    {
        IKnockbackReceiver receiver = GetComponent<IKnockbackReceiver>();
        if (receiver != null)
        {
            receiver.OnKnockbackEnd();
            if (showDebugLogs)
                Debug.Log($"📢 通知IKnockbackReceiver击退结束: {gameObject.name}");
            return;
        }

        ChasingEnemy2 chasingEnemy2 = GetComponent<ChasingEnemy2>();
        if (chasingEnemy2 != null)
        {
            chasingEnemy2.OnKnockbackEnd();
            if (showDebugLogs)
                Debug.Log($"📢 通知ChasingEnemy2击退结束: {gameObject.name}");
            return;
        }
    }
    #endregion
    
    #region 敌人组件控制
    private void DisableEnemyComponentsDuringKnockback()
    {
        IKnockbackReceiver receiver = GetComponent<IKnockbackReceiver>();
        if (receiver != null)
        {
            StopNavAgentForKnockback();
            SetAttackDetectorEnabled(false);

            if (showDebugLogs)
                Debug.Log($"🔧 使用IKnockbackReceiver：暂停NavMeshAgent/AttackDetector，逻辑由回调自行管理");
            return;
        }

        ChasingEnemy2 chasingEnemy2 = GetComponent<ChasingEnemy2>();
        if (chasingEnemy2 != null)
        {
            StopNavAgentForKnockback();
            SetAttackDetectorEnabled(false);

            if (showDebugLogs)
                Debug.Log($"🔧 使用ChasingEnemy2：暂停NavMeshAgent/AttackDetector，状态由回调管理");
            return;
        }

        ChasingEnemy chasingEnemy = GetComponent<ChasingEnemy>();
        if (chasingEnemy != null)
        {
            chasingEnemy.enabled = false;
        }

        StopNavAgentForKnockback();
        SetAttackDetectorEnabled(false);
    }

    private void StopNavAgentForKnockback()
    {
        if (_navAgent == null) return;

        if (_navAgent.isActiveAndEnabled)
        {
            _navAgent.isStopped = true;
            _navAgent.velocity = Vector3.zero;
            _navAgent.ResetPath();
            _navAgent.updatePosition = false;
            _navAgent.updateRotation = false;
        }
        else
        {
            _navAgent.enabled = false;
        }
    }

    private void RestoreNavAgentAfterKnockback()
    {
        if (_navAgent == null) return;

        _navAgent.enabled = true;

        if (!_navAgent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
            {
                _navAgent.Warp(hit.position);
                transform.position = hit.position;
            }
            else
            {
                if (showDebugLogs)
                    Debug.LogWarning($"⚠️ {gameObject.name} 未找到附近的NavMesh点，暂停NavMeshAgent");
                _navAgent.enabled = false;
                return;
            }
        }

        _navAgent.nextPosition = transform.position;
        _navAgent.ResetPath();
        _navAgent.isStopped = false;
        _navAgent.updatePosition = true;
        _navAgent.updateRotation = true;
    }

    private void SetAttackDetectorEnabled(bool enabled)
    {
        EnemyAttackDetector attackDetector = GetComponent<EnemyAttackDetector>();
        if (attackDetector != null)
        {
            attackDetector.enabled = enabled;
        }
    }

    private void EnableEnemyComponentsAfterKnockback()
    {
        IKnockbackReceiver receiver = GetComponent<IKnockbackReceiver>();
        if (receiver != null)
        {
            RestoreNavAgentAfterKnockback();
            SetAttackDetectorEnabled(true);

            if (showDebugLogs)
                Debug.Log($"🔧 使用IKnockbackReceiver：恢复NavMeshAgent/AttackDetector，逻辑由回调自行恢复");
            return;
        }

        ChasingEnemy2 chasingEnemy2 = GetComponent<ChasingEnemy2>();
        if (chasingEnemy2 != null)
        {
            RestoreNavAgentAfterKnockback();
            SetAttackDetectorEnabled(true);

            if (showDebugLogs)
                Debug.Log($"🔧 使用ChasingEnemy2：恢复NavMeshAgent/AttackDetector，状态由回调管理");
            return;
        }

        RestoreNavAgentAfterKnockback();

        ChasingEnemy chasingEnemy = GetComponent<ChasingEnemy>();
        if (chasingEnemy != null)
        {
            chasingEnemy.enabled = true;
        }

        SetAttackDetectorEnabled(true);
    }
    #endregion
    
    #region 公共方法
    public void ForceStopKnockback()
    {
        if (_isKnockbackActive)
        {
            EndKnockback();
        }
    }
    
    public void ConfigureKnockback(float speed, float duration)
    {
        knockbackSpeed = Mathf.Max(1f, speed);
        knockbackDuration = Mathf.Max(0.1f, duration);
    }
    
    public Vector3 GetCurrentKnockbackDirection()
    {
        return _isKnockbackActive ? _knockbackDirection : Vector3.zero;
    }

    public Vector3 GetKnockbackStartPosition()
    {
        return _knockbackStartPosition;
    }
    
    public Vector3 GetKnockbackTargetPosition()
    {
        return _knockbackTargetPosition;
    }

    public float GetKnockbackProgress()
    {
        return _isKnockbackActive ? (_knockbackTimer / knockbackDuration) : 0f;
    }
    
    public void SetCanBeKnockbacked(bool canBeKnockbacked)
    {
        this.canBeKnockbacked = canBeKnockbacked;
    }
    
    public void SetIsObject(bool isObject)
    {
        this.isObject = isObject;
    }
    
    /// <summary>🔧 新增：运行时配置碰撞参数</summary>
    public void ConfigureCollision(float radius, float skin, float maxStep)
    {
        collisionRadius = Mathf.Max(0.1f, radius);
        skinWidth = Mathf.Max(0.01f, skin);
        maxMoveStepPerFrame = Mathf.Max(0.05f, maxStep);
    }
    
    /// <summary>🔧 新增：手动触发碰撞事件</summary>
    public void TriggerManualCollision(GameObject target)
    {
        if (_stateDetector != null)
        {
            _stateDetector.ProcessCollisionImmediately(target);
        }
    }
    #endregion
}