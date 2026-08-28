using UnityEngine;

public class Pickup : MonoBehaviour, IResettable
{
    [SerializeField] private ItemDefinition item;   //拖物品SO
    private Vector2 startPos;
    private SpriteRenderer sr;
    private Collider2D col;
    private bool taken;

    private void Start()
    {
        startPos = transform.position;
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    //碰到装备拾取 col勾trigger
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (taken) return;
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;
        taken = true;
        //放入背包里
        Inventory.Add(item);
        player.GetComponent<EquipSystem>()?.Equip(item);    //自动装备变换参数
        //FloatingTextPool.Instance.Show($"+{item.DisplayName}",transform.position);
    }

    public void ResetLevelObject()
    {

    }
}