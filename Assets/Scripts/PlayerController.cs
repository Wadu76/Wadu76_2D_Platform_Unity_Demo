using System.Collections;
using UnityEngine;

enum PlayerState { Ground, Jump, Fall, Dash }

public class PlayerController : MonoBehaviour
{
    private PlayerState currentState = PlayerState.Ground;      //状态机控制
    public float horizontalMoveLastFrame;           //记录水平方向的最后一个输入（主要1 or -1）
    private SpriteRenderer sr;
    private Animator anim;
    public float moveSpeed = 5f;
    public float jumpForce = 10f;

    [Header("跳跃缓冲")]
    public float coyoteTime = 0.1f;        //土狼时间计时：离开地面可以继续跳（后输入） 0.1s == 6fps
    public float jumpBufferTime = 0.1f;    //跳跃输入缓冲：落地前按跳可以预输入

    //平时的基础重力
    [SerializeField]
    private float baseGravity = 4f;

    //上升时松开跳：重力×2.5，剪短上升
    [SerializeField]
    private float jumpCutMultiplier = 2.5f;

    //下落时 重力x2.5, 下坠更快
    [SerializeField]
    private float fallMutiplier = 2.5f;

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

    private bool dashPressed;   //Update里面采集判断要不要dash，一帧一次
    private float invincibleTimer;
    private float stateTime;    //当前状态计时（目前主要针对dash）

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

