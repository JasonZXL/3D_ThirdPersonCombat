using UnityEngine;
using System.Collections.Generic;

public abstract class TutorialTask
{
    public string RoomName { get; protected set; }
    public TutorialTaskType TaskType { get; protected set; }
    public int RequiredCount { get; protected set; }
    public int CurrentProgress { get; protected set; }
    public bool IsCompleted { get; protected set; }

    protected TutorialTask(string roomName, TutorialTaskType taskType, int requiredCount)
    {
        RoomName = roomName;
        TaskType = taskType;
        RequiredCount = requiredCount;
        CurrentProgress = 0;
        IsCompleted = false;
    }

    public virtual void Initialize()
    {
        // 子类可以重写以进行初始化
    }

    public abstract bool ShouldProcessEvent(ColorInteractionEvent interaction);
    
    public abstract void ProcessEvent(ColorInteractionEvent interaction);
    
    protected void IncrementProgress()
    {
        CurrentProgress++;
        if (CurrentProgress >= RequiredCount)
        {
            IsCompleted = true;
        }
    }
}

public class BlockSameColorAttacksTask : TutorialTask
{
    public BlockSameColorAttacksTask(string roomName, int requiredCount) 
        : base(roomName, TutorialTaskType.BlockSameColorAttacks, requiredCount)
    {
    }

    public override bool ShouldProcessEvent(ColorInteractionEvent interaction)
    {
        // 检查是否为敌人攻击玩家的事件
        if (interaction.Type != ColorInteractionType.EnemyAttackPlayer)
            return false;

        // 检查是否为同色攻击
        ColorComponent sourceColor = interaction.Source?.GetComponent<ColorComponent>();
        ColorComponent targetColor = interaction.Target?.GetComponent<ColorComponent>();
        
        return sourceColor != null && targetColor != null && sourceColor.IsSameColor(targetColor);
    }

    public override void ProcessEvent(ColorInteractionEvent interaction)
    {
        // 同色敌人攻击玩家，玩家成功抵挡
        IncrementProgress();
        
        Debug.Log($"[BlockSameColorAttacksTask] 成功抵挡同色攻击! 进度: {CurrentProgress}/{RequiredCount}");
    }
}

public class AttackOppositeColorEnemiesTask : TutorialTask
{
    public AttackOppositeColorEnemiesTask(string roomName, int requiredCount) 
        : base(roomName, TutorialTaskType.AttackOppositeEnemy, requiredCount)
    {
    }

    public override bool ShouldProcessEvent(ColorInteractionEvent interaction)
    {
        // 检查是否为玩家攻击敌人的事件
        if (interaction.Type != ColorInteractionType.PlayerAttackEnemy)
            return false;

        // 检查是否为异色攻击
        ColorComponent sourceColor = interaction.Source?.GetComponent<ColorComponent>();
        ColorComponent targetColor = interaction.Target?.GetComponent<ColorComponent>();
        
        return sourceColor != null && targetColor != null && !sourceColor.IsSameColor(targetColor);
    }

    public override void ProcessEvent(ColorInteractionEvent interaction)
    {
        // 玩家成功攻击了异色敌人
        IncrementProgress();
        
        Debug.Log($"[AttackOppositeColorEnemiesTask] 成功攻击异色敌人! 进度: {CurrentProgress}/{RequiredCount}");
    }
}

public class PushObjectToEnemyTask : TutorialTask
{
    public PushObjectToEnemyTask(string roomName, int requiredCount) 
        : base(roomName, TutorialTaskType.PushObjectToEnemy, requiredCount)
    {
    }

    public override bool ShouldProcessEvent(ColorInteractionEvent interaction)
    {
        // 检查是否为物体推动击中敌人的事件
        // 假设你的系统中有这种交互类型
        if (interaction.Type != ColorInteractionType.PlayerAttackObject)
            return false;

        return true;
    }

    public override void ProcessEvent(ColorInteractionEvent interaction)
    {
        // 玩家成功推动物体撞击敌人
        IncrementProgress();
        
        Debug.Log($"[PushObjectToEnemyTask] 成功推动物体击中敌人! 进度: {CurrentProgress}/{RequiredCount}");
    }
}

