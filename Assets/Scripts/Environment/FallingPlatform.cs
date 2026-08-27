using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 可掉落平台，每次死亡需要复位，因此要实现ResetLevelObject的接口函数
/// </summary>
public class FallingPlatform : MonoBehaviour, IResettable
{

    [SerializeField]
    private float delayBeforeFall = 0.5f;   //玩家接触后掉落的延迟时间
    [SerializeField]
    private float fallSpeed = 4f;   //平台下坠速度
    [SerializeField]
    private float fallDistance = 3f;   //下坠长度，落后触发消失/保留
    [SerializeField]
    private bool disappear = true;  //掉落后是否消失，true就消失
    [SerializeField]
    private float resetTime = 2f;   //消失/保留后多久复位（-1只靠重生复位）

    private enum State { Idle, Delay, Falling, Done }
    private State state = State.Idle;
    private Vector2 startPos;
    private SpriteRenderer sr;
    private Collider2D col;
    private float timer;
    private float fallen;   //已下落距离

    private void Start()
    {
        startPos = transform.position;
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    private void Update()
    {
        switch (state)
        {
            case State.Delay:
                timer -= Time.deltaTime;
                // 要塌了的反馈:震动渐强 + 颜色变红(玩家看得出平台要掉)
                float progress = 1f - timer / delayBeforeFall;    // 0→1,越临近越危险
                float shake = Mathf.Lerp(0.03f, 0.1f, progress);
                //抖动会让wallslide判定一直混乱，且不知为何有时候碰到墙不会判定开始fall
                //transform.position = (Vector3)startPos + new Vector3(Mathf.Sin(Time.time * 50f) * shake, 0f, 0f);
                sr.color = Color.Lerp(Color.white, new Color(0.85f, 0.35f, 0.35f),
progress);
                if (timer <= 0f)
                {
                    transform.position = startPos;  //回正开始下坠
                    state = State.Falling;
                    fallen = 0f;
                }
                break;

            case State.Falling:
                float step = fallSpeed * Time.deltaTime;
                transform.position -= new Vector3(0f, step, 0f);
                fallen += step;
                if (fallen >= fallDistance)
                {
                    if (disappear)  //掉完了
                    {
                        sr.enabled = false;
                        col.enabled = false;
                    }
                    state = State.Done;
                    timer = resetTime;
                }
                break;

            case State.Done:
                if (resetTime >= 0f)
                {
                    timer -= Time.deltaTime;
                    if (timer <= 0f) ResetLevelObject();    //自动复位
                }
                break;
        }


    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (state != State.Idle) return;
        if (collision.gameObject.GetComponent<PlayerController>() == null) return;
        state = State.Delay;
        timer = delayBeforeFall;
    }

    //重生的时候被调用
    public void ResetLevelObject()
    {
        transform.position = startPos;
        sr.color = Color.white;   // 复位颜色
        sr.enabled = true;
        col.enabled = true;
        state = State.Idle;
    }
}
