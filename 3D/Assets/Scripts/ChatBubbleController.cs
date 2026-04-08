using UnityEngine;
using TMPro;
using System.Collections;

public class ChatBubbleController : MonoBehaviour
{
    public TextMeshProUGUI textComponent;
    
    public float floatHeight = 0.2f;
    public float floatSpeed = 1f;
    
    private Vector3 originalPosition;
    
    void Start()
    {
        originalPosition = transform.localPosition;
        
        
        // 启动浮动动画
        StartCoroutine(FloatAnimation());
    }
    
    private IEnumerator FloatAnimation()
    {
        while (true)
        {
            float newY = originalPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
            transform.localPosition = new Vector3(originalPosition.x, newY, originalPosition.z);
            yield return null;
        }
    }
    
    // 更新消息内容
    public void SetMessage(string message)
    {
        if (textComponent != null)
        {
            textComponent.text = message;
        }
    }
}