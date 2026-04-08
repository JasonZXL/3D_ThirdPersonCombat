#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class HeartCreator : MonoBehaviour
{
    [MenuItem("Tools/UI/创建心形血量系统")]
    public static void CreateHeartHealthSystem()
    {
        Debug.Log("❤️ 开始创建心形血量系统...");
        
        // 1. 创建玩家血条UI
        CreatePlayerHeartUI();
        
        // 2. 创建心形预制体
        CreateHeartPrefab();
        
        // 3. 创建敌人血条预制体
        CreateEnemyHeartUIPrefab();
        
        Debug.Log("✅ 心形血量系统创建完成！");
    }
    
    private static void CreatePlayerHeartUI()
    {
        // 确保有Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // 创建EventSystem
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        
        // 创建玩家血条容器
        GameObject playerHeartsContainer = new GameObject("PlayerHeartsContainer", typeof(RectTransform));
        playerHeartsContainer.transform.SetParent(canvas.transform);
        
        // 设置容器位置（屏幕左上角）
        RectTransform containerRect = playerHeartsContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1);
        containerRect.anchorMax = new Vector2(0, 1);
        containerRect.pivot = new Vector2(0, 1);
        containerRect.anchoredPosition = new Vector2(20, -20);
        
        Debug.Log("✅ 玩家心形容器创建完成");
    }
    
    private static void CreateHeartPrefab()
    {
        // 创建心形游戏对象
        GameObject heart = new GameObject("Heart", typeof(Image));
        
        // 设置RectTransform
        RectTransform rect = heart.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(50, 50);
        
        // 添加Outline效果
        Outline outline = heart.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        
        // 保存为预制体
        string prefabPath = "Assets/HeartPrefab.prefab";
        PrefabUtility.SaveAsPrefabAsset(heart, prefabPath);
        DestroyImmediate(heart);
        
        Debug.Log($"✅ 心形预制体创建完成: {prefabPath}");
    }
    
    private static void CreateEnemyHeartUIPrefab()
    {
        // 创建敌人的UI Canvas
        GameObject enemyUI = new GameObject("EnemyHealthUI", typeof(Canvas), typeof(CanvasScaler));
        
        // 配置Canvas为World Space
        Canvas canvas = enemyUI.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        
        // 调整Canvas大小
        RectTransform canvasRect = enemyUI.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 50);
        
        // 创建心形容器
        GameObject heartsContainer = new GameObject("HeartsContainer", typeof(RectTransform));
        heartsContainer.transform.SetParent(enemyUI.transform);
        
        RectTransform containerRect = heartsContainer.GetComponent<RectTransform>();
        containerRect.anchoredPosition = Vector2.zero;
        
        // 保存为预制体
        string prefabPath = "Assets/EnemyHealthUI.prefab";
        PrefabUtility.SaveAsPrefabAsset(enemyUI, prefabPath);
        DestroyImmediate(enemyUI);
        
        Debug.Log($"✅ 敌人血条UI预制体创建完成: {prefabPath}");
    }
}
#endif
