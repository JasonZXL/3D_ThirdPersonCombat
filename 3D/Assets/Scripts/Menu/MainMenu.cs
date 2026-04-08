using UnityEngine;
using UnityEngine.SceneManagement;  // 用于加载场景

public class MainMenu : MonoBehaviour
{
    // 开始游戏：加载下一个场景（或指定名称的场景）
    public void StartGame()
    {
        // 这里可以替换为你的游戏场景名称
        SceneManager.LoadScene("Tutorial");
    }

    // 退出游戏
    public void QuitGame()
    {
        // 如果在编辑器中测试，退出无效，但会输出日志
        Debug.Log("退出游戏");
        Application.Quit();
    }
}