using UnityEngine;

public static class CameraDirectionHelper
{
    // 获取从玩家指向敌人的方向（水平面）
    public static Vector3 GetKnockbackDirection(GameObject player, GameObject enemy)
    {
        if (player == null || enemy == null) return Vector3.zero;
        
        // 计算从玩家到敌人的向量，投影到水平面
        Vector3 playerToEnemy = enemy.transform.position - player.transform.position;
        playerToEnemy.y = 0; // 保持水平
        
        // 归一化得到方向
        Vector3 direction = playerToEnemy.normalized;
        
        Debug.Log($"🎯 击退方向计算: 玩家位置={player.transform.position}, 敌人位置={enemy.transform.position}, 水平方向={direction}");
        
        return direction;
    }
    
    // 获取击退目标位置（保持Y坐标不变）
    public static Vector3 GetKnockbackTargetPosition(GameObject player, GameObject enemy, float knockbackDistance)
    {
        Vector3 direction = GetKnockbackDirection(player, enemy);
        Vector3 enemyPosition = enemy.transform.position;
        
        // 计算击退目标位置（保持Y坐标不变）
        Vector3 targetPosition = enemyPosition + direction * knockbackDistance;
        targetPosition.y = enemyPosition.y; // 保持原始Y坐标
        
        Debug.Log($"🎯 击退目标位置: 当前位置={enemyPosition}, 水平目标位置={targetPosition}, 距离={knockbackDistance}");
        
        return targetPosition;
    }
    
    // 检查击退目标位置是否有效（不会让敌人穿过玩家）
    public static bool IsValidKnockbackPosition(Vector3 playerPosition, Vector3 enemyPosition, Vector3 targetPosition)
    {
        // 计算击退前后的距离变化
        float currentDistance = Vector3.Distance(playerPosition, enemyPosition);
        float targetDistance = Vector3.Distance(playerPosition, targetPosition);
        
        bool isValid = targetDistance > currentDistance;
        
        if (!isValid)
        {
            Debug.LogWarning($"⚠️ 无效击退位置: 当前距离={currentDistance}, 目标距离={targetDistance}");
        }
        
        return isValid;
    }
}
