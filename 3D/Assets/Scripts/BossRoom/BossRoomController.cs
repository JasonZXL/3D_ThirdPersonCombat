using UnityEngine;
using System.Collections.Generic;

public class BossRoomController : MonoBehaviour
{
    [System.Serializable]
    public class BossRoom
    {
        public string roomName;
        public Transform roomObstaclePoint;
        public GameObject roomObstaclePrefab;
        public Transform entryTriggerPoint;
        public GameObject airWallPrefab;
        public Transform airWallSpawnPoint;
        public GameObject EnemyPrefab;
        public Transform EnemySpawnPoint;
        public int enemyCount = 1;
        public bool spawnEnemiesImmediately = true;
    }

    [Header("Boss房间配置")]
    [SerializeField] private List<BossRoom> bossRooms = new List<BossRoom>();
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;
    
    private Dictionary<string, GameObject> spawnedRoomObstacles = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> spawnedAirWalls = new Dictionary<string, GameObject>();
    private Dictionary<string, bool> roomActivated = new Dictionary<string, bool>();
    private GameObject currentActiveAirWall;
    private BossRoom currentActiveRoom;
    
    private List<GameObject> currentRoomEnemies = new List<GameObject>();

    private void Start()
    {
        InitializeRoomStatus();
        SetupTriggers();
    }

    private void OnDestroy()
    {
        CleanupCurrentEnemies();
    }

    private void InitializeRoomStatus()
    {
        foreach (var room in bossRooms)
        {
            roomActivated[room.roomName] = false;
        }
    }

    private void SetupTriggers()
    {
        foreach (var room in bossRooms)
        {
            if (room.entryTriggerPoint != null)
            {
                // 为每个入口点添加触发器组件
                var collider = room.entryTriggerPoint.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                
                // 添加触发器脚本
                var triggerScript = room.entryTriggerPoint.gameObject.AddComponent<BossRoomTrigger>();
                triggerScript.roomName = room.roomName;
                triggerScript.controller = this;
            }
        }
    }

    public void HandlePlayerEnteredRoom(string roomName)
    {
        Log($"玩家进入房间: {roomName}");
        
        BossRoom room = bossRooms.Find(r => r.roomName == roomName);
        if (room == null)
        {
            LogWarning($"未找到房间配置: {roomName}");
            return;
        }

        // 如果房间已经激活过，直接返回
        if (roomActivated[roomName])
        {
            Log($"房间 {roomName} 已经激活过，跳过");
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
        
        // 标记房间为已激活
        roomActivated[roomName] = true;
        
        // 生成敌人
        if (room.spawnEnemiesImmediately)
        {
            SpawnEnemiesForCurrentRoom();
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

    private void SpawnEnemy(BossRoom room, int index)
    {
        Vector3 spawnPosition = room.EnemySpawnPoint.position;
        if (room.enemyCount > 1)
        {
            float spacing = 2.0f;
            float offsetX = (index - (room.enemyCount - 1) / 2.0f) * spacing;
            spawnPosition += new Vector3(offsetX, 0, 0);
        }
        
        GameObject enemy = Instantiate(room.EnemyPrefab, spawnPosition, room.EnemySpawnPoint.rotation);
        var bossHealth = enemy.GetComponent<HealthSystem>();
        
        // 使用静态引用
        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.BindBoss(bossHealth);
            Log($"成功绑定Boss血量UI: {enemy.name}");
        }
        else
        {
            Debug.LogError($"❌ BossHealthBarUI实例未找到！请确保Boss血条UI在场景中");
        }
        
        enemy.name = $"Enemy_{room.roomName}_{index}";
        currentRoomEnemies.Add(enemy);
        Log($"生成敌人: {enemy.name} 在位置: {spawnPosition}");
    }

    private void SpawnAirWall(BossRoom room)
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

    private void SpawnObstacle(BossRoom room)
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

    // 手动移除空气墙（例如在敌人全部被消灭后调用）
    public void RemoveAirWall(string roomName)
    {
        if (spawnedAirWalls.ContainsKey(roomName) && spawnedAirWalls[roomName] != null)
        {
            Destroy(spawnedAirWalls[roomName]);
            spawnedAirWalls.Remove(roomName);
            Log($"移除空气墙: {roomName}");
        }
    }

    // 手动移除障碍物
    public void RemoveObstacle(string roomName)
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
            Debug.Log($"[BossRoomController] {message}");
        }
    }

    private void LogWarning(string message)
    {
        if (showDebugLogs)
        {
            Debug.LogWarning($"[BossRoomController] {message}");
        }
    }
}

// 简单的触发器脚本
public class BossRoomTrigger : MonoBehaviour
{
    public string roomName;
    public BossRoomController controller;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            controller.HandlePlayerEnteredRoom(roomName);
        }
    }
}
