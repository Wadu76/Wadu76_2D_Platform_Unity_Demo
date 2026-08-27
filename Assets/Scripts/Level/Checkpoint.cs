using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;                 // 用来点亮视觉
    [SerializeField] private Color activeColor = Color.green;   // 激活后颜色
    [SerializeField] private Color inactiveColor = new Color(0.6f, 0.6f, 0.6f); // 未激活灰

    private bool isActive;

    private void Start()
    {
        sr.color = inactiveColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isActive) return;                       // 已激活就忽略
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;
        isActive = true;
        sr.color = activeColor;                     // 点亮
        GameState.spawnPoint = transform.position;  // 更新重生点
        GameState.hasSpawnPoint = true;
    }
}