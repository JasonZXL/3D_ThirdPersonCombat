using UnityEngine;
using UnityEngine.AI;

public class FinalBossPhaseOneContext
{
    public Transform BossTransform;
    public Transform PlayerTransform;
    public Transform CameraTransform;
    public NavMeshAgent NavAgent;
    public Animator Animator;
    public HealthSystem BossHealth;
    public ColorComponent BossColor;
    public ColorComponent PlayerColor;
    public GameObject BossGameObject;
    public FinalBossPhaseOneConfig Config;
    public GameObject PrepareAttackTelegraphPrefab;
    public Transform PrepareAttackTelegraphSpawnPoint;
    public Renderer[] BossRenderers;
    public ColorVisualizer[] BossColorVisualizers;

    public float DistanceToPlayer
    {
        get
        {
            if (BossTransform == null || PlayerTransform == null) return float.MaxValue;
            return Vector3.Distance(BossTransform.position, PlayerTransform.position);
        }
    }

    public Vector3 GetReferenceForwardOnPlane()
    {
        Vector3 forward = Vector3.zero;

        if (CameraTransform != null)
            forward = CameraTransform.forward;
        else if (PlayerTransform != null)
            forward = PlayerTransform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.001f && PlayerTransform != null && BossTransform != null)
            forward = PlayerTransform.position - BossTransform.position;

        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.001f)
            forward = Vector3.forward;

        return forward.normalized;
    }

    public void FacePlayer(float rotateSpeed)
    {
        if (BossTransform == null || PlayerTransform == null) return;

        Vector3 toPlayer = PlayerTransform.position - BossTransform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude <= 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        BossTransform.rotation = Quaternion.RotateTowards(BossTransform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
    }

    public void StopMovement()
    {
        if (NavAgent == null) return;
        NavAgent.isStopped = true;
        NavAgent.ResetPath();
    }

    public void MoveTowardsPlayer(float speed, float stopDistance)
    {
        if (BossTransform == null || PlayerTransform == null) return;

        if (NavAgent != null && NavAgent.enabled)
        {
            NavAgent.isStopped = false;
            NavAgent.speed = speed;
            NavAgent.stoppingDistance = stopDistance;
            NavAgent.SetDestination(PlayerTransform.position);
            return;
        }

        Vector3 direction = (PlayerTransform.position - BossTransform.position);
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            BossTransform.position += direction.normalized * speed * Time.deltaTime;
    }

    public void MoveTowardsWorldPoint(Vector3 targetPoint, float speed, float stopDistance)
    {
        if (BossTransform == null)
            return;

        Vector3 flatTargetPoint = targetPoint;
        flatTargetPoint.y = BossTransform.position.y;

        if (NavAgent != null && NavAgent.enabled)
        {
            NavAgent.isStopped = false;
            NavAgent.speed = speed;
            NavAgent.stoppingDistance = stopDistance;
            NavAgent.SetDestination(flatTargetPoint);
            return;
        }

        Vector3 direction = flatTargetPoint - BossTransform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > stopDistance * stopDistance)
            BossTransform.position += direction.normalized * speed * Time.deltaTime;
    }

    public void StrafeAroundPlayer(float directionSign, float speed, float preferredRadius)
    {
        if (BossTransform == null || PlayerTransform == null) return;

        Vector3 offset = BossTransform.position - PlayerTransform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude < 0.001f)
            offset = -PlayerTransform.forward;

        Vector3 radial = offset.normalized;
        Vector3 tangent = new Vector3(-radial.z, 0f, radial.x) * Mathf.Sign(directionSign);
        Vector3 desiredPosition = PlayerTransform.position + radial * preferredRadius + tangent * 1.25f;

        if (NavAgent != null && NavAgent.enabled)
        {
            NavAgent.isStopped = false;
            NavAgent.speed = speed;
            NavAgent.stoppingDistance = 0f;
            NavAgent.SetDestination(desiredPosition);
            return;
        }

        Vector3 move = desiredPosition - BossTransform.position;
        move.y = 0f;
        if (move.sqrMagnitude > 0.001f)
            BossTransform.position += move.normalized * speed * Time.deltaTime;
    }

    public void MoveSidewaysFacingPlayer(float directionSign, float speed)
    {
        if (BossTransform == null)
            return;

        Vector3 sideways = BossTransform.right * Mathf.Sign(directionSign);
        sideways.y = 0f;

        if (sideways.sqrMagnitude <= 0.001f)
            return;

        sideways.Normalize();

        if (NavAgent != null && NavAgent.enabled)
        {
            NavAgent.isStopped = false;
            NavAgent.ResetPath();
            NavAgent.Move(sideways * speed * Time.deltaTime);
            return;
        }

        BossTransform.position += sideways * speed * Time.deltaTime;
    }

    public void MoveAwayFromPlayer(float speed)
    {
        if (BossTransform == null || PlayerTransform == null) return;

        Vector3 direction = BossTransform.position - PlayerTransform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            direction = -BossTransform.forward;

        if (NavAgent != null && NavAgent.enabled)
        {
            NavAgent.isStopped = false;
            NavAgent.speed = speed;
            NavAgent.stoppingDistance = 0f;
            NavAgent.SetDestination(BossTransform.position + direction.normalized * speed);
            return;
        }

        BossTransform.position += direction.normalized * speed * Time.deltaTime;
    }

    public void TeleportToWorldPoint(Vector3 targetPoint)
    {
        if (BossTransform == null)
            return;

        Vector3 flatTargetPoint = targetPoint;
        flatTargetPoint.y = BossTransform.position.y;

        if (NavAgent != null && NavAgent.enabled)
        {
            NavAgent.ResetPath();
            NavAgent.Warp(flatTargetPoint);
            NavAgent.isStopped = true;
            return;
        }

        BossTransform.position = flatTargetPoint;
    }

    public void SnapFacePlayer()
    {
        if (BossTransform == null || PlayerTransform == null)
            return;

        Vector3 toPlayer = PlayerTransform.position - BossTransform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude <= 0.001f)
            return;

        BossTransform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
    }

    public bool IsPlayerWithinDistance(float distance)
    {
        return DistanceToPlayer <= distance;
    }

    public void PublishBossAttack()
    {
        if (BossGameObject == null || PlayerTransform == null) return;
        ColorEventBus.PublishEnemyAttack(BossGameObject, PlayerTransform.gameObject);
    }

    public void PlayPrepareAttackTelegraph()
    {
        if (PrepareAttackTelegraphPrefab == null)
            return;

        Transform spawnPoint = PrepareAttackTelegraphSpawnPoint != null
            ? PrepareAttackTelegraphSpawnPoint
            : BossTransform;

        if (spawnPoint == null)
            return;

        Object.Instantiate(
            PrepareAttackTelegraphPrefab,
            spawnPoint.position,
            spawnPoint.rotation);
    }

    public void SetBossVisualVisible(bool visible)
    {
        if (BossRenderers != null)
        {
            for (int i = 0; i < BossRenderers.Length; i++)
            {
                if (BossRenderers[i] != null)
                    BossRenderers[i].enabled = visible;
            }
        }

        if (BossColorVisualizers != null)
        {
            for (int i = 0; i < BossColorVisualizers.Length; i++)
            {
                if (BossColorVisualizers[i] != null)
                    BossColorVisualizers[i].enabled = visible;
            }
        }
    }
}
