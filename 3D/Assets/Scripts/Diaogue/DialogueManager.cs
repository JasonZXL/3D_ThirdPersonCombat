using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI dialogueText;
    public Image speakerAvatar;

    [Header("Settings")]
    public float typingSpeed = 0.05f;

    private DialogueData currentDialogue;
    private int currentLineIndex;
    private bool isTyping = false;
    private Coroutine typingCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        dialoguePanel.SetActive(false);
    }

    public void StartDialogue(DialogueData dialogueData)
    {
        if (dialogueData == null || dialogueData.dialogueLines.Length == 0) return;

        currentDialogue = dialogueData;
        currentLineIndex = 0;
        dialoguePanel.SetActive(true);

        DisplayLine(currentDialogue.dialogueLines[currentLineIndex]);
    }

    private void DisplayLine(DialogueLine line)
    {
        speakerText.text = line.speakerName;
        
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
            
        typingCoroutine = StartCoroutine(TypeText(line.dialogueText));

        // 设置头像（如果有）
        if (speakerAvatar != null)
        {
            // 这里可以根据说话者名称加载对应头像
            // 或者为DialogueLine添加一个Sprite字段来直接指定头像
        }

        // 播放语音（如果有）
        if (line.voiceClip != null)
        {
            AudioSource.PlayClipAtPoint(line.voiceClip, Camera.main.transform.position);
        }
    }

    private IEnumerator TypeText(string text)
    {
        isTyping = true;
        dialogueText.text = "";
        
        foreach (char letter in text.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }
        
        isTyping = false;

        // 如果启用自动前进，设置计时器
        if (currentDialogue.autoAdvance)
        {
            yield return new WaitForSeconds(currentDialogue.autoAdvanceDelay);
            NextLine();
        }
    }

    public void NextLine()
    {
        if (isTyping && currentDialogue.canSkip)
        {
            StopCoroutine(typingCoroutine);
            dialogueText.text = currentDialogue.dialogueLines[currentLineIndex].dialogueText;
            isTyping = false;
            return;
        }

        currentLineIndex++;
        
        if (currentLineIndex < currentDialogue.dialogueLines.Length)
        {
            DisplayLine(currentDialogue.dialogueLines[currentLineIndex]);
        }
        else
        {
            EndDialogue();
        }
    }

    private void EndDialogue()
    {
        dialoguePanel.SetActive(false);
        currentDialogue = null;
    }

    private void Update()
    {
        // 检测输入以推进对话
        if (dialoguePanel.activeInHierarchy && Input.GetKeyDown(KeyCode.Space))
        {
            NextLine();
        }
    }
}