using UnityEngine;

/// <summary>
/// 不使用Rigidbody的子弹控制器（无重力影响）
/// 提供多种移动方式：Transform移动、CharacterController移动
/// </summary>
public class ProjectileController : MonoBehaviour
{
    #region 子弹设置
    [Header("子弹设置")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float speed = 10f;
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private LayerMask hitLayers;
    
    [Header("移动方式")]
    [SerializeField] private MovementType movementType = MovementType.Transform;
    [SerializeField] private bool useCurve = false;
    [SerializeField] private AnimationCurve speedCurve;
    
    [Header("追踪设置")]
    [SerializeField] private bool useHoming = false;
    [SerializeField] private float homingStrength = 5f;
    [SerializeField] private float homingStartDistance = 3f;
    
    [Header("视觉效果")]
    [SerializeField] private bool rotateTowardsDirection = true;
    [SerializeField] private float rotationSpeed = 10f;
    #endregion

    #region 枚举定义
    public enum MovementType
    {
        Transform,
        CharacterController
    }
    #endregion

    #region 内部变量
    private Vector3 targetPosition;
    private Vector3 direction;
    private float spawnTime;
    private CharacterController charController;
    private GameObject owner;
    private bool isInitialized = false;
    private Vector3 startPosition;
    private float travelDistance;
    private float currentDistance = 0f;
    #endregion

    #region Unity生命周期
    private void Awake()
    {
        spawnTime = Time.time;
        
        // 根据移动类型获取组件
        if (movementType == MovementType.CharacterController)
        {
            charController = GetComponent<CharacterController>();
            if (charController == null)
            {
                charController = gameObject.AddComponent<CharacterController>();
                charController.radius = 0.1f;
                charController.height = 0.2f;
                charController.center = new Vector3(0, 0.1f, 0);
            }
            
            // 关键修复：禁用重力影响
            // CharacterController 默认会受到重力影响，需要手动移动来避免下坠
        }
        
        // 确保没有 Rigidbody（避免物理引擎影响）
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;  // 设为运动学，不受物理影响
            rb.useGravity = false;   // 禁用重力
        }
    }

    private void Start()
    {
        Invoke("DestroyProjectile", lifeTime);
    }

    private void Update()
    {
        if (!isInitialized) return;
        
        UpdateProjectile();
        
        // 更新存活时间检查
        if (Time.time - spawnTime > lifeTime)
        {
            DestroyProjectile();
        }
    }
    #endregion

    #region 子弹功能
    public void InitializeProjectile(Vector3 targetPos, float projectileSpeed, GameObject projectileOwner)
    {
        targetPosition = targetPos;
        speed = projectileSpeed;
        owner = projectileOwner;
        startPosition = transform.position;
        
        // 计算初始方向（水平方向，忽略Y轴差异）
        Vector3 directionToTarget = targetPosition - startPosition;
        directionToTarget.y = 0;  // 关键修复：保持水平飞行
        direction = directionToTarget.normalized;
        
        travelDistance = Vector3.Distance(startPosition, targetPosition);
        
        isInitialized = true;
        
    }

