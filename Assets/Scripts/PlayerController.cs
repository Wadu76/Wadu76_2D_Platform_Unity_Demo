using System.Collections;
//using System.Numerics;

//using System.Numerics;

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public float horizontalMoveLastFrame;
    private SpriteRenderer sr;
    private Animator anim;
    public float moveSpeed = 5f;
    public float jumpForce = 10f;

    [Header("跳跃缓冲")]
    public float coyoteTime = 0.1f;        //土狼时间计时：离开地面可以继续跳（后输入） 0.1s == 6fps
    public float jumpBufferTime = 0.1f;    //跳跃输入缓冲：落地前按跳可以预输入

    //平时的基础重力
    [SerializeField]
    public float baseGravity = 4f;

    //上升时松开跳：重力×2.5，剪短上升
    [SerializeField]
    public float jumpCutMultiplier = 2.5f;

    //下落时 重力x2.5, 下坠更快
    [SerializeField]
    public float fallMutiplier = 2.5f;

    private bool jumpHeld; //跳跃键是否还按着

    //地面检测点
    [SerializeField]
    public Transform groundCheck;

    public float groundCheckRadius = 0.2f;

    public LayerMask groundLayer;
    //public LayerMask[] groundLayers;

    //dash手感参数
    [Header("Dash")]
    public float dashSpeed = 15f;       //冲刺水平速度
    public float dashTime = 0.18f;      //冲刺持续时间
    public float dashCooldown = 0.4f;       //冷却时间
    public float dashStretch = 1.35f;   //拉伸幅度，scale.x
    public float dashGravity = 0.5f;    //dash期间重力缩放量
    public float dashGhostInterval = 0.03f;     //残影生成间隔
    public float dashIFrame = 0.3f;     //无敌时长，默认和dash时长一致
    public bool dashBlink = true;      //无敌期间闪烁提示

    public bool IsInvincible => isInvincible;

    //为了Visual独立
    public Transform visual;

    private Rigidbody2D rb;
    private bool isGrounded;
    //private bool jumpPressed;

    //jumping buffer timers
    private float coyoteTimer;      //土狼时间剩余
    private float jumpBufferTimer;  //缓冲剩余
    //public float moveX;

    //dash状态
    private bool isDashing;
    private bool isInvincible;
    private Vector2 dashDirection;
    private float dashCooldownTimer;
    private Vector3 baseScale;
    private GhostTrail ghost;
    //为引用可穿越平台的下落时间
    private PlatformPenetration platformPenetration;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
        ghost = GetComponentInChildren<GhostTrail>();
        platformPenetration = GetComponent<PlatformPenetration>();
        baseScale = visual.localScale;

    }

    void Update()
    {
        OptimizedInput();
        if (Input.GetButtonDown("Jump"))
        {
            //S+空格下穿平台，不进入跳跃缓冲，让身体下去
            if (Input.GetKey(KeyCode.S))
            {

                platformPenetration.DropThrough();
            }
            else
            {
                //jumpPressed = true;
                //跳到空中（也可能没在空中就开始计时器）
                jumpBufferTimer = jumpBufferTime;   //按了跳就重置计时器
            }

        }
        jumpBufferTimer -= Time.deltaTime;
        //update中获取跳跃输入
        jumpHeld = Input.GetButton("Jump");
        // 左右按键：A/D 或 ←/→，返回 -1 / 0 / 1
        //此处的moveX处理图像反转
        UpdateCharacterFacing(horizontalMoveLastFrame);
        //dash冷却计时
        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        //触发冲刺 shift 且未在冲 且 冷却过了
        if (Input.GetKeyDown(KeyCode.K) && !isDashing && dashCooldownTimer <= 0f)
        {
            StartCoroutine(DashRoutine());
        }

    }

    void FixedUpdate()
    {
        //dash重力优先于其他重力逻辑
        if (isDashing)
        {
            rb.gravityScale = dashGravity;
        }
        //定这一帧的重力
        else if (rb.velocity.y < 0f)
        {
            rb.gravityScale = baseGravity * fallMutiplier;    //下落：加倍
        }
        else if (rb.velocity.y > 0f && !jumpHeld)
        {
            rb.gravityScale = baseGravity * jumpCutMultiplier; //上升时松开空格，猛增重力剪短上升过程
        }
        else
        {
            rb.gravityScale = baseGravity;  //其余时间正常
        }


        //check if we're on the fround
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        //土狼时间： 在地面就充满，离开就开始计时
        if (isGrounded) coyoteTimer = coyoteTime;
        else coyoteTimer -= Time.deltaTime;


        //此处的moveX处理移动速度
        float moveX = horizontalMoveLastFrame;
        if (isDashing)
        {
            //冲刺：速度全方向接管，保证直线不飘
            rb.velocity = dashDirection * dashSpeed;
        }
        else
        {
            // 关键：只改 X 速度，Y 速度原样保留（重力 / 以后的跳跃都靠它）
            rb.velocity = new Vector2(moveX * moveSpeed, rb.velocity.y);
        }

        //我们不再判断是否跳跃那一下的布尔值，把布尔值抽成连续的短时间缓冲池，放大判定
        if (jumpBufferTimer > 0 && (isGrounded || coyoteTimer > 0f) && !isDashing)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            //anim.SetTrigger("Jump");
            jumpBufferTimer = 0f;   //消耗缓冲
            coyoteTimer = 0f;       //消耗土狼时间
        }
        //jumpPressed = false;
        anim.SetBool("IsGrounded", isGrounded);
        anim.SetFloat("VelocityY", rb.velocity.y);
    }

    void OptimizedInput()
    {
        // 按下：谁后按谁生效（"最后按下的键优先"）
        if (Input.GetKeyDown(KeyCode.A))
            horizontalMoveLastFrame = -1;
        if (Input.GetKeyDown(KeyCode.D))
            horizontalMoveLastFrame = 1;

        // 松开：问"另一边是否还按着"——还按着就切到那边，否则停下
        // 注意用 GetKey（查"按住"），不能用 GetKeyDown（只在一帧内为 true）
        if (Input.GetKeyUp(KeyCode.A))
            horizontalMoveLastFrame = Input.GetKey(KeyCode.D) ? 1 : 0;
        if (Input.GetKeyUp(KeyCode.D))
            horizontalMoveLastFrame = Input.GetKey(KeyCode.A) ? -1 : 0;
    }

    void UpdateCharacterFacing(float horizontalMoveLastFrame)
    {
        float moveX = horizontalMoveLastFrame;
        if (moveX != 0)
        {
            sr.flipX = moveX < 0;
        }
        anim.SetFloat("Speed", Mathf.Abs(moveX));
    }

    //dash实现
    Vector2 GetDashDirection()
    {
        //A/D/W/S + 方向键都这样映射
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"),
                            Input.GetAxisRaw("Vertical"));
        if (input == Vector2.zero)
        {
            input = new Vector2(sr.flipX ? -1 : 1, 0f); //没按方向就朝着面朝向dash
        }

        return input.normalized;    //不放大斜向速度
    }

    IEnumerator DashRoutine()
    {
        isDashing = true;
        isInvincible = true;
        dashCooldownTimer = dashCooldown;
        dashDirection = GetDashDirection();
        anim.SetBool("IsDashing", true);

        //并行启动特效
        StartCoroutine(StretchRoutine());
        StartCoroutine(GhostRoutine());
        StartCoroutine(BlinkRoutine());

        yield return new WaitForSeconds(dashTime);

        isDashing = false;
        anim.SetBool("IsDashing", false);

        //无敌到dashIFrame
        yield return new WaitForSeconds(Mathf.Max(0f, dashIFrame - dashTime));
        isInvincible = false;
    }


    //拉伸残影
    IEnumerator StretchRoutine()
    {
        float stretchDuration = dashTime * 0.3f;
        float t = 0f;
        while (t < stretchDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / stretchDuration);
            Vector3 s = baseScale;
            s.x = baseScale.x * Mathf.Lerp(1f, 1f + dashStretch,
k);
            visual.localScale = s;
            yield return null;
        }
        float relaxDuration = dashTime * 0.7f;
        t = 0f;
        while (t < relaxDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / relaxDuration);
            Vector3 s = baseScale;
            s.x = baseScale.x * Mathf.Lerp(1f + dashStretch, 1f,
k);
            visual.localScale = s;
            yield return null;
        }
        visual.localScale = baseScale;
    }

    IEnumerator GhostRoutine()
    {
        while (isDashing)
        {
            if (ghost != null) ghost.SpawnGhost();
            yield return new WaitForSeconds(dashGhostInterval);
        }
    }

    IEnumerator BlinkRoutine()
    {
        while (isInvincible)
        {
            sr.enabled = !dashBlink;   //开闪烁就隐藏,关闪烁就保持可见
            yield return new WaitForSeconds(0.1f);
            sr.enabled = true;
            yield return new WaitForSeconds(0.1f);
        }
        sr.enabled = true;
    }
}
