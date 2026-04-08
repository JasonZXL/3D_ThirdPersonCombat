using UnityEngine;

public class AutoDestroyEffect : MonoBehaviour
{
    public float delay = 2f; // 手动设置等待时间

    void Start()
    {
        // 或自动获取粒子系统的最长持续时间
        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>();
        float maxDuration = 0f;
        foreach (var ps in systems)
        {
            if (ps.main.duration > maxDuration)
                maxDuration = ps.main.duration;
        }
        if (maxDuration > 0f)
            delay = maxDuration;

        // 开始延时销毁
        Destroy(gameObject, delay);
    }
}
