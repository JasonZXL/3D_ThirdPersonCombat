using UnityEngine;
using System.Collections;
using UnityEngine.XR;

public class ColorInteractionManager : MonoBehaviour
{
    #region Singleton Implementation
    /// <summary>Singleton instance</summary>
    public static ColorInteractionManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeKnockbackCollisionConfig();
            if (showDebugLogs) Debug.Log("🎮 ColorInteractionManager singleton created");
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region Interaction Settings
    [Header("Interaction Settings")]
    [SerializeField] private float repulseForce = 10f;
    [SerializeField] private float cooldownReduction = 0.5f;
    
    [Header("Knockback Collision Settings")]
    [SerializeField] private float knockbackEnemyKillDamage = 999f;
    [SerializeField] private float knockbackObjectSelfDamage = 1f;
    [SerializeField] private float knockbackObstacleDamage = 2f;
    [SerializeField] private GameObject knockbackCollisionEffect;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;
    
    [Header("Billiard Physics Settings")]
    [SerializeField] private bool enableBilliardPhysics = true;
    [SerializeField] private bool restrictToHorizontal = true;
    [SerializeField] private float energyTransferMultiplier = 1.0f;
    #endregion

    #region Event Subscription
    private void OnEnable()
    {
        ColorEventBus.OnColorInteraction += HandleColorInteraction;
        if (showDebugLogs) Debug.Log("ColorInteractionManager registered for color events");
    }
    
    private void OnDisable()
    {
        ColorEventBus.OnColorInteraction -= HandleColorInteraction;
        if (showDebugLogs) Debug.Log("ColorInteractionManager unregistered from color events");
    }
    #endregion

    #region Event Handling
    private void HandleColorInteraction(ColorInteractionEvent interaction)
    {
        if (showDebugLogs) Debug.Log($"Received color event: {interaction.Source?.name}({interaction.SourceColor}) -> {interaction.Target?.name}({interaction.TargetColor})");
        
        switch (interaction.Type)
        {
            case ColorInteractionType.EnemyAttackPlayer:
            case ColorInteractionType.PlayerAttackEnemy:
                HandleAttackInteraction(interaction);
                break;
            case ColorInteractionType.PlayerAttackObject:
                HandlePlayerAttackObject(interaction);
                break;
            case ColorInteractionType.Collision:
                HandleCollisionInteraction(interaction);
                break;
            case ColorInteractionType.Ability:
                HandleAbilityInteraction(interaction);
                break;
        }
    }
    
    private void HandleCollisionInteraction(ColorInteractionEvent interaction)
    {
        if (showDebugLogs) Debug.Log($"HandleCollisionInteraction called: {interaction.Source?.name} -> {interaction.Target?.name}, type: {interaction.Type}");

        bool isSourceEnemy = interaction.Source.CompareTag("Enemy");
        bool isSourceObject = interaction.Source.CompareTag("Object");
        bool isTargetEnemy = interaction.Target.CompareTag("Enemy");
        bool isTargetObject = interaction.Target.CompareTag("Object");
        bool isTargetObstacle = interaction.Target.CompareTag("Obstacle");
    
        KnockbackSystem sourceKnockback = interaction.Source.GetComponent<KnockbackSystem>();
        bool isSourceKnockingBack = sourceKnockback != null && sourceKnockback.IsKnockbackActive;

        if (!isSourceKnockingBack)
        {
            if (showDebugLogs) Debug.Log($"Skipping collision: {interaction.Source?.name} not in knockback state");
            return;
        }

        if (isTargetObstacle)
        {
            HandleCollisionWithObstacle(interaction, isSourceEnemy, isSourceObject);
            return;
        }

        if ((!isSourceEnemy && !isSourceObject) || (!isTargetEnemy && !isTargetObject))
        {
            if (showDebugLogs) Debug.Log($"Skipping collision: not enemy/object collision");
            return;
        }

        ColorComponent sourceColorComp = interaction.Source.GetComponent<ColorComponent>();
        ColorComponent targetColorComp = interaction.Target.GetComponent<ColorComponent>();
        
        if (sourceColorComp == null || targetColorComp == null)
        {
            if (showDebugLogs) Debug.LogWarning("Collision interaction failed: missing color component");
            return;
        }
        
        if (isSourceObject && isTargetEnemy)
        {
            HandleObjectToEnemyCollision(interaction, sourceColorComp, targetColorComp);
        }
        else if (isSourceEnemy && isTargetObject)
        {
            HandleEnemyToObjectCollision(interaction, sourceColorComp, targetColorComp);
        }
        else if (isSourceEnemy && isTargetEnemy)
        {
            HandleEnemyToEnemyCollision(interaction, sourceColorComp, targetColorComp);
        }
        else if (isSourceObject && isTargetObject)
        {
            HandleObjectToObjectCollision(interaction, sourceColorComp, targetColorComp);
        }
    }
    #endregion

