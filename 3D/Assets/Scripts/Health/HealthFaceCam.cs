using UnityEngine;

public class HealthFaceCam : MonoBehaviour
{
    [Header("朝向设置")]
    [SerializeField] private bool useMainCamera = true;
    [SerializeField] private Transform targetCamera;
    [SerializeField] private bool onlyYAxis = true; // 只围绕Y轴旋转
    
    private void Awake()
    {
        if (useMainCamera && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }
    }
    
    private void LateUpdate()
    {
        if (targetCamera == null) return;
        
        if (onlyYAxis)
        {
            // 只围绕Y轴旋转，保持UI水平
            Vector3 lookDirection = targetCamera.position - transform.position;
            lookDirection.y = 0; // 锁定Y轴
            
            if (lookDirection != Vector3.zero)
            {
                Quaternion rotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = rotation;
            }
        }
        else
        {
            // 完全朝向相机
            transform.LookAt(transform.position + targetCamera.rotation * Vector3.forward,
                           targetCamera.rotation * Vector3.up);
        }
    }
}
