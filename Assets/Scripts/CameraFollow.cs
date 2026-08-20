using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    //要将player的rigidbody设置为持续插值的，防止角色自己抖动
    //此脚本lerp防止镜头抖动
    [SerializeField] private Transform target;
    //smoothSPeed越大画面跟随越紧
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private Vector3 offset = new Vector3(0, 1.77f, 0);

    void LateUpdate()
    {
        /*Vector3 pos = new Vector3(target.position.x, target.position.y + 1.77f, target.position.z);   // 插值后的平滑位置
        pos.z = transform.position.z;    // 保持相机自己的 z(-10),只对齐 x/y
        transform.position = pos;*/

        // 目标只取target的X Y，Z强制沿用相机本身的z，不跟随角色
        Vector3 desiredPos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z //锁死相机Z，不使用target的z
        );

        Vector3 smoothedPos = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPos;
    }
}
