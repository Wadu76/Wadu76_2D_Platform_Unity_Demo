using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Parallax : MonoBehaviour
{
    private Transform _mainCamera;  //maincamera's  TF

    private Vector3 _lastPosition;  //Camera's position

    [SerializeField] private float speed = 1f;  //视差效果参数

    void Start()
    {
        _mainCamera = Camera.main.transform;
        _lastPosition = _mainCamera.position;
    }

    void LateUpdate()
    {
        ParallaxMove();
    }

    void ParallaxMove()
    {
        float deltaX = _mainCamera.position.x - _lastPosition.x;
        transform.position += new Vector3(deltaX * speed, 0, 0);
        _lastPosition = _mainCamera.position;
    }
}
