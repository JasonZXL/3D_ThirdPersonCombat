using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class ThirdPersonCam2 : MonoBehaviour
{
    #region 摄像机配置参数
    
    [Header("基础设置")]
    public Transform CameraTarget;          // 摄像机跟随的目标
    public float TopClamp = 70.0f;          // 垂直旋转上限角度
    public float BottomClamp = -30.0f;      // 垂直旋转下限角度
    public float sensitivity = 0.1f;        // 摄像机旋转灵敏度
    
    [Header("距离控制")]
    public float defaultDistance = 5f;      // 默认摄像机距离
    public float minDistance = 2f;          // 最小摄像机距离
    public float maxDistance = 10f;         // 最大摄像机距离
    public float zoomSpeed = 5f;            // 缩放速度
    
    [Header("行为模式")]
    public bool idleOrbit = true;           // 空闲时摄像机环绕目标
    public bool followWhenMoving = true;    // 移动时摄像机跟随目标
    public bool rotateCharacterWhenMoving = true; // 移动时旋转角色朝向摄像机
    
    [Header("平滑设置")]
    public float movementThreshold = 0.1f;  // 移动检测阈值
    public float rotationSmoothTime = 0.12f; // 角色旋转平滑时间
    public float zoomSmoothTime = 0.1f;     // 缩放平滑时间
    
    [Header("防穿透设置")]
    public float sphereCastRadius = 0.3f;   // SphereCast检测半径
    public float occlusionBuffer = 0.1f;    // 防穿透缓冲距离（避免紧贴墙面抖动）
    public float occlusionSmoothTime = 0.1f; // 防穿透距离平滑时间
    public Vector3 pivotOffset = new Vector3(0, 1.5f, 0); // 摄像机枢轴点偏移（通常从目标中心向上偏移）
    public LayerMask occlusionLayers = -1;  // 防穿透检测层（默认所有层）
    public List<string> ignoreTags = new List<string>(); // 忽略的Tag（如Player、IgnoreRaycast等）
    
    #endregion
    
    #region 输入状态变量
    
    private Vector2 _look;                  // 鼠标视角输入
    private float _scrollInput;             // 滚轮缩放输入
    
    #endregion
    
    #region 摄像机状态变量
    
    private float _cinemachineTargetYaw;    // 水平旋转目标角度
    private float _cinemachineTargetPitch;  // 垂直旋转目标角度
    private float _currentDistance;         // 当前摄像机距离
    private float _targetDistance;          // 目标摄像机距离
    private float _occlusionAdjustedDistance; // 防穿透调整后的距离
    
    private Vector3 _lastTargetPosition;    // 上一帧目标位置
    private bool _isTargetMoving = false;   // 目标移动状态
    
    #endregion
    
    #region 平滑过渡变量
    
    private float _rotationVelocity;        // 角色旋转平滑速度
    private float _distanceVelocity;        // 距离变化平滑速度
    private float _occlusionDistanceVelocity; // 防穿透距离平滑速度
    
    #endregion
    
    #region 组件引用
    
    private Camera _mainCamera;             // 主摄像机引用
    private Transform _cameraTransform;     // 摄像机变换组件
    
    #endregion
    
    #region 常量
    
    private const float _threshold = 0.01f; // 输入阈值
    private const float _maxPitchClamp = 89.0f; // 最大垂直角度限制（避免90度）
    
    #endregion
    
    #region Unity生命周期方法
    
    /// <summary>初始化摄像机状态和组件</summary>
    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            _cameraTransform = _mainCamera.transform;
        }
        
        if (CameraTarget != null)
        {
            _cinemachineTargetYaw = CameraTarget.transform.rotation.eulerAngles.y;
            _cinemachineTargetPitch = 0f; // 初始化为0度，避免垂直角度过大
        }
        
        _currentDistance = _targetDistance = _occlusionAdjustedDistance = defaultDistance;
        
        if (CameraTarget != null)
        {
            _lastTargetPosition = CameraTarget.position;
        }
        
        // 如果没有设置忽略Tag，添加一些常用默认值
        if (ignoreTags.Count == 0)
        {
            ignoreTags.Add("Player");
            ignoreTags.Add("IgnoreRaycast");
        }
    }
    
    /// <summary>在LateUpdate中更新摄像机位置（避免抖动）</summary>
    private void LateUpdate()
    {
        if (CameraTarget == null) return;
        
        UpdateTargetMovementState();
        HandleInput();
        UpdateCamera();
        
        if (rotateCharacterWhenMoving)
        {
            UpdateCharacterRotation();
        }
    }
    
    #endregion
    
    #region 目标状态检测系统
    
    /// <summary>检测目标是否在移动</summary>
    private void UpdateTargetMovementState()
    {
        Vector3 currentPosition = CameraTarget.position;
        float movementDelta = Vector3.Distance(currentPosition, _lastTargetPosition);
        
        _isTargetMoving = movementDelta > movementThreshold;
        _lastTargetPosition = currentPosition;
    }
    
    #endregion
    
    #region 输入处理系统
    
    /// <summary>处理所有摄像机输入（鼠标和滚轮）</summary>
    private void HandleInput()
    {
        ProcessScrollInput();
        SmoothDistanceTransition();
    }
    
    /// <summary>处理鼠标滚轮缩放输入</summary>
    private void ProcessScrollInput()
    {
        _scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(_scrollInput) > 0.01f)
        {
            _targetDistance -= _scrollInput * zoomSpeed;
            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        }
    }
    
    /// <summary>平滑过渡摄像机距离变化</summary>
    private void SmoothDistanceTransition()
    {
        _currentDistance = Mathf.SmoothDamp(_currentDistance, _targetDistance, 
            ref _distanceVelocity, zoomSmoothTime);
    }
    
    #endregion
    
    #region 摄像机控制系统
    
    /// <summary>更新摄像机位置和旋转</summary>
    private void UpdateCamera()
    {
        ProcessMouseRotation();
        ClampCameraAngles();
        HandleCameraOcclusion();
        ApplyCameraTransform();
    }
    
    /// <summary>处理鼠标旋转输入并根据状态应用</summary>
    private void ProcessMouseRotation()
    {
        if (_look.sqrMagnitude >= _threshold)
        {
            // 空闲模式或关闭跟随时：摄像机自由旋转
            if ((idleOrbit && !_isTargetMoving) || !followWhenMoving)
            {
                _cinemachineTargetYaw += _look.x * sensitivity;
                _cinemachineTargetPitch -= _look.y * sensitivity;
            }
            // 移动模式：同时影响摄像机和角色
            else if (followWhenMoving && _isTargetMoving)
            {
                _cinemachineTargetYaw += _look.x * sensitivity;
                _cinemachineTargetPitch -= _look.y * sensitivity;
            }
        }
    }
    
    /// <summary>限制摄像机旋转角度在合理范围内</summary>
    private void ClampCameraAngles()
    {
        // 确保角度不会接近90度，避免摄像机完全垂直
        float actualTopClamp = Mathf.Min(TopClamp, _maxPitchClamp);
        float actualBottomClamp = Mathf.Max(BottomClamp, -_maxPitchClamp);
        
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, actualBottomClamp, actualTopClamp);
    }
    
    /// <summary>防穿透系统：检测遮挡并调整摄像机距离</summary>
    private void HandleCameraOcclusion()
    {
        // 计算理想摄像机位置
        Vector3 desiredPosition = CalculateCameraPositionWithDistance(_currentDistance);
        Vector3 pivotPosition = CameraTarget.position + pivotOffset;
        
        // 计算从枢轴点到理想位置的方向和距离
        Vector3 directionToCamera = (desiredPosition - pivotPosition).normalized;
        float distanceToDesired = Vector3.Distance(pivotPosition, desiredPosition);
        
        // 执行SphereCast检测遮挡
        RaycastHit[] hits = Physics.SphereCastAll(
            pivotPosition,
            sphereCastRadius,
            directionToCamera,
            distanceToDesired,
            occlusionLayers,
            QueryTriggerInteraction.Ignore
        );
        
        // 找到最近的遮挡物（排除目标自身和忽略的Tag）
        float closestHitDistance = distanceToDesired;
        bool hasValidHit = false;
        
        foreach (RaycastHit hit in hits)
        {
            // 跳过忽略的Tag
            if (ignoreTags.Contains(hit.collider.tag)) continue;
            
            // 跳过摄像机目标自身
            if (hit.collider.transform.IsChildOf(CameraTarget)) continue;
            
            // 跳过摄像机自身（避免自检测）
            if (hit.collider.transform == _cameraTransform) continue;
            
            // 更新最近的有效命中距离
            if (hit.distance < closestHitDistance)
            {
                closestHitDistance = hit.distance;
                hasValidHit = true;
            }
        }
        
        // 根据检测结果调整距离
        if (hasValidHit)
        {
            // 从命中点向后退一点，避免紧贴墙面抖动
            float adjustedDistance = closestHitDistance - occlusionBuffer;
            adjustedDistance = Mathf.Max(adjustedDistance, minDistance);
            
            // 平滑过渡到调整后的距离
            _occlusionAdjustedDistance = Mathf.SmoothDamp(
                _occlusionAdjustedDistance,
                adjustedDistance,
                ref _occlusionDistanceVelocity,
                occlusionSmoothTime
            );
        }
        else
        {
            // 没有遮挡，平滑恢复到目标距离
            _occlusionAdjustedDistance = Mathf.SmoothDamp(
                _occlusionAdjustedDistance,
                _currentDistance,
                ref _occlusionDistanceVelocity,
                occlusionSmoothTime
            );
        }
    }
    
    /// <summary>应用计算后的摄像机变换</summary>
    private void ApplyCameraTransform()
    {
        Vector3 cameraPosition = CalculateCameraPositionWithDistance(_occlusionAdjustedDistance);
        
        if (_cameraTransform != null)
        {
            _cameraTransform.position = cameraPosition;
            _cameraTransform.LookAt(CameraTarget.position + pivotOffset);
        }
    }
    
    /// <summary>根据指定距离计算球形摄像机位置</summary>
    private Vector3 CalculateCameraPositionWithDistance(float distance)
    {
        // 确保角度在安全范围内
        float pitch = Mathf.Clamp(_cinemachineTargetPitch, -89f, 89f);
        float yaw = _cinemachineTargetYaw;
        
        // 正确的球坐标系计算公式：
        // x = distance * sin(yaw) * cos(pitch)
        // y = distance * sin(pitch)
        // z = distance * cos(yaw) * cos(pitch)
        
        float radYaw = yaw * Mathf.Deg2Rad;
        float radPitch = pitch * Mathf.Deg2Rad;
        
        // 计算球坐标点
        float x = distance * Mathf.Sin(radYaw) * Mathf.Cos(radPitch);
        float y = distance * Mathf.Sin(radPitch);
        float z = distance * Mathf.Cos(radYaw) * Mathf.Cos(radPitch);
        
        // 返回相对于目标枢轴点的位置
        Vector3 pivotPosition = CameraTarget.position + pivotOffset;
        return pivotPosition + new Vector3(x, y, z);
    }
    
    /// <summary>计算理想摄像机位置（使用当前目标距离）</summary>
    private Vector3 CalculateDesiredCameraPosition()
    {
        return CalculateCameraPositionWithDistance(_currentDistance);
    }
    
    #endregion
    
    #region 角色朝向控制系统
    
    /// <summary>根据摄像机方向更新角色朝向</summary>
    private void UpdateCharacterRotation()
    {
        if (!_isTargetMoving) return;
        
        Vector3 cameraForward = _mainCamera?.transform.forward ?? Vector3.forward;
        cameraForward.y = 0;
        
        if (cameraForward.sqrMagnitude >= _threshold)
        {
            float targetAngle = Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(CameraTarget.eulerAngles.y, targetAngle, 
                ref _rotationVelocity, rotationSmoothTime);
            
            CameraTarget.rotation = Quaternion.Euler(0.0f, angle, 0.0f);
        }
    }
    
    #endregion
    
    #region 工具方法
    
    /// <summary>将角度限制在指定范围内并处理360度循环</summary>
    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
    
    /// <summary>可视化调试方法（在Scene视图中绘制）</summary>
    private void OnDrawGizmosSelected()
    {
        if (CameraTarget == null) return;
        
        // 绘制枢轴点
        Vector3 pivotPos = CameraTarget.position + pivotOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pivotPos, 0.1f);
        
        // 绘制摄像机位置
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_cameraTransform.position, 0.2f);
            
            // 绘制SphereCast检测范围
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Vector3 desiredPos = CalculateDesiredCameraPosition();
            Vector3 direction = (desiredPos - pivotPos).normalized;
            float distance = Vector3.Distance(pivotPos, desiredPos);
            
            // 绘制检测胶囊
            Gizmos.DrawWireSphere(pivotPos, sphereCastRadius);
            Gizmos.DrawWireSphere(pivotPos + direction * distance, sphereCastRadius);
            
            // 连接线
            Gizmos.DrawLine(pivotPos, pivotPos + direction * distance);
        }
    }
    
    #endregion
    
    #region 输入回调方法
    
    /// <summary>Input System视角输入回调</summary>
    public void OnLook(InputValue value)
    {
        _look = value.Get<Vector2>();
    }
    
    #endregion
    
    #region 公共方法接口
    
    /// <summary>设置摄像机跟随目标</summary>
    public void SetTarget(Transform newTarget)
    {
        CameraTarget = newTarget;
    }
    
    /// <summary>设置摄像机距离（可选择立即应用）</summary>
    public void SetDistance(float distance, bool immediate = false)
    {
        _targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        if (immediate)
        {
            _currentDistance = _targetDistance;
            _occlusionAdjustedDistance = _targetDistance;
        }
    }
    
    /// <summary>设置摄像机角度（可选择立即应用）</summary>
    public void SetAngles(float yaw, float pitch, bool immediate = false)
    {
        _cinemachineTargetYaw = yaw;
        _cinemachineTargetPitch = Mathf.Clamp(pitch, BottomClamp, TopClamp);
    }
    
    /// <summary>获取当前摄像机距离</summary>
    public float GetCurrentDistance()
    {
        return _currentDistance;
    }
    
    /// <summary>获取当前摄像机角度（水平,垂直）</summary>
    public Vector2 GetCurrentAngles()
    {
        return new Vector2(_cinemachineTargetYaw, _cinemachineTargetPitch);
    }
    
    /// <summary>获取目标是否在移动</summary>
    public bool IsTargetMoving()
    {
        return _isTargetMoving;
    }
    
    /// <summary>添加忽略Tag</summary>
    public void AddIgnoreTag(string tag)
    {
        if (!ignoreTags.Contains(tag))
        {
            ignoreTags.Add(tag);
        }
    }
    
    /// <summary>移除忽略Tag</summary>
    public void RemoveIgnoreTag(string tag)
    {
        ignoreTags.Remove(tag);
    }
    
    #endregion
}