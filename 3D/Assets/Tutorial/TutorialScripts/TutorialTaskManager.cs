using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public enum TutorialTaskType
{
    BlockSameColorAttacks,    // 成功抵挡同色敌人攻击
    AttackOppositeEnemy,      // 攻击异色敌人
    PushObjectToEnemy,        // 推动物体撞击敌人
    UseColorChangeAbility,    // 使用变色技能
    UseThrowAbility,          // 使用扔掷技能（包含拾取和扔出）
    CheckpointGate,          // 检查点大门 - 持续监听前置任务完成状态
}

public class TutorialTaskManager : MonoBehaviour
{
    #region Singleton
    public static TutorialTaskManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region Events
    public static event Action<string> OnPlayerEnteredRoom;               // 玩家进入房间
    public static event Action<string, int, int> OnTaskProgressUpdated;   // 任务进度更新
    public static event Action<string> OnTaskCompleted;                   // 任务完成

    public static event Action<string> OnColorChanged;      // 玩家使用技能
    public static event Action<string> OnObjectPickedUp;    // 玩家拾取物体
    public static event Action<string> OnObjectThrown;      // 玩家扔出物体
    
    // 用于外部触发房间进入
    public static void TriggerRoomEnter(string roomName)
    {
        OnPlayerEnteredRoom?.Invoke(roomName);
    }

    public static void TriggerColorChanged(string roomName) => OnColorChanged?.Invoke(roomName);
    public static void TriggerObjectPickedUp(string roomName) => OnObjectPickedUp?.Invoke(roomName);
    public static void TriggerObjectThrown(string roomName) => OnObjectThrown?.Invoke(roomName);
    #endregion

    #region UI Reference
    [Header("UI References")]
    [SerializeField] private GameObject taskUIPrefab;
    [SerializeField] private GameObject checkpointUIPrefab;
    [SerializeField] private Transform taskUIParent;
    
    private Dictionary<string, TutorialTaskUI> activeTaskUIs = new Dictionary<string, TutorialTaskUI>();
    private TutorialTaskUI checkpointUI; // Checkpoint UI引用
    #endregion

    #region Task Tracking
    private Dictionary<string, TutorialTask> activeTasks = new Dictionary<string, TutorialTask>();
    private Dictionary<string, int> taskProgress = new Dictionary<string, int>();
    private Dictionary<string, int> taskRequirements = new Dictionary<string, int>();
    private Dictionary<string, TutorialTaskType> roomTaskTypes = new Dictionary<string, TutorialTaskType>(); // 房间任务类型映射
    private HashSet<string> completedRooms = new HashSet<string>(); // 记录已完成的房间
    #endregion

    #region Task Configuration
    [System.Serializable]
    public class TaskConfig
    {
        public TutorialTaskType taskType;
        public string descriptionFormat;
        public Color completeColor = Color.green;  // 完成任务时的文字颜色
    }

    [Header("任务配置")]
    [SerializeField] private List<TaskConfig> taskConfigs = new List<TaskConfig>();
    
    private Dictionary<TutorialTaskType, TaskConfig> taskConfigMap = new Dictionary<TutorialTaskType, TaskConfig>();
    #endregion

    private void OnEnable()
    {
        OnColorChanged += HandleColorChanged;
        OnObjectPickedUp += HandleObjectPickedUp;
        OnObjectThrown += HandleObjectThrown;
    }

    private void OnDisable()
    {
        OnColorChanged -= HandleColorChanged;
        OnObjectPickedUp -= HandleObjectPickedUp;
        OnObjectThrown -= HandleObjectThrown;
    }

    private void HandleColorChanged(string roomName)
    {
        if (!activeTasks.ContainsKey(roomName)) return;
        var taskType = roomTaskTypes[roomName];
        if (taskType != TutorialTaskType.UseColorChangeAbility) return;

        // 进度直接加1（如果已完成则不再累加）
        int current = taskProgress[roomName];
        if (current >= taskRequirements[roomName]) return;

        taskProgress[roomName] = current + 1;
        UpdateTaskUI(roomName, taskProgress[roomName], taskRequirements[roomName]);

        if (taskProgress[roomName] >= taskRequirements[roomName])
            CompleteTask(roomName);
    }

