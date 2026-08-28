using System.Collections.Generic;

public static class Inventory
{
    private static readonly List<ItemDefinition> items = new();
    public static void Add(ItemDefinition item) => items.Add(item);
    public static bool Remove(ItemDefinition item) => items.Remove(item);


    //计算有几个物品
    public static int CountOf(ItemDefinition item)
    {
        int n = 0;
        foreach (var it in items) if (it == item) n++;
        return n;
    }

    //去重列表，每种物品一个UI
    public static List<ItemDefinition> GetAllDistinct()
    {
        var seen = new HashSet<ItemDefinition>();
        var result = new List<ItemDefinition>();
        foreach (var it in items)
            if (seen.Add(it)) result.Add(it);
        return result;
    }
}