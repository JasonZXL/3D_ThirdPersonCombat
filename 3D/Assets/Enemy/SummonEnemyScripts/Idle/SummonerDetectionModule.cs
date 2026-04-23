using UnityEngine;

/// <summary>
/// Summoner 敌人的玩家感知检测模块。
/// 功能：提供"球形保底发现"与"锥形视野发现"两层检测逻辑，向 SummonerIdleModule 返回是否发现玩家的布尔结果及发现原因。
/// 工作链路：由 SummonerIdleModule 持有并初始化；每帧通过 CanDetectPlayer() 被 Idle 模块查询；Gizmos 由宿主 MonoBehaviour 转调 DrawDetectionGizmos()。
/// 设计原则：仅负责感知判断，不参与状态切换、动画、移动控制；检测行为固定可预测，不含随机逻辑。
/// </summary>
public class SummonerDetectionModule
{
    // ──────────────────────────────────────────────
    // 外部引用
    // ──────────────────────────────────────────────

    private Transform selfTransform;
    private Transform playerTransform;

    // ──────────────────────────────────────────────
    // 配置（Initialize 后只读）
    // ──────────────────────────────────────────────

    private bool  enableDetectDebug;
    private bool  showDetectGizmos;
    private bool  enableMissReasonDebug;
    private float guaranteeDetectRadius;
    private float visionDetectRadius;
    private float visionAngle;

    // ──────────────────────────────────────────────
    // 运行时状态
    // ──────────────────────────────────────────────

    private string lastDetectReason      = "None";
    private float  lastDistanceToPlayer  = 0f;
    private float  lastAngleToPlayer     = 0f;
    private bool   lastCanDetect         = false;

    // ──────────────────────────────────────────────
    // Debug 节流
    // ──────────────────────────────────────────────

    private float nextMissDebugTime  = 0f;
    private const float MissDebugInterval = 0.5f;

    // ══════════════════════════════════════════════
    // 公开接口
    // ══════════════════════════════════════════════

