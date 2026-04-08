using UnityEngine;
using System;

public static class ColorEventBus
{
    // 调试开关
    public static bool ShowDebugLogs = false;

    // 颜色交互事件
    public static event Action<ColorInteractionEvent> OnColorInteraction;
    
    // 发布颜色交互事件
    public static void PublishColorInteraction(ColorInteractionEvent interactionEvent)
    {
        if (ShowDebugLogs)
        {
            Debug.Log($"🚀🚀🚀 ColorEventBus.PublishColorInteraction被调用");
            Debug.Log($"🚀 事件信息: {interactionEvent.Source?.name}({interactionEvent.SourceColor}) -> " +
                 $"{interactionEvent.Target?.name}({interactionEvent.TargetColor}) | 类型: {interactionEvent.Type}");
        
            // 打印调用堆栈
            Debug.Log($"🚀 调用堆栈: {Environment.StackTrace}");
        }

        // 检查是否有订阅者
        if (OnColorInteraction == null)
        {
            Debug.LogWarning($"🚀❌ 警告: OnColorInteraction事件没有订阅者!");
        }
        else
        {
            Debug.Log($"🚀✅ 事件订阅者数量: {OnColorInteraction.GetInvocationList().Length}");
        }
        
        OnColorInteraction?.Invoke(interactionEvent);
    }
    
    // 专门的方法发布不同类型的攻击
    public static void PublishEnemyAttack(GameObject enemy, GameObject player)
    {
        var attackEvent = new ColorInteractionEvent(enemy, player, ColorInteractionType.EnemyAttackPlayer);
        PublishColorInteraction(attackEvent);
    }
    
    public static void PublishPlayerAttack(GameObject player, GameObject enemy)
    {
        var attackEvent = new ColorInteractionEvent(player, enemy, ColorInteractionType.PlayerAttackEnemy);
        PublishColorInteraction(attackEvent);
    }

    public static void PublishPlayerAttackObject(GameObject player, GameObject obj)
    {
        var attackEvent = new ColorInteractionEvent(player, obj, ColorInteractionType.PlayerAttackObject);
        PublishColorInteraction(attackEvent);
    }
    
    public static void PublishCollision(GameObject source, GameObject target)
    {
        var collisionEvent = new ColorInteractionEvent(source, target, ColorInteractionType.Collision);
        PublishColorInteraction(collisionEvent);
    }
}