    #region Attack Interaction Handling
    private void HandleAttackInteraction(ColorInteractionEvent interaction)
    {
        ColorComponent source = interaction.Source?.GetComponent<ColorComponent>();
        ColorComponent target = interaction.Target?.GetComponent<ColorComponent>();
        
        if (source == null || target == null) return;
        
        switch (interaction.Type)
        {
            case ColorInteractionType.EnemyAttackPlayer:
                HandleEnemyAttackPlayer(interaction, source, target);
                break;
            case ColorInteractionType.PlayerAttackEnemy:
                HandlePlayerAttackEnemy(interaction, source, target);
                break;
        }
    }

    private void HandlePlayerAttackObject(ColorInteractionEvent interaction)
    {
        ColorComponent sourceColorComp = interaction.Source?.GetComponent<ColorComponent>();
        ColorComponent targetColorComp = interaction.Target?.GetComponent<ColorComponent>();

        if (sourceColorComp.IsOppositeColor(targetColorComp))
        {
            ApplyRepulseEffectToObject(interaction.Target, interaction.Source);
            
            // Spawn effect at the object's position
            if (knockbackCollisionEffect != null)
            {
                Instantiate(knockbackCollisionEffect, GetEffectPosition(interaction.Target), Quaternion.identity);
            }
        }
    }

    private void HandleEnemyAttackPlayer(ColorInteractionEvent interaction, ColorComponent enemy, ColorComponent player)
    {
        if (enemy.IsSameColor(player))
        {
            HandleSameColorEnemyAttack(interaction, enemy, player);
        }
        else if (enemy.IsOppositeColor(player))
        {
            HandleOppositeColorEnemyAttack(interaction, enemy, player);
        }
    }

    private void HandlePlayerAttackEnemy(ColorInteractionEvent interaction, ColorComponent player, ColorComponent enemy)
    {
        if (player.IsSameColor(enemy))
        {
            HandleSameColorPlayerAttack(interaction, player, enemy);
        }
        else if (player.IsOppositeColor(enemy))
        {
            HandleOppositeColorPlayerAttack(interaction, player, enemy);
        }
    }

    private void ApplyRepulseEffectToObject(GameObject obj, GameObject player)
    {
        KnockbackSystem objectKnockback = obj.GetComponent<KnockbackSystem>();
        bool canKnockbackObject = objectKnockback != null && objectKnockback.CanBeKnockbacked;

        if (!canKnockbackObject)
        {
            if (showDebugLogs) Debug.LogWarning($"Object {obj.name} cannot be knocked back: missing KnockbackSystem or not knockbackable");
            return;
        }

        Vector3 targetPosition = CameraDirectionHelper.GetKnockbackTargetPosition(player, obj, repulseForce);

        if (CameraDirectionHelper.IsValidKnockbackPosition(player.transform.position, obj.transform.position, targetPosition))
        {
            if (objectKnockback != null)
            {
                objectKnockback.ApplyKnockbackToPosition(targetPosition);
            }
            else
            {
                if (showDebugLogs) Debug.LogWarning($"Object {obj.name} cannot be knocked back: missing KnockbackSystem or not knockbackable");
            }
            if (showDebugLogs) Debug.Log($"Object {obj.name} knocked back to: {targetPosition}");
            
            KnockbackCollisionDetector collisionDetector = obj.GetComponent<KnockbackCollisionDetector>();
            if (collisionDetector != null)
            {
                collisionDetector.StartKnockback();
                if (showDebugLogs) Debug.Log($"Started collision detector for object {obj.name}");
            }
            else
            {
                if (showDebugLogs) Debug.LogWarning($"Object {obj.name} missing KnockbackCollisionDetector component");
            }
        }
    }
    #endregion

    #region Specific Attack Handling
    private void HandleSameColorEnemyAttack(ColorInteractionEvent interaction, ColorComponent enemy, ColorComponent player)
    {
        if (showDebugLogs) Debug.Log("Same color enemy attacks player!");
        
        BaseEnemy baseEnemy = interaction.Source.GetComponent<BaseEnemy>();
        if (baseEnemy != null)
        {
            baseEnemy.OnColorInteraction(interaction);
        }
        
        PlayerColorController playerController = interaction.Target.GetComponent<PlayerColorController>();
        if (playerController != null)
        {
            playerController.ReduceCooldown(cooldownReduction);
        }
    }