        //dashpressed根据是否消耗，是否按下进行累积而不是覆盖
        dashPressed = Input.GetKeyDown(KeyCode.K) || dashPressed;
        if (invincibleTimer > 0f)   //冷却在状态机里面判断
        {
            invincibleTimer -= Time.deltaTime;
            if (invincibleTimer <= 0f) isInvincible = false;
        }

    }

    void FixedUpdate()
    {
        UpdateGravity();

        //check if we're on the ground
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        //土狼时间： 在地面就充满，离开就开始计时
        if (isGrounded) coyoteTimer = coyoteTime;
        else coyoteTimer -= Time.deltaTime;

        if (!TryStartDash()) TryStartJump();

        switch (currentState)
        {
            case PlayerState.Ground: UpdateGround(); break;
            case PlayerState.Jump: UpdateJump(); break;
            case PlayerState.Fall: UpdateFall(); break;
            case PlayerState.Dash: UpdateDash(); break;
        }

        //jumpPressed = false;
        anim.SetBool("IsGrounded", isGrounded);
        anim.SetFloat("VelocityY", rb.velocity.y);
        anim.SetBool("IsDashing", currentState == PlayerState.Dash);
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
        //拉伸部分，占dash的30%
        float stretchDuration = dashTime * 0.3f;
        float t = 0f;
        while (t < stretchDuration)
        {
            t += Time.deltaTime;
            //将t/stretchDuration限制在01，作为插值系数
            float k = Mathf.Clamp01(t / stretchDuration);
            //间接获取初始scale
            Vector3 s = baseScale;
            //将间接scale根据dash持续时间系数变化
            s.x = baseScale.x * Mathf.Lerp(1f, 1f + dashStretch,
k);
            //计算完后同时将player的scale缩放
            visual.localScale = s;
            //直到下一次update继续，以实现逐帧动画（可能有毫秒级别误差忽略不计）
            yield return null;
        }

        //回缩过程 占dash的70%
        float relaxDuration = dashTime * 0.7f;
        t = 0f;
        while (t < relaxDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / relaxDuration);
            Vector3 s = baseScale;
            //1-dashStretch（1.35）
            s.x = baseScale.x * Mathf.Lerp(1f + dashStretch, 1f,
k);
            visual.localScale = s;
            yield return null;
        }
        visual.localScale = baseScale;
    }

    //生成系列残影的协程
    IEnumerator GhostRoutine()
    {
        //dash期间持续生成ghost残影
        while (isDashing)
        {
            if (ghost != null) ghost.SpawnGhost();
            //残影生成有间隔 dashGhostInterval 
            yield return new WaitForSeconds(dashGhostInterval);
        }
    }
    //展示无敌过程高亮协程
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

    private void UpdateGravity()
    {
        //更新重力
        //dash重力优先于其他重力逻辑
        if (currentState == PlayerState.Dash)
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
    }

    private bool TryStartDash()
    {
        //在此消耗update搜集的dashPressed
        if (!dashPressed) return false;
        dashPressed = false;
        //尝试看是否要dash 看是否按了K，目前不能在dash状态，且dashcd要转好
        if (currentState != PlayerState.Dash && dashCooldownTimer <= 0f)
        {
            //满足便在此切换为dashState，返回true
            ChangeState(PlayerState.Dash);
            return true;
        }
        //上述条件有一个不满足就返回本帧无法dash
        return false;
    }

    private bool TryStartJump()
    {
        //jump与jumpbuffer和cotyoteTimer绑定，每次跳了后面会清0，因此靠这两判断能否跳
        //当然dash过程无法切换到jump，要先”落地“才行
        if (jumpBufferTimer > 0f && (isGrounded || coyoteTimer > 0f) && currentState != PlayerState.Dash)
        {
            //类比上面的tryDash
            ChangeState(PlayerState.Jump);
            return true;
        }
        return false;
    }

    // 用于把目前的PlayerState切到next
    private void ChangeState(PlayerState next)
    {
        currentState = next;
        //该变量目前仅针对dash
        //由于每个状态都可能会切向dash，因此任何状态切换时将statetime置为0
        //即当dash切换到该nextstate时，后续不会出cd问题
        stateTime = 0f;     //重置当前状态持续时间

        //要切换到跳时就把跳的状态重置，给玩家加上跳跃的力
        if (next == PlayerState.Jump)
        {
            //执行跳逻辑
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpBufferTimer = 0f;   //消耗缓冲
            coyoteTimer = 0f;       //消耗土狼
        }
        //要切换到dash时同样的重置其该配置的参数
        else if (next == PlayerState.Dash)
        {
            isDashing = true;       //切换到dash自然正在dash
            isInvincible = true;    //启动无敌
            dashCooldownTimer = dashCooldown;   //置cd
            dashDirection = GetDashDirection(); //获取dash方向用于计算dash速度矢量
            anim.SetBool("IsDashing", true);    //设置动画的dash为true
            //切换到dash状态自然开启其特效的coroutines
            StartCoroutine(StretchRoutine());
            StartCoroutine(GhostRoutine());
            StartCoroutine(BlinkRoutine());
        }
    }

    private void UpdateGround()
    {
        //在地板上时更新速度
        rb.velocity = new Vector2(horizontalMoveLastFrame * moveSpeed, rb.velocity.y);
        //当走出平台就切换到fallstate
        if (!isGrounded && rb.velocity.y < 0f) ChangeState(PlayerState.Fall);
    }

    private void UpdateJump()
    {
        //更新跳状态，更新速度
        rb.velocity = new Vector2(horizontalMoveLastFrame * moveSpeed, rb.velocity.y);
        //当y向变为非正时切换为fall
        if (rb.velocity.y <= 0) ChangeState(PlayerState.Fall);
    }

    private void UpdateFall()
    {
        //更新下落状态速度
        rb.velocity = new Vector2(horizontalMoveLastFrame * moveSpeed, rb.velocity.y);
        //触地了，切换到GroudState
        if (isGrounded) ChangeState(PlayerState.Ground);
    }

    private void UpdateDash()
    {
        //stateTime在该函数里和dashTime比较，不写死方便后面其他需要计时的状态扩展
        stateTime += Time.fixedDeltaTime;
        rb.velocity = dashDirection * dashSpeed;
        if (stateTime >= dashTime)
        {
            //持续时间超过dash该持续的时间了
            isDashing = false;
            anim.SetBool("IsDashing", false);
            invincibleTimer = dashIFrame - dashTime;    //无敌延续
            //dash结束，按落地/速度定新的状态
            if (isGrounded) ChangeState(PlayerState.Ground);
            else ChangeState(rb.velocity.y > 0f ? PlayerState.Jump : PlayerState.Fall);
        }
    }
}
