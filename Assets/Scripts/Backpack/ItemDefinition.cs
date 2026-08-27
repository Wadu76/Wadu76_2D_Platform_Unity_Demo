using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//SO实现接口
public class ItemDefinition : ScriptableObject, IItemEffect
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private float moveSpeedMul = 1f;
    [SerializeField] private float jumpForceMul = 1f;
    [SerializeField] private float gravityMul = 1f;
    [SerializeField] private int dashBouns = 0;

    //接口数据契约 need to be public
    public string ItemId => itemId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public float MoveSpeedMul => moveSpeedMul;
    public float JumpForceMul => jumpForceMul;
    public float GravityMul => gravityMul;
    public int DashBonus => dashBouns;

    //行为钩 以后可以播音效/特效
    public void Apply(PlayerController player) { }
    public void Remove(PlayerController player) { }

}
