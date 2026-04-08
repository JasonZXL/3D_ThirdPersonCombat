using UnityEngine;
using System.Collections.Generic;
using System;

public class EnemyStateCollisionDetector : MonoBehaviour
{
    #region 状态定义系统
    /// <summary>敌人状态枚举</summary>
    public enum EnemyState
    {
        None = 0,
        Knockback = 1,
        Frozen = 2,
        Electrified = 3,
        Burning = 4,
    }
    
    [Serializable]
    public class StateConfig
    {
        public EnemyState state;
        public Color debugColor = Color.white;
        public bool isActive = false;
        public float collisionRadius = 1.2f;
        public LayerMask detectionLayers;
    }
    
    [Header("状态配置")]
    [SerializeField] private List<StateConfig> stateConfigs = new List<StateConfig>();
    #endregion

    #region 碰撞效果配置
    [Serializable]
    public class CollisionEffect
    {
        public EnemyState state;
        public string targetTag;
        public bool requireOppositeColor = false;
        
        public float damageAmount = 0f;
        public bool instantKill = false;
        public GameObject spawnEffectPrefab;
        public string customEventName = "";
    }
    
    [Header("碰撞效果配置")]
    [SerializeField] private List<CollisionEffect> collisionEffects = new List<CollisionEffect>();
    
    [Header("击退状态特定配置")]
    [SerializeField] private float knockbackObjectDamage = 1f;
    [SerializeField] private LayerMask knockbackDetectionLayers = ~0; // 默认全层，若未配置则在Awake时设为常用层
    [SerializeField] private List<string> ignoredCollisionTags = new List<string> { "Ground", "Untagged" }; // 新增：忽略的标签列表
    #endregion

    #region 检测系统配置
    [Header("检测系统设置")]
    [SerializeField] private float baseCheckFrequency = 0.1f;
    [SerializeField] private bool showDebugVisuals = true;
    #endregion

    #region 调试设置
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion

    #region 内部状态变量
    private Dictionary<EnemyState, StateConfig> _stateConfigMap = new Dictionary<EnemyState, StateConfig>();
    private HashSet<EnemyState> _activeStates = new HashSet<EnemyState>();
    private float _checkTimer = 0f;
    private Dictionary<GameObject, float> _collisionCooldown = new Dictionary<GameObject, float>();
    
    private ColorComponent _colorComponent;
    private EnemyHealthSystem _enemyHealthSystem;
    
    public event Action<EnemyState, GameObject> OnStateCollision;
    public event Action<EnemyState> OnStateActivated;
    public event Action<EnemyState> OnStateDeactivated;
    #endregion

    #region Unity生命周期
    private void Awake()
    {
        InitializeComponentCache();
        InitializeStateConfigMap();
        InitializeDefaultConfigs();
        InitializeDefaultCollisionEffects();
    }
    
    private void Update()
    {
        if (_activeStates.Count == 0) return;
        
        _checkTimer += Time.deltaTime;
        if (_checkTimer >= baseCheckFrequency)
        {
            PerformStateCollisionDetection();
            _checkTimer = 0f;
        }
        
        UpdateCollisionCooldown();
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals || !Application.isPlaying) return;
        
