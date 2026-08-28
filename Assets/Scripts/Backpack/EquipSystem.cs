using UnityEngine;


//装备的业务
public class EquipSystem : MonoBehaviour
{
    private PlayerController player;

    private void Start()
    {
        player = GetComponent<PlayerController>();
    }

    //同一种已安装，先替换掉旧的，再安新的
    public void Equip(ItemDefinition item)
    {
        if (player.HasEffect(item.ItemId)) player.RemoveEffect(item.ItemId);
        player.ApplyEffect(item);
    }

    //卸 按itemId移除并还原
    public void Unequip(ItemDefinition item) => player.RemoveEffect(item.ItemId);

}