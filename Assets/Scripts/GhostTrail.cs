using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class GhostTrail : MonoBehaviour
{
    [Header("Ghost Trail")]
    public float ghostLifetime = 0.3f;      //残影生成到消失的时间
    public Color ghostColor = new Color(1f, 1f, 1f, 0.5f);  //半透明色残影

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    //玩家当前位置生成一个残影再淡出销毁
    public void SpawnGhost()
    {
        GameObject ghost = new GameObject("Ghost");
        ghost.transform.position = transform.position;
        ghost.transform.rotation = transform.rotation;
        ghost.transform.localScale = transform.localScale;  //残影继承本体的拉伸

        SpriteRenderer g = ghost.AddComponent<SpriteRenderer>();
        g.sprite = sr.sprite;
        g.flipX = sr.flipX;
        g.sortingOrder = sr.sortingOrder - 1; //压在玩家下面一层
        g.color = ghostColor;

        StartCoroutine(FadeAndDestroy(ghost, g));
    }

    IEnumerator FadeAndDestroy(GameObject ghost, SpriteRenderer g)
    {
        Color start = g.color;
        float t = 0f;
        while (t < ghostLifetime)
        {
            t += Time.deltaTime;
            g.color = new Color(start.r, start.g, start.b,
                                Mathf.Lerp(start.a, 0f, t / ghostLifetime));
            yield return null;
        }
        Destroy(ghost);
    }
}