        foreach (EnemyState state in _activeStates)
        {
            if (_stateConfigMap.TryGetValue(state, out StateConfig config))
            {
                Gizmos.color = config.debugColor;
                Gizmos.DrawWireSphere(transform.position, config.collisionRadius);
                
                Gizmos.color = new Color(config.debugColor.r, config.debugColor.g, config.debugColor.b, 0.2f);
                Gizmos.DrawSphere(transform.position, config.collisionRadius);
            }
        }
    }
    #endregion

    #region 初始化系统
    /// <summary>初始化组件缓存</summary>
    private void InitializeComponentCache()
    {
        _colorComponent = GetComponent<ColorComponent>();
        _enemyHealthSystem = GetComponent<EnemyHealthSystem>();
        
        if (_enemyHealthSystem == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"⚠️ {gameObject.name} 缺少EnemyHealthSystem组件");
        }
    }
    
    /// <summary>初始化状态配置映射</summary>
    private void InitializeStateConfigMap()
    {
        _stateConfigMap.Clear();
        foreach (StateConfig config in stateConfigs)
        {
            if (!_stateConfigMap.ContainsKey(config.state))
            {
                _stateConfigMap[config.state] = config;
            }
        }
    }
    
    /// <summary>初始化默认配置</summary>
    private void InitializeDefaultConfigs()
    {
        if (!_stateConfigMap.ContainsKey(EnemyState.Knockback))
        {
            StateConfig knockbackConfig = new StateConfig
            {
                state = EnemyState.Knockback,
                debugColor = Color.red,
                collisionRadius = 1.2f,
                // 默认包含敌人与障碍相关层，避免仅 Default 层才能触发
                detectionLayers = (knockbackDetectionLayers.value == 0)
                    ? LayerMask.GetMask("Enemy", "Obstacle", "Object", "Default", "Environment")
                    : knockbackDetectionLayers
            };
            stateConfigs.Add(knockbackConfig);
            _stateConfigMap[EnemyState.Knockback] = knockbackConfig;
        }
    }
    
    /// <summary>初始化默认碰撞效果</summary>
    private void InitializeDefaultCollisionEffects()
    {
        bool hasKnockbackEnemyEffect = false;
        bool hasKnockbackObjectEffect = false;
        
        foreach (CollisionEffect effect in collisionEffects)
        {
            if (effect.state == EnemyState.Knockback)
            {
                if (effect.targetTag == "Enemy") hasKnockbackEnemyEffect = true;
                if (effect.targetTag == "Object") hasKnockbackObjectEffect = true;
            }
        }
        
        if (!hasKnockbackEnemyEffect)
        {
            collisionEffects.Add(new CollisionEffect
            {
                state = EnemyState.Knockback,
                targetTag = "Enemy",
                requireOppositeColor = true,
                instantKill = true
            });
        }
        
        if (!hasKnockbackObjectEffect && knockbackObjectDamage > 0)
        {
            collisionEffects.Add(new CollisionEffect
            {
                state = EnemyState.Knockback,
                targetTag = "Object",
                damageAmount = knockbackObjectDamage
            });
        }
    }
    #endregion

    #region 状态管理系统
    /// <summary>激活状态</summary>
    public void ActivateState(EnemyState state)
    {
        if (!_stateConfigMap.ContainsKey(state))
        {
            if (showDebugLogs)
                Debug.LogWarning($"⚠️ 尝试激活未配置的状态: {state}");
            return;
        }
        
        if (!_activeStates.Contains(state))
        {
            _activeStates.Add(state);
            _stateConfigMap[state].isActive = true;
            
            if (showDebugLogs)
                Debug.Log($"✅ {gameObject.name} 激活状态: {state}");
                
            OnStateActivated?.Invoke(state);
        }
    }
    
    /// <summary>停用状态</summary>
    public void DeactivateState(EnemyState state)
    {
        if (_activeStates.Contains(state))
        {
            _activeStates.Remove(state);
            if (_stateConfigMap.ContainsKey(state))
            {
                _stateConfigMap[state].isActive = false;
            }
            
            if (showDebugLogs)
                Debug.Log($"❌ {gameObject.name} 结束状态: {state}");
                
            OnStateDeactivated?.Invoke(state);
        }
    }
    
    /// <summary>检查是否处于指定状态</summary>
    public bool IsInState(EnemyState state) => _activeStates.Contains(state);
    
    /// <summary>获取所有激活状态</summary>
    public List<EnemyState> GetActiveStates() => new List<EnemyState>(_activeStates);
    
    /// <summary>清除所有状态</summary>
    public void ClearAllStates()
    {
        List<EnemyState> statesToClear = new List<EnemyState>(_activeStates);
        foreach (EnemyState state in statesToClear)
        {
            DeactivateState(state);
        }
    }
    #endregion

    #region 碰撞检测系统
    /// <summary>执行状态碰撞检测</summary>
    private void PerformStateCollisionDetection()
    {
        // 修复：创建副本避免在遍历时修改集合
        List<EnemyState> statesToProcess = new List<EnemyState>(_activeStates);
        
        foreach (EnemyState state in statesToProcess)
        {
            // 再次检查状态是否仍然激活（可能在之前的状态处理中被停用）
            if (!_activeStates.Contains(state)) continue;
            
            if (_stateConfigMap.TryGetValue(state, out StateConfig config))
            {
                DetectCollisionsForState(state, config);
            }
        }
    }
    
    /// <summary>检测指定状态的碰撞</summary>
    private void DetectCollisionsForState(EnemyState state, StateConfig config)
    {
        Collider[] hitColliders = Physics.OverlapSphere(
            transform.position,
            config.collisionRadius,
            config.detectionLayers
        );
        
        foreach (Collider collider in hitColliders)
        {
            GameObject hitObject = collider.gameObject;
            
            if (!IsValidCollisionTarget(hitObject, state)) continue;
            
            ProcessStateCollision(state, hitObject);
        }
    }
    
    /// <summary>检查是否为有效的碰撞目标</summary>
    private bool IsValidCollisionTarget(GameObject target, EnemyState state)
    {
        // 排除自己
        if (target == gameObject) return false;
        
        // 击退状态只处理可造成效果的目标，避免玩家被误判为Object
        if (state == EnemyState.Knockback)
        {
            bool isObject = target.CompareTag("Object");
            bool isEnemy = target.CompareTag("Enemy");
            bool isEnvironment = target.CompareTag("Obstacle") || target.CompareTag("Environment");
            
            if (!(isObject || isEnemy || isEnvironment))
            {
                if (showDebugLogs)
                    Debug.Log($"🚫 击退忽略无关目标: {target.name} (Tag: {target.tag})");
                return false;
            }
        }
        
        // 检查是否在忽略标签列表中
        if (IsIgnoredTag(target.tag))
        {
            if (showDebugLogs && state == EnemyState.Knockback)
                Debug.Log($"🚫 忽略 {target.tag} 标签的物体: {target.name}");
            return false;
        }
        
        Collider collider = target.GetComponent<Collider>();
        if (collider != null && collider.isTrigger) return false;
        
        if (IsInCollisionCooldown(target, state)) return false;
        
        return true;
    }
    
    /// <summary>检查是否为被忽略的标签</summary>
    private bool IsIgnoredTag(string tag)
    {
        return ignoredCollisionTags.Contains(tag);
    }
    
    /// <summary>处理状态碰撞</summary>
    private void ProcessStateCollision(EnemyState state, GameObject target)
    {
        OnStateCollision?.Invoke(state, target);
        ApplyCollisionEffects(state, target);
        AddCollisionCooldown(target, state);
    }
    #endregion

    #region 碰撞效果应用系统
    /// <summary>应用碰撞效果</summary>
    private void ApplyCollisionEffects(EnemyState state, GameObject target)
    {
        // 对于击退状态，发布颜色交互事件
        if (state == EnemyState.Knockback)
        {
            HandleKnockbackCollision(target);
            return;
        }
        
        // 其他状态的通用处理
        string targetTag = target.tag;
        foreach (CollisionEffect effect in collisionEffects)
        {
            if (effect.state == state && effect.targetTag == targetTag)
            {
                if (effect.requireOppositeColor && !IsOppositeColor(target))
                {
                    continue;
                }
                
                ApplySingleCollisionEffect(effect, target, state);
                
                if (showDebugLogs)
                    Debug.Log($"💥 状态碰撞: {state} → {targetTag} ({target.name})");
            }
        }
    }
    
    /// <summary>处理击退碰撞</summary>
    private void HandleKnockbackCollision(GameObject target)
    {
        string targetTag = target.tag;
        
        // 检查是否为有效的Object标签
        bool isObject = targetTag == "Object";
        bool isEnemy = targetTag == "Enemy";
        
        if (showDebugLogs)
            Debug.Log($"🚀 击退碰撞检测: {gameObject.name} → {target.name} (Tag: {targetTag}, IsObject: {isObject}, IsEnemy: {isEnemy})");
        
        // 只要不是被忽略的标签，都视为有效碰撞，发布颜色交互事件
        ColorInteractionEvent collisionEvent = new ColorInteractionEvent(
            gameObject,
            target,
            ColorInteractionType.Collision,
            target.transform.position
        );
        
        ColorEventBus.PublishColorInteraction(collisionEvent);
        
        // 撞到任何有效物体后立即结束击退
        ForceStopKnockback();
    }
    
    /// <summary>应用单个碰撞效果</summary>
    private void ApplySingleCollisionEffect(CollisionEffect effect, GameObject target, EnemyState state)
    {
        if (effect.damageAmount > 0 || effect.instantKill)
        {
            ApplyDamageOrKill(effect, target, state);
        }
        
        if (effect.spawnEffectPrefab != null)
        {
            Instantiate(effect.spawnEffectPrefab, target.transform.position, Quaternion.identity);
        }
        
        if (!string.IsNullOrEmpty(effect.customEventName))
        {
            if (showDebugLogs)
                Debug.Log($"📢 触发自定义事件: {effect.customEventName}");
        }
    }
    
    /// <summary>应用伤害或秒杀效果</summary>
    private void ApplyDamageOrKill(CollisionEffect effect, GameObject target, EnemyState state)
    {
        if (target.CompareTag("Enemy"))
        {
            EnemyHealthSystem targetEnemyHealth = target.GetComponent<EnemyHealthSystem>();
            if (targetEnemyHealth != null)
            {
                if (effect.instantKill)
                {
                    targetEnemyHealth.TakeDamage(999, gameObject);
                }
                else if (effect.damageAmount > 0)
                {
                    targetEnemyHealth.TakeDamage((int)effect.damageAmount, gameObject);
                }
            }
        }
    }
    
    /// <summary>检查是否为相反颜色</summary>
    private bool IsOppositeColor(GameObject other)
    {
        if (_colorComponent == null) return false;
        
        ColorComponent otherColor = other.GetComponent<ColorComponent>();
        if (otherColor == null) return false;
        
        return _colorComponent.IsOppositeColor(otherColor);
    }
    #endregion

    #region 碰撞冷却系统
    /// <summary>检查是否在碰撞冷却中</summary>
    private bool IsInCollisionCooldown(GameObject target, EnemyState state)
    {
        if (_collisionCooldown.TryGetValue(target, out float cooldownEndTime))
        {
            return Time.time < cooldownEndTime;
        }
        return false;
    }
    
    /// <summary>添加碰撞冷却</summary>
    private void AddCollisionCooldown(GameObject target, EnemyState state)
    {
        _collisionCooldown[target] = Time.time + 0.5f;
    }
    
    /// <summary>更新碰撞冷却</summary>
    private void UpdateCollisionCooldown()
    {
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
    #endregion

    #region 快捷方法
    /// <summary>开始击退状态</summary>
    public void StartKnockback() => ActivateState(EnemyState.Knockback);
    
    /// <summary>停止击退状态</summary>
    public void StopKnockback() => DeactivateState(EnemyState.Knockback);
    
    /// <summary>检查击退是否激活</summary>
    public bool IsKnockbackActive() => IsInState(EnemyState.Knockback);
    
    /// <summary>强制停止击退</summary>
    public void ForceStopKnockback()
    {
        if (IsInState(EnemyState.Knockback))
        {
            DeactivateState(EnemyState.Knockback);
            
            KnockbackSystem knockbackSystem = GetComponent<KnockbackSystem>();
            if (knockbackSystem != null)
            {
                knockbackSystem.ForceStopKnockback();
            }
        }
    }
    
    /// <summary>获取击退撞物体的伤害值</summary>
    public float GetKnockbackObjectDamage() => knockbackObjectDamage;
    
    /// <summary>设置击退撞物体的伤害值</summary>
    public void SetKnockbackObjectDamage(float damage)
    {
        knockbackObjectDamage = Mathf.Max(0, damage);
    }
    #endregion
}