    private void UpdateProjectile()
    {
        // 更新追踪逻辑
        if (useHoming && owner != null)
        {
            UpdateHoming();
        }
        
        // 计算当前速度
        float currentSpeed = speed;
        if (useCurve && speedCurve != null)
        {
            float progress = Mathf.Clamp01(currentDistance / travelDistance);
            currentSpeed = speed * speedCurve.Evaluate(progress);
        }
        
        // 移动子弹
        MoveProjectile(currentSpeed);
        
        // 旋转子弹面向移动方向
        if (rotateTowardsDirection && direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        
        // 更新移动距离
        currentDistance += currentSpeed * Time.deltaTime;
    }

    private void MoveProjectile(float moveSpeed)
    {
        // 关键修复：只在水平方向移动，保持当前高度
        Vector3 movement = direction * moveSpeed * Time.deltaTime;
        movement.y = 0;  // 确保没有垂直移动
        
        switch (movementType)
        {
            case MovementType.Transform:
                // 使用Transform直接移动（无重力影响）
                transform.position += movement;
                break;
                
            case MovementType.CharacterController:
                // 使用CharacterController移动
                if (charController != null && charController.enabled)
                {
                    // 关键修复：CharacterController.Move 不会自动应用重力
                    // 我们只移动水平方向
                    charController.Move(movement);
                }
                else
                {
                    // 回退到Transform移动
                    transform.position += movement;
                }
                break;
        }
    }

    private void UpdateHoming()
    {
        // 检查是否在追踪范围内
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        
        if (distanceToTarget < homingStartDistance && owner != null)
        {
            // 重新计算方向并添加追踪（保持水平）
            Vector3 newDirection = (targetPosition - transform.position);
            newDirection.y = 0;  // 保持水平追踪
            newDirection = newDirection.normalized;
            
            direction = Vector3.Slerp(direction, newDirection, homingStrength * Time.deltaTime);
            direction.y = 0;  // 再次确保水平
            direction = direction.normalized;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (movementType == MovementType.CharacterController)
        {
            HandleCollision(hit.gameObject);
        }
    }

    private void HandleCollision(GameObject hitObject)
    {
        // 忽略发射者
        if (hitObject == owner) return;
        
        // 检查是否应该命中的层
        if (((1 << hitObject.layer) & hitLayers) == 0)
            return;

        // 对玩家造成伤害
        if (hitObject.CompareTag("Player"))
        {
            ApplyDamage(hitObject);
        }
        else if (hitObject.CompareTag("Enemy") && owner != null && !owner.CompareTag("Enemy"))
        {
            // 如果子弹不是敌人发射的，可以对敌人造成伤害
            ApplyDamage(hitObject);
        }

        // 生成命中特效
        if (hitEffect != null)
        {
            Instantiate(hitEffect, transform.position, Quaternion.identity);
        }

        // 销毁子弹
        DestroyProjectile();
    }

    private void ApplyDamage(GameObject target)
    {
        Debug.Log($"💥 子弹命中: {target.name}, 伤害: {damage}");
        
        // 发送伤害事件
        ColorInteractionEvent damageEvent = new ColorInteractionEvent(
            owner != null ? owner : this.gameObject,
            target,
            ColorInteractionType.EnemyAttackPlayer,
            target.transform.position
        );
        
        // 通过颜色事件系统发送伤害
        ColorEventBus.PublishColorInteraction(damageEvent);
    }

    private void DestroyProjectile()
    {
        if (this == null || gameObject == null) return;
        
        // 销毁前生成特效
        if (hitEffect != null && currentDistance < travelDistance * 0.9f)
        {
            Instantiate(hitEffect, transform.position, Quaternion.identity);
        }
        
        Destroy(gameObject);
    }
    #endregion

    #region 公共接口
    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0.1f, newSpeed);
    }

    public void SetDamage(float newDamage)
    {
        damage = Mathf.Max(0, newDamage);
    }

    public void SetHomingEnabled(bool enabled)
    {
        useHoming = enabled;
    }

    public void ChangeMovementType(MovementType newType)
    {
        movementType = newType;
        
        if (movementType == MovementType.CharacterController && charController == null)
        {
            charController = gameObject.AddComponent<CharacterController>();
        }
    }
    #endregion

    #region 调试可视化
    private void OnDrawGizmosSelected()
    {
        if (!isInitialized) return;
        
        // 绘制移动方向
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, direction * 2f);
        
        // 绘制目标位置
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetPosition, 0.5f);
        
        // 绘制追踪范围
        if (useHoming)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPosition, homingStartDistance);
        }
        
        // 显示子弹信息
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.5f,
            $"速度: {speed:F1}\n" +
            $"距离: {currentDistance:F1}/{travelDistance:F1}\n" +
            $"追踪: {(useHoming ? "启用" : "禁用")}\n" +
            $"高度: {transform.position.y:F2}"
        );
        #endif
    }
    #endregion
}
