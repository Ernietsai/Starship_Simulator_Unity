using UnityEngine;

[System.Serializable]

public class RocketEngine : MonoBehaviour
{
    public Transform engineTransform; // 引擎掛載點的 Transform
    public float currentThrust;       // 當前推力
    public Vector2 currentGimbal;     // 當前偏轉方向 (x, y)

    void Awake()
    {
        // 如果沒手動指定，就預設為自己
        if (engineTransform == null) engineTransform = this.transform;
    }
}
