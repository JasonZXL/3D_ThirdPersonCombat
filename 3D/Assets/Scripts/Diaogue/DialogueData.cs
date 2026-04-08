using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string speakerName;
    [TextArea(3, 5)]
    public string dialogueText;
    public AudioClip voiceClip;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue System/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] dialogueLines;
    public bool canSkip = true;
    public bool autoAdvance = false;
    public float autoAdvanceDelay = 3f;
}
