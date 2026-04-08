using UnityEngine;
using System.Collections;

public class NPCInteract : MonoBehaviour, IInteractable
{
    [Header("对话设置")]
    public DialogueData dialogueData;
    public GameObject chatBubblePrefab; // 2D聊天气泡预制体
    public Vector3 bubbleOffset = new Vector3(0, 2f, 0); // 气泡位置偏移

    [Header("基本设置")]
    public string interactPrompt = "与NPC对话";
    
    [Header("视觉反馈")]
    public bool showDebugLogs = true;
    public ParticleSystem interactEffect;
    
    [Header("互动行为")]
    public bool facePlayerDuringInteraction = true;
    public float interactionDuration = 3.0f;
    
    // 组件引用
    private Animator _animator;
    private AudioSource _audioSource;
    private bool _isInteracting = false;
    private GameObject _interactor;
    
    // 玩家接近检测
    private bool _playerInRange = false;
    private GameObject _currentChatBubble; // 当前聊天气泡实例
    
    void Start()
    {
        // 获取组件引用
        _animator = GetComponent<Animator>();
        _audioSource = GetComponent<AudioSource>();
        
        // 如果没有AudioSource，添加一个
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
    }
    
    void Update()
    {
        // 检测玩家接近并显示聊天气泡
        if (_playerInRange && chatBubblePrefab != null && _currentChatBubble == null && !_isInteracting)
        {
            ShowChatBubble();
        }
        else if ((!_playerInRange || _isInteracting) && _currentChatBubble != null)
        {
            HideChatBubble();
        }
        
        // 确保聊天气泡跟随NPC
        if (_currentChatBubble != null)
        {
            _currentChatBubble.transform.position = transform.position + bubbleOffset;
            
            // 使聊天气泡始终面向摄像机
            _currentChatBubble.transform.rotation = Quaternion.LookRotation(
                _currentChatBubble.transform.position - Camera.main.transform.position);
        }
    }
    
    public void Interact(GameObject interactor)
    {
        // 如果正在互动中，忽略新的互动请求
        if (_isInteracting) return;
        
        // 保存互动者引用
        _interactor = interactor;
        
        // 设置互动状态
        _isInteracting = true;
        
        // 隐藏聊天气泡
        HideChatBubble();
        
        // 执行互动序列
        StartCoroutine(InteractionSequence());
    }
    
    private System.Collections.IEnumerator InteractionSequence()
    {
        // 步骤1: 面向玩家（如果需要）
        if (facePlayerDuringInteraction)
        {
            FaceInteractor();
        }
        
        // 步骤2: 播放互动动画
        PlayInteractionAnimation();
        
        // 步骤3: 播放音效
        PlayInteractionSound();
        
        // 步骤4: 显示视觉反馈
        ShowVisualFeedback();
        
        // 步骤5: 显示调试信息
        if (showDebugLogs)
        {
            Debug.Log($"与 {gameObject.name} 互动成功");
            Debug.Log($"互动者: {_interactor.name}");
        }
        
        // 步骤6: 启动对话
        if (dialogueData != null)
        {
            DialogueManager.Instance.StartDialogue(dialogueData);
            
            // 等待对话结束
            while (DialogueManager.Instance.dialoguePanel.activeInHierarchy)
            {
                yield return null;
            }
        }
        else
        {
            // 如果没有对话数据，执行自定义互动逻辑
            ExecuteCustomInteraction();
            
            // 等待互动持续时间
            yield return new WaitForSeconds(interactionDuration);
        }
        
        // 步骤7: 结束互动
        EndInteraction();
    }
    
    // 显示聊天气泡
    private void ShowChatBubble()
    {
        if (chatBubblePrefab != null)
        {
            _currentChatBubble = Instantiate(chatBubblePrefab, 
                transform.position + bubbleOffset, 
                Quaternion.identity);
            
            // 设置气泡的父对象为NPC，但保持世界坐标
            _currentChatBubble.transform.SetParent(transform, true);
        }
    }
    
    // 隐藏聊天气泡
    private void HideChatBubble()
    {
        if (_currentChatBubble != null)
        {
            Destroy(_currentChatBubble);
            _currentChatBubble = null;
        }
    }
    
    // 玩家进入触发区域
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = true;
        }
    }
    
    // 玩家离开触发区域
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = false;
        }
    }
    
    // 触发对话NPC面朝玩家
    private void FaceInteractor()
    {
        if (_interactor != null)
        {
            Vector3 direction = _interactor.transform.position - transform.position;
            direction.y = 0;
            
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
    
    private void PlayInteractionAnimation()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("Interact");
        }
    }
    
    private void PlayInteractionSound()
    {
        // 这里可以添加播放音效的逻辑
    }
    
    private void ShowVisualFeedback()
    {
        if (interactEffect != null)
        {
            interactEffect.Play();
        }
    }
    
    private void ExecuteCustomInteraction()
    {
        // 这里是您可以扩展的自定义互动逻辑
        ShowSimpleDialogue();
    }
    
    private void ShowSimpleDialogue()
    {
        Debug.Log($"{gameObject.name}: 你好，旅行者！");
    }
    
    private void EndInteraction()
    {
        _isInteracting = false;
        _interactor = null;
        
        // 如果玩家还在范围内，重新显示聊天气泡
        if (_playerInRange)
        {
            ShowChatBubble();
        }
        
        // 可以在这里添加互动结束后的逻辑
        if (_animator != null)
        {
            _animator.SetTrigger("EndInteract");
        }
    }
    
    // 可视化互动范围
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 2f);
    }
    
    // 公共方法，可供其他系统调用
    public bool CanInteract()
    {
        return !_isInteracting;
    }
    
    public void ForceEndInteraction()
    {
        EndInteraction();
    }
}