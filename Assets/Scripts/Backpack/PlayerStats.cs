using UnityEngine;

//玩家基础参数SO
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Player/Stats")]
public class PlayerStats : ScriptableObject
{
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public float baseGravity = 4f;
    public int maxDashes = 1;
}