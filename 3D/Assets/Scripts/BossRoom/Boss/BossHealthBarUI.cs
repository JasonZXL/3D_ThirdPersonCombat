using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private GameObject root; // 整个血条UI根节点（可隐藏/显示）

    [Header("Boss引用")]
    [SerializeField] private HealthSystem bossHealthSystem;

    [Header("自动设置")]
    [Tooltip("如果为true，会自动用MaxHP初始化Slider.maxValue")]
    [SerializeField] private bool autoSetMaxFromBoss = true;

    [Header("行为设置")]
    [SerializeField] private bool showOnBind = true; // 绑定Boss时显示血条
    [SerializeField] private bool hideOnBossDeath = true; // Boss死亡时隐藏血条

    [Header("调试")]
    [SerializeField] private bool showDebugLogs = false;

    private int cachedMaxHp = -1;
    private bool isBound = false;

    public static BossHealthBarUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        if (root == null) root = gameObject;
        if (hpSlider == null) hpSlider = GetComponentInChildren<Slider>(true);
    }

    private void Start()
    {
        if (hpSlider == null)
        {
            Debug.LogError("❌ [BossHealthBarUI] 未找到 Slider 引用，请拖入 hpSlider");
            enabled = false;
            return;
        }

        // 初始化时隐藏血条（等待绑定Boss）
        if (root != null)
        {
            root.SetActive(false);
        }

        // 如果已经有预设的bossHealthSystem，自动绑定
        if (bossHealthSystem != null)
        {
            BindBoss(bossHealthSystem);
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("ℹ️ [BossHealthBarUI] 未检测到预分配的Boss，等待手动绑定");
        }
    }

    private void OnEnable()
    {
        // 如果已经绑定过Boss，重新启用时更新UI
        if (isBound && bossHealthSystem != null)
        {
            UpdateSlider(forceInitMax: true);
        }
    }

    private void Update()
    {
        // 只更新已绑定的Boss
        if (isBound && bossHealthSystem != null)
        {
            UpdateSlider(forceInitMax: false);
        }
    }

    public void BindBoss(HealthSystem boss)
    {
        // 解除之前的事件监听
        UnbindEvents();
        
        // 绑定新的Boss
        bossHealthSystem = boss;
        isBound = true;
        cachedMaxHp = -1;
        
        // 绑定事件
        BindEvents();
        
        // 更新UI
        UpdateSlider(forceInitMax: true);
        
        // 如果需要，显示血条
        if (showOnBind && root != null)
        {
            root.SetActive(true);
            if (showDebugLogs)
                Debug.Log("🔗 [BossHealthBarUI] 绑定Boss血量成功，显示血条");
        }

        if (showDebugLogs)
            Debug.Log($"🔗 [BossHealthBarUI] 绑定Boss: {boss.gameObject.name}");
    }

    public void UnbindBoss()
    {
        UnbindEvents();
        bossHealthSystem = null;
        isBound = false;
        
        // 隐藏血条
        if (root != null)
        {
            root.SetActive(false);
        }
        
        if (showDebugLogs)
            Debug.Log("🔗 [BossHealthBarUI] 解除Boss绑定，隐藏血条");
    }

    private void BindEvents()
    {
        if (bossHealthSystem != null)
        {
            // 监听血量变化事件
            bossHealthSystem.OnHeartsChanged += OnHeartsChanged;
            bossHealthSystem.OnDeath += OnBossDeath;
        }
    }

    private void UnbindEvents()
    {
        if (bossHealthSystem != null)
        {
            bossHealthSystem.OnHeartsChanged -= OnHeartsChanged;
            bossHealthSystem.OnDeath -= OnBossDeath;
        }
    }

    private void OnHeartsChanged(int current, int max)
    {
        // 血量变化时更新UI
        UpdateSlider(forceInitMax: false);
    }

    private void OnBossDeath()
    {
        if (showDebugLogs)
            Debug.Log("☠️ [BossHealthBarUI] Boss已死亡");
        
        // Boss死亡时隐藏血条
        if (hideOnBossDeath && root != null)
        {
            root.SetActive(false);
        }
    }

    private void UpdateSlider(bool forceInitMax)
    {
        if (bossHealthSystem == null) return;

        int current = ReadCurrentHpSafe(bossHealthSystem);
        int max = ReadMaxHpSafe(bossHealthSystem);

        if (current == int.MaxValue) return;

        // 自动设置最大血量
        if (autoSetMaxFromBoss && max != int.MaxValue)
        {
            if (forceInitMax || cachedMaxHp != max)
            {
                cachedMaxHp = max;
                hpSlider.maxValue = max;
            }
        }

        hpSlider.value = current;

        // 确保血条在血量大于0时显示
        if (root != null)
        {
            // 如果Boss血量大于0但血条隐藏了，显示它
            if (current > 0 && !root.activeSelf && isBound)
            {
                root.SetActive(true);
                if (showDebugLogs)
                    Debug.Log($"📊 [BossHealthBarUI] 血量不为0，显示血条");
            }
            
            // Boss死亡时隐藏血条
            if (hideOnBossDeath && current <= 0 && root.activeSelf)
            {
                if (showDebugLogs)
                    Debug.Log($"📊 [BossHealthBarUI] 血量为0，隐藏血条");
                root.SetActive(false);
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"📊 [BossHealthBarUI] 血量更新: {current}/{max}");
    }

    // -------- HealthSystem 兼容读取（反射） --------
    // 注意：保持原有的反射读取方法不变
    private int ReadCurrentHpSafe(HealthSystem hs)
    {
        Type t = hs.GetType();

        // 常见属性名
        string[] propNames = { "CurrentHearts", "currentHearts", "HP", "Hp" };
        foreach (var pn in propNames)
        {
            var p = t.GetProperty(pn, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(int))
            {
                try { return (int)p.GetValue(hs); } catch { }
            }
        }

        // 常见字段名
        string[] fieldNames = { "currentHearts", "CurrentHearts", "hp", "HP" };
        foreach (var fn in fieldNames)
        {
            var f = t.GetField(fn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(int))
            {
                try { return (int)f.GetValue(hs); } catch { }
            }
        }

        return int.MaxValue;
    }

    private int ReadMaxHpSafe(HealthSystem hs)
    {
        Type t = hs.GetType();

        // 常见属性名
        string[] propNames = { "MaxHearts", "maxHearts", "MaxHP", "maxHP" };
        foreach (var pn in propNames)
        {
            var p = t.GetProperty(pn, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(int))
            {
                try { return (int)p.GetValue(hs); } catch { }
            }
        }

        // 常见字段名
        string[] fieldNames = { "maxHearts", "MaxHearts", "maxHP", "MaxHP" };
        foreach (var fn in fieldNames)
        {
            var f = t.GetField(fn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(int))
            {
                try { return (int)f.GetValue(hs); } catch { }
            }
        }

        return int.MaxValue;
    }

    private void OnDestroy()
    {
        // 清理事件监听
        UnbindEvents();
    }
}

