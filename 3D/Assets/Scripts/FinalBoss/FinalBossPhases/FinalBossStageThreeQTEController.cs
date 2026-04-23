using UnityEngine;

public class FinalBossStageThreeQTEController : FinalBossStageBase
{
    [Header("Stage Three")]
    [SerializeField] private FinalBossStageTwoController previousStageTwoController;
    [SerializeField] private FinalBossPlayerActionLock playerActionLock;
    [SerializeField] private FinalBossPlayerQTEInputRouter qteInputRouter;
    [SerializeField] private FinalBossQTESequenceController qteSequenceController;
    [SerializeField] private FinalBossQTESequenceData[] qteSequencePerSegment;
    [SerializeField] private FinalBossQTEPromptUI qtePromptUI;
    [SerializeField] private FinalBossStageThreeSetupController setupController;
    [SerializeField] private FinalBossStageThreeTimelineDirector timelineDirector;
    [SerializeField] private GameObject[] stageThreeTimelineCameraRoots;
    [SerializeField] private GameObject[] qteHoldCameraRoots;
    [SerializeField] private ThirdPersonCam2 thirdPersonCameraController;
    [SerializeField] private GameObject thirdPersonGameplayCameraRoot;
    [SerializeField] private Camera thirdPersonGameplayCamera;
    [SerializeField] private bool triggerGameOverUIOnFailure = true;

    [Header("Reserved For Stage Three Performance")]
    [SerializeField] private FinalBossQTEBeam qteBeamPrefab;
    [SerializeField] private Transform qteSpawnPoint;
    [SerializeField] private ColorComponent playerColor;

    private Unity.Cinemachine.CinemachineBrain cinemachineBrain;
    private int activeQTEHoldCameraIndex = -1;

    public override void Initialize(FinalBossBattleDirector owner, BossController boss)
    {
        base.Initialize(owner, boss);

        previousStageTwoController ??= GetComponentInParent<FinalBossStageTwoController>();

        if (playerActionLock == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerActionLock = player.GetComponent<FinalBossPlayerActionLock>();
        }

        if (qteInputRouter == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                qteInputRouter = player.GetComponent<FinalBossPlayerQTEInputRouter>();
        }

        if (thirdPersonCameraController == null)
            thirdPersonCameraController = FindAnyObjectByType<ThirdPersonCam2>();

        if (thirdPersonGameplayCamera == null)
        {
            Camera[] sceneCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneCameras.Length; i++)
            {
                Camera sceneCamera = sceneCameras[i];
                if (sceneCamera == null)
                    continue;

                if (sceneCamera.GetComponent<Unity.Cinemachine.CinemachineBrain>() != null)
                    continue;

                thirdPersonGameplayCamera = sceneCamera;
                break;
            }
        }

        if (thirdPersonGameplayCameraRoot == null && thirdPersonGameplayCamera != null)
            thirdPersonGameplayCameraRoot = thirdPersonGameplayCamera.gameObject;

        if (playerColor == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerColor = player.GetComponent<ColorComponent>();
        }

        qteSequenceController ??= GetComponentInChildren<FinalBossQTESequenceController>(true);
        qtePromptUI ??= GetComponentInChildren<FinalBossQTEPromptUI>(true);
        setupController ??= GetComponentInChildren<FinalBossStageThreeSetupController>(true);
        timelineDirector ??= GetComponentInChildren<FinalBossStageThreeTimelineDirector>(true);

        if (cinemachineBrain == null)
            cinemachineBrain = FindAnyObjectByType<Unity.Cinemachine.CinemachineBrain>(FindObjectsInactive.Include);

