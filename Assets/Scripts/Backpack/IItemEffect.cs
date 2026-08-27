using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//物品效果接口
//针对任何能给玩家加参数的东西
//数据（参数） + 行为（装卸钩） 比实现
public interface IItemEffect
{
    string ItemId { get; }        //用于去重，替换
    void Apply(PlayerController player);        //装上钩子
    void Remove(PlayerController player);       //卸下钩子'
    //参数乘法修改器
    float MoveSpeedMul { get; }   //移速
    float JumpForceMul { get; } //跳跃修改器
    float GravityMul { get; }
    int DashBonus { get; }  //dash次数修改器
}
