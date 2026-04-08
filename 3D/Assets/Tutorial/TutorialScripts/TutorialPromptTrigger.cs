using UnityEngine;
using System.Collections;

public class TutorialPromptTrigger : MonoBehaviour
{
    [Header("按键提示设置")]
    [Tooltip("按键提示的Canvas预制体")]
    public GameObject promptCanvasPrefab;
    
    [Tooltip("提示显示的位置偏移（相对于触发器）")]
    public Vector3 promptPositionOffset = new Vector3(0, 2f, 0);
    
    [Tooltip("玩家离开后提示消失的延迟时间（秒）")]
    public float disappearDelay = 0.5f;
    
    [Header("调试信息")]
    [Tooltip("是否显示调试日志")]
    public bool showDebugLogs = true;
    
    private GameObject currentPrompt;
    private GameObject player;
    private Coroutine disappearCoroutine;
    private bool isPlayerInside = false;
    
    private void Start()
    {
        // 验证设置
        if (promptCanvasPrefab == null)
        {
            Debug.LogError("❌ TutorialPromptTrigger: 未设置按键提示Canvas预制体！");
        }
        
        // 确保触发器有Collider并设置为Trigger
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("❌ TutorialPromptTrigger: 该物体缺少Collider组件！");
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning("⚠️ TutorialPromptTrigger: Collider未设置为Trigger，已自动设置");
            col.isTrigger = true;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // 检测是否是玩家
        if (other.CompareTag("Player"))
        {
            if (showDebugLogs)
            {
                Debug.Log($"✅ 玩家进入教学区域: {gameObject.name}");
            }
            
            player = other.gameObject;
            isPlayerInside = true;
            
            // 取消消失协程（如果存在）
            if (disappearCoroutine != null)
            {
                StopCoroutine(disappearCoroutine);
                disappearCoroutine = null;
            }
            
            // 显示提示
            ShowPrompt();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        // 检测玩家离开
        if (other.CompareTag("Player"))
        {
            if (showDebugLogs)
            {
                Debug.Log($"🚶 玩家离开教学区域: {gameObject.name}");
            }
            
            isPlayerInside = false;
            
            // 启动延迟消失
            if (currentPrompt != null && disappearCoroutine == null)
            {
                disappearCoroutine = StartCoroutine(HidePromptWithDelay());
            }
        }
    }
    
    private void Update()
    {
        // 如果提示存在且玩家在范围内，保持提示面向玩家
        if (currentPrompt != null && player != null && isPlayerInside)
        {
            MakePromptFacePlayer();
        }
    }
    
    private void ShowPrompt()
    {
        // 如果提示已存在，直接返回
        if (currentPrompt != null)
        {
            return;
        }
        
        if (promptCanvasPrefab == null)
        {
            Debug.LogError("❌ 无法显示提示：未设置Canvas预制体！");
            return;
        }
        
        // 计算提示显示位置
        Vector3 promptPosition = transform.position + promptPositionOffset;
        
        // 生成提示Canvas
        currentPrompt = Instantiate(promptCanvasPrefab, promptPosition, Quaternion.identity);
        
        if (showDebugLogs)
        {
            Debug.Log($"💡 生成按键提示: 位置={promptPosition}");
        }
        
        // 立即让提示面向玩家
        MakePromptFacePlayer();
    }
    
    private void MakePromptFacePlayer()
    {
        if (currentPrompt == null || player == null) return;
        
        // 使用CameraDirectionHelper计算方向
        Vector3 directionToPlayer = CameraDirectionHelper.GetKnockbackDirection(
            currentPrompt, 
            player
        );
        
        // 反转方向，因为我们要让提示面向玩家
        directionToPlayer = -directionToPlayer;
        
        // 如果方向有效，旋转提示
        if (directionToPlayer != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            currentPrompt.transform.rotation = targetRotation;
        }
    }
    
    private IEnumerator HidePromptWithDelay()
    {
        if (showDebugLogs)
        {
            Debug.Log($"⏰ 提示将在 {disappearDelay} 秒后消失");
        }
        
        yield return new WaitForSeconds(disappearDelay);
        
        // 销毁提示
        if (currentPrompt != null)
        {
            if (showDebugLogs)
            {
                Debug.Log("🗑️ 销毁按键提示");
            }
            
            Destroy(currentPrompt);
            currentPrompt = null;
        }
        
        disappearCoroutine = null;
    }
    
    private void OnDestroy()
    {
        // 清理
        if (currentPrompt != null)
        {
            Destroy(currentPrompt);
        }
    }
    
    // 可视化触发范围（编辑器中）
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        
        // 绘制触发范围
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (col is BoxCollider)
            {
                BoxCollider box = col as BoxCollider;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider)
            {
                SphereCollider sphere = col as SphereCollider;
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
        
        // 绘制提示位置
        Gizmos.color = Color.green;
        Vector3 promptPos = transform.position + promptPositionOffset;
        Gizmos.DrawSphere(promptPos, 0.2f);
        Gizmos.DrawLine(transform.position, promptPos);
    }
}