    private void HandleObjectPickedUp(string roomName)
    {
        if (!activeTasks.ContainsKey(roomName)) return;
        var taskType = roomTaskTypes[roomName];
        if (taskType != TutorialTaskType.UseThrowAbility) return;

        int current = taskProgress[roomName];
        if (current >= taskRequirements[roomName]) return;

        taskProgress[roomName] = current + 1;
        UpdateTaskUI(roomName, taskProgress[roomName], taskRequirements[roomName]);

        if (taskProgress[roomName] >= taskRequirements[roomName])
            CompleteTask(roomName);
    }

    private void HandleObjectThrown(string roomName)
    {
        if (!activeTasks.ContainsKey(roomName)) return;
        var taskType = roomTaskTypes[roomName];
        if (taskType != TutorialTaskType.UseThrowAbility) return;

        int current = taskProgress[roomName];
        if (current >= taskRequirements[roomName]) return;

        taskProgress[roomName] = current + 1;
        UpdateTaskUI(roomName, taskProgress[roomName], taskRequirements[roomName]);

        if (taskProgress[roomName] >= taskRequirements[roomName])
            CompleteTask(roomName);
    }
    private void Initialize()
    {
        // 初始化任务配置映射
        foreach (var config in taskConfigs)
        {
            taskConfigMap[config.taskType] = config;
        }

        // 订阅颜色交互事件来跟踪任务进度
        SubscribeToGameEvents();
    }

    private void SubscribeToGameEvents()
    {
        // 订阅颜色交互事件
        ColorEventBus.OnColorInteraction += HandleColorInteraction;
    }

    private void UnsubscribeFromGameEvents()
    {
        ColorEventBus.OnColorInteraction -= HandleColorInteraction;
    }

    private void OnDestroy()
    {
        // 清理所有检查点任务的订阅
        foreach (var kvp in activeTasks)
        {
            if (kvp.Value is CheckpointGateTask checkpointTask)
            {
                checkpointTask.Cleanup();
            }
        }
        
        UnsubscribeFromGameEvents();
    }

    public void StartTutorialTask(string roomName, TutorialTaskType taskType, int requiredCount)
    {
        if (activeTasks.ContainsKey(roomName))
        {
            Debug.LogWarning($"房间 {roomName} 已有进行中的任务");
            return;
        }

        // 记录房间任务类型
        roomTaskTypes[roomName] = taskType;

        // 创建新任务
        TutorialTask task = CreateTask(taskType, roomName, requiredCount);
        if (task == null)
        {
            Debug.LogError($"无法创建任务类型: {taskType}");
            return;
        }

        // 存储任务
        activeTasks[roomName] = task;
        taskProgress[roomName] = 0;
        taskRequirements[roomName] = requiredCount;

        // 如果有Checkpoint UI，则更新Checkpoint UI中的房间信息
        if (checkpointUI != null)
        {
            // 获取任务描述格式
            string description = GetTaskDescription(taskType);
            checkpointUI.UpdateRoomInCheckpoint(roomName, description, 0, requiredCount);
        }
        else
        {
            // 如果没有Checkpoint UI，则创建单独的UI
            CreateTaskUI(roomName, taskType, requiredCount);
        }

        // 初始化任务
        task.Initialize();

        Debug.Log($"开始教程任务: {roomName}, 类型: {taskType}, 需要: {requiredCount}次");
    }

    private TutorialTask CreateTask(TutorialTaskType taskType, string roomName, int requiredCount, List<string> prerequisiteRooms = null)
    {
        switch (taskType)
        {
            case TutorialTaskType.BlockSameColorAttacks:
                return new BlockSameColorAttacksTask(roomName, requiredCount);
            case TutorialTaskType.AttackOppositeEnemy:
                return new AttackOppositeColorEnemiesTask(roomName, requiredCount);
            case TutorialTaskType.PushObjectToEnemy:
                return new PushObjectToEnemyTask(roomName, requiredCount);
            case TutorialTaskType.UseColorChangeAbility:
                return new UseColorChangeAbilityTask(roomName, requiredCount);
            case TutorialTaskType.UseThrowAbility:
                return new UseThrowAbilityTask(roomName, requiredCount);
            case TutorialTaskType.CheckpointGate:
                if (prerequisiteRooms == null || prerequisiteRooms.Count == 0)
                {
                    Debug.LogError($"检查点任务 {roomName} 缺少前置房间配置！");
                    return null;
                }
                return new CheckpointGateTask(roomName, prerequisiteRooms);
            default:
                return null;
        }
    }

