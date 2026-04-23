using System;
using System.Reflection;
using UnityEngine;

public class FinalBossDamageGate : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FinalBossBattleDirector battleDirector;
    [SerializeField] private HealthSystem bossHealthSystem;

    [Header("Reflection")]
    [SerializeField] private string customHealthFieldName = "currentHearts";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private FieldInfo healthField;
    private MethodInfo dieMethod;
    private bool reflectionInitialized;
    private int hpAtFrameStart = int.MaxValue;
    private bool qteFinisherCommitted;

    private void Awake()
    {
        battleDirector ??= GetComponentInParent<FinalBossBattleDirector>();
        bossHealthSystem ??= GetComponent<HealthSystem>();
    }

    private void Update()
    {
        if (!reflectionInitialized && bossHealthSystem != null)
        {
            InitializeReflection();
            reflectionInitialized = true;
        }

        hpAtFrameStart = ReadCurrentHealth();
    }

    private void LateUpdate()
    {
        if (battleDirector == null || bossHealthSystem == null)
            return;

        if (qteFinisherCommitted)
            return;

        int hpNow = ReadCurrentHealth();
        if (hpNow == int.MaxValue || hpAtFrameStart == int.MaxValue)
            return;

        switch (battleDirector.CurrentState)
        {
            case FinalBossBattleDirector.BattleState.Stage2:
                RollbackIfDamaged(hpNow, hpAtFrameStart, "Stage2 body invincible");
                break;

            case FinalBossBattleDirector.BattleState.Stage3:
                RollbackIfDamaged(hpNow, hpAtFrameStart, "Stage3 accepts QTE finisher only");
                break;
        }
    }

    public void ApplyQTEFinisherKill()
    {
        if (qteFinisherCommitted || bossHealthSystem == null)
            return;

        qteFinisherCommitted = true;

        int currentHp = ReadCurrentHealth();
        if (currentHp == int.MaxValue)
        {
            LogError("Cannot apply QTE finisher: health field unresolved");
            return;
        }

        if (currentHp > 0)
            WriteCurrentHealth(0);

        if (dieMethod != null)
        {
            dieMethod.Invoke(bossHealthSystem, null);
            Log("ApplyQTEFinisherKill -> invoked Die()");
            return;
        }

        LogWarning("ApplyQTEFinisherKill -> Die() not found, boss HP forced to 0 only");
    }

    private void RollbackIfDamaged(int hpNow, int hpAtStart, string reason)
    {
        if (hpNow >= hpAtStart)
            return;

        WriteCurrentHealth(hpAtStart);
        Log($"Rollback damage -> {reason} ({hpNow} -> {hpAtStart})");
    }

    private void InitializeReflection()
    {
        Type type = bossHealthSystem.GetType();

        if (!string.IsNullOrWhiteSpace(customHealthFieldName))
        {
            healthField = type.GetField(customHealthFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        if (healthField == null)
        {
            string[] candidateNames = { "currentHearts", "_currentHearts", "currentHealth", "_currentHealth", "health", "_health", "hp", "_hp" };

            foreach (string fieldName in candidateNames)
            {
                FieldInfo candidate = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (candidate != null && candidate.FieldType == typeof(int))
                {
                    healthField = candidate;
                    break;
                }
            }
        }

        dieMethod = type.GetMethod("Die", BindingFlags.Instance | BindingFlags.NonPublic);

        if (healthField == null)
            LogError("Failed to resolve boss health field");
        else
            Log($"Reflection ready -> healthField={healthField.Name}, dieMethod={(dieMethod != null ? "OK" : "NULL")}");
    }

    private int ReadCurrentHealth()
    {
        if (bossHealthSystem == null || healthField == null)
            return int.MaxValue;

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
        if (bossHealthSystem == null || healthField == null)
            return;

        try
        {
            healthField.SetValue(bossHealthSystem, value);
        }
        catch (Exception ex)
        {
            LogError($"WriteCurrentHealth failed: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossDamageGate] {message}");
    }

    private void LogWarning(string message)
    {
        if (showDebugLogs)
            Debug.LogWarning($"[FinalBossDamageGate] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[FinalBossDamageGate] {message}");
    }
}
