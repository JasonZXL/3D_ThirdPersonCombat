using UnityEngine;

public class RoomEntryTrigger : MonoBehaviour
{
    [Header("房间设置")]
    [SerializeField] private string roomName;
    [SerializeField] private TutorialTaskType expectedTaskType;
    
    [Header("触发器设置")]
    [SerializeField] private bool oneTimeOnly = true;
    [SerializeField] private LayerMask playerLayer = 1 << 0; // Default layer
    
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (oneTimeOnly && hasTriggered)
            return;
            
        // 检查是否为玩家
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            if (other.CompareTag("Player"))
            {
                TriggerRoomEntry();
            }
        }
    }

    private void TriggerRoomEntry()
    {
        Debug.Log($"玩家进入房间: {roomName}");
        
        // 触发房间进入事件
        TutorialTaskManager.TriggerRoomEnter(roomName);
        
        hasTriggered = true;
        
        // 可选: 禁用触发器
        if (oneTimeOnly)
        {
            GetComponent<Collider>().enabled = false;
        }
    }
}
