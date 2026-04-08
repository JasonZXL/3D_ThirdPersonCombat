using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelExit : MonoBehaviour
{
    [Header("传送设置")]
    [Tooltip("目标场景名称")]
    public string targetSceneName;
    
    [Tooltip("允许传送的标签")]
    public string targetTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }
}
