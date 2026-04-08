using UnityEngine;

/// <summary>
/// BossStunWindow：眩晕输出窗口管理器
/// - 监听 ChasingEnemyStunController.IsStunning 的状态变化
/// - Stun开始：开启 BossWeakPoint（背部可受击）
/// - Stun结束：关闭 BossWeakPoint
///
/// 推荐挂载位置：Boss根物体（和BossController同级）
/// 必要引用：
/// - ChasingEnemyStunController（Boss身上的眩晕控制器）
/// - BossWeakPoint（背部弱点子物体上的脚本）
/// </summary>
public class BossStunWindow : MonoBehaviour
{
    [Header("引用（可自动寻找）")]
    [SerializeField] private ChasingEnemyStunController stunController;
    [SerializeField] private BossWeakPoint weakPoint;

    [Header("窗口规则")]
    [Tooltip("眩晕开始时是否自动开启弱点")]
    [SerializeField] private bool enableWeakPointOnStunStart = true;

    [Tooltip("眩晕结束时是否自动关闭弱点")]
    [SerializeField] private bool disableWeakPointOnStunEnd = true;

    [Header("安全兜底")]
    [Tooltip("启用时：如果脚本Disable/Destroy，会强制关闭弱点，避免弱点永久开启")]
    [SerializeField] private bool forceCloseWeakPointOnDisable = true;

    [Header("调试")]
    [SerializeField] private bool showDebugLogs = true;

    // runtime
    private bool lastIsStunning = false;
    private bool initialized = false;

    private void Awake()
    {
        // 自动找引用（先在根上找，再在子物体里找）
        if (stunController == null)
        {
            stunController = GetComponent<ChasingEnemyStunController>();
            if (stunController == null)
                stunController = GetComponentInChildren<ChasingEnemyStunController>(true);
        }

        if (weakPoint == null)
        {
            weakPoint = GetComponentInChildren<BossWeakPoint>(true);
        }
    }

    private void OnEnable()
    {
        InitializeState();
    }

    private void Start()
    {
        InitializeState();
    }

    private void InitializeState()
    {
        if (initialized) return;
        initialized = true;

        if (stunController == null)
        {
            Debug.LogWarning($"⚠️ [BossStunWindow] 未找到 ChasingEnemyStunController，无法管理眩晕窗口：{name}");
            return;
        }

        if (weakPoint == null)
        {
            Debug.LogWarning($"⚠️ [BossStunWindow] 未找到 BossWeakPoint，无法开启/关闭弱点：{name}");
            return;
        }

        lastIsStunning = stunController.IsStunning;

        // 初始化时同步一次弱点状态（避免场景加载时状态不一致）
        if (lastIsStunning && enableWeakPointOnStunStart)
        {
            weakPoint.SetWeakPointActive(true);
            if (showDebugLogs)
                Debug.Log($"🎯 [BossStunWindow] 初始化：Boss当前处于眩晕 -> 弱点开启");
        }
        else
        {
            weakPoint.SetWeakPointActive(false);
            if (showDebugLogs)
                Debug.Log($"🎯 [BossStunWindow] 初始化：Boss非眩晕 -> 弱点关闭");
        }
    }

    private void Update()
    {
        if (stunController == null || weakPoint == null) return;

        bool isStunning = stunController.IsStunning;

        // 状态无变化就不处理
        if (isStunning == lastIsStunning) return;

        // 眩晕开始
        if (isStunning && enableWeakPointOnStunStart)
        {
            weakPoint.SetWeakPointActive(true);

            if (showDebugLogs)
                Debug.Log($"🌀 [BossStunWindow] 眩晕开始 -> 开启弱点窗口");
        }

        // 眩晕结束
        if (!isStunning && disableWeakPointOnStunEnd)
        {
            weakPoint.SetWeakPointActive(false);

            if (showDebugLogs)
                Debug.Log($"✅ [BossStunWindow] 眩晕结束 -> 关闭弱点窗口");
        }

        lastIsStunning = isStunning;
    }

    private void OnDisable()
    {
        if (!forceCloseWeakPointOnDisable) return;
        if (weakPoint == null) return;

        // 防止弱点永久开着（比如脚本被关掉/切场景）
        weakPoint.SetWeakPointActive(false);

        if (showDebugLogs)
            Debug.Log($"🧹 [BossStunWindow] Disable时强制关闭弱点窗口");
    }
}

