using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ThrowObjectAbility
/// 右键（或 AbilityKey）两段式：
/// 1) 对准 ObjectTag 物体 -> Destroy（收起） -> isHolding = true
/// 2) isHolding == true 再按一次 -> Instantiate 新物体并用 Knockback 扔出去
///
/// 设计目标：
/// - 复用 TargetSelector 的 “相机中心选择” 风格
/// - 不使用 Rigidbody（全程 CharacterController / KnockbackSystem）
/// - 扔出的物体颜色 = 玩家当前颜色（释放瞬间）
/// </summary>
public class ThrowObjectAbility : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode abilityKey = KeyCode.Mouse1;
    [SerializeField] private string abilityInputAction = "Ability";
    [SerializeField] private bool useNewInputSystem = false;

    [Header("Pickup Settings")]
    [SerializeField] private string objectTag = "Object";
    [SerializeField] private float pickupCooldown = 0.15f;

    [Header("Throw Settings")]
    [SerializeField] private GameObject objectPrefab;         // 要扔出去生成的物体（你的唯一 object）
    [SerializeField] private Transform firePoint;             // 发射位置（建议挂在玩家身上/武器点）
    [SerializeField] private float throwDistance = 12f;       // 目标落点距离（沿相机前方）
    [SerializeField] private float minThrowDistance = 2f;     // 避免贴脸发射
    [SerializeField] private LayerMask aimHitMask = ~0;       // 用于射线命中地面/墙体（可选）

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // Runtime
    private bool isHolding = false;
    private float lastActionTime = -999f;

    // 复用 ColorChangeAbility 的选中系统
    private TargetSelector targetSelector;

    private void Awake()
    {
        targetSelector = GetComponent<TargetSelector>();
        if (targetSelector == null)
        {
            targetSelector = gameObject.AddComponent<TargetSelector>();
            Log("✅ 自动添加 TargetSelector 组件");
        }

        if (firePoint == null)
        {
            // 如果你忘了配，就用玩家自身位置稍微抬高一点
            GameObject fp = new GameObject("ThrowFirePoint_Auto");
            fp.transform.SetParent(transform);
            fp.transform.localPosition = new Vector3(0f, 1.2f, 0.5f);
            firePoint = fp.transform;
            LogWarning("⚠️ 未设置 FirePoint，已自动创建 ThrowFirePoint_Auto");
        }
    }

    private void LateUpdate()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        bool pressed = false;

        if (useNewInputSystem)
        {
            // 你当前 ColorChangeAbility 里是通过 OnAbility(InputValue) 触发的
            // 所以这里不主动轮询 InputAction，直接走回调即可（防止重复触发）
            return;
        }
        else
        {
            pressed = Input.GetKeyDown(abilityKey);
        }

        if (pressed)
        {
            TryUseAbility();
        }
    }

    /// <summary>
    /// 新输入系统回调（与你的 ColorChangeAbility 保持一致）
    /// 将 PlayerInput 的 Action 名设置为 "Ability"，并把它绑定到此函数即可。
    /// </summary>
    public void OnAbility(InputValue value)
    {
        if (!useNewInputSystem) return;
        if (value == null) return;
        if (!value.isPressed) return;

        TryUseAbility();
    }

    private void TryUseAbility()
    {
        if (Time.time - lastActionTime < pickupCooldown)
            return;

        lastActionTime = Time.time;

        if (!isHolding)
        {
            TryPickupObject();
        }
        else
        {
            ThrowNewObject();
        }
    }

    private void TryPickupObject()
    {
        Selectable selectable = targetSelector != null ? targetSelector.TrySelectTarget() : null;
        if (selectable == null)
        {
            Log("🟨 未选中任何目标，无法收起物体");
            return;
        }

        GameObject targetObj = selectable.gameObject;

        if (!targetObj.CompareTag(objectTag))
        {
            Log($"🟨 选中目标不是 ObjectTag({objectTag})：{targetObj.name}");
            return;
        }

        // 收起：Destroy
        Log($"📦 收起物体：{targetObj.name} -> Destroy + isHolding=true");
        Destroy(targetObj);

        // 触发拾取事件 (教学关用于检测是否完成拾取任务)
        string currentRoom = TutorialRoomController.GetCurrentRoomName();
        if (!string.IsNullOrEmpty(currentRoom))
        {
            TutorialTaskManager.TriggerObjectPickedUp(currentRoom);
        }

        isHolding = true;
    }

    private void ThrowNewObject()
    {
        if (objectPrefab == null)
        {
            LogWarning("❌ objectPrefab 未设置，无法投掷");
            return;
        }

        if (firePoint == null)
        {
            LogWarning("❌ firePoint 未设置，无法投掷");
            return;
        }

        // 计算投掷目标点：以相机中心方向为准
        Vector3 targetPosition = CalculateThrowTargetPosition();

        GameObject obj = Instantiate(objectPrefab, firePoint.position, firePoint.rotation);
        Log($"🟦 投掷生成物体：{obj.name} at {firePoint.position} -> target {targetPosition}");

        // 颜色 = 玩家当前颜色（释放瞬间）
        ApplyObjectColorFromPlayer(obj);

        // 进入击退状态（你希望调用 KnockbackSystem）
        KnockbackSystem ks = obj.GetComponent<KnockbackSystem>();
        if (ks != null)
        {
            ks.ApplyKnockbackToPosition(targetPosition);
            Log($"💨 ApplyKnockbackToPosition -> {targetPosition}");

            // 启动 KnockbackCollisionDetector（确保撞 Boss 会触发你那套撞击逻辑）
            KnockbackCollisionDetector detector = obj.GetComponent<KnockbackCollisionDetector>();
            if (detector != null)
            {
                detector.StartKnockback();
                Log("✅ StartKnockback()");
            }
        }
        else
        {
            LogWarning("⚠️ 物体缺少 KnockbackSystem，无法被投掷移动（请给 objectPrefab 加上）");
        }

        // 触发丢出事件 (教学关用于检测是否完成扔出任务)
        string currentRoom = TutorialRoomController.GetCurrentRoomName();
        if (!string.IsNullOrEmpty(currentRoom))
        {
            TutorialTaskManager.TriggerObjectThrown(currentRoom);
        }

        // 扔完：清空 holding
        isHolding = false;
    }

    private Vector3 CalculateThrowTargetPosition()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            // 没有主相机就按玩家 forward 扔
            Vector3 fallback = firePoint.position + transform.forward * Mathf.Max(minThrowDistance, throwDistance);
            LogWarning("⚠️ Camera.main 为空，使用玩家 forward 作为投掷方向");
            return fallback;
        }

        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;

        // 优先 raycast 命中点（更自然：对准地面/墙面）
        if (Physics.Raycast(origin, dir, out RaycastHit hit, throwDistance, aimHitMask))
        {
            float dist = Vector3.Distance(firePoint.position, hit.point);
            if (dist < minThrowDistance)
            {
                return firePoint.position + dir * minThrowDistance;
            }
            return hit.point;
        }

        // 没命中：固定距离
        return firePoint.position + dir * Mathf.Max(minThrowDistance, throwDistance);
    }

    private void ApplyObjectColorFromPlayer(GameObject obj)
    {
        ColorComponent playerColor = GetComponent<ColorComponent>();
        if (playerColor == null)
        {
            LogWarning("⚠️ 玩家缺少 ColorComponent，投掷物体无法继承颜色");
            return;
        }

        ColorComponent objColor = obj.GetComponent<ColorComponent>();
        if (objColor == null)
        {
            LogWarning("⚠️ 投掷物体缺少 ColorComponent，无法设置颜色");
            return;
        }

        objColor.CurrentColor = playerColor.CurrentColor;

        Log($"🎨 投掷物体颜色设置为玩家当前颜色：{playerColor.CurrentColor}");
    }

    public bool IsHoldingObject() => isHolding;

    private void Log(string msg)
    {
        if (showDebugLogs)
            Debug.Log($"[ThrowObjectAbility] {msg}");
    }

    private void LogWarning(string msg)
    {
        if (showDebugLogs)
            Debug.LogWarning($"[ThrowObjectAbility] {msg}");
    }
}