    private void CreateTaskUI(string roomName, TutorialTaskType taskType, int requiredCount)
    {
        if (taskUIPrefab == null || taskUIParent == null)
        {
            Debug.LogWarning("任务UI预制体或父级未设置");
            return;
        }

        // 实例化UI
        GameObject uiObject = Instantiate(taskUIPrefab, taskUIParent);
        TutorialTaskUI taskUI = uiObject.GetComponent<TutorialTaskUI>();
        
        if (taskUI == null)
        {
            Debug.LogError("任务UI预制体缺少TutorialTaskUI组件");
            Destroy(uiObject);
            return;
        }

        // 获取任务描述
        string description = GetTaskDescription(taskType);
        
        // 初始化UI，传入基础描述格式
        taskUI.Initialize(roomName, $"{description} (0/{requiredCount})");
        
        activeTaskUIs[roomName] = taskUI;
    }

    private string GetTaskDescription(TutorialTaskType taskType)
    {
        if (taskConfigMap.TryGetValue(taskType, out TaskConfig config))
        {
            return config.descriptionFormat;
        }
        else
        {
            return $"任务: {taskType}";
        }
    }

    private void HandleColorInteraction(ColorInteractionEvent interaction)
    {
        // 遍历所有活动任务，检查是否有任务需要处理此事件
        foreach (var kvp in activeTasks)
        {
            string roomName = kvp.Key;
            TutorialTask task = kvp.Value;
            
            if (task.ShouldProcessEvent(interaction))
            {
                task.ProcessEvent(interaction);
                
                // 更新进度
                taskProgress[roomName] = task.CurrentProgress;
                
                // 更新UI显示
                if (checkpointUI != null)
                {
                    // 更新Checkpoint UI中的房间信息
                    string description = GetTaskDescription(roomTaskTypes[roomName]);
                    checkpointUI.UpdateRoomInCheckpoint(roomName, description, 
                        task.CurrentProgress, taskRequirements[roomName]);
                }
                else
                {
                    // 更新独立UI
                    UpdateTaskUI(roomName, task.CurrentProgress, taskRequirements[roomName]);
                }
                
                // 触发进度更新事件
                OnTaskProgressUpdated?.Invoke(roomName, task.CurrentProgress, taskRequirements[roomName]);
                
                // 检查是否完成
                if (task.IsCompleted)
                {
                    CompleteTask(roomName);
                }
                
                // 一个事件可能只被一个任务处理
                break;
            }
        }
    }

    private void CompleteTask(string roomName)
    {
        if (!activeTasks.ContainsKey(roomName))
        {
            return;
        }

        // 获取任务类型用于完成UI
        TutorialTaskType taskType = activeTasks[roomName].TaskType;
        
        // 记录房间完成状态
        completedRooms.Add(roomName);
        Debug.Log($"房间 {roomName} 已标记为完成");
        
        // 触发完成事件（这会通知所有监听的检查点任务）
        OnTaskCompleted?.Invoke(roomName);
        
        // 如果是检查点任务，特殊处理
        if (taskType == TutorialTaskType.CheckpointGate)
        {
            // 对于检查点任务，我们只需要标记UI为完成
            if (checkpointUI != null)
            {
                // 所有房间完成后，Checkpoint UI会显示完成状态
                // 这已经由TutorialTaskUI处理了
            }
        }
        else
        {
            // 对于普通任务，更新UI
            if (checkpointUI != null)
            {
                // 在Checkpoint UI中标记房间为完成
                checkpointUI.MarkRoomAsCompleteInCheckpoint(roomName);
            }
            else
            {
                // 完成独立UI显示
                CompleteTaskUI(roomName, taskType);
            }
        }

        // 清理任务
        if (taskType != TutorialTaskType.CheckpointGate)
        {
            activeTasks.Remove(roomName);
            taskProgress.Remove(roomName);
            taskRequirements.Remove(roomName);
            roomTaskTypes.Remove(roomName);
            
            // 移除独立UI
            if (activeTaskUIs.ContainsKey(roomName))
            {
                StartCoroutine(DelayedRemoveUI(roomName, 2.5f));
            }
        }
        else
        {
            // 检查点任务完成后也要清理，但延迟一段时间让用户看到完成状态
            StartCoroutine(DelayedCompleteCheckpoint(roomName, 3f));
        }

        Debug.Log($"任务完成: {roomName}");
    }

