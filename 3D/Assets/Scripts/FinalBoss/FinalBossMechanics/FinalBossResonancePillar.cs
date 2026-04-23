using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FinalBossResonancePillar : BaseEnemy
{
    [Header("Pillar")]
    [SerializeField] private FinalBossResonancePillarHealth pillarHealth;
    [SerializeField] private int maxHealth = 6;
    [SerializeField] private int playerAttackDamage = 1;
    [SerializeField] private int knockbackObjectDamage = 3;
    [SerializeField] private bool requireOppositeColorForPlayerDamage = true;
    [SerializeField] private bool requireOppositeColorForObjectDamage = false;

    private FinalBossStageTwoController owner;
    private bool destroyed;

    public event Action<FinalBossResonancePillar> OnPillarDestroyed;
    public bool IsDestroyed => destroyed;
    public bool CanAcceptHits => owner != null && owner.IsActive && !destroyed;
    public int CurrentHealth => pillarHealth != null ? pillarHealth.CurrentHearts : 0;

    // ══════════════════════════════════════════════════════════════
    #region Unity 生命周期
    // ══════════════════════════════════════════════════════════════

    protected override void Awake()
    {
        base.Awake();
        pillarHealth ??= GetComponent<FinalBossResonancePillarHealth>();
    }

    private void OnEnable()
    {
        ColorEventBus.OnColorInteraction -= HandleColorInteraction;
        ColorEventBus.OnColorInteraction += HandleColorInteraction;
    }

    private void OnDisable()
    {
        ColorEventBus.OnColorInteraction -= HandleColorInteraction;
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 初始化与重置
    // ══════════════════════════════════════════════════════════════

    public void Initialize(FinalBossStageTwoController stageTwo)
    {
        owner = stageTwo;

        if (pillarHealth == null)
            pillarHealth = GetComponent<FinalBossResonancePillarHealth>();

        if (pillarHealth == null)
            pillarHealth = gameObject.AddComponent<FinalBossResonancePillarHealth>();

        pillarHealth.Configure(maxHealth);
        LogResolvedColorState("Initialize");
    }

    public void ResetPillar()
    {
        destroyed = false;
        pillarHealth?.ResetHealth(maxHealth);
        gameObject.SetActive(true);
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 命中检测
    // ══════════════════════════════════════════════════════════════

    public bool TryRegisterObjectHit(GameObject sourceObject)
    {
        if (!CanAcceptHits || sourceObject == null)
            return false;

        ColorComponent sourceColor = sourceObject.GetComponent<ColorComponent>();
        if (requireOppositeColorForObjectDamage && !CanSourceDamageByOppositeColor(sourceColor, "ObjectCollision"))
            return false;

        return true;
    }

    private void HandleColorInteraction(ColorInteractionEvent interaction)
    {
        if (!CanAcceptHits)
            return;

        if (interaction.Source == null || interaction.Target == null)
            return;

        if (!IsInteractionTargetingThisPillar(interaction.Target))
            return;

        switch (interaction.Type)
        {
            case ColorInteractionType.PlayerAttackEnemy:
                if (requireOppositeColorForPlayerDamage &&
                    !CanSourceDamageByOppositeColor(interaction.Source.GetComponent<ColorComponent>(), "PlayerAttack"))
                {
                    Log($"PlayerAttack BLOCKED -> source={interaction.Source.name}");
                    break;
                }

                ApplyDamage(playerAttackDamage, interaction.Source, "PlayerAttackEnemyEvent");
                break;

            case ColorInteractionType.Collision:
                if (TryRegisterObjectHit(interaction.Source))
                    ApplyDamage(knockbackObjectDamage, interaction.Source, "CollisionEvent");
                break;
        }
    }

    /// <summary>
    /// 颜色交互事件响应（override BaseEnemy）。
    /// 柱子通过 ColorEventBus 订阅处理，此处仅做基类日志。
    /// </summary>
    public override void OnColorInteraction(ColorInteractionEvent interaction)
    {
        base.OnColorInteraction(interaction);
    }

    private bool IsInteractionTargetingThisPillar(GameObject target)
    {
        if (target == gameObject)
            return true;

        Transform targetTransform = target.transform;
        return targetTransform == transform || targetTransform.IsChildOf(transform);
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 颜色判定
    // ══════════════════════════════════════════════════════════════

    private bool CanSourceDamageByOppositeColor(ColorComponent sourceColor, string reason)
    {
        if (colorComponent == null)
        {
            Log($"{reason} BLOCKED -> pillar ColorComponent missing");
            return false;
        }

        if (sourceColor == null)
        {
            Log($"{reason} BLOCKED -> source ColorComponent missing");
            return false;
        }

        bool isOpposite = sourceColor.IsOppositeColor(colorComponent);
        Log($"{reason} ColorCheck -> source={sourceColor.CurrentColor}, pillar={colorComponent.CurrentColor}, isOpposite={isOpposite}");
        return isOpposite;
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 伤害与销毁
    // ══════════════════════════════════════════════════════════════

    private void ApplyDamage(int damage, GameObject source, string reason)
    {
        if (destroyed || pillarHealth == null)
            return;

        Log($"ApplyDamage -> damage={damage}, source={source.name}, reason={reason}");

        pillarHealth.TakeDamage(damage);

        if (pillarHealth.CurrentHearts <= 0)
        {
            destroyed = true;
            Log($"Pillar DESTROYED by {source.name} via {reason}");
            OnPillarDestroyed?.Invoke(this);
            gameObject.SetActive(false);
        }
    }

    #endregion


    // ══════════════════════════════════════════════════════════════
    #region 调试日志
    // ══════════════════════════════════════════════════════════════

    private void LogResolvedColorState(string context)
    {
        if (!showDebugLogs)
            return;

        string colorInfo = colorComponent != null
            ? $"color={colorComponent.CurrentColor}"
            : "colorComponent=NULL";

        Debug.Log($"[ResonancePillar] {context} -> {gameObject.name}, {colorInfo}");
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[ResonancePillar] {gameObject.name}: {message}");
    }

    #endregion
}
