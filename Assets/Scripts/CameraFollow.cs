using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    void LateUpdate()
    {
        Vector3 pos = new Vector3(target.position.x, target.position.y + 1.77f, target.position.z);   // 插值后的平滑位置
        pos.z = transform.position.z;    // 保持相机自己的 z(-10),只对齐 x/y
        transform.position = pos;
    }
}