        setupController?.Initialize(bossController);
        timelineDirector?.Initialize(this);
        DeactivateAllStageThreeCameras();
        DeactivateAllQTEHoldCameras();
    }

    public override void EnterStage()
    {
        base.EnterStage();

        previousStageTwoController?.CleanupStageEnterSpawnObject();

        ActivateStageThreeTimelineCameras();
        DeactivateAllQTEHoldCameras();

        if (thirdPersonCameraController != null)
            thirdPersonCameraController.enabled = false;

        SetGameplayCameraActive(false);
        SetCinemachineBrainCameraActive(true);

        if (playerActionLock != null)
            playerActionLock.LockAllGameplayActions();

        if (qteInputRouter != null)
            qteInputRouter.EnableRouter(false);

        setupController?.PrepareStage(bossController);

        if (timelineDirector != null)
        {
            timelineDirector.BeginStage();
            return;
        }

        BeginQTESequence();
    }

    public void BeginQTESequence()
    {
        if (!IsActive)
            return;

        int currentSegmentIndex = timelineDirector != null ? timelineDirector.CurrentSegmentIndex : 0;
        FinalBossQTESequenceData qteSequenceData = GetQTESequenceForSegment(currentSegmentIndex);
        if (qteSequenceData == null)
        {
            NotifyQTESuccess();
            return;
        }

        ActivateQTEHoldCamera(currentSegmentIndex);

        if (qteInputRouter != null)
            qteInputRouter.EnableRouter(true);

        if (qteSequenceController != null)
            qteSequenceController.BeginSequence(this, qteInputRouter, qteSequenceData, qtePromptUI);
    }

    public bool TryBeginQTEForCurrentSegment(int segmentIndex)
    {
        FinalBossQTESequenceData qteSequenceData = GetQTESequenceForSegment(segmentIndex);
        if (qteSequenceData == null)
            return false;

        BeginQTESequence();
        return true;
    }

    public override void ExitStage()
    {
        timelineDirector?.StopAll();
        DeactivateAllStageThreeCameras();
        DeactivateAllQTEHoldCameras();

        if (qteSequenceController != null)
            qteSequenceController.StopSequence();

        if (qteInputRouter != null)
            qteInputRouter.EnableRouter(false);

        if (playerActionLock != null)
            playerActionLock.UnlockAllGameplayActions();

        SetCinemachineBrainCameraActive(false);
        SetGameplayCameraActive(true);

        if (thirdPersonCameraController != null)
            thirdPersonCameraController.enabled = true;

        base.ExitStage();
    }

    public override bool CanBossTakeDirectDamage() => false;

    public void NotifyQTESuccess()
    {
        DeactivateCurrentQTEHoldCamera();

        if (qteInputRouter != null)
            qteInputRouter.EnableRouter(false);

        if (timelineDirector != null)
        {
            timelineDirector.HandleQTESuccess();
            return;
        }

        CompleteStage();
    }

    public void NotifyQTEFailure(string reason)
    {
        DeactivateCurrentQTEHoldCamera();

        if (qteInputRouter != null)
            qteInputRouter.EnableRouter(false);

        timelineDirector?.HandleQTEFailure();
        ForceFail(reason);

        if (triggerGameOverUIOnFailure && UIManager.Instance != null)
            UIManager.Instance.TriggerGameOver();
    }

    public void NotifyStageThreeSequenceFinished()
    {
        CompleteStage();
    }

    private FinalBossQTESequenceData GetQTESequenceForSegment(int segmentIndex)
    {
        if (qteSequencePerSegment == null || segmentIndex < 0 || segmentIndex >= qteSequencePerSegment.Length)
            return null;

        return qteSequencePerSegment[segmentIndex];
    }

    private void ActivateQTEHoldCamera(int segmentIndex)
    {
        DeactivateAllQTEHoldCameras();

        if (qteHoldCameraRoots == null || segmentIndex < 0 || segmentIndex >= qteHoldCameraRoots.Length)
            return;

        GameObject holdCameraRoot = qteHoldCameraRoots[segmentIndex];
        if (holdCameraRoot == null)
            return;

        holdCameraRoot.SetActive(true);
        activeQTEHoldCameraIndex = segmentIndex;
        ForceBrainCutToActiveCamera();
    }

    private void ForceBrainCutToActiveCamera()
    {
        if (cinemachineBrain == null)
            return;

        cinemachineBrain.enabled = false;
        cinemachineBrain.enabled = true;
    }

    private void DeactivateCurrentQTEHoldCamera()
    {
        if (qteHoldCameraRoots == null || activeQTEHoldCameraIndex < 0 || activeQTEHoldCameraIndex >= qteHoldCameraRoots.Length)
        {
            activeQTEHoldCameraIndex = -1;
            return;
        }

        GameObject holdCameraRoot = qteHoldCameraRoots[activeQTEHoldCameraIndex];
        if (holdCameraRoot != null)
            holdCameraRoot.SetActive(false);

        activeQTEHoldCameraIndex = -1;
    }

    private void DeactivateAllQTEHoldCameras()
    {
        if (qteHoldCameraRoots == null)
        {
            activeQTEHoldCameraIndex = -1;
            return;
        }

        for (int i = 0; i < qteHoldCameraRoots.Length; i++)
        {
            if (qteHoldCameraRoots[i] != null)
                qteHoldCameraRoots[i].SetActive(false);
        }

        activeQTEHoldCameraIndex = -1;
    }

    private void ActivateStageThreeTimelineCameras()
    {
        SetCameraGroupActive(stageThreeTimelineCameraRoots, true);
    }

    private void DeactivateAllStageThreeCameras()
    {
        SetCameraGroupActive(stageThreeTimelineCameraRoots, false);
        SetCameraGroupActive(qteHoldCameraRoots, false);
        activeQTEHoldCameraIndex = -1;
    }

    private void SetCameraGroupActive(GameObject[] cameraRoots, bool active)
    {
        if (cameraRoots == null)
            return;

        for (int i = 0; i < cameraRoots.Length; i++)
        {
            if (cameraRoots[i] != null)
                cameraRoots[i].SetActive(active);
        }
    }

    private void SetGameplayCameraActive(bool active)
    {
        if (thirdPersonGameplayCameraRoot != null)
        {
            thirdPersonGameplayCameraRoot.SetActive(active);
            return;
        }

        if (thirdPersonGameplayCamera != null)
            thirdPersonGameplayCamera.enabled = active;
    }

    private void SetCinemachineBrainCameraActive(bool active)
    {
        if (cinemachineBrain == null)
            return;

        cinemachineBrain.gameObject.SetActive(active);

        Camera brainCamera = cinemachineBrain.GetComponent<Camera>();
        if (brainCamera != null)
            brainCamera.enabled = active;

        cinemachineBrain.enabled = active;
    }
}