    /// <summary>
    /// 初始化 Detection 模块所需引用与配置。
    /// 功能：注入 Transform 引用与 DetectionConfig 数据；若 playerTransform 传入为 null 则立即尝试通过 Tag 查找。
    /// 工作链路：由 SummonerIdleModule.Initialize() 调用一次；完成后 CanDetectPlayer() 可被正常使用。
    /// 对下游影响：所有配置字段初始化完毕，运行时状态字段清零；后续所有检测基于此配置运行。
    /// </summary>
    public void Initialize(
        Transform       selfTransform,
        Transform       playerTransform,
        bool            enableDebug,
        DetectionConfig config)
    {
        this.selfTransform   = selfTransform;
        this.playerTransform = playerTransform;

        enableDetectDebug     = enableDebug;
        showDetectGizmos      = config.showDetectGizmos;
        enableMissReasonDebug = config.enableMissReasonDebug;
        guaranteeDetectRadius = config.guaranteeDetectRadius;
        visionDetectRadius    = config.visionDetectRadius;
        visionAngle           = config.visionAngle;

        lastDetectReason     = "None";
        lastCanDetect        = false;
        nextMissDebugTime    = 0f;

        if (this.playerTransform == null)
            RefreshPlayerReferenceIfNeeded();

        LogDetect("[Init] Self = "                + selfTransform.name);
        LogDetect("[Init] guaranteeDetectRadius = " + guaranteeDetectRadius);
        LogDetect("[Init] visionDetectRadius = "   + visionDetectRadius);
        LogDetect("[Init] visionAngle = "          + visionAngle + "° (halfAngle = " + (visionAngle * 0.5f) + "°)");
        LogDetect("[Init] playerTransform cached = " + (this.playerTransform != null));
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 对玩家执行完整检测流程，返回当前是否应判定为"发现玩家"。
    /// 功能：按固定顺序执行"球形保底发现 -> 超距离剔除 -> 锥形视野发现"；命中时记录 lastDetectReason 并返回 true，未命中返回 false。
    /// 工作链路：由 SummonerIdleModule.CanInterruptByPlayerDetect() 每帧调用；返回 true 时由 Idle 模块输出退出日志，由总控脚本负责切换到 Alert。
    /// 对下游影响：更新 lastDetectReason / lastDistanceToPlayer / lastAngleToPlayer / lastCanDetect；不修改任何外部状态。
    /// </summary>
    public bool CanDetectPlayer()
    {
        // Step 1：确保玩家引用有效
        RefreshPlayerReferenceIfNeeded();
        if (playerTransform == null)
        {
            lastCanDetect    = false;
            lastDetectReason = "None";
            return false;
        }

        float distance = Vector3.Distance(selfTransform.position, playerTransform.position);
        lastDistanceToPlayer = distance;

        // Step 2：球形保底发现（优先级最高，不受朝向影响）
        if (IsPlayerInGuaranteeRadius(distance))
        {
            lastCanDetect    = true;
            lastDetectReason = "GuaranteeRadius";

            LogDetect("[Guarantee] Player detected by guarantee radius."
                + " | PlayerPos = "  + playerTransform.position
                + " | Distance = "   + distance.ToString("F2")
                + " | Threshold = "  + guaranteeDetectRadius
                + " | Reason = GuaranteeRadius");

            return true;
        }

        // Step 3：超出锥形视野最大距离，直接排除
        if (distance > visionDetectRadius)
        {
            lastCanDetect    = false;
            lastDetectReason = "None";

            if (enableDetectDebug && enableMissReasonDebug && Time.time >= nextMissDebugTime)
            {
                nextMissDebugTime = Time.time + MissDebugInterval;
                LogDetect("[Miss] Player outside vision radius."
                    + " | Distance = "     + distance.ToString("F2")
                    + " | VisionRadius = " + visionDetectRadius);
            }

            return false;
        }

        // Step 4：锥形视野发现
        if (IsPlayerInVisionCone())
        {
            lastCanDetect    = true;
            lastDetectReason = "VisionCone";

            LogDetect("[Vision] Player detected by vision cone."
                + " | PlayerPos = "      + playerTransform.position
                + " | Distance = "       + lastDistanceToPlayer.ToString("F2")
                + " | Angle = "          + lastAngleToPlayer.ToString("F1") + "°"
                + " | VisionHalfAngle = " + (visionAngle * 0.5f).ToString("F1") + "°"
                + " | Reason = VisionCone");

            return true;
        }

        // Step 5：在视野距离内但角度不满足
        lastCanDetect    = false;
        lastDetectReason = "None";

        if (enableDetectDebug && enableMissReasonDebug && Time.time >= nextMissDebugTime)
        {
            nextMissDebugTime = Time.time + MissDebugInterval;
            LogDetect("[Miss] Player inside vision radius but outside vision cone."
                + " | Angle = "          + lastAngleToPlayer.ToString("F1") + "°"
                + " | VisionHalfAngle = " + (visionAngle * 0.5f).ToString("F1") + "°");
        }

        return false;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 当 playerTransform 为空时，尝试通过 Tag 重新查找玩家引用。
    /// 功能：调用 GameObject.FindGameObjectWithTag("Player") 重新缓存 playerTransform；找到后输出 debug 日志。
    /// 工作链路：由 CanDetectPlayer() 内部每帧调用；外部也可手动调用以提前刷新引用。
    /// 对下游影响：若查找成功，playerTransform 不再为 null，后续 CanDetectPlayer() 可正常执行检测。
    /// </summary>
    public void RefreshPlayerReferenceIfNeeded()
    {
        if (playerTransform != null)
            return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            LogDetect("[PlayerRef] Re-acquired player reference: " + playerTransform.name);
        }
        else
        {
            LogDetect("[PlayerRef] Player not found in scene. Tag 'Player' missing or object inactive.");
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回最近一次检测命中的原因字符串。
    /// 功能：提供"GuaranteeRadius" / "VisionCone" / "None"三种结果，供 Idle 模块在退出日志或状态切换时读取。
    /// 工作链路：由 SummonerIdleModule 在 CanInterruptByPlayerDetect 返回 true 后读取；纯只读查询，无副作用。
    /// </summary>
    public string GetLastDetectReason()
    {
        return lastDetectReason;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 返回当前 Detection 模块内部状态的简要调试摘要字符串。
    /// 功能：汇总 lastCanDetect / lastDetectReason / lastDistanceToPlayer / lastAngleToPlayer，供外部调试面板或日志使用。
    /// 工作链路：可由总控脚本或外部 Debug 面板随时调用；不修改任何内部状态，纯只读。
    /// </summary>
    public string GetDebugDetectSummary()
    {
        return $"[SummonerDetect Summary] CanDetect={lastCanDetect} | Reason={lastDetectReason} | Distance={lastDistanceToPlayer:F2} | Angle={lastAngleToPlayer:F1}°";
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 在 Scene 视图中绘制球形保底范围与锥形视野边界的 Gizmos 可视化。
    /// 功能：绘制绿色球形（保底半径）、黄色三线锥（视野锥）及玩家方向线（命中红色 / 未命中灰色）。
    /// 工作链路：由宿主 MonoBehaviour（如 SummonerIdleTester）在 OnDrawGizmos / OnDrawGizmosSelected 中转调；不影响任何运行时状态。
    /// 对下游影响：纯绘制，无副作用；showDetectGizmos == false 时直接返回不绘制任何内容。
    /// </summary>
    public void DrawDetectionGizmos()
    {
        if (!showDetectGizmos)
            return;

        if (selfTransform == null)
            return;

        Vector3 origin  = selfTransform.position;
        Vector3 forward = selfTransform.forward;

        // 1. 球形保底范围（绿色）
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin, guaranteeDetectRadius);

        // 2. 锥形视野（黄色）：中线 + 左右边界线 + 扇形边缘插值
        float   halfAngle = visionAngle * 0.5f;
        Vector3 leftDir   = Quaternion.Euler(0f, -halfAngle, 0f) * forward;
        Vector3 rightDir  = Quaternion.Euler(0f,  halfAngle, 0f) * forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + forward  * visionDetectRadius); // 中心线
        Gizmos.DrawLine(origin, origin + leftDir  * visionDetectRadius); // 左边界
        Gizmos.DrawLine(origin, origin + rightDir * visionDetectRadius); // 右边界

        // 扇形边缘插值线段（10 段，让锥形弧度更直观）
        int   arcSegments   = 10;
        float anglePerStep  = visionAngle / arcSegments;
        for (int i = 0; i < arcSegments; i++)
        {
            float   fromAngle = -halfAngle + anglePerStep * i;
            float   toAngle   = fromAngle + anglePerStep;
            Vector3 fromDir   = Quaternion.Euler(0f, fromAngle, 0f) * forward;
            Vector3 toDir     = Quaternion.Euler(0f, toAngle,   0f) * forward;
            Gizmos.DrawLine(
                origin + fromDir * visionDetectRadius,
                origin + toDir   * visionDetectRadius);
        }

        // 3. 玩家方向线（命中=红色，未命中=灰色）
        if (playerTransform != null)
        {
            Gizmos.color = lastCanDetect ? Color.red : Color.gray;
            Gizmos.DrawLine(origin, playerTransform.position);
        }
    }

    // ══════════════════════════════════════════════
    // 内部工具方法
    // ══════════════════════════════════════════════

    /// <summary>
    /// 判断玩家是否进入球形保底发现范围。
    /// 功能：纯距离比较，不受朝向影响；distance 由调用方传入以避免重复计算。
    /// 工作链路：由 CanDetectPlayer() 在 Step 2 调用；返回 true 时 CanDetectPlayer 立即返回发现结果。
    /// </summary>
    private bool IsPlayerInGuaranteeRadius(float distance)
    {
        return distance <= guaranteeDetectRadius;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 判断玩家是否处于锥形视野范围内。
    /// 功能：计算 selfTransform.forward（XZ 平面）与"玩家方向向量"的夹角，与 visionAngle 半角比较；同时更新 lastAngleToPlayer。
    /// 算法：Vector3.Angle 计算两归一化向量夹角（0~180°），Y 轴分量置零以忽略高度差，仅在水平面判断视野。
    /// 工作链路：由 CanDetectPlayer() 在 Step 4 调用；返回 true 时 CanDetectPlayer 记录 VisionCone 原因并返回 true。
    /// </summary>
    private bool IsPlayerInVisionCone()
    {
        if (playerTransform == null)
            return false;

        Vector3 toPlayer = playerTransform.position - selfTransform.position;
        toPlayer.y = 0f;

        // 玩家几乎在同一点，视为在视野内
        if (toPlayer.sqrMagnitude <= 0.001f)
        {
            lastAngleToPlayer = 0f;
            return true;
        }

        Vector3 forward = selfTransform.forward;
        forward.y = 0f;

        float angle = Vector3.Angle(forward.normalized, toPlayer.normalized);
        lastAngleToPlayer = angle;

        return angle <= visionAngle * 0.5f;
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// 统一的 Detection 模块 Debug 输出方法，受 enableDetectDebug 开关控制。
    /// 功能：在 enableDetectDebug == true 时输出带固定前缀 [SummonerDetect] 的日志，否则静默。
    /// 工作链路：被本模块所有内部函数调用；外部无需调用此方法。
    /// </summary>
    private void LogDetect(string message)
    {
        if (!enableDetectDebug)
            return;
        Debug.Log("[SummonerDetect]" + message);
    }
}
