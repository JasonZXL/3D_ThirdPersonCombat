using UnityEngine;
using System.Collections.Generic;

public class PlayerAttackDetector : MonoBehaviour
{
    #region 攻击设置
    [Header("攻击设置")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private KeyCode attackKey = KeyCode.Mouse0; // 鼠标左键
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private LayerMask objectLayerMask; // 新增：物体层掩码
    [SerializeField] private float attackRadius = 0.5f;
    
    [Header("物体攻击设置")]
    [SerializeField] private bool canAttackObjects = true; // 是否可以攻击物体
    #endregion

    #region 调试设置
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion

    #region 可视化设置
    [Header("攻击范围可视化")]
    [SerializeField] private bool showRangeIndicator = true;
    [SerializeField] private Color rangeIndicatorColor = new Color(1f, 0.4f, 0.1f, 0.35f);
    [SerializeField] private int rangeIndicatorSegments = 36;
    private LineRenderer rangeIndicator;
    #endregion

    #region 内部变量
    private float lastAttackTime = 0f;
    private ColorComponent playerColor;
    private readonly HashSet<GameObject> hitEnemiesThisSwing = new HashSet<GameObject>();
    private readonly HashSet<GameObject> hitObjectsThisSwing = new HashSet<GameObject>();
    [Header("动画设置")]
    [SerializeField] private Animator playerAnimator;  // 玩家动画控制器
    [SerializeField] private string attackAnimationTrigger = "Attack";  // 触发器名称
    #endregion

    #region Unity生命周期
    private void Awake()
    {
        playerColor = GetComponent<ColorComponent>();
        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
            if (playerAnimator == null)
            {
                // 如果当前物体没有，尝试在子物体中查找
                playerAnimator = GetComponentInChildren<Animator>();
            }
        }
        
        // 初始化层掩码
        if (enemyLayerMask.value == 0)
        {
            enemyLayerMask = LayerMask.GetMask("Enemy");
        }
        
        if (objectLayerMask.value == 0)
        {
            objectLayerMask = LayerMask.GetMask("Object");
        }
        
        if (showDebugLogs)
            Debug.Log("🗡️ 玩家攻击检测器初始化");
        
        SetupRangeIndicator();
    }
    
    private void Update()
    {
        UpdateRangeIndicator();

        if (Input.GetKeyDown(attackKey) && Time.time >= lastAttackTime + attackCooldown)
        {
            TryAttack();
        }
    }
    #endregion

    #region 攻击逻辑
    /// <summary>
    /// 尝试攻击
    /// </summary>
    private void TryAttack()
    {
        if (showDebugLogs)
            Debug.Log("🗡️ 玩家尝试攻击");
        
        // 触发攻击动画
        if (playerAnimator != null && !string.IsNullOrEmpty(attackAnimationTrigger))
        {
            playerAnimator.SetTrigger(attackAnimationTrigger);
        }

        hitEnemiesThisSwing.Clear();
        hitObjectsThisSwing.Clear();
        
        // 检测敌人
        DetectAndAttackEnemies();
        
        // 检测物体
        if (canAttackObjects)
        {
            DetectAndAttackObjects();
        }
        
        lastAttackTime = Time.time;
    }
    
    /// <summary>
    /// 检测并攻击敌人
    /// </summary>
    private void DetectAndAttackEnemies()
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            attackRadius,
            transform.forward,
            attackRange,
            enemyLayerMask,
            QueryTriggerInteraction.Collide
        );
        
        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.CompareTag("Enemy")) continue;

            // 检查命中的是否是弱点
            BossWeakPoint weakPoint = hit.collider.GetComponent<BossWeakPoint>();
            if (weakPoint != null)
            {
                // 命中弱点：Target 设为弱点子物体本身
                if (!hitEnemiesThisSwing.Contains(hit.collider.gameObject))
                {
                    hitEnemiesThisSwing.Add(hit.collider.gameObject);
                    if (showDebugLogs)
                        Debug.Log($"🗡️ 玩家攻击弱点: {hit.collider.gameObject.name}");
                    ColorEventBus.PublishPlayerAttack(gameObject, hit.collider.gameObject); // ← Target = 弱点子物体
                }
            }
            else
            {
                // 命中普通敌人：Target 设为敌人根物体
                GameObject enemyRoot = GetEnemyRoot(hit.collider.gameObject);
                if (enemyRoot == null) continue;
                if (hitEnemiesThisSwing.Contains(enemyRoot)) continue;

                hitEnemiesThisSwing.Add(enemyRoot);
                AttackEnemy(enemyRoot);
            }
        }
    }
    
    /// <summary>
    /// 检测并攻击物体
    /// </summary>
    private void DetectAndAttackObjects()
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            attackRadius,
            transform.forward,
            attackRange,
            objectLayerMask,
            QueryTriggerInteraction.Ignore
        );
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Object"))
            {
                GameObject obj = hit.collider.gameObject;
                
                if (hitObjectsThisSwing.Contains(obj))
                {
                    continue;
                }

                hitObjectsThisSwing.Add(obj);
                AttackObject(obj);
            }
        }
    }
    
    /// <summary>
    /// 攻击敌人
    /// </summary>
    private void AttackEnemy(GameObject enemy)
    {
        if (showDebugLogs)
            Debug.Log($"🗡️ 玩家攻击敌人: {enemy.name}");
            
        ColorEventBus.PublishPlayerAttack(gameObject, enemy);
    }
    
    /// <summary>
    /// 攻击物体
    /// </summary>
    private void AttackObject(GameObject obj)
    {
        if (showDebugLogs)
            Debug.Log($"🗡️ 玩家攻击物体: {obj.name}");
            
        ColorEventBus.PublishPlayerAttackObject(gameObject, obj);
    }
    #endregion

    #region 调试工具
    /// <summary>
    /// 可视化攻击范围
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position + transform.forward * attackRange, attackRadius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * attackRange);
        
        // 物体攻击范围（不同颜色）
        if (canAttackObjects)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + transform.forward * (attackRange * 0.8f), attackRadius * 0.8f);
        }
    }
    #endregion

    #region 辅助方法
    private GameObject GetEnemyRoot(GameObject hitObject)
    {
        EnemyHealthSystem health = hitObject.GetComponentInParent<EnemyHealthSystem>();
        return health != null ? health.gameObject : hitObject;
    }

    private void SetupRangeIndicator()
    {
        if (!showRangeIndicator) return;

        rangeIndicator = gameObject.AddComponent<LineRenderer>();
        rangeIndicator.loop = true;
        rangeIndicator.useWorldSpace = true;
        rangeIndicator.widthMultiplier = 0.03f;
        rangeIndicator.material = new Material(Shader.Find("Sprites/Default"));
        rangeIndicator.startColor = rangeIndicatorColor;
        rangeIndicator.endColor = rangeIndicatorColor;
        rangeIndicator.positionCount = rangeIndicatorSegments;
    }

    private void UpdateRangeIndicator()
    {
        if (!showRangeIndicator || rangeIndicator == null) return;

        Vector3 center = transform.position + transform.forward * attackRange;
        float radius = attackRadius;
        float angleStep = 360f / rangeIndicatorSegments;

        for (int i = 0; i < rangeIndicatorSegments; i++)
        {
            float rad = Mathf.Deg2Rad * angleStep * i;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            rangeIndicator.SetPosition(i, center + offset);
        }
    }
    #endregion
    
    /// <summary>
    /// 设置是否可以攻击物体
    /// </summary>
    public void SetCanAttackObjects(bool canAttack)
    {
        canAttackObjects = canAttack;
    }
}