    private IEnumerator DelayedCompleteCheckpoint(string roomName, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 检查点任务完成后的清理
        if (activeTasks.ContainsKey(roomName))
        {
            activeTasks.Remove(roomName);
            taskProgress.Remove(roomName);
            taskRequirements.Remove(roomName);
            roomTaskTypes.Remove(roomName);
            
            // 移除Checkpoint UI
            RemoveCheckpointUI();
        }
    }
    public void UpdateTaskUI(string roomName, int currentProgress, int requiredCount)
    {
        if (activeTaskUIs.TryGetValue(roomName, out TutorialTaskUI taskUI))
        {
            taskUI.UpdateProgress(currentProgress, requiredCount);
        }
    }

    private void CompleteTaskUI(string roomName, TutorialTaskType taskType)
    {
        if (activeTaskUIs.TryGetValue(roomName, out TutorialTaskUI taskUI))
        {
            // 获取完成颜色
            Color completeColor = Color.green; // 默认完成颜色为绿色
            if (taskConfigMap.TryGetValue(taskType, out TaskConfig config))
            {
                completeColor = config.completeColor;
            }
            
            taskUI.MarkAsComplete(completeColor);
        }
    }

    private IEnumerator DelayedRemoveUI(string roomName, float delay)
    {
        // 等待显示完成状态
        yield return new WaitForSeconds(delay);
        
        if (activeTaskUIs.TryGetValue(roomName, out TutorialTaskUI taskUI))
        {
            if (taskUI != null)
            {
                // 触发淡出动画
                taskUI.HideUI();
                
                // 等待淡出动画完成后再从字典中移除
                yield return new WaitForSeconds(0.5f);
                
                activeTaskUIs.Remove(roomName);
            }
        }
    }

    // 检查房间是否已完成
    public bool IsRoomCompleted(string roomName)
    {
        return completedRooms.Contains(roomName);
    }

    // 启动检查点门任务
    public void StartCheckpointGateTask(string roomName, List<string> prerequisiteRooms)
    {
        if (checkpointUI != null)
        {
            Debug.LogWarning($"已经存在Checkpoint UI");
            return;
        }

        // 创建检查点UI
        CreateCheckpointUI(roomName, prerequisiteRooms);
        
        Debug.Log($"开始检查点门任务: {roomName}, 监听房间: {string.Join(", ", prerequisiteRooms)}");
    }

    // 创建检查点UI（显示待完成列表）
    private void CreateCheckpointUI(string checkpointRoomName, List<string> prerequisiteRooms)
    {
        if (checkpointUIPrefab == null || taskUIParent == null)
        {
            Debug.LogWarning("Checkpoint UI预制体或父级未设置");
            return;
        }

        GameObject uiObject = Instantiate(checkpointUIPrefab, taskUIParent);
        TutorialTaskUI taskUI = uiObject.GetComponent<TutorialTaskUI>();
        
        if (taskUI == null)
        {
            Debug.LogError("Checkpoint UI预制体缺少TutorialTaskUI组件");
            Destroy(uiObject);
            return;
        }

        // 初始化Checkpoint UI
        taskUI.InitializeAsCheckpoint(checkpointRoomName, prerequisiteRooms);
        
        checkpointUI = taskUI;
        
        // 如果已经有房间任务在进行，更新这些房间的信息
        foreach (var roomName in prerequisiteRooms)
        {
            if (activeTasks.ContainsKey(roomName))
            {
                string description = GetTaskDescription(roomTaskTypes[roomName]);
                checkpointUI.UpdateRoomInCheckpoint(roomName, description, 
                    taskProgress[roomName], taskRequirements[roomName]);
            }
        }
    }

    public void CompleteCheckpointTask(string checkpointRoomName)
    {
        if (!activeTasks.ContainsKey(checkpointRoomName))
        {
            return;
        }

        // 标记检查点任务完成
        CompleteTask(checkpointRoomName);
    }

    // 移除Checkpoint UI
    public void RemoveCheckpointUI()
    {
        if (checkpointUI != null)
        {
            checkpointUI.HideUI();
            checkpointUI = null;
        }
    }
}