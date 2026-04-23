using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class FinalBossStageTwoController : FinalBossStageBase
{
    [Header("Stage Two")]
    [SerializeField] private List<FinalBossResonancePillar> pillars = new List<FinalBossResonancePillar>();
    [SerializeField] private BossController controlledBoss;
    [SerializeField] private BossMeleeModule meleeModule;
    [SerializeField] private BossRangedModule rangedModule;
    [SerializeField] private NavMeshAgent bossAgent;
    [SerializeField] private Animator bossAnimator;
    [SerializeField] private ColorComponent bossColorComponent;
    [SerializeField] private string stageTwoLoopBool = "IsStageTwoLoop";
    [SerializeField] private Transform stageCenterAnchor;
    [SerializeField] private Transform playerTeleportAnchor;
    [SerializeField] private GameObject stageEnterSpawnPrefab;
    [SerializeField] private Transform stageEnterSpawnAnchor;
    [SerializeField] private bool destroyStageEnterSpawnOnExit = true;
    [SerializeField] private FinalBossStageTwoBarrier stageBarrierPrefab;
    [SerializeField] private Transform barrierAnchor;
    [SerializeField] private float fallbackBarrierRadius = 3.5f;
    [SerializeField] private FinalBossShockwaveEmitter shockwaveEmitter;
    [SerializeField] private float colorSwapInterval = 4.5f;

    private int clearedPillarCount;
    private FinalBossStageTwoBarrier barrierInstance;
    private bool barrierSpawnedAtRuntime;
    private Collider[] bossColliders;
    private float colorSwapTimer;
    private GameObject stageEnterSpawnInstance;

    public override void Initialize(FinalBossBattleDirector owner, BossController boss)
    {
        base.Initialize(owner, boss);
        controlledBoss = boss;

        if (controlledBoss != null)
        {
            meleeModule ??= controlledBoss.GetComponentInChildren<BossMeleeModule>(true);
            rangedModule ??= controlledBoss.GetComponentInChildren<BossRangedModule>(true);
            bossAgent ??= controlledBoss.GetComponent<NavMeshAgent>();
            bossAnimator ??= controlledBoss.GetComponentInChildren<Animator>(true);
            bossColorComponent ??= controlledBoss.GetComponent<ColorComponent>();
            bossColliders = controlledBoss.GetComponentsInChildren<Collider>(true);
        }

        barrierAnchor ??= controlledBoss != null ? controlledBoss.transform : transform;
        if (stageBarrierPrefab == null)
            stageBarrierPrefab = GetComponentInChildren<FinalBossStageTwoBarrier>(true);

        if (pillars.Count == 0)
            pillars.AddRange(GetComponentsInChildren<FinalBossResonancePillar>(true));

        foreach (FinalBossResonancePillar pillar in pillars)
        {
            if (pillar == null)
                continue;

            pillar.Initialize(this);
            pillar.OnPillarDestroyed -= HandlePillarDestroyed;
            pillar.OnPillarDestroyed += HandlePillarDestroyed;
            pillar.gameObject.SetActive(false);
        }
    }

    public override void EnterStage()
    {
        base.EnterStage();
        clearedPillarCount = 0;
        colorSwapTimer = 0f;

        TeleportBossToStageCenter();
        SpawnStageEnterObject();

        if (controlledBoss != null)
            controlledBoss.enabled = false;

        if (meleeModule != null)
            meleeModule.SetActive(false);

        if (rangedModule != null)
            rangedModule.SetActive(false);

        if (bossAgent != null)
        {
            bossAgent.isStopped = true;
            bossAgent.ResetPath();
        }

        SetStageTwoLoopAnimation(true);
        EnsureBarrierActive();
        TeleportPlayerToStageStart();

        if (shockwaveEmitter != null)
        {
            shockwaveEmitter.SetSourceColorComponent(bossColorComponent);
            shockwaveEmitter.StartEmitter();
        }

        foreach (FinalBossResonancePillar pillar in pillars)
        {
            if (pillar != null)
                pillar.ResetPillar();
        }
    }

    public override void ExitStage()
    {
        if (shockwaveEmitter != null)
            shockwaveEmitter.StopEmitter();

        DestroyBarrier();
        SetStageTwoLoopAnimation(false);
        SetPillarsVisible(false);
        CleanupStageEnterSpawnObject();
        base.ExitStage();
    }

    private void Update()
    {
        if (!IsActive || bossColorComponent == null || colorSwapInterval <= 0f)
            return;

        colorSwapTimer += Time.deltaTime;
        if (colorSwapTimer < colorSwapInterval)
            return;

        colorSwapTimer = 0f;
        bossColorComponent.ToggleColor();
        Log($"StageTwo BossColor Toggle -> {bossColorComponent.CurrentColor}");
    }

    public override bool CanBossTakeDirectDamage() => false;

    public override bool IsStageComplete()
    {
        int validPillarCount = 0;
        for (int i = 0; i < pillars.Count; i++)
        {
            if (pillars[i] != null)
                validPillarCount++;
        }

        return validPillarCount > 0 && clearedPillarCount >= validPillarCount;
    }

    public void CleanupStageEnterSpawnObject()
    {
        DestroyAllSceneObjectsWithObjectTag();
    }

    private void HandlePillarDestroyed(FinalBossResonancePillar pillar)
    {
        clearedPillarCount++;
        Log($"Pillar destroyed -> {clearedPillarCount}");

        if (IsStageComplete())
        {
            DestroyBarrier();
            CompleteStage();
        }
    }

    private void EnsureBarrierActive()
    {
        if (barrierInstance == null)
        {
            barrierInstance = GetComponentInChildren<FinalBossStageTwoBarrier>(true);
            barrierSpawnedAtRuntime = false;

            if (barrierInstance == null && stageBarrierPrefab != null)
            {
                barrierInstance = Instantiate(
                    stageBarrierPrefab,
                    barrierAnchor != null ? barrierAnchor.position : transform.position,
                    Quaternion.identity,
                    transform);
                barrierSpawnedAtRuntime = true;
            }

            if (barrierInstance == null)
            {
                GameObject barrierObject = new GameObject("StageTwoBarrier_Auto");
                barrierObject.transform.SetParent(transform);
                barrierObject.transform.position = barrierAnchor != null ? barrierAnchor.position : transform.position;
                barrierInstance = barrierObject.AddComponent<FinalBossStageTwoBarrier>();
                barrierInstance.ConfigureFallbackSphere(fallbackBarrierRadius);
                barrierSpawnedAtRuntime = true;
            }
        }

        if (barrierInstance == null)
            return;

        barrierInstance.gameObject.SetActive(true);
        barrierInstance.Initialize(barrierAnchor != null ? barrierAnchor : transform, bossColliders);
    }

    private void DestroyBarrier()
    {
        if (barrierInstance == null)
            return;

        if (barrierSpawnedAtRuntime)
        {
            Destroy(barrierInstance.gameObject);
            barrierInstance = null;
            barrierSpawnedAtRuntime = false;
            return;
        }

        barrierInstance.gameObject.SetActive(false);
    }

    private void SetStageTwoLoopAnimation(bool active)
    {
        if (bossAnimator == null || string.IsNullOrWhiteSpace(stageTwoLoopBool))
            return;

        bossAnimator.SetBool(stageTwoLoopBool, active);
    }

    private void SetPillarsVisible(bool visible)
    {
        for (int i = 0; i < pillars.Count; i++)
        {
            if (pillars[i] != null)
                pillars[i].gameObject.SetActive(visible);
        }
    }

    private void TeleportBossToStageCenter()
    {
        if (controlledBoss == null || stageCenterAnchor == null)
            return;

        Transform bossTransform = controlledBoss.transform;
        Vector3 targetPosition = stageCenterAnchor.position;
        Quaternion targetRotation = stageCenterAnchor.rotation;

        CharacterController characterController = controlledBoss.GetComponent<CharacterController>();
        bool ccWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null && ccWasEnabled)
            characterController.enabled = false;

        if (bossAgent != null && bossAgent.enabled)
        {
            bossAgent.isStopped = true;
            bossAgent.ResetPath();
            bossAgent.Warp(targetPosition);
            bossAgent.nextPosition = targetPosition;
        }

        bossTransform.position = targetPosition;
        bossTransform.rotation = targetRotation;

        if (characterController != null)
            characterController.enabled = ccWasEnabled;

        Log($"TeleportBossToStageCenter -> {targetPosition}");
    }

    private void TeleportPlayerToStageStart()
    {
        if (playerTeleportAnchor == null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        Transform playerTransform = playerObject.transform;
        Vector3 targetPosition = playerTeleportAnchor.position;
        Quaternion targetRotation = playerTeleportAnchor.rotation;

        CharacterController playerCharacterController = playerObject.GetComponent<CharacterController>();
        bool ccWasEnabled = playerCharacterController != null && playerCharacterController.enabled;
        if (playerCharacterController != null && ccWasEnabled)
            playerCharacterController.enabled = false;

        NavMeshAgent playerAgent = playerObject.GetComponent<NavMeshAgent>();
        if (playerAgent != null && playerAgent.enabled)
        {
            playerAgent.isStopped = true;
            playerAgent.ResetPath();
            playerAgent.Warp(targetPosition);
            playerAgent.nextPosition = targetPosition;
        }

        playerTransform.position = targetPosition;
        playerTransform.rotation = targetRotation;

        if (playerCharacterController != null)
            playerCharacterController.enabled = ccWasEnabled;

        Log($"TeleportPlayerToStageStart -> {targetPosition}");
    }

    private void SpawnStageEnterObject()
    {
        if (stageEnterSpawnPrefab == null || stageEnterSpawnAnchor == null)
            return;

        DestroyStageEnterObject();
        stageEnterSpawnInstance = Instantiate(
            stageEnterSpawnPrefab,
            stageEnterSpawnAnchor.position,
            stageEnterSpawnAnchor.rotation);

        Log($"SpawnStageEnterObject -> {stageEnterSpawnInstance.name} at {stageEnterSpawnAnchor.position}");
    }

    private void DestroyStageEnterObject()
    {
        if (!destroyStageEnterSpawnOnExit || stageEnterSpawnInstance == null)
            return;

        ForceDestroyStageEnterObject();
    }

    private void ForceDestroyStageEnterObject()
    {
        if (stageEnterSpawnInstance == null)
            return;

        Log($"DestroyStageEnterObject -> {stageEnterSpawnInstance.name}");
        Destroy(stageEnterSpawnInstance);
        stageEnterSpawnInstance = null;
    }

    private void DestroyAllSceneObjectsWithObjectTag()
    {
        GameObject[] sceneObjects = GameObject.FindGameObjectsWithTag("Object");
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject sceneObject = sceneObjects[i];
            if (sceneObject == null)
                continue;

            if (stageEnterSpawnInstance == sceneObject)
                stageEnterSpawnInstance = null;

            Log($"DestroyAllSceneObjectsWithObjectTag -> {sceneObject.name}");
            Destroy(sceneObject);
        }
    }
}
