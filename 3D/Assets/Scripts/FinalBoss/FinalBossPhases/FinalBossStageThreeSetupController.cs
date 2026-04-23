using UnityEngine;
using UnityEngine.AI;

public class FinalBossStageThreeSetupController : MonoBehaviour
{
    [Header("Stage Three Anchors")]
    [SerializeField] private Transform bossAnchor;
    [SerializeField] private Transform playerAnchor;
    [SerializeField] private Transform bossLookAtTarget;
    [SerializeField] private Transform playerLookAtTarget;

    [Header("References")]
    [SerializeField] private Transform bossRootMover;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private NavMeshAgent bossAgent;
    [SerializeField] private CharacterController playerCharacterController;
    [SerializeField] private NavMeshAgent playerAgent;

    [Header("Rules")]
    [SerializeField] private bool snapBossRotationFromAnchor = true;
    [SerializeField] private bool snapPlayerRotationFromAnchor = true;
    [SerializeField] private bool alignBossToLookTarget = true;
    [SerializeField] private bool alignPlayerToLookTarget = true;
    [SerializeField] private bool keepOnlyYawRotation = true;
    [SerializeField] private bool stopBossAgentOnPrepare = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public void Initialize(BossController bossController)
    {
        if (bossController != null)
        {
            bossRootMover ??= bossController.transform.parent != null ? bossController.transform.parent : bossController.transform;
            bossAgent ??= bossController.GetComponent<NavMeshAgent>();
        }

        if (playerRoot == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerRoot = player.transform;
                playerCharacterController ??= player.GetComponent<CharacterController>();
                playerAgent ??= player.GetComponent<NavMeshAgent>();
            }
        }
    }

    public void PrepareStage(BossController bossController)
    {
        Initialize(bossController);

        if (stopBossAgentOnPrepare && bossAgent != null)
        {
            bossAgent.isStopped = true;
            bossAgent.ResetPath();
        }

        TeleportBossToAnchor();
        TeleportPlayerToAnchor();
        AlignBossFacing();
        AlignPlayerFacing();
    }

    private void TeleportBossToAnchor()
    {
        if (bossRootMover == null || bossAnchor == null)
            return;

        if (bossAgent != null)
            bossAgent.Warp(bossAnchor.position);
        else
            bossRootMover.position = bossAnchor.position;

        bossRootMover.position = bossAnchor.position;

        if (snapBossRotationFromAnchor)
            bossRootMover.rotation = bossAnchor.rotation;

        Log($"TeleportBossToAnchor -> pos={bossAnchor.position}");
    }

    private void TeleportPlayerToAnchor()
    {
        if (playerRoot == null || playerAnchor == null)
            return;

        if (playerCharacterController != null)
            playerCharacterController.enabled = false;

        if (playerAgent != null)
        {
            playerAgent.Warp(playerAnchor.position);
            playerAgent.isStopped = true;
        }
        else
        {
            playerRoot.position = playerAnchor.position;
        }

        playerRoot.position = playerAnchor.position;

        if (snapPlayerRotationFromAnchor)
            playerRoot.rotation = playerAnchor.rotation;

        if (playerCharacterController != null)
            playerCharacterController.enabled = true;

        Log($"TeleportPlayerToAnchor -> pos={playerAnchor.position}");
    }

    private void AlignBossFacing()
    {
        if (!alignBossToLookTarget || bossRootMover == null || bossLookAtTarget == null)
            return;

        Vector3 direction = bossLookAtTarget.position - bossRootMover.position;
        RotateTransformTowards(bossRootMover, direction);
        Log($"AlignBossFacing -> target={bossLookAtTarget.name}");
    }

    private void AlignPlayerFacing()
    {
        if (!alignPlayerToLookTarget || playerRoot == null || playerLookAtTarget == null)
            return;

        Vector3 direction = playerLookAtTarget.position - playerRoot.position;
        RotateTransformTowards(playerRoot, direction);
        Log($"AlignPlayerFacing -> target={playerLookAtTarget.name}");
    }

    private void RotateTransformTowards(Transform targetTransform, Vector3 direction)
    {
        if (targetTransform == null || direction.sqrMagnitude <= 0.0001f)
            return;

        if (keepOnlyYawRotation)
            direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        targetTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossStageThreeSetupController] {message}");
    }
}
