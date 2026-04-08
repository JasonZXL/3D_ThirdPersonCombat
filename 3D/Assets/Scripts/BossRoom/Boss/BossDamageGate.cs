using System;
using System.Reflection;
using UnityEngine;

public class BossDamageGate : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private BossController bossController;
    [SerializeField] private HealthSystem bossHealthSystem;
    [SerializeField] private GameObject weakPointObject;

    [Header("反射配置")]
    [Tooltip("血量字段的名称")]
    [SerializeField] private string customHealthFieldName = "currentHearts"; // 默认值已设为 currentHearts

    [Header("Boss二阶段规则")]
    [SerializeField] private bool bodyInvincibleInPhase2 = true;
    [SerializeField] private bool showDebugLogs = true;

    private int hpAtFrameStart = int.MaxValue;
    private bool weakPointHitThisFrame = false;

    // 反射缓存
    private FieldInfo healthField;
    private bool reflectInitialized = false;

    private void Awake()
    {
        if (bossController == null)
            bossController = GetComponent<BossController>();
        if (bossHealthSystem == null)
            bossHealthSystem = GetComponent<HealthSystem>();
        if (weakPointObject == null && showDebugLogs)
            Debug.LogWarning("⚠️ [BossDamageGate] weakPointObject 未设置，弱点命中无法标记！");
    }

    private void OnEnable()
    {
        ColorEventBus.OnColorInteraction += HandleColorInteraction;
    }

    private void OnDisable()
    {
        ColorEventBus.OnColorInteraction -= HandleColorInteraction;
    }

    private void Update()
    {
        if (!reflectInitialized && bossHealthSystem != null)
        {
            InitializeReflection();
            reflectInitialized = true;
        }

        hpAtFrameStart = ReadCurrentHealth();
        weakPointHitThisFrame = false;
    }

    private void LateUpdate()
    {
        if (!ShouldBeInvincible()) return;

        int hpNow = ReadCurrentHealth();
        if (hpNow == int.MaxValue || hpAtFrameStart == int.MaxValue) return;

        if (hpNow < hpAtFrameStart && !weakPointHitThisFrame)
        {
            if (showDebugLogs)
                Debug.LogWarning($"🛡️ [BossDamageGate] Phase2本体无敌：回滚血量 {hpNow} → {hpAtFrameStart}");

            WriteCurrentHealth(hpAtFrameStart);
        }
    }

    private bool ShouldBeInvincible()
    {
        if (!bodyInvincibleInPhase2) return false;
        if (bossController == null) return false;
        return bossController.IsPhase2; // 需要在 BossController 中添加该属性
    }

    private void HandleColorInteraction(ColorInteractionEvent interaction)
    {
        if (interaction.Source == null || interaction.Target == null) return;
        if (interaction.Type != ColorInteractionType.PlayerAttackEnemy) return;
        if (weakPointObject == null) return;

        if (interaction.Target == weakPointObject)
        {
            weakPointHitThisFrame = true;
            if (showDebugLogs)
                Debug.Log($"🎯 [BossDamageGate] 弱点命中，允许本帧伤害");
        }
    }

    private void InitializeReflection()
    {
        if (bossHealthSystem == null) return;
        Type t = bossHealthSystem.GetType();

        // 优先使用自定义字段名
        if (!string.IsNullOrEmpty(customHealthFieldName))
        {
            healthField = t.GetField(customHealthFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (healthField != null)
            {
                if (showDebugLogs)
                    Debug.Log($"[BossDamageGate] 使用自定义字段: {customHealthFieldName}");
                return;
            }
            else
            {
                Debug.LogWarning($"[BossDamageGate] 找不到自定义字段 {customHealthFieldName}，尝试自动探测...");
            }
        }

        // 自动探测常见字段名
        string[] commonFieldNames = { "currentHearts", "_currentHearts", "currentHealth", "_currentHealth", "health", "_health", "hp", "_hp" };
        foreach (string fieldName in commonFieldNames)
        {
            FieldInfo field = t.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int))
            {
                healthField = field;
                if (showDebugLogs)
                    Debug.Log($"[BossDamageGate] 自动探测到字段: {fieldName}");
                return;
            }
        }

        Debug.LogError("[BossDamageGate] 无法找到任何可读写的血量字段！请检查 HealthSystem 的实际成员名。");
    }

    private int ReadCurrentHealth()
    {
        if (bossHealthSystem == null) return int.MaxValue;
        if (healthField == null) return int.MaxValue;

        try
        {
            return (int)healthField.GetValue(bossHealthSystem);
        }
        catch
        {
            return int.MaxValue;
        }
    }

    private void WriteCurrentHealth(int value)
    {
        if (bossHealthSystem == null) return;
        if (healthField == null) return;

        try
        {
            healthField.SetValue(bossHealthSystem, value);
            if (showDebugLogs)
                Debug.Log($"[BossDamageGate] 通过字段 {healthField.Name} 设置血量成功");
        }
        catch (Exception e)
        {
            Debug.LogError($"[BossDamageGate] 设置字段失败: {e.Message}");
        }
    }
}