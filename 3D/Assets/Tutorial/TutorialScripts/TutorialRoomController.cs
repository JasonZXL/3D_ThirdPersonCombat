using UnityEngine;
using System.Collections.Generic;

public class TutorialRoomController : MonoBehaviour
{
    [System.Serializable]
    public class TutorialRoom
    {
        public string roomName;
        public Transform roomObstaclePoint;
        public GameObject roomObstaclePrefab;
        public Transform entryTriggerPoint;
        public GameObject airWallPrefab;
        public Transform airWallSpawnPoint;
        public TutorialTaskType taskType;
        public int requiredCount;
        public string taskDescriptionFormat;
        public GameObject EnemyPrefab;
        public Transform EnemySpawnPoint;
        public int enemyCount = 1;
        public bool spawnEnemiesImmediately = true;
        
        [Header("检查点设置")]
        public List<string> prerequisiteRooms = new List<string>();
        
        [Header("延迟生成设置")]
        public bool waitForPrerequisites = false;
    }

    [Header("教程房间配置")]
    [SerializeField] private List<TutorialRoom> tutorialRooms = new List<TutorialRoom>();
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;
    
    private Dictionary<string, GameObject> spawnedRoomObstacles = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> spawnedAirWalls = new Dictionary<string, GameObject>();
    private Dictionary<string, bool> roomTaskStatus = new Dictionary<string, bool>();
    private Dictionary<string, int> roomTaskProgress = new Dictionary<string, int>();
    private GameObject currentActiveAirWall;
    private TutorialRoom currentActiveRoom;
    
    private List<GameObject> currentRoomEnemies = new List<GameObject>();
    
    // 新增：追踪等待前置条件的房间
    private Dictionary<string, TutorialRoom> waitingRooms = new Dictionary<string, TutorialRoom>();

