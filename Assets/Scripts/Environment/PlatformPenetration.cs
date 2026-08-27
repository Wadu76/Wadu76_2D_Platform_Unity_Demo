using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformPenetration : MonoBehaviour
{


    [SerializeField] private Collider2D moveCollider;   //玩家本身的碰撞collider
    [SerializeField] private Collider2D platformSensor; //穿越平台用的Sensor（从玩家膝盖到头，面向方向突出，isTrigger）
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LayerMask platformMask;    //platform的layer

    [SerializeField] private float dropThroughTime = 0.5f;  //S+空格强制下降时间
    [SerializeField] private float riseSpeedThreshold = 1f; //从下往上升的速度阈值

    //记录几个layer从而不许每帧NameToLayer读
    private int _layerCharacter;
    private int _layerCharacterIgnorePlatform;
    private float dropThroughTimer; //S+空格下落计时器

    //只读 判断player是否在IgnorePlatform状态，判断可以穿越否
    public bool CanPenetratePlatform => moveCollider.gameObject.layer == _layerCharacterIgnorePlatform;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        _layerCharacter = LayerMask.NameToLayer("Character");
        _layerCharacterIgnorePlatform = LayerMask.NameToLayer("CharacterIgnorePlatform");
    }

    void FixedUpdate()
    {
        if (dropThroughTimer > 0f)
            dropThroughTimer -= Time.fixedDeltaTime;

        RefreshPlatformPenetrate();
    }

    //
    void RefreshPlatformPenetrate()
    {
        bool needPenetrate =
            dropThroughTimer > 0f        //S+空格允许下降时
            || rb.velocity.y > riseSpeedThreshold;  //从下往下升时
                                                    //加了个限制但是这样会破坏迎面装墙还是会有碰撞
                                                    //|| SensorHitsPlatformAboveFeet();
                                                    //|| (rb.velocity.y >= 0f && platformSensor.IsTouchingLayers(platformMask));   //迎面撞上墙体或者还没穿完

        //计算完应用
        SetPenetration(needPenetrate);

    }

    //S+空格下穿入口，由controller调用为public
    public void DropThrough()
    {
        dropThroughTimer = dropThroughTime;
    }

    void SetPenetration(bool penetrate)
    {
        //penetrate为可否穿越的bool标志 可穿越就为IgnorePlat
        int target = penetrate ? _layerCharacterIgnorePlatform : _layerCharacter;
        if (moveCollider.gameObject.layer == target) return;     //层级没变就不变
        //变了就切换为target
        moveCollider.gameObject.layer = target;
    }

    // 遍历 sensor 碰到的每个平台,只要有一个平台的顶高于玩家脚底 → 该穿

}
