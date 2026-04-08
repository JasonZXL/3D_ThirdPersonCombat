using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TutorialTaskUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI taskText;      // 任务文字
    [SerializeField] private Image checkMarkImage;          // 勾号图片
    [SerializeField] private GameObject checkMarkObject;    // 勾号GameObject（可选）

    [Header("动画设置")]
    [SerializeField] private float fadeDuration = 0.3f;     // 渐变时间

    private string roomName;
    private CanvasGroup canvasGroup;
    private bool isHiding = false;  // 防止重复触发淡出

    // 房间任务列表：按顺序存储房间名，便于保持顺序
    private List<string> roomTaskOrder = new List<string>();
    
    // 房间任务详情字典：房间名 -> (是否完成, 任务描述, 当前进度, 总进度)
    private Dictionary<string, (bool isCompleted, string description, int current, int total)> roomTaskDetails = 
        new Dictionary<string, (bool, string, int, int)>();
    
    private bool isCheckpointUI = false; // 是否为Checkpoint UI

    private void Awake()
    {
        // 获取CanvasGroup用于淡入淡出
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    // 普通房间UI初始化
    public void Initialize(string roomName, string description)
    {
        this.roomName = roomName;
        
        // 隐藏勾号
        SetCheckMarkVisible(false);
        
        // 初始化为透明并开始淡入
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            StartCoroutine(FadeIn());
        }
        
        // 如果是普通房间任务，直接显示描述
        if (taskText != null)
        {
            taskText.text = description;
        }
    }

    // Checkpoint UI初始化
    public void InitializeAsCheckpoint(string checkpointRoomName, List<string> roomNames)
    {
        this.roomName = checkpointRoomName;
        this.isCheckpointUI = true;
        
        // 初始化房间顺序和任务状态
        foreach (var room in roomNames)
        {
            roomTaskOrder.Add(room);
            roomTaskDetails[room] = (false, $"Complete the tutorial {room}", 0, 0);
        }
        
        // 隐藏勾号
        SetCheckMarkVisible(false);
        
        // 初始化为透明并开始淡入
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            StartCoroutine(FadeIn());
        }
        
        // 更新UI显示
        UpdateCheckpointDisplay();
    }

    private IEnumerator FadeIn()
    {
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    public void UpdateProgress(int current, int total)
    {
        if (taskText != null && !isCheckpointUI)
        {
            // 普通房间UI显示描述 + 进度
            taskText.text = $"{taskText.text.Split('(')[0].Trim()} ({current}/{total})";
        }
    }

    public void MarkAsComplete(Color completeColor)
    {
        // 显示勾号
        SetCheckMarkVisible(true);
        
        // 改变文字颜色
        if (taskText != null && !isCheckpointUI)
        {
            taskText.color = completeColor;
        }
        
        // 播放简单的完成动画
        StartCoroutine(CompleteAnimation());
    }

    private IEnumerator CompleteAnimation()
    {
        // 简单的缩放动画
        if (checkMarkImage != null)
        {
            Vector3 originalScale = checkMarkImage.transform.localScale;
            float duration = 0.3f;
            float elapsedTime = 0f;
            
            // 放大
            while (elapsedTime < duration)
            {
                float scale = Mathf.Lerp(1f, 1.3f, elapsedTime / duration);
                checkMarkImage.transform.localScale = new Vector3(scale, scale, scale);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 缩小回原样
            elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                float scale = Mathf.Lerp(1.3f, 1f, elapsedTime / duration);
                checkMarkImage.transform.localScale = new Vector3(scale, scale, scale);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            checkMarkImage.transform.localScale = originalScale;
        }
    }

    // 更新Checkpoint UI显示
    private void UpdateCheckpointDisplay()
    {
        if (taskText != null && isCheckpointUI)
        {
            string displayText = "Complete all tutorial rooms:\n";
            int index = 1;
            
            // 只显示未完成的房间
            foreach (var roomName in roomTaskOrder)
            {
                if (!roomTaskDetails.ContainsKey(roomName)) continue;
                
                var details = roomTaskDetails[roomName];
                
                // 只显示未完成的房间
                if (!details.isCompleted)
                {
                    string roomText = $"{index}. {details.description}";
                    
                    // 如果有进度信息，添加进度显示
                    if (details.total > 0)
                    {
                        roomText += $" ({details.current}/{details.total})";
                    }
                    
                    displayText += roomText + "\n";
                    index++;
                }
            }
            
            // 如果所有房间都已完成，显示完成信息
            if (index == 1) // 没有未完成的任务
            {
                displayText = "All tutorials completed!";
                SetCheckMarkVisible(true);
                StartCoroutine(CompleteAnimation());
            }
            
            taskText.text = displayText;
        }
    }

    // 更新Checkpoint中特定房间的信息
    public void UpdateRoomInCheckpoint(string roomName, string description, int current, int total)
    {
        if (isCheckpointUI && roomTaskDetails.ContainsKey(roomName))
        {
            var details = roomTaskDetails[roomName];
            // 只更新未完成房间的信息
            if (!details.isCompleted)
            {
                roomTaskDetails[roomName] = (false, description, current, total);
                UpdateCheckpointDisplay();
            }
        }
    }

    // 标记Checkpoint中特定房间为完成
    public void MarkRoomAsCompleteInCheckpoint(string roomName)
    {
        if (isCheckpointUI && roomTaskDetails.ContainsKey(roomName))
        {
            var details = roomTaskDetails[roomName];
            roomTaskDetails[roomName] = (true, details.description, details.current, details.total);
            UpdateCheckpointDisplay(); // 这会从显示中移除已完成的任务
        }
    }

    private void SetCheckMarkVisible(bool visible)
    {
        // 使用GameObject激活/禁用
        if (checkMarkObject != null)
        {
            checkMarkObject.SetActive(visible);
        }
        
        // 或者使用Image组件
        if (checkMarkImage != null)
        {
            checkMarkImage.gameObject.SetActive(visible);
        }
    }

    public void HideUI()
    {
        // 防止重复调用
        if (isHiding) return;
        
        isHiding = true;
        // 淡出动画
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        if (canvasGroup != null)
        {
            float elapsedTime = 0f;
            float startAlpha = canvasGroup.alpha;
            
            while (elapsedTime < fadeDuration)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsedTime / fadeDuration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }
        
        // 动画完成后销毁
        Destroy(gameObject);
    }
}