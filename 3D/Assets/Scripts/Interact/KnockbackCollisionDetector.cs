using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>🔧 优化：支持多种碰撞形状的检测器</summary>
public class KnockbackCollisionDetector : MonoBehaviour
{
    #region 碰撞形状选择
    [Header("🔧 碰撞形状配置")]
    [Tooltip("选择碰撞检测的形状类型")]
    [SerializeField] private CollisionShapeType shapeType = CollisionShapeType.AutoDetect;
    
    public enum CollisionShapeType
    {
        AutoDetect,      // 自动检测（根据Collider类型）
        Sphere,          // 球体（适合圆形敌人）
        Box,             // 盒子（适合长方形物体）
        Capsule,         // 胶囊体（适合人形角色）
        CustomBounds     // 自定义边界（完全手动配置）
    }
    #endregion
    
    #region 球体配置
    [Header("球体配置 (Shape Type = Sphere)")]
    [SerializeField] private float sphereRadius = 1.2f;
    [SerializeField] private Vector3 sphereCenterOffset = Vector3.zero;
    #endregion
    
    #region 盒子配置
    [Header("盒子配置 (Shape Type = Box)")]
    [Tooltip("盒子的尺寸 (X=宽, Y=高, Z=长)")]
    [SerializeField] private Vector3 boxSize = new Vector3(1f, 2f, 1f);
    [Tooltip("盒子中心相对于物体的偏移")]
    [SerializeField] private Vector3 boxCenterOffset = Vector3.zero;
    [Tooltip("盒子的旋转（欧拉角）")]
    [SerializeField] private Vector3 boxRotation = Vector3.zero;
    #endregion
    
    #region 胶囊体配置
    [Header("胶囊体配置 (Shape Type = Capsule)")]
    [Tooltip("胶囊体半径")]
    [SerializeField] private float capsuleRadius = 0.5f;
    [Tooltip("胶囊体高度")]
    [SerializeField] private float capsuleHeight = 2f;
    [Tooltip("胶囊体中心偏移")]
    [SerializeField] private Vector3 capsuleCenterOffset = Vector3.zero;
    [Tooltip("胶囊体方向")]
    [SerializeField] private CapsuleDirection capsuleDirection = CapsuleDirection.Y;
    
    public enum CapsuleDirection
    {
        X = 0,  // 横向
        Y = 1,  // 纵向（默认）
        Z = 2   // 前后
    }
    #endregion
    
    #region 自定义边界配置
    [Header("自定义边界配置 (Shape Type = CustomBounds)")]
    [Tooltip("自定义边界的最小点（本地坐标）")]
    [SerializeField] private Vector3 customBoundsMin = new Vector3(-0.5f, 0f, -0.5f);
    [Tooltip("自定义边界的最大点（本地坐标）")]
    [SerializeField] private Vector3 customBoundsMax = new Vector3(0.5f, 2f, 0.5f);
    #endregion
    
    #region 调试设置
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showDebugVisuals = true;
    #endregion

    #region 碰撞检测配置
    [Header("碰撞检测配置")]
    [SerializeField] private float checkFrequency = 0.1f;
    [SerializeField] private LayerMask detectionLayers = ~0;
    [SerializeField] private List<string> targetTags = new List<string> { "Enemy", "Object", "Obstacle" };
    [SerializeField] private List<string> ignoredTags = new List<string> { "Ground", "Untagged", "Player" };
    #endregion
    
    #region 碰撞冷却设置
    [Header("碰撞冷却")]
    [SerializeField] private bool avoidDuplicateCollisions = true;
    [SerializeField] private float collisionCooldown = 0.2f;
    #endregion

