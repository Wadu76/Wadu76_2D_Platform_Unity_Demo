using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackGoundScroller : MonoBehaviour
{

    private Camera mainCamera;
    private float bgWidth;  //背景宽度
    void Start()
    {
        mainCamera = Camera.main;
        GetBgWidth();
    }

    void Update()
    {
        BgMove();
    }

    void GetBgWidth()
    {
        //获取背景实际宽度
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        bgWidth = spriteRenderer.bounds.size.x;

    }

    void BgMove()
    {
        //背景与摄像机水平距离
        float distance = mainCamera.transform.position.x - transform.position.x;

        if (Mathf.Abs(distance) > bgWidth)
        {
            transform.position += Vector3.right * bgWidth * 2 * Mathf.Sign(distance);
        }
    }
}
