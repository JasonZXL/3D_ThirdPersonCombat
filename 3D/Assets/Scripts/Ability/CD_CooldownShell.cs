using UnityEngine;

public class CooldownShellController : MonoBehaviour
{
    [Header("引用")]
    public PlayerColorController playerController;
    public Renderer shellRenderer;

    [Header("Shader参数名")]
    public string fillProperty = "Fill";
    public string minYProperty = "MinY";
    public string heightProperty = "Height";

    [Header("可选参数")]
    public bool autoSetBounds = true; // 是否自动计算 MinY/Height

    private Material shellMat;

    void Start()
    {
        shellMat = shellRenderer.material;

        // 初始隐藏
        shellRenderer.gameObject.SetActive(false);

        if (autoSetBounds)
        {
            SetBoundsFromRenderer();
        }
    }

    void Update()
    {
        float cdPercent = playerController.CooldownPercentage;

        // 如果没有CD → 隐藏外壳
        if (cdPercent <= 0f)
        {
            if (shellRenderer.gameObject.activeSelf)
                shellRenderer.gameObject.SetActive(false);

            return;
        }

        // 有CD → 显示
        if (!shellRenderer.gameObject.activeSelf)
            shellRenderer.gameObject.SetActive(true);

        // 转换为 0→1 上升进度
        float fill = 1f - cdPercent;

        shellMat.SetFloat(fillProperty, fill);
    }

    /// <summary>
    /// 自动从 MeshRenderer 获取模型底部和高度，传给 Shader
    /// </summary>
    void SetBoundsFromRenderer()
    {
        if (shellRenderer == null)
            return;

        MeshRenderer meshRenderer = shellRenderer.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogWarning("Shell Renderer 没有 MeshRenderer 组件！");
            return;
        }

        Bounds bounds = meshRenderer.bounds;

        // bounds 是 World Space 的，如果 Shader 用 Object Space，需要转换：
        Transform t = meshRenderer.transform;
        Vector3 minLocal = t.InverseTransformPoint(bounds.min);
        Vector3 maxLocal = t.InverseTransformPoint(bounds.max);

        float minY = minLocal.y;
        float height = maxLocal.y - minLocal.y;

        shellMat.SetFloat(minYProperty, minY);
        shellMat.SetFloat(heightProperty, height);
    }
}