    #region 编辑器可视化设置
    [Header("编辑器可视化")]
    [SerializeField] private bool alwaysShowInEditor = true;
    [SerializeField] private bool showWhenSelected = true;
    [SerializeField] private Color editorIdleColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);
    [SerializeField] private Color editorActiveColor = new Color(1f, 0.5f, 0f, 0.3f);
    [SerializeField] private Color wireframeColor = new Color(1f, 0.5f, 0f, 0.8f);
    #endregion

    #region 击退状态控制
    [Header("击退状态碰撞检测")]
    [SerializeField] private bool enableInitialOverlapProtection = true;
    [SerializeField] private float initialOverlapProtectionDuration = 0.3f;
    [SerializeField] private float initialOverlapSizeMultiplier = 0.1f;

    private Vector3 _originalSize;  // 存储原始尺寸（根据形状类型）
    private float _overlapProtectionTimer = 0f;
    private bool _isInOverlapProtection = false;
    #endregion

    #region 内部状态变量
    private bool _isKnockbackActive = false;
    private float _checkTimer = 0f;
    private Dictionary<GameObject, float> _collisionCooldown = new Dictionary<GameObject, float>();
    
    private ColorComponent _colorComponent;
    private KnockbackSystem _knockbackSystem;
    private Collider _mainCollider;
    
    public event Action<GameObject> OnKnockbackCollision;
    #endregion

    #region Unity生命周期
    private void Awake()
    {
        InitializeComponentCache();
        AutoDetectCollisionShape();
        StoreOriginalSize();
        
        if (showDebugLogs)
            Debug.Log($"🔄 KnockbackCollisionDetector 初始化: {gameObject.name}, 形状类型: {shapeType}");
    }
    
    private void Update()
    {
        if (!_isKnockbackActive) return;
        
        _checkTimer += Time.deltaTime;
        if (_checkTimer >= checkFrequency)
        {
            PerformKnockbackCollisionDetection();
            _checkTimer = 0f;
        }
        
        UpdateCollisionCooldown();
        UpdateOverlapProtection();
    }
    #endregion

    #region 初始化系统
    private void InitializeComponentCache()
    {
        _colorComponent = GetComponent<ColorComponent>();
        _knockbackSystem = GetComponent<KnockbackSystem>();
        _mainCollider = GetComponent<Collider>();
        
        if (_mainCollider == null)
        {
            _mainCollider = GetComponentInChildren<Collider>();
        }
        
        if (_knockbackSystem == null && showDebugLogs)
            Debug.LogWarning($"⚠️ {gameObject.name} 缺少KnockbackSystem组件");
    }
    
    /// <summary>🔧 自动检测碰撞形状</summary>
    private void AutoDetectCollisionShape()
    {
        if (shapeType != CollisionShapeType.AutoDetect) return;
        
        if (_mainCollider == null)
        {
            shapeType = CollisionShapeType.Sphere; // 默认
            if (showDebugLogs)
                Debug.LogWarning($"⚠️ {gameObject.name} 未找到Collider，使用默认球体形状");
            return;
        }
        
        // 根据Collider类型自动选择
        if (_mainCollider is SphereCollider)
        {
            SphereCollider sphere = _mainCollider as SphereCollider;
            shapeType = CollisionShapeType.Sphere;
            sphereRadius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            sphereCenterOffset = sphere.center;
            
            if (showDebugLogs)
                Debug.Log($"✅ 自动检测: 球体 (半径={sphereRadius:F2})");
        }
        else if (_mainCollider is BoxCollider)
        {
            BoxCollider box = _mainCollider as BoxCollider;
            shapeType = CollisionShapeType.Box;
            boxSize = Vector3.Scale(box.size, transform.lossyScale);
            boxCenterOffset = box.center;
            
            if (showDebugLogs)
                Debug.Log($"✅ 自动检测: 盒子 (尺寸={boxSize})");
        }
        else if (_mainCollider is CapsuleCollider)
        {
            CapsuleCollider capsule = _mainCollider as CapsuleCollider;
            shapeType = CollisionShapeType.Capsule;
            capsuleRadius = capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            capsuleHeight = capsule.height * transform.lossyScale.y;
            capsuleCenterOffset = capsule.center;
            capsuleDirection = (CapsuleDirection)capsule.direction;
            
            if (showDebugLogs)
                Debug.Log($"✅ 自动检测: 胶囊体 (半径={capsuleRadius:F2}, 高度={capsuleHeight:F2})");
        }
        else
        {
            // 使用Bounds作为盒子
            shapeType = CollisionShapeType.Box;
            Bounds bounds = _mainCollider.bounds;
            boxSize = bounds.size;
            boxCenterOffset = transform.InverseTransformPoint(bounds.center);
            
            if (showDebugLogs)
                Debug.Log($"✅ 自动检测: 使用Bounds作为盒子 (尺寸={boxSize})");
        }
    }
    
    /// <summary>存储原始尺寸</summary>
    private void StoreOriginalSize()
    {
        switch (shapeType)
        {
            case CollisionShapeType.Sphere:
                _originalSize = new Vector3(sphereRadius, sphereRadius, sphereRadius);
                break;
            case CollisionShapeType.Box:
                _originalSize = boxSize;
                break;
            case CollisionShapeType.Capsule:
                _originalSize = new Vector3(capsuleRadius, capsuleHeight, capsuleRadius);
                break;
            case CollisionShapeType.CustomBounds:
                _originalSize = customBoundsMax - customBoundsMin;
                break;
        }
    }
    #endregion

    #region 编辑器可视化
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals || !alwaysShowInEditor) return;
        
        Gizmos.color = editorIdleColor;
        DrawCollisionShape(false);
        
        Gizmos.color = wireframeColor;
        DrawCollisionShape(true);
        
        // 重叠保护可视化
        if (Application.isPlaying && _isInOverlapProtection)
        {
            Gizmos.color = Color.yellow;
            DrawCollisionShape(true, initialOverlapSizeMultiplier);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugVisuals || !showWhenSelected) return;
        
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        DrawCollisionShape(false);
        
        Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
        DrawCollisionShape(true);
        
        // 显示保护区域
        if (enableInitialOverlapProtection)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            DrawCollisionShape(false, initialOverlapSizeMultiplier);
            
            Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
            DrawCollisionShape(true, initialOverlapSizeMultiplier);
        }
        
        #if UNITY_EDITOR
        Vector3 labelPos = GetCollisionCenter() + Vector3.up * GetMaxDimension() * 1.2f;
        UnityEditor.Handles.Label(labelPos, GetShapeInfoString());
        #endif
    }
    
    /// <summary>绘制碰撞形状</summary>
    private void DrawCollisionShape(bool wireframe, float sizeMultiplier = 1f)
    {
        Vector3 center = GetCollisionCenter();
        
        switch (shapeType)
        {
            case CollisionShapeType.Sphere:
                float radius = sphereRadius * sizeMultiplier;
                if (wireframe)
                    Gizmos.DrawWireSphere(center, radius);
                else
                    Gizmos.DrawSphere(center, radius);
                break;
                
            case CollisionShapeType.Box:
                Vector3 size = boxSize * sizeMultiplier;
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(center, Quaternion.Euler(boxRotation) * transform.rotation, size);
                Gizmos.matrix = rotationMatrix;
                if (wireframe)
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                else
                    Gizmos.DrawCube(Vector3.zero, Vector3.one);
                Gizmos.matrix = Matrix4x4.identity;
                break;
                
            case CollisionShapeType.Capsule:
                DrawCapsuleGizmo(center, capsuleRadius * sizeMultiplier, capsuleHeight * sizeMultiplier, wireframe);
                break;
                
            case CollisionShapeType.CustomBounds:
                Vector3 min = customBoundsMin * sizeMultiplier;
                Vector3 max = customBoundsMax * sizeMultiplier;
                Vector3 boundsSize = max - min;
                Vector3 boundsCenter = transform.TransformPoint((min + max) * 0.5f);
                if (wireframe)
                    Gizmos.DrawWireCube(boundsCenter, boundsSize);
                else
                    Gizmos.DrawCube(boundsCenter, boundsSize);
                break;
        }
    }
    
    /// <summary>绘制胶囊体Gizmo</summary>
    private void DrawCapsuleGizmo(Vector3 center, float radius, float height, bool wireframe)
    {
        Vector3 direction = Vector3.up;
        if (capsuleDirection == CapsuleDirection.X) direction = Vector3.right;
        else if (capsuleDirection == CapsuleDirection.Z) direction = Vector3.forward;
        
        direction = transform.TransformDirection(direction);
        
        float halfHeight = height * 0.5f;
        Vector3 top = center + direction * (halfHeight - radius);
        Vector3 bottom = center - direction * (halfHeight - radius);
        
        if (wireframe)
        {
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);
        }
        else
        {
            Gizmos.DrawSphere(top, radius);
            Gizmos.DrawSphere(bottom, radius);
        }
    }
    
    /// <summary>获取形状信息字符串</summary>
    private string GetShapeInfoString()
    {
        switch (shapeType)
        {
            case CollisionShapeType.Sphere:
                return $"球体\n半径: {sphereRadius:F2}m\n偏移: {sphereCenterOffset}";
            case CollisionShapeType.Box:
                return $"盒子\n尺寸: {boxSize}\n偏移: {boxCenterOffset}\n旋转: {boxRotation}";
            case CollisionShapeType.Capsule:
                return $"胶囊体\n半径: {capsuleRadius:F2}m\n高度: {capsuleHeight:F2}m\n方向: {capsuleDirection}";
            case CollisionShapeType.CustomBounds:
                return $"自定义边界\nMin: {customBoundsMin}\nMax: {customBoundsMax}";
            default:
                return "未知形状";
        }
    }
    
    /// <summary>获取最大尺寸（用于标签定位）</summary>
    private float GetMaxDimension()
    {
        switch (shapeType)
        {
            case CollisionShapeType.Sphere:
                return sphereRadius * 2f;
            case CollisionShapeType.Box:
                return Mathf.Max(boxSize.x, boxSize.y, boxSize.z);
            case CollisionShapeType.Capsule:
                return Mathf.Max(capsuleRadius * 2f, capsuleHeight);
            case CollisionShapeType.CustomBounds:
                Vector3 size = customBoundsMax - customBoundsMin;
                return Mathf.Max(size.x, size.y, size.z);
            default:
                return 1f;
        }
    }
    #endregion

    #region 初始重叠保护系统
    private void StartOverlapProtection()
    {
        _isInOverlapProtection = true;
        _overlapProtectionTimer = initialOverlapProtectionDuration;
        
        // 根据形状类型缩小尺寸
        switch (shapeType)
        {
            case CollisionShapeType.Sphere:
                sphereRadius = Mathf.Max(0.05f, _originalSize.x * initialOverlapSizeMultiplier);
                break;
            case CollisionShapeType.Box:
                boxSize = Vector3.Max(Vector3.one * 0.05f, _originalSize * initialOverlapSizeMultiplier);
                break;
            case CollisionShapeType.Capsule:
                capsuleRadius = Mathf.Max(0.05f, _originalSize.x * initialOverlapSizeMultiplier);
                capsuleHeight = Mathf.Max(0.1f, _originalSize.y * initialOverlapSizeMultiplier);
                break;
            case CollisionShapeType.CustomBounds:
                Vector3 center = (customBoundsMin + customBoundsMax) * 0.5f;
                Vector3 size = (customBoundsMax - customBoundsMin) * initialOverlapSizeMultiplier;
                customBoundsMin = center - size * 0.5f;
                customBoundsMax = center + size * 0.5f;
                break;
        }
        
        if (showDebugLogs)
            Debug.Log($"🛡️ {gameObject.name} 进入初始重叠保护: {shapeType}");
    }

    private void EndOverlapProtection()
    {
        _isInOverlapProtection = false;
        _overlapProtectionTimer = 0f;
        
        // 恢复原始尺寸
        switch (shapeType)
        {
            case CollisionShapeType.Sphere:
                sphereRadius = _originalSize.x;
                break;
            case CollisionShapeType.Box:
                boxSize = _originalSize;
                break;
            case CollisionShapeType.Capsule:
                capsuleRadius = _originalSize.x;
                capsuleHeight = _originalSize.y;
                break;
            case CollisionShapeType.CustomBounds:
                Vector3 center = (customBoundsMin + customBoundsMax) * 0.5f;
                customBoundsMin = center - _originalSize * 0.5f;
                customBoundsMax = center + _originalSize * 0.5f;
                break;
        }
        
        if (showDebugLogs)
            Debug.Log($"🛡️ {gameObject.name} 退出初始重叠保护");
    }

    private void UpdateOverlapProtection()
    {
        if (!_isInOverlapProtection) return;
        
        _overlapProtectionTimer -= Time.deltaTime;
        
        if (_overlapProtectionTimer <= 0f)
        {
            EndOverlapProtection();
        }
    }
    #endregion

    #region 碰撞检测系统
    /// <summary>🔧 核心：根据形状类型执行碰撞检测</summary>
    private void PerformKnockbackCollisionDetection()
    {
        if (showDebugLogs)
        {
            Debug.Log($"🕵️🕵️🕵️ KnockbackCollisionDetector检测: {gameObject.name} 开始检测，形状类型: {shapeType}");
        }

        Collider[] hitColliders = GetOverlappingColliders();

        if (showDebugLogs)
        {
            Debug.Log($"🕵️ 检测到 {hitColliders.Length} 个碰撞体");
        }
        
        foreach (Collider collider in hitColliders)
        {
            GameObject hitObject = collider.gameObject;
            
            if (!IsValidCollisionTarget(hitObject)) continue;
            
            ProcessKnockbackCollision(hitObject);
        }
    }
    
    /// <summary>🔧 根据形状获取重叠的碰撞体</summary>
    private Collider[] GetOverlappingColliders()
    {
        Vector3 center = GetCollisionCenter();
        
        switch (shapeType)
        {
            case CollisionShapeType.Sphere:
                return Physics.OverlapSphere(center, sphereRadius, detectionLayers);
                
            case CollisionShapeType.Box:
                Quaternion rotation = Quaternion.Euler(boxRotation) * transform.rotation;
                return Physics.OverlapBox(center, boxSize * 0.5f, rotation, detectionLayers);
                
            case CollisionShapeType.Capsule:
                Vector3 direction = GetCapsuleDirection();
                float halfHeight = (capsuleHeight * 0.5f) - capsuleRadius;
                Vector3 point1 = center + direction * halfHeight;
                Vector3 point2 = center - direction * halfHeight;
                return Physics.OverlapCapsule(point1, point2, capsuleRadius, detectionLayers);
                
            case CollisionShapeType.CustomBounds:
                Vector3 boundsCenter = transform.TransformPoint((customBoundsMin + customBoundsMax) * 0.5f);
                Vector3 boundsSize = customBoundsMax - customBoundsMin;
                return Physics.OverlapBox(boundsCenter, boundsSize * 0.5f, transform.rotation, detectionLayers);
                
            default:
                return new Collider[0];
        }
    }
    
    /// <summary>获取碰撞中心点</summary>
    public Vector3 GetCollisionCenter()
    {
        switch (shapeType)
        {
            case CollisionShapeType.Sphere:
                return transform.position + transform.TransformDirection(sphereCenterOffset);
            case CollisionShapeType.Box:
                return transform.position + transform.TransformDirection(boxCenterOffset);
            case CollisionShapeType.Capsule:
                return transform.position + transform.TransformDirection(capsuleCenterOffset);
            case CollisionShapeType.CustomBounds:
                return transform.TransformPoint((customBoundsMin + customBoundsMax) * 0.5f);
            default:
                return transform.position;
        }
    }
    
    /// <summary>获取胶囊体方向向量</summary>
    private Vector3 GetCapsuleDirection()
    {
        Vector3 localDir = Vector3.up;
        if (capsuleDirection == CapsuleDirection.X) localDir = Vector3.right;
        else if (capsuleDirection == CapsuleDirection.Z) localDir = Vector3.forward;
        return transform.TransformDirection(localDir);
    }
    
    private bool IsValidCollisionTarget(GameObject target)
    {
        if (target == gameObject) return false;
        
        string targetTag = target.tag;
        bool hasValidTag = false;
        
        foreach (string validTag in targetTags)
        {
            if (targetTag == validTag)
            {
                hasValidTag = true;
                break;
            }
        }
        
        if (!hasValidTag) return false;
        
        foreach (string ignoredTag in ignoredTags)
        {
            if (targetTag == ignoredTag) return false;
        }
        
        Collider collider = target.GetComponent<Collider>();
        if (collider != null && collider.isTrigger) return false;
        
        if (avoidDuplicateCollisions && IsInCollisionCooldown(target)) return false;
        
        return true;
        
    }
    
    private void ProcessKnockbackCollision(GameObject target)
    {
        if (showDebugLogs)
            Debug.Log($"🕵️💥 ProcessKnockbackCollision被调用: {gameObject.name} -> {target.name}");
        
        // 检查目标是否为Boss（通过是否有 BossCollisionHandler 组件）
        BossCollisionHandler bossHandler = target.GetComponent<BossCollisionHandler>();
        if (bossHandler != null)
        {
            // 直接由Boss处理，不发布通用事件
            bossHandler.HandleObjectCollision(gameObject);
            AddCollisionCooldown(target); // 避免频繁触发
            if (showDebugLogs)
                Debug.Log($"🕵️✅ Boss特殊碰撞处理完成，跳过通用事件");
            return;
        }
        OnKnockbackCollision?.Invoke(target);
        
        PublishKnockbackCollisionEvent(target);
        
        AddCollisionCooldown(target);
        if (showDebugLogs)
            Debug.Log($"🕵️✅ ProcessKnockbackCollision完成: {gameObject.name} -> {target.name}");
    }
    
    private void PublishKnockbackCollisionEvent(GameObject target)
    {
        if (showDebugLogs)
            Debug.Log($"📢📢📢 PublishKnockbackCollisionEvent开始: {gameObject.name}({gameObject.tag}) -> {target.name}({target.tag})");
        // 检查颜色组件是否存在
        ColorComponent sourceColorComp = GetComponent<ColorComponent>();
        ColorComponent targetColorComp = target.GetComponent<ColorComponent>();
    
        if (showDebugLogs)
        {
            if (sourceColorComp != null)
                Debug.Log($"📢 源颜色组件: {sourceColorComp.CurrentColor}");
            else
                Debug.Log($"📢❌ 源颜色组件: 不存在!");

            if (targetColorComp != null)
                Debug.Log($"📢 目标颜色组件: {targetColorComp.CurrentColor}");
            else
                Debug.Log($"📢❌ 目标颜色组件: 不存在!");
        }

        ColorInteractionEvent collisionEvent = new ColorInteractionEvent(
            gameObject,
            target,
            ColorInteractionType.Collision,
            target.transform.position
        );
        if (showDebugLogs)
            Debug.Log($"📢 创建事件: 源={collisionEvent.Source?.name}, 目标={collisionEvent.Target?.name}, 类型={collisionEvent.Type}");
        
        ColorEventBus.PublishColorInteraction(collisionEvent);
        if (showDebugLogs)
            Debug.Log($"📢✅ PublishKnockbackCollisionEvent完成");
    }
    
    private string GetObjectType()
    {
        if (gameObject.CompareTag("Enemy")) return "Enemy";
        if (gameObject.CompareTag("Object")) return "Object";
        return "Unknown";
    }
    #endregion

    #region 碰撞冷却系统
    private bool IsInCollisionCooldown(GameObject target)
    {
        if (!avoidDuplicateCollisions) return false;
        
        if (_collisionCooldown.TryGetValue(target, out float cooldownEndTime))
        {
            return Time.time < cooldownEndTime;
        }
        return false;
    }
    
    private void AddCollisionCooldown(GameObject target)
    {
        if (!avoidDuplicateCollisions) return;
        
        _collisionCooldown[target] = Time.time + collisionCooldown;
    }
    
    private void UpdateCollisionCooldown()
    {
        if (!avoidDuplicateCollisions) return;
        
        List<GameObject> toRemove = new List<GameObject>();
        
        foreach (var kvp in _collisionCooldown)
        {
            if (Time.time >= kvp.Value)
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (GameObject key in toRemove)
        {
            _collisionCooldown.Remove(key);
        }
    }
    
    public void ClearCollisionCooldown()
    {
        _collisionCooldown.Clear();
    }
    #endregion

    #region 公共接口
    public void StartKnockback()
    {
        if (_isKnockbackActive) return;
        
        _isKnockbackActive = true;
        ClearCollisionCooldown();

        if (enableInitialOverlapProtection)
        {
            StartOverlapProtection();
        }
        
        if (showDebugLogs)
            Debug.Log($"✅ {gameObject.name} 开始击退碰撞检测 ({shapeType})" + 
                (enableInitialOverlapProtection ? $" (重叠保护启用: {initialOverlapProtectionDuration}秒)" : ""));
    }
    
    public void StopKnockback()
    {
        if (!_isKnockbackActive) return;
        
        _isKnockbackActive = false;
        ClearCollisionCooldown();
        
        if (_isInOverlapProtection)
        {
            EndOverlapProtection();
        }
        
        if (showDebugLogs)
            Debug.Log($"❌ {gameObject.name} 停止击退碰撞检测");
    }
    
    public bool IsKnockbackActive() => _isKnockbackActive;
    
    /// <summary>🔧 新增：立即处理指定的碰撞目标（用于KnockbackSystem检测到碰撞时立即触发）</summary>
    public void ProcessCollisionImmediately(GameObject target)
    {
        if (!_isKnockbackActive)
        {
            if (showDebugLogs)
                Debug.LogWarning($"⚠️ {gameObject.name} 不在击退状态，无法处理碰撞");
            return;
        }
        
        if (target == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"⚠️ 碰撞目标为空");
            return;
        }
        
        if (!IsValidCollisionTarget(target))
        {
            if (showDebugLogs)
                Debug.Log($"⏭️ {gameObject.name} 跳过无效碰撞目标: {target.name}");
            if (showDebugLogs)
                Debug.Log($"[DEBUG] target={target.name}, tag={target.tag}, targetTags=[{string.Join(",", targetTags)}], ignored=[{string.Join(",", ignoredTags)}]");
            return;
        }
        
        if (showDebugLogs)
            Debug.Log($"🚨 立即处理碰撞: {gameObject.name} -> {target.name}");
        
        ProcessKnockbackCollision(target);
    }
    
    public void ForceStopKnockback()
    {
        if (_knockbackSystem != null)
        {
            _knockbackSystem.ForceStopKnockback();
        }
        
        StopKnockback();
    }
    
    public ColorComponent GetColorComponent() => _colorComponent;
    public KnockbackSystem GetKnockbackSystem() => _knockbackSystem;
    
    /// <summary>🔧 设置碰撞形状类型</summary>
    public void SetShapeType(CollisionShapeType type)
    {
        shapeType = type;
        if (type == CollisionShapeType.AutoDetect)
        {
            AutoDetectCollisionShape();
        }
        StoreOriginalSize();
    }
    
    /// <summary>配置球体</summary>
    public void ConfigureSphere(float radius, Vector3 centerOffset)
    {
        shapeType = CollisionShapeType.Sphere;
        sphereRadius = Mathf.Max(0.1f, radius);
        sphereCenterOffset = centerOffset;
        StoreOriginalSize();
    }
    
    /// <summary>配置盒子</summary>
    public void ConfigureBox(Vector3 size, Vector3 centerOffset, Vector3 rotation)
    {
        shapeType = CollisionShapeType.Box;
        boxSize = Vector3.Max(Vector3.one * 0.1f, size);
        boxCenterOffset = centerOffset;
        boxRotation = rotation;
        StoreOriginalSize();
    }
    
    /// <summary>配置胶囊体</summary>
    public void ConfigureCapsule(float radius, float height, Vector3 centerOffset, CapsuleDirection direction)
    {
        shapeType = CollisionShapeType.Capsule;
        capsuleRadius = Mathf.Max(0.1f, radius);
        capsuleHeight = Mathf.Max(0.2f, height);
        capsuleCenterOffset = centerOffset;
        capsuleDirection = direction;
        StoreOriginalSize();
    }
    
    /// <summary>配置自定义边界</summary>
    public void ConfigureCustomBounds(Vector3 min, Vector3 max)
    {
        shapeType = CollisionShapeType.CustomBounds;
        customBoundsMin = min;
        customBoundsMax = max;
        StoreOriginalSize();
    }
    
    public void SetCheckFrequency(float frequency)
    {
        checkFrequency = Mathf.Max(0.01f, frequency);
    }
    
    public void AddTargetTag(string tag)
    {
        if (!targetTags.Contains(tag))
        {
            targetTags.Add(tag);
        }
    }
    
    public void RemoveTargetTag(string tag)
    {
        targetTags.Remove(tag);
    }
    
    public void AddIgnoredTag(string tag)
    {
        if (!ignoredTags.Contains(tag))
        {
            ignoredTags.Add(tag);
        }
    }
    
    public void RemoveIgnoredTag(string tag)
    {
        ignoredTags.Remove(tag);
    }
    
    public void SetDetectionLayers(LayerMask layers)
    {
        detectionLayers = layers;
    }
    
    public List<string> GetTargetTags() => new List<string>(targetTags);
    public List<string> GetIgnoredTags() => new List<string>(ignoredTags);
    
    /// <summary>获取当前形状类型</summary>
    public CollisionShapeType GetShapeType() => shapeType;
    
    /// <summary>获取当前形状的边界框（用于调试）</summary>
    public Bounds GetShapeBounds()
    {
        Vector3 center = GetCollisionCenter();
        Vector3 size = Vector3.one;
        
        switch (shapeType)
        {
            case CollisionShapeType.Sphere:
                size = Vector3.one * sphereRadius * 2f;
                break;
            case CollisionShapeType.Box:
                size = boxSize;
                break;
            case CollisionShapeType.Capsule:
                size = new Vector3(capsuleRadius * 2f, capsuleHeight, capsuleRadius * 2f);
                break;
            case CollisionShapeType.CustomBounds:
                size = customBoundsMax - customBoundsMin;
                break;
        }
        
        return new Bounds(center, size);
    }
    #endregion
}