using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonMove2 : MonoBehaviour
{
    #region 移动配置参数
    
    [Header("移动速度")]
    // 移动速度变量
    public float walkspeed = 4.0f;          // 基础行走速度
    public float runspeed = 8.0f;           // 跑步速度
    
    // 旋转平滑参数
    public float rotationSmoothTime = 0.12f; // 角色转向平滑时间
    
    #endregion
    
    #region 组件引用
    
    private CharacterController _controller; // 角色控制器组件
    private Animator _animator;             // 动画控制器组件
    private GameObject _mainCamera;         // 主摄像机引用
    private SlowMotionManager slowMotionManager;   // 慢动作管理器引用
    
    #endregion
    
    #region 输入状态变量
    
    private bool _pressingW = false;        // W键按下状态
    private bool _pressingA = false;        // A键按下状态
    private bool _pressingS = false;        // S键按下状态
    private bool _pressingD = false;        // D键按下状态
    private bool _pressingShift = false;    // Shift键按下状态
    
    private Vector2 _move;                  // 输入移动向量
    private bool _wasRunning = false;       // 上一帧是否在奔跑
    
    #endregion
    
    #region 移动计算变量
    
    private Vector3 _cameraForward;         // 摄像机前方向（水平）
    private Vector3 _cameraRight;           // 摄像机右方向（水平）
    private Vector3 _moveDirection;         // 最终移动方向
    private float _rotationVelocity;        // 旋转平滑速度
    
    #endregion
    
    #region 互动系统变量
    
    private float _interactCooldown = 0f;   // 互动冷却计时器
    
    #endregion
    
    #region 判断移动状态
    
    /// <summary>获取玩家是否正在移动</summary>
    public bool IsMoving
    {
        get
        {
            return _pressingW || _pressingA || _pressingS || _pressingD || 
                   Mathf.Abs(_move.x) > 0.1f || Mathf.Abs(_move.y) > 0.1f;
        }
    }
    
    #endregion
    
    #region 初始化和更新方法
    
    /// <summary>初始化组件引用</summary>
    void Start()
    {
        if (_mainCamera == null)
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }

        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        slowMotionManager = SlowMotionManager.Instance;
    }
    
    /// <summary>每帧更新移动和互动逻辑</summary>
    void Update()
    {
        UpdateThirdPersonMovement();
    }
    
    #endregion
    
    #region 移动控制系统
    
    /// <summary>更新第三人称移动逻辑</summary>
    private void UpdateThirdPersonMovement()
    {
        CalculateCameraDirections();
        UpdateShiftKeyState();
        
        float moveMagnitude = _move.magnitude;
        _animator.SetFloat("Speed", moveMagnitude);
        
        ResetMoveDirection();
        
        if (IsMoving)
        {
            ProcessMovementInput();
        }
        else
        {
            _animator.SetInteger("moveDirection", 0);
            
            // 当停止移动时，确保奔跑状态被重置
            if (_wasRunning)
            {
                _animator.SetBool("isRunning", false);
                _wasRunning = false;
            }
        }
        
        ApplyMovement();
    }
    
    /// <summary>计算摄像机水平方向向量</summary>
    private void CalculateCameraDirections()
    {
        if (_mainCamera == null) return;
        
        _cameraForward = _mainCamera.transform.forward;
        _cameraRight = _mainCamera.transform.right;
        _cameraForward.y = 0;
        _cameraRight.y = 0;
        
        // 确保向量不是零向量
        if (_cameraForward.sqrMagnitude < 0.01f)
        {
            _cameraForward = transform.forward;
        }
        if (_cameraRight.sqrMagnitude < 0.01f)
        {
            _cameraRight = transform.right;
        }
        
        _cameraForward.Normalize();
        _cameraRight.Normalize();
    }
    
    /// <summary>更新Shift键状态（跑步/行走切换）</summary>
    private void UpdateShiftKeyState()
    {
        _pressingShift = Keyboard.current.leftShiftKey.isPressed;
    }
    
    /// <summary>重置移动方向变量</summary>
    private void ResetMoveDirection()
    {
        _moveDirection = Vector3.zero;
    }
    
    /// <summary>处理移动输入并计算移动方向</summary>
    private void ProcessMovementInput()
    {
        Vector2 inputDirection = GetCombinedInputDirection();
        
        if (inputDirection.magnitude > 1)
        {
            inputDirection.Normalize();
        }
        
        // 确保摄像机方向向量有效
        if (_cameraForward.sqrMagnitude < 0.01f || _cameraRight.sqrMagnitude < 0.01f)
        {
            CalculateCameraDirections();
        }
        
        _moveDirection = (_cameraForward * inputDirection.y + _cameraRight * inputDirection.x).normalized;
        
        // 确保移动方向有效
        if (_moveDirection.sqrMagnitude < 0.01f && inputDirection.magnitude > 0.1f)
        {
            _moveDirection = transform.forward;
        }
        
        RotateTowardsMoveDirection(inputDirection);
        
        float currentSpeed = CalculateCurrentSpeed(inputDirection);
        int moveDirection = 1; 
        
        _animator.SetInteger("moveDirection", moveDirection); 
        
        // 更新奔跑状态
        bool shouldBeRunning = _pressingShift && IsMoving && inputDirection.magnitude > 0.1f;
        _animator.SetBool("isRunning", shouldBeRunning);
        _wasRunning = shouldBeRunning; 
    }
    
    /// <summary>获取组合输入方向向量</summary>
    private Vector2 GetCombinedInputDirection()
    {
        Vector2 inputDirection = Vector2.zero;
        
        if (_pressingW) inputDirection.y += 1;
        if (_pressingS) inputDirection.y -= 1;
        if (_pressingA) inputDirection.x -= 1;
        if (_pressingD) inputDirection.x += 1;
        
        return inputDirection;
    }
    
    /// <summary>旋转角色朝向移动方向</summary>
    private void RotateTowardsMoveDirection(Vector2 inputDirection)
    {
        if (inputDirection.magnitude < 0.1f) return;
        
        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.y) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _rotationVelocity, rotationSmoothTime);
        transform.rotation = Quaternion.Euler(0.0f, angle, 0.0f);
    }
    
    /// <summary>根据移动方向和状态计算当前速度</summary>
    private float CalculateCurrentSpeed(Vector2 inputDirection)
    {
        float baseSpeed = _pressingShift ? runspeed : walkspeed;
        
        if (Mathf.Abs(inputDirection.x) > 0.1f) // 斜向
            return baseSpeed * 0.9f;
        else // 直线
            return baseSpeed;
    }
    
    /// <summary>应用最终移动到角色控制器</summary>
    private void ApplyMovement()
    {
        Vector3 velocity = new Vector3(0, -1, 0); // 基础重力
        
        if (_moveDirection.magnitude > 0.1f)
        {
            float currentSpeed = _pressingShift ? runspeed : walkspeed;
            velocity += _moveDirection * currentSpeed * Time.deltaTime;
        }

        float deltaTime = slowMotionManager != null && slowMotionManager.IsSlowMotionActive() 
            ? Time.unscaledDeltaTime 
            : Time.deltaTime;
        
        _controller.Move(velocity);
    }
    
    #endregion
    
    #region 输入回调方法
    
    /// <summary>Input System移动输入回调</summary>
    void OnMove(InputValue value)
    {
        _move = value.Get<Vector2>();
        
        // 更新按键状态
        _pressingW = _move.y > 0.1f;
        _pressingS = _move.y < -0.1f;
        _pressingA = _move.x < -0.1f;
        _pressingD = _move.x > 0.1f;
    }
    
    #endregion
    
    #region 公共方法
    
    /// <summary>获取当前移动方向（标准化）</summary>
    public Vector3 GetMoveDirection()
    {
        return _moveDirection;
    }
    
    /// <summary>强制设置角色移动方向（用于外部控制）</summary>
    public void SetMoveDirection(Vector3 direction)
    {
        _moveDirection = direction.normalized;
    }
    
    #endregion
}
