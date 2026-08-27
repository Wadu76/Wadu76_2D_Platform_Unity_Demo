using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IResettable
{
    void ResetLevelObject();    //玩家重生的时候调用，复位自己的状态
}