public class UseColorChangeAbilityTask : TutorialTask
{
    public UseColorChangeAbilityTask(string roomName, int requiredCount) 
        : base(roomName, TutorialTaskType.UseColorChangeAbility, requiredCount) { }

    public override bool ShouldProcessEvent(ColorInteractionEvent interaction)
    {
        // 进度更新已在 TutorialTaskManager.HandleColorChanged 中直接处理，
        // 此处不需要额外逻辑，返回 false 表示不处理事件。
        return false;
    }

    public override void ProcessEvent(ColorInteractionEvent interaction)
    {
        // 不需要实现
    }
}

public class UseThrowAbilityTask : TutorialTask
{
    public UseThrowAbilityTask(string roomName, int requiredCount) 
        : base(roomName, TutorialTaskType.UseThrowAbility, requiredCount) { }

    public override bool ShouldProcessEvent(ColorInteractionEvent interaction) => false;
    public override void ProcessEvent(ColorInteractionEvent interaction) { }
}

// ============================================
// CheckpointGateTask
// ============================================
public class CheckpointGateTask : TutorialTask
{
    private List<string> requiredRoomNames;
    private Dictionary<string, bool> roomCompletionStatus;
    
    public CheckpointGateTask(string roomName, List<string> requiredRooms) 
        : base(roomName, TutorialTaskType.CheckpointGate, requiredRooms.Count)
    {
        requiredRoomNames = new List<string>(requiredRooms);
        roomCompletionStatus = new Dictionary<string, bool>();
        
        foreach (string room in requiredRoomNames)
        {
            roomCompletionStatus[room] = false;
        }
    }
    
    public override void Initialize()
    {
        // 订阅任务完成事件，持续监听
        TutorialTaskManager.OnTaskCompleted += OnAnyRoomCompleted;
        
        // 初始检查已完成的房间
        CheckAllRooms();
        
        // 初始化完成状态
        if (CurrentProgress >= RequiredCount)
        {
            IsCompleted = true;
        }
    }
    
    private void OnAnyRoomCompleted(string completedRoomName)
    {
        // 检查是否是我们监听的房间
        if (roomCompletionStatus.ContainsKey(completedRoomName))
        {
            if (!roomCompletionStatus[completedRoomName])
            {
                roomCompletionStatus[completedRoomName] = true;
                CurrentProgress++;
                
                Debug.Log($"[CheckpointGate] 房间 {completedRoomName} 完成! 进度: {CurrentProgress}/{RequiredCount}");
                
                // 检查是否全部完成
                if (CurrentProgress >= RequiredCount)
                {
                    IsCompleted = true;
                    Debug.Log($"[CheckpointGate] 所有教学关卡已完成！");
                    
                    // 触发检查点任务完成事件
                    if (TutorialTaskManager.Instance != null)
                    {
                        TutorialTaskManager.Instance.CompleteCheckpointTask(RoomName);
                    }
                }
            }
        }
    }
    
    private void CheckAllRooms()
    {
        // 检查所有房间的当前完成状态
        foreach (string roomName in requiredRoomNames)
        {
            if (TutorialTaskManager.Instance != null && TutorialTaskManager.Instance.IsRoomCompleted(roomName))
            {
                if (!roomCompletionStatus[roomName])
                {
                    roomCompletionStatus[roomName] = true;
                    CurrentProgress++;
                }
            }
        }
    }
    
    public List<string> GetIncompleteRooms()
    {
        List<string> incomplete = new List<string>();
        foreach (var kvp in roomCompletionStatus)
        {
            if (!kvp.Value)
            {
                incomplete.Add(kvp.Key);
            }
        }
        return incomplete;
    }
    
    public List<string> GetRequiredRooms()
    {
        return new List<string>(requiredRoomNames);
    }

    public override bool ShouldProcessEvent(ColorInteractionEvent interaction)
    {
        // 检查点任务不需要处理游戏事件
        return false;
    }

    public override void ProcessEvent(ColorInteractionEvent interaction)
    {
        // 检查点任务不处理事件
    }
    
    // 清理订阅
    public void Cleanup()
    {
        TutorialTaskManager.OnTaskCompleted -= OnAnyRoomCompleted;
    }
}