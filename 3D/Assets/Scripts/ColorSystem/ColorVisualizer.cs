using UnityEngine;


public class ColorVisualizer : MonoBehaviour
{
    [Header("视觉设置")]
    [SerializeField] private Renderer enemyRenderer;
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Material redMaterial;
    [SerializeField] private Material blueMaterial;

    [Header("调试设置")]
    [SerializeField] private bool showDebugLogs = true;
    

    private ColorComponent colorComponent;
    
    private void Awake()
    {
        colorComponent = GetComponent<ColorComponent>();
        
        if (enemyRenderer == null)
        {
            enemyRenderer = GetComponentInChildren<Renderer>();
        }

        if (playerRenderer == null)
        {
            enemyRenderer = GetComponentInChildren<Renderer>();
        }
        
        if (colorComponent != null)
        {
            colorComponent.OnColorChanged += OnColorChanged;
            UpdateVisualColor(colorComponent.CurrentColor);
        }

        if (showDebugLogs)
        {
            Debug.Log($"🎨 ColorVisualizer 初始化: {gameObject.name}");
        }
    }
    
    private void OnDestroy()
    {
        if (colorComponent != null)
        {
            colorComponent.OnColorChanged -= OnColorChanged;
        }
    }
    
    private void OnColorChanged(ColorType oldColor, ColorType newColor)
    {
        UpdateVisualColor(newColor);
        if (showDebugLogs)
        {
            Debug.Log($"🎨 颜色可视化更新: {oldColor} -> {newColor}");
        }
    }
    
    private void UpdateVisualColor(ColorType color)
    {
        if (enemyRenderer == null) return;
        
        switch (color)
        {
            case ColorType.Red:
                enemyRenderer.material = redMaterial;
                break;
            case ColorType.Blue:
                enemyRenderer.material = blueMaterial;
                break;
        }

        if (playerRenderer == null) return;
        
        switch (color)
        {
            case ColorType.Red:
                playerRenderer.material = redMaterial;
                break;
            case ColorType.Blue:
                playerRenderer.material = blueMaterial;
                break;
        }
    }
    
}