    public static TutorialRoomController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }
    public static string GetCurrentRoomName()
    {
        return Instance?.currentActiveRoom?.roomName;
    }

    private void Start()
    {
        InitializeRoomStatus();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        TutorialTaskManager.OnTaskCompleted -= HandlePrerequisiteCompleted;
        CleanupCurrentEnemies();
    }

    private void InitializeRoomStatus()
    {
        foreach (var room in tutorialRooms)
        {
            roomTaskStatus[room.roomName] = false;
            roomTaskProgress[room.roomName] = 0;
        }
    }

    private void SubscribeToEvents()
    {
        TutorialTaskManager.OnPlayerEnteredRoom += HandlePlayerEnteredRoom;
        TutorialTaskManager.OnTaskProgressUpdated += HandleTaskProgressUpdated;
        TutorialTaskManager.OnTaskCompleted += HandleTaskCompleted;
        
        // 订阅前置任务完成事件
        TutorialTaskManager.OnTaskCompleted += HandlePrerequisiteCompleted;
    }

    private void UnsubscribeFromEvents()
    {
        TutorialTaskManager.OnPlayerEnteredRoom -= HandlePlayerEnteredRoom;
        TutorialTaskManager.OnTaskProgressUpdated -= HandleTaskProgressUpdated;
        TutorialTaskManager.OnTaskCompleted -= HandleTaskCompleted;
        TutorialTaskManager.OnTaskCompleted -= HandlePrerequisiteCompleted;
    }

    private void HandlePlayerEnteredRoom(string roomName)
    {
        Log($"玩家进入房间: {roomName}");
        
        TutorialRoom room = tutorialRooms.Find(r => r.roomName == roomName);
        if (room == null)
        {
            LogWarning($"未找到房间配置: {roomName}");
            return;
        }

        // 如果是CheckpointGate任务（纯UI监听）
        if (room.taskType == TutorialTaskType.CheckpointGate)
        {
            TutorialTaskManager.Instance.StartCheckpointGateTask(roomName, room.prerequisiteRooms);
            return;
        }

        // 如果房间任务已完成，直接返回
        if (roomTaskStatus[roomName])
        {
            Log($"房间 {roomName} 任务已完成，跳过");
            return;
        }

        // 清理上一个房间的敌人
        CleanupCurrentEnemies();
        
        // 设置当前活动房间
        currentActiveRoom = room;
        
        // 生成空气墙
        SpawnAirWall(room);

        // 生成房间障碍物
        SpawnObstacle(room);
        
        // 检查是否需要等待前置条件
        if (room.waitForPrerequisites && room.prerequisiteRooms.Count > 0)
        {
            // 检查前置房间是否已经全部完成
            bool allPrerequisitesCompleted = CheckPrerequisitesCompleted(room);
            
            if (allPrerequisitesCompleted)
            {
                // 前置已完成，立即生成敌人并开始任务
                Log($"房间 {roomName} 的前置条件已满足，立即开始任务");
                SpawnEnemiesForRoom(room);
                TutorialTaskManager.Instance.StartTutorialTask(roomName, room.taskType, room.requiredCount);
            }
            else
            {
                // 前置未完成，将房间加入等待列表
                Log($"房间 {roomName} 等待前置房间完成: {string.Join(", ", room.prerequisiteRooms)}");
                if (!waitingRooms.ContainsKey(roomName))
                {
                    waitingRooms[roomName] = room;
                }
            }
        }
        else
        {
            // 普通房间：立即处理
            if (room.spawnEnemiesImmediately)
            {
                SpawnEnemiesForCurrentRoom();
            }
            
            TutorialTaskManager.Instance.StartTutorialTask(roomName, room.taskType, room.requiredCount);
        }
    }

    // 检查房间的前置条件是否完成
    private bool CheckPrerequisitesCompleted(TutorialRoom room)
    {
        foreach (string prereqRoom in room.prerequisiteRooms)
        {
            if (!TutorialTaskManager.Instance.IsRoomCompleted(prereqRoom))
            {
                return false;
            }
        }
        return true;
    }

    // 修改：处理前置房间完成的回调
    private void HandlePrerequisiteCompleted(string completedRoomName)
    {
        Log($"检测到房间 {completedRoomName} 完成，检查等待中的房间...");
        
        // 检查所有等待中的房间
        List<string> roomsToActivate = new List<string>();
        
        foreach (var kvp in waitingRooms)
        {
            string waitingRoomName = kvp.Key;
            TutorialRoom waitingRoom = kvp.Value;
            
            // 检查这个等待房间是否需要刚完成的房间作为前置
            if (waitingRoom.prerequisiteRooms.Contains(completedRoomName))
            {
                // 检查该房间的所有前置是否都完成了
                if (CheckPrerequisitesCompleted(waitingRoom))
                {
                    roomsToActivate.Add(waitingRoomName);
                }
            }
        }
        
        // 激活所有前置条件已满足的房间
        foreach (string roomName in roomsToActivate)
        {
            TutorialRoom room = waitingRooms[roomName];
            Log($"房间 {roomName} 的所有前置房间已完成，生成敌人并开始任务！");
            
            // 生成敌人
            SpawnEnemiesForRoom(room);
            
            // 开始任务
            TutorialTaskManager.Instance.StartTutorialTask(
                room.roomName, 
                room.taskType, 
                room.requiredCount
            );
            
            // 从等待列表中移除
            waitingRooms.Remove(roomName);
        }
    }

    public void SpawnEnemiesForCurrentRoom()
    {
        if (currentActiveRoom == null)
        {
            LogWarning("当前没有活动房间，无法生成敌人");
            return;
        }

        if (currentActiveRoom.EnemyPrefab == null)
        {
            LogWarning($"房间 {currentActiveRoom.roomName} 的敌人预制体未设置");
            return;
        }

        if (currentActiveRoom.EnemySpawnPoint == null)
        {
            LogWarning($"房间 {currentActiveRoom.roomName} 的敌人生成点未设置");
            return;
        }

        CleanupCurrentEnemies();
        
        for (int i = 0; i < currentActiveRoom.enemyCount; i++)
        {
            SpawnEnemy(currentActiveRoom, i);
        }
        
        Log($"在房间 {currentActiveRoom.roomName} 生成了 {currentActiveRoom.enemyCount} 个敌人");
    }

    private void SpawnEnemiesForRoom(TutorialRoom room)
    {
        if (room.EnemyPrefab == null)
        {
            LogWarning($"房间 {room.roomName} 的敌人预制体未设置");
            return;
        }

        if (room.EnemySpawnPoint == null)
        {
            LogWarning($"房间 {room.roomName} 的敌人生成点未设置");
            return;
        }

        // 注意：不清理 currentRoomEnemies，因为可能不是当前活动房间
        // 而是为等待中的房间生成敌人
        
        for (int i = 0; i < room.enemyCount; i++)
        {
            SpawnEnemy(room, i);
        }
        
        Log($"在房间 {room.roomName} 生成了 {room.enemyCount} 个敌人");
    }

    private void SpawnEnemy(TutorialRoom room, int index)
    {
        Vector3 spawnPosition = room.EnemySpawnPoint.position;
        if (room.enemyCount > 1)
        {
            float spacing = 2.0f;
            float offsetX = (index - (room.enemyCount - 1) / 2.0f) * spacing;
            spawnPosition += new Vector3(offsetX, 0, 0);
        }
        
        GameObject enemy = Instantiate(room.EnemyPrefab, spawnPosition, room.EnemySpawnPoint.rotation);
        enemy.name = $"Enemy_{room.roomName}_{index}";
        
        currentRoomEnemies.Add(enemy);
        
        Log($"生成敌人: {enemy.name} 在位置: {spawnPosition}");
    }

    private void SpawnAirWall(TutorialRoom room)
    {
        if (spawnedAirWalls.ContainsKey(room.roomName) && spawnedAirWalls[room.roomName] != null)
        {
            Destroy(spawnedAirWalls[room.roomName]);
        }

        if (room.airWallPrefab != null && room.airWallSpawnPoint != null)
        {
            GameObject airWall = Instantiate(room.airWallPrefab, room.airWallSpawnPoint.position, 
                room.airWallSpawnPoint.rotation);
            airWall.name = $"AirWall_{room.roomName}";
            
            spawnedAirWalls[room.roomName] = airWall;
            currentActiveAirWall = airWall;
            
            Log($"生成空气墙: {airWall.name}");
        }
    }

    private void SpawnObstacle(TutorialRoom room)
    {
        if (spawnedRoomObstacles.ContainsKey(room.roomName) && spawnedRoomObstacles[room.roomName] != null)
        {
            Destroy(spawnedRoomObstacles[room.roomName]);
        }

        if (room.roomObstaclePrefab != null && room.roomObstaclePoint != null)
        {
            GameObject obstacle = Instantiate(room.roomObstaclePrefab, room.roomObstaclePoint.position, 
                room.roomObstaclePoint.rotation);
            obstacle.name = $"Obstacle_{room.roomName}";
            
            spawnedRoomObstacles[room.roomName] = obstacle;
            
            Log($"生成房间障碍物: {obstacle.name}");
        }
    }

    private void HandleTaskProgressUpdated(string roomName, int currentProgress, int requiredCount)
    {
        roomTaskProgress[roomName] = currentProgress;
        TutorialTaskManager.Instance.UpdateTaskUI(roomName, currentProgress, requiredCount);
    }

    private void HandleTaskCompleted(string roomName)
    {
        Log($"房间 {roomName} 任务完成!");
        
        roomTaskStatus[roomName] = true;
        RemoveAirWall(roomName);
        RemoveObstacle(roomName);
        
        // 只在完成的是当前活动房间时才清理敌人
        if (currentActiveRoom != null && currentActiveRoom.roomName == roomName)
        {
            CleanupCurrentEnemies();
            currentActiveRoom = null;
            currentActiveAirWall = null;
        }
    }

    private void RemoveAirWall(string roomName)
    {
        if (spawnedAirWalls.ContainsKey(roomName) && spawnedAirWalls[roomName] != null)
        {
            Destroy(spawnedAirWalls[roomName]);
            spawnedAirWalls.Remove(roomName);
            Log($"移除空气墙: {roomName}");
        }
    }

    private void RemoveObstacle(string roomName)
    {
        if (spawnedRoomObstacles.ContainsKey(roomName) && spawnedRoomObstacles[roomName] != null)
        {
            Destroy(spawnedRoomObstacles[roomName]);
            spawnedRoomObstacles.Remove(roomName);
            Log($"移除房间障碍物: {roomName}");
        }
    }

    private void CleanupCurrentEnemies()
    {
        foreach (GameObject enemy in currentRoomEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        currentRoomEnemies.Clear();
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[TutorialRoomController] {message}");
        }
    }

    private void LogWarning(string message)
    {
        if (showDebugLogs)
        {
            Debug.LogWarning($"[TutorialRoomController] {message}");
        }
    }
}
