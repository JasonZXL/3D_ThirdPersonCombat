using UnityEngine;

public static class BilliardPhysicsHelper
{
    /// <summary>
    /// 计算水平面台球碰撞后的速度方向（忽略Y轴）
    /// </summary>
    public static void CalculateBilliardCollisionHorizontal(
        Vector3 cueBallPos,
        Vector3 cueBallVel,
        Vector3 targetBallPos,
        float radius,
        out Vector3 cueBallNewVel,
        out Vector3 targetBallNewVel)
    {
        // 将位置投影到水平面（Y=0）
        Vector3 cueBallPosFlat = new Vector3(cueBallPos.x, 0, cueBallPos.z);
        Vector3 targetBallPosFlat = new Vector3(targetBallPos.x, 0, targetBallPos.z);
        
        // 将速度投影到水平面（Y=0）
        Vector3 cueBallVelFlat = new Vector3(cueBallVel.x, 0, cueBallVel.z);
        
        // 计算水平面上的碰撞法线
        Vector3 collisionNormal = (targetBallPosFlat - cueBallPosFlat).normalized;
        
        // 计算母球速度在法线方向上的分量
        float vn = Vector3.Dot(cueBallVelFlat, collisionNormal);
        
        // 如果速度方向远离目标球，没有碰撞效应
        if (vn <= 0)
        {
            cueBallNewVel = cueBallVelFlat;
            targetBallNewVel = Vector3.zero;
            return;
        }
        
        // 台球碰撞公式（质量相同）：
        // 目标球获得法线方向的速度：v_target = vn * n
        // 母球失去法线方向的速度：v_cue_new = v_cue - vn * n
        
        cueBallNewVel = cueBallVelFlat - vn * collisionNormal;
        targetBallNewVel = vn * collisionNormal;
    }
    
    /// <summary>
    /// 计算水平面撞击角度（忽略Y轴）
    /// </summary>
    public static float CalculateImpactAngleHorizontal(Vector3 cueBallVelocity, Vector3 cueToTarget)
    {
        // 将向量投影到水平面
        Vector3 velDir = new Vector3(cueBallVelocity.x, 0, cueBallVelocity.z).normalized;
        Vector3 targetDir = new Vector3(cueToTarget.x, 0, cueToTarget.z).normalized;
        
        // 计算角度余弦值
        float cosAngle = Vector3.Dot(velDir, targetDir);
        
        return cosAngle;
    }
    
    /// <summary>
    /// 计算水平反射方向（忽略Y轴）
    /// </summary>
    public static Vector3 CalculateReflectionHorizontal(Vector3 incidentDir, Vector3 normal)
    {
        // 投影到水平面
        Vector3 incidentFlat = new Vector3(incidentDir.x, 0, incidentDir.z).normalized;
        Vector3 normalFlat = new Vector3(normal.x, 0, normal.z).normalized;
        
        // 反射公式: R = I - 2*(I·N)*N
        float dot = Vector3.Dot(incidentFlat, normalFlat);
        return incidentFlat - 2 * dot * normalFlat;
    }
    
    /// <summary>
    /// 计算水平面击退目标位置（保持原始Y坐标）
    /// </summary>
    public static Vector3 CalculateHorizontalTargetPosition(
        Vector3 currentPosition,
        Vector3 direction,
        float distance,
        float originalY)
    {
        // 方向投影到水平面
        Vector3 dirFlat = new Vector3(direction.x, 0, direction.z).normalized;
        
        // 计算水平位移
        Vector3 horizontalDisplacement = dirFlat * distance;
        
        // 返回保持原始Y坐标的位置
        return new Vector3(
            currentPosition.x + horizontalDisplacement.x,
            originalY,
            currentPosition.z + horizontalDisplacement.z
        );
    }
}