    private void HandleOppositeColorEnemyAttack(ColorInteractionEvent interaction, ColorComponent enemy, ColorComponent player)
    {
        if (showDebugLogs) Debug.Log("Opposite color enemy attacks player!");
        
        HealthSystem playerHealth = interaction.Target.GetComponent<HealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(1);
            if (knockbackCollisionEffect != null)
            {
                Instantiate(knockbackCollisionEffect, GetEffectPosition(interaction.Target), Quaternion.identity);
            }
        }
    }

    private void HandleSameColorPlayerAttack(ColorInteractionEvent interaction, ColorComponent player, ColorComponent enemy)
    {
        if (showDebugLogs) Debug.Log("Player attacks same color enemy!");
        
        BaseEnemy baseEnemy = interaction.Target.GetComponent<BaseEnemy>();
        if (baseEnemy != null)
        {
            baseEnemy.OnColorInteraction(interaction);
        }
    }

    private void HandleOppositeColorPlayerAttack(ColorInteractionEvent interaction, ColorComponent player, ColorComponent enemy)
    {
        if (showDebugLogs) Debug.Log("Player attacks opposite color enemy!");
        
        ApplyRepulseEffectToEnemy(interaction.Target, interaction.Source);
        
        HealthSystem enemyHealth = interaction.Target.GetComponent<HealthSystem>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(1);
        }
        
        if (knockbackCollisionEffect != null)
        {
            Instantiate(knockbackCollisionEffect, GetEffectPosition(interaction.Target), Quaternion.identity);
        }
    }
    #endregion

    #region Obstacle Collision Handling
    private void HandleCollisionWithObstacle(ColorInteractionEvent interaction, bool isSourceEnemy, bool isSourceObject)
    {
        if (showDebugLogs) Debug.Log($"{interaction.Source.name} collided with obstacle {interaction.Target.name}");
        
        KnockbackSystem sourceKnockback = interaction.Source.GetComponent<KnockbackSystem>();
        KnockbackCollisionDetector sourceDetector = interaction.Source.GetComponent<KnockbackCollisionDetector>();

        if (sourceKnockback == null)
        {
            if (showDebugLogs) Debug.LogWarning($"{interaction.Source.name} missing KnockbackSystem, cannot handle obstacle collision");
            return;
        }

        if (isSourceEnemy)
        {
            if (showDebugLogs) Debug.Log($"Enemy {interaction.Source.name} hit obstacle, taking damage");
            EnemyHealthSystem enemyHealth = interaction.Source.GetComponent<EnemyHealthSystem>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage((int)knockbackObstacleDamage, interaction.Target);
            }
            
            sourceKnockback.ForceStopKnockback();
            if (sourceDetector != null) sourceDetector.StopKnockback();
        }
        else if (isSourceObject && enableBilliardPhysics)
        {
            if (showDebugLogs) Debug.Log($"Object {interaction.Source.name} hit obstacle {interaction.Target.name} -> billiard physics bounce");
            ApplyBilliardPhysicsToObstacle(interaction.Source, interaction.Target);
        }
        else if (isSourceObject)
        {
            if (showDebugLogs) Debug.Log($"Object {interaction.Source.name} hit obstacle -> stopping knockback");
            sourceKnockback.ForceStopKnockback();
            if (sourceDetector != null) sourceDetector.StopKnockback();
        }
    }

    private void ApplyBilliardPhysicsToObstacle(GameObject obj, GameObject obstacle)
    {
        if (showDebugLogs) Debug.Log($"Entering ApplyBilliardPhysicsToObstacle: object={obj.name}, obstacle={obstacle.name}");

        KnockbackSystem objectKnockback = obj.GetComponent<KnockbackSystem>();
        if (objectKnockback == null)
        {
            if (showDebugLogs) Debug.LogWarning($"Object {obj.name} missing KnockbackSystem component");
            return;
        }

        Vector3 objectKnockbackDirection = objectKnockback.GetCurrentKnockbackDirection();
        Vector3 objectPosition = obj.transform.position;
        Vector3 obstaclePosition = obstacle.transform.position;
        
        if (objectKnockbackDirection == Vector3.zero)
        {
            Vector3 startPos = objectKnockback.GetKnockbackStartPosition();
            Vector3 currentPos = obj.transform.position;
            Vector3 dir = currentPos - startPos;
            dir.y = 0;
            objectKnockbackDirection = dir.normalized;
            
            if (objectKnockbackDirection == Vector3.zero)
            {
                objectKnockbackDirection = (objectPosition - obstaclePosition).normalized;
                objectKnockbackDirection.y = 0;
                objectKnockbackDirection.Normalize();
            }
            if (showDebugLogs) Debug.Log($"Knockback direction recalculated: {objectKnockbackDirection}");
        }
        
        float objectOriginalY = objectPosition.y;
        
        Collider obstacleCollider = obstacle.GetComponent<Collider>();
        Vector3 impactNormal;
        
        if (obstacleCollider != null)
        {
            Vector3 closestPointOnObstacle = obstacleCollider.ClosestPoint(objectPosition);
            impactNormal = (objectPosition - closestPointOnObstacle).normalized;
        }
        else
        {
            impactNormal = (objectPosition - obstaclePosition).normalized;
        }
        
        Vector3 impactNormalFlat = new Vector3(impactNormal.x, 0, impactNormal.z).normalized;
        
        if (impactNormalFlat == Vector3.zero)
        {
            impactNormalFlat = -objectKnockbackDirection;
            impactNormalFlat.y = 0;
            impactNormalFlat.Normalize();
        }
        
        if (showDebugLogs) Debug.Log($"Obstacle collision normal (horizontal): {impactNormalFlat}");
        
        Vector3 objectStartFlat = new Vector3(objectKnockback.GetKnockbackStartPosition().x, 0, objectKnockback.GetKnockbackStartPosition().z);
        Vector3 objectTargetFlat = new Vector3(objectKnockback.GetKnockbackTargetPosition().x, 0, objectKnockback.GetKnockbackTargetPosition().z);
        float objectTotalDistance = Vector3.Distance(objectStartFlat, objectTargetFlat);
        
        Vector3 objectCurrentFlat = new Vector3(objectPosition.x, 0, objectPosition.z);
        float objectTraveledDistance = Vector3.Distance(objectStartFlat, objectCurrentFlat);
        float objectRemainingDistance = objectTotalDistance - objectTraveledDistance;
        
        float energyLossFactor = 0.8f;
        float objectBounceDistance = objectRemainingDistance * energyLossFactor;
        
        if (showDebugLogs) Debug.Log($"Object movement: total={objectTotalDistance:F2}, traveled={objectTraveledDistance:F2}, remaining={objectRemainingDistance:F2}, bounce={objectBounceDistance:F2}");
        
        objectKnockback.ForceStopKnockback();
        
        if (objectBounceDistance > 0.1f)
        {
            Vector3 objectNewDirection = BilliardPhysicsHelper.CalculateReflectionHorizontal(objectKnockbackDirection, impactNormalFlat);
            objectNewDirection = new Vector3(objectNewDirection.x, 0, objectNewDirection.z).normalized;
            if (objectNewDirection == Vector3.zero) objectNewDirection = -impactNormalFlat;
            
            Vector3 objectNewTarget = BilliardPhysicsHelper.CalculateHorizontalTargetPosition(objectPosition, objectNewDirection, objectBounceDistance, objectOriginalY);
            
            if (showDebugLogs) Debug.Log($"Object bounce direction: {objectNewDirection}, distance={objectBounceDistance:F2}");
            
            objectKnockback.ApplyKnockbackToPosition(objectNewTarget);
            
            KnockbackCollisionDetector objectDetector = obj.GetComponent<KnockbackCollisionDetector>();
            if (objectDetector != null)
            {
                objectDetector.StartKnockback();
                if (showDebugLogs) Debug.Log($"Restarted collision detector for object {obj.name}");
            }
        }
        else
        {
            if (showDebugLogs) Debug.Log($"Object energy exhausted, stopping");
            KnockbackCollisionDetector objectDetector = obj.GetComponent<KnockbackCollisionDetector>();
            if (objectDetector != null) objectDetector.StopKnockback();
        }
    }

    private void ApplyBilliardPhysicsToEnemy(GameObject enemy, GameObject obj, ColorInteractionEvent interaction)
    {
        if (showDebugLogs) Debug.Log($"Entering ApplyBilliardPhysicsToEnemy: object={obj.name}, enemy={enemy.name}");
    
        KnockbackSystem enemyKnockback = enemy.GetComponent<KnockbackSystem>();
        KnockbackSystem objectKnockback = obj.GetComponent<KnockbackSystem>();

        if (enemyKnockback == null || objectKnockback == null)
        {
            if (showDebugLogs) Debug.LogWarning($"Missing KnockbackSystem: enemyKnockback={enemyKnockback != null}, objectKnockback={objectKnockback != null}");
            return;
        }

        Vector3 objectKnockbackDirection = objectKnockback.GetCurrentKnockbackDirection();
        Vector3 objectPosition = obj.transform.position;
        Vector3 enemyPosition = enemy.transform.position;
        
        if (objectKnockbackDirection == Vector3.zero)
        {
            Vector3 startPos = objectKnockback.GetKnockbackStartPosition();
            Vector3 currentPos = obj.transform.position;
            Vector3 dir = currentPos - startPos;
            dir.y = 0;
            objectKnockbackDirection = dir.normalized;
            
            if (objectKnockbackDirection == Vector3.zero)
            {
                objectKnockbackDirection = (enemyPosition - objectPosition).normalized;
                objectKnockbackDirection.y = 0;
                objectKnockbackDirection.Normalize();
            }
            if (showDebugLogs) Debug.Log($"Object knockback direction recalculated: {objectKnockbackDirection}");
        }
        
        float objectOriginalY = objectPosition.y;
        float enemyOriginalY = enemyPosition.y;
        
        Vector3 impactNormal = (enemyPosition - objectPosition).normalized;
        Vector3 impactNormalFlat = new Vector3(impactNormal.x, 0, impactNormal.z).normalized;
        
        float impactAngleCos = BilliardPhysicsHelper.CalculateImpactAngleHorizontal(objectKnockbackDirection, enemyPosition - objectPosition);
        float impactAngleDeg = Mathf.Acos(Mathf.Clamp(impactAngleCos, -1f, 1f)) * Mathf.Rad2Deg;
        if (showDebugLogs) Debug.Log($"Horizontal impact angle: {impactAngleDeg:F1}° (cos={impactAngleCos:F2})");
        
        Vector3 cueBallNewVel, targetBallNewVel;
        BilliardPhysicsHelper.CalculateBilliardCollisionHorizontal(objectPosition, objectKnockbackDirection * repulseForce, enemyPosition, 1.0f, out cueBallNewVel, out targetBallNewVel);
        
        Vector3 objectStartFlat = new Vector3(objectKnockback.GetKnockbackStartPosition().x, 0, objectKnockback.GetKnockbackStartPosition().z);
        Vector3 objectTargetFlat = new Vector3(objectKnockback.GetKnockbackTargetPosition().x, 0, objectKnockback.GetKnockbackTargetPosition().z);
        float objectTotalDistance = Vector3.Distance(objectStartFlat, objectTargetFlat);
        
        Vector3 objectCurrentFlat = new Vector3(objectPosition.x, 0, objectPosition.z);
        float objectTraveledDistance = Vector3.Distance(objectStartFlat, objectCurrentFlat);
        float objectRemainingDistance = objectTotalDistance - objectTraveledDistance;
        
        if (showDebugLogs) Debug.Log($"Object movement: total={objectTotalDistance:F2}, traveled={objectTraveledDistance:F2}, remaining={objectRemainingDistance:F2}");
        
        float energyTransferRatio = CalculateEnergyTransfer(impactAngleCos);
        float enemyEnergyRatio = energyTransferRatio;
        float objectEnergyRatio = 1.0f - energyTransferRatio;
        
        float enemyKnockbackDistance = objectRemainingDistance * enemyEnergyRatio;
        float objectNewKnockbackDistance = objectRemainingDistance * objectEnergyRatio;
        
        if (showDebugLogs) Debug.Log($"Energy distribution: object retains {objectEnergyRatio:P0}, enemy gets {enemyEnergyRatio:P0}");
        
        objectKnockback.ForceStopKnockback();
        
        if (objectNewKnockbackDistance > 0.1f)
        {
            Vector3 objectNewDirection = BilliardPhysicsHelper.CalculateReflectionHorizontal(objectKnockbackDirection, impactNormal);
            objectNewDirection = new Vector3(objectNewDirection.x, 0, objectNewDirection.z).normalized;
            Vector3 objectNewTarget = BilliardPhysicsHelper.CalculateHorizontalTargetPosition(objectPosition, objectNewDirection, objectNewKnockbackDistance, objectOriginalY);
            
            if (showDebugLogs) Debug.Log($"Object new direction: {objectNewDirection}, distance={objectNewKnockbackDistance:F2}");
            
            objectKnockback.ApplyKnockbackToPosition(objectNewTarget);
            
            KnockbackCollisionDetector objectDetector = obj.GetComponent<KnockbackCollisionDetector>();
            if (objectDetector != null) objectDetector.StartKnockback();
        }
        else
        {
            if (showDebugLogs) Debug.Log($"Object energy exhausted, stopping");
        }
        
        if (targetBallNewVel != Vector3.zero && enemyKnockbackDistance > 0.1f)
        {
            Vector3 enemyKnockbackDirection = targetBallNewVel.normalized;
            enemyKnockbackDirection = new Vector3(enemyKnockbackDirection.x, 0, enemyKnockbackDirection.z).normalized;
            if (enemyKnockbackDirection == Vector3.zero) enemyKnockbackDirection = impactNormalFlat;
            
            Vector3 enemyTarget = BilliardPhysicsHelper.CalculateHorizontalTargetPosition(enemyPosition, enemyKnockbackDirection, enemyKnockbackDistance, enemyOriginalY);
            
            if (showDebugLogs) Debug.Log($"Enemy knockback: direction={enemyKnockbackDirection}, distance={enemyKnockbackDistance:F2}");
            
            enemyKnockback.ApplyKnockbackToPosition(enemyTarget);
            
            KnockbackCollisionDetector enemyDetector = enemy.GetComponent<KnockbackCollisionDetector>();
            if (enemyDetector != null) enemyDetector.StartKnockback();
        }
        else
        {
            if (showDebugLogs) Debug.Log($"Enemy did not receive enough energy, no knockback");
        }
    }

    private float CalculateEnergyTransfer(float impactAngleCos)
    {
        float absAngleCos = Mathf.Abs(impactAngleCos);
        float energyTransfer = Mathf.Clamp01(absAngleCos * absAngleCos);
        return energyTransfer * energyTransferMultiplier;
    }
    #endregion

    #region Enemy-Object Collision Handling
    private void HandleEnemyToEnemyCollision(ColorInteractionEvent interaction, ColorComponent sourceColor, ColorComponent targetColor)
    {
        KnockbackSystem sourceKnockback = interaction.Source.GetComponent<KnockbackSystem>();
        KnockbackCollisionDetector sourceDetector = interaction.Source.GetComponent<KnockbackCollisionDetector>();

        if (sourceKnockback != null) sourceKnockback.ForceStopKnockback();
        if (sourceDetector != null) sourceDetector.StopKnockback();
        
        if (sourceColor.IsOppositeColor(targetColor))
        {
            if (showDebugLogs) Debug.Log($"{interaction.Source.name} hit opposite color enemy {interaction.Target.name} -> instant kill");
            EnemyHealthSystem targetHealth = interaction.Target.GetComponent<EnemyHealthSystem>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage((int)knockbackEnemyKillDamage, interaction.Source);
            }
            
            if (knockbackCollisionEffect != null)
            {
                Vector3 effectPos = (GetEffectPosition(interaction.Source) + GetEffectPosition(interaction.Target)) * 0.5f;
                Instantiate(knockbackCollisionEffect, effectPos, Quaternion.identity);
            }
        }
        else
        {
            if (showDebugLogs) Debug.Log($"{interaction.Source.name} hit same color enemy {interaction.Target.name} -> only stop");
        }
    }

    private void HandleObjectToEnemyCollision(ColorInteractionEvent interaction, ColorComponent sourceColor, ColorComponent targetColor)
    {
        if (showDebugLogs) Debug.Log($"  Entering HandleObjectToEnemyCollision: {interaction.Source.name} -> {interaction.Target.name}");
        if (showDebugLogs) Debug.Log($"  Source color: {sourceColor.CurrentColor}, target color: {targetColor.CurrentColor}");
        if (showDebugLogs) Debug.Log($"  Opposite: {sourceColor.IsOppositeColor(targetColor)}");
        if (showDebugLogs) Debug.Log($"  Billiard physics: {enableBilliardPhysics}");
        
        KnockbackSystem sourceKnockback = interaction.Source.GetComponent<KnockbackSystem>();
        if (sourceKnockback == null || !sourceKnockback.IsKnockbackActive)
        {
            if (showDebugLogs) Debug.Log($"Object {interaction.Source.name} not in knockback state, skipping");
            return;
        }
        
        if (sourceColor.IsOppositeColor(targetColor))
        {
            if (knockbackCollisionEffect != null)
            {
                Vector3 effectPos = (GetEffectPosition(interaction.Source) + GetEffectPosition(interaction.Target)) * 0.5f;
                Instantiate(knockbackCollisionEffect, effectPos, Quaternion.identity);
            }
            
            if (enableBilliardPhysics)
            {
                if (showDebugLogs) Debug.Log($"Object {interaction.Source.name} hit opposite color enemy {interaction.Target.name} -> billiard physics");
                if (restrictToHorizontal) ApplyBilliardPhysicsToEnemy(interaction.Target, interaction.Source, interaction);
                EnemyHealthSystem sourceHealth = interaction.Source.GetComponent<EnemyHealthSystem>();
                if (sourceHealth != null) sourceHealth.TakeDamage((int)knockbackObjectSelfDamage, interaction.Target);
            }
            else
            {
                if (showDebugLogs) Debug.Log($"Object {interaction.Source.name} hit opposite color enemy {interaction.Target.name} -> simple knockback");
                ApplyRepulseEffectToEnemyInDirectionFromObject(interaction.Target, interaction.Source);
                StopObjectKnockbackImmediately(interaction.Source);
            }
        }
        else
        {
            if (showDebugLogs) Debug.Log($"Object {interaction.Source.name} hit same color enemy {interaction.Target.name} -> only stop object");
            StopObjectKnockbackImmediately(interaction.Source);
        }
    }

    private void StopObjectKnockbackImmediately(GameObject obj)
    {
        KnockbackSystem objectKnockback = obj.GetComponent<KnockbackSystem>();
        KnockbackCollisionDetector objectDetector = obj.GetComponent<KnockbackCollisionDetector>();

        if (objectKnockback != null) objectKnockback.ForceStopKnockback();
        if (objectDetector != null) objectDetector.StopKnockback();
        if (showDebugLogs) Debug.Log($"Immediately stopped knockback for object {obj.name}");
    }

    private void ApplyRepulseEffectToEnemyInDirectionFromObject(GameObject enemy, GameObject obj)
    {
        if (showDebugLogs) Debug.Log($"Applying knockback using object->enemy direction: object={obj.name}, enemy={enemy.name}");
        
        KnockbackSystem enemyKnockback = enemy.GetComponent<KnockbackSystem>();
        if (enemyKnockback != null)
        {
            Vector3 directionFromObjectToEnemy = (enemy.transform.position - obj.transform.position).normalized;
            directionFromObjectToEnemy.Normalize();
            Vector3 targetPosition = enemy.transform.position + directionFromObjectToEnemy * repulseForce;
            
            if (showDebugLogs) Debug.Log($"Knockback: enemy current={enemy.transform.position}, target={targetPosition}, distance={repulseForce}");
            
            enemyKnockback.ApplyKnockbackToPosition(targetPosition);
            if (showDebugLogs) Debug.Log($"Enemy {enemy.name} knocked back to: {targetPosition}");
            
            KnockbackCollisionDetector enemyDetector = enemy.GetComponent<KnockbackCollisionDetector>();
            if (enemyDetector != null)
            {
                enemyDetector.StartKnockback();
                if (showDebugLogs) Debug.Log($"Started collision detector for enemy {enemy.name}");
            }
        }
        else
        {
            if (showDebugLogs) Debug.LogWarning($"Cannot knock back enemy: missing KnockbackSystem");
        }
    }

    private void HandleEnemyToObjectCollision(ColorInteractionEvent interaction, ColorComponent sourceColor, ColorComponent targetColor)
    {
        if (showDebugLogs) Debug.Log($"Processing enemy->object collision: {interaction.Source.name} -> {interaction.Target.name}");
        
        if (sourceColor.IsOppositeColor(targetColor))
        {
            if (showDebugLogs) Debug.Log($"Enemy {interaction.Source.name} hit opposite color object {interaction.Target.name} -> enemy takes damage");
            EnemyHealthSystem sourceHealth = interaction.Source.GetComponent<EnemyHealthSystem>();
            if (sourceHealth != null) sourceHealth.TakeDamage((int)knockbackObjectSelfDamage, interaction.Target);
            
            if (knockbackCollisionEffect != null)
            {
                Vector3 effectPos = (GetEffectPosition(interaction.Source) + GetEffectPosition(interaction.Target)) * 0.5f;
                Instantiate(knockbackCollisionEffect, effectPos, Quaternion.identity);
            }
        }
        else
        {
            if (showDebugLogs) Debug.Log($"Enemy {interaction.Source.name} hit same color object {interaction.Target.name} -> only stop");
        }
        
        KnockbackSystem enemyKnockback = interaction.Source.GetComponent<KnockbackSystem>();
        KnockbackCollisionDetector enemyDetector = interaction.Source.GetComponent<KnockbackCollisionDetector>();
        if (enemyKnockback != null) enemyKnockback.ForceStopKnockback();
        if (enemyDetector != null) enemyDetector.StopKnockback();
    }
    
    private void HandleObjectToObjectCollision(ColorInteractionEvent interaction, ColorComponent sourceColor, ColorComponent targetColor)
    {
        KnockbackSystem sourceKnockback = interaction.Source.GetComponent<KnockbackSystem>();
        KnockbackCollisionDetector sourceDetector = interaction.Source.GetComponent<KnockbackCollisionDetector>();

        if (sourceKnockback != null) sourceKnockback.ForceStopKnockback();
        if (sourceDetector != null) sourceDetector.StopKnockback();
        
        if (sourceColor.IsOppositeColor(targetColor))
        {
            if (showDebugLogs) Debug.Log($"Object {interaction.Source.name} hit opposite color object {interaction.Target.name} -> target object knocked back");
            ApplyRepulseEffectToObject(interaction.Target, interaction.Source);
            
            if (knockbackCollisionEffect != null)
            {
                Vector3 effectPos = (GetEffectPosition(interaction.Source) + GetEffectPosition(interaction.Target)) * 0.5f;
                Instantiate(knockbackCollisionEffect, effectPos, Quaternion.identity);
            }
            
            KnockbackCollisionDetector targetDetector = interaction.Target.GetComponent<KnockbackCollisionDetector>();
            if (targetDetector != null) targetDetector.StartKnockback();
        }
        else
        {
            if (showDebugLogs) Debug.Log($"Object {interaction.Source.name} hit same color object {interaction.Target.name} -> only stop");
        }
    }
    #endregion

    #region Ability Interaction Handling
    private void HandleAbilityInteraction(ColorInteractionEvent interaction)
    {
        if (showDebugLogs) Debug.Log($"Processing ability interaction: {interaction.Source?.name}({interaction.SourceColor}) -> {interaction.Target?.name}({interaction.TargetColor})");
        
        ColorComponent sourceColorComp = interaction.Source?.GetComponent<ColorComponent>();
        ColorComponent targetColorComp = interaction.Target?.GetComponent<ColorComponent>();
        
        if (sourceColorComp == null || targetColorComp == null)
        {
            if (showDebugLogs) Debug.LogWarning("⚠️ Ability interaction failed: missing color component");
            return;
        }
        
        if (sourceColorComp.IsOppositeColor(targetColorComp))
        {
            HandleOppositeColorAbility(interaction, sourceColorComp, targetColorComp);
        }
    }

    private void HandleOppositeColorAbility(ColorInteractionEvent interaction, ColorComponent source, ColorComponent target)
    {
        if (showDebugLogs) Debug.Log($"Opposite color ability: {interaction.Source.name}({source.CurrentColor}) -> {interaction.Target.name}({target.CurrentColor})");
        
        if (knockbackCollisionEffect != null)
        {
            Instantiate(knockbackCollisionEffect, GetEffectPosition(interaction.Target), Quaternion.identity);
        }
    }
    #endregion

    #region Helper Methods
    private void InitializeKnockbackCollisionConfig()
    {
        if (showDebugLogs) Debug.Log($"Knockback collision config: kill damage={knockbackEnemyKillDamage}, self damage={knockbackObjectSelfDamage}, obstacle damage={knockbackObstacleDamage}");
    }
    
    private void ApplyRepulseEffectToEnemy(GameObject enemy, GameObject source)
    {
        if (source.CompareTag("Player"))
        {
            KnockbackSystem enemyKnockback = enemy.GetComponent<KnockbackSystem>();
            if (enemyKnockback != null)
            {
                Vector3 targetPosition = CameraDirectionHelper.GetKnockbackTargetPosition(source, enemy, repulseForce);
                if (CameraDirectionHelper.IsValidKnockbackPosition(source.transform.position, enemy.transform.position, targetPosition))
                {
                    enemyKnockback.ApplyKnockbackToPosition(targetPosition);
                    if (showDebugLogs) Debug.Log($"Player attack: enemy {enemy.name} knocked back to {targetPosition}");
                    
                    KnockbackCollisionDetector collisionDetector = enemy.GetComponent<KnockbackCollisionDetector>();
                    if (collisionDetector != null)
                    {
                        collisionDetector.StartKnockback();
                        if (showDebugLogs) Debug.Log($"Started collision detector for enemy {enemy.name}");
                    }
                }
            }
        }
        else
        {
            Vector3 direction = (enemy.transform.position - source.transform.position).normalized;
            ApplyRepulseEffectToEnemyInDirection(enemy, direction);
        }
    }
    
    private void ApplyRepulseEffectToEnemyInDirection(GameObject enemy, Vector3 direction)
    {
        KnockbackSystem enemyKnockback = enemy.GetComponent<KnockbackSystem>();
        if (enemyKnockback != null)
        {
            Vector3 targetPosition = enemy.transform.position + direction * repulseForce;
            if (showDebugLogs) Debug.Log($"Directional knockback: enemy={enemy.name}, direction={direction}, target={targetPosition}");
            enemyKnockback.ApplyKnockbackToPosition(targetPosition);
            if (showDebugLogs) Debug.Log($"Enemy {enemy.name} knocked back to: {targetPosition}");
            
            KnockbackCollisionDetector collisionDetector = enemy.GetComponent<KnockbackCollisionDetector>();
            if (collisionDetector != null)
            {
                collisionDetector.StartKnockback();
                if (showDebugLogs) Debug.Log($"Started collision detector for enemy {enemy.name}");
            }
        }
    }
    
    /// <summary>Gets a suitable position for spawning effects: collider center, renderer center, or transform position.</summary>
    private Vector3 GetEffectPosition(GameObject obj)
    {
        if (obj == null) return Vector3.zero;
        Collider col = obj.GetComponent<Collider>();
        if (col != null) return col.bounds.center;
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null) return rend.bounds.center;
        return obj.transform.position;
    }
    
    public float GetKnockbackObjectSelfDamage() => knockbackObjectSelfDamage;
    public float GetKnockbackEnemyKillDamage() => knockbackEnemyKillDamage;
    public float GetKnockbackObstacleDamage() => knockbackObstacleDamage;
    #endregion
}