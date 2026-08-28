using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

enum PlayerState { Ground, Jump, Fall, Dash, WallSlide, WallJump }

public class PlayerController : MonoBehaviour
{
    private PlayerState currentState = PlayerState.Ground;      //状态机控制
    public float horizontalMoveLastFrame;           //记录水平方向的最后一个输入（主要1 or -1）
    private SpriteRenderer sr;
    private Animator anim;
    public float moveSpeed = 5f;
    public float jumpForce = 10f;

    [SerializeField] public Transform visual;

    [Header("跳跃缓冲")]
    public float coyoteTime = 0.1f;        //土狼时间计时：离开地面可以继续跳（后输入） 0.1s == 6fps
    public float jumpBufferTime = 0.1f;    //跳跃输入缓冲：落地前按跳可以预输入

    //平时的基础重力
    [SerializeField]
    private float baseGravity = 4f;
    [SerializeField]
    private PlayerStats baseStats;  //玩家基础参数SO
    private readonly List<IItemEffect> activeEffects = new();   //当前已装备的效果
    //上升时松开跳：重力×2.5，剪短上升
    [SerializeField]
    private float jumpCutMultiplier = 2.5f;

    //下落时 重力x2.5, 下坠更快
    [SerializeField]
    private float fallMutiplier = 2.5f;

    private bool jumpHeld; //跳跃键是否还按着

    //地面检测点
    [SerializeField]
    private Transform groundCheck;
    [SerializeField] private Transform groundCheckRight;
    [SerializeField]
    private Transform wallCheckLeft;
    [SerializeField]
    private Transform wallCheckRight;

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
    [SerializeField] private int maxDashes = 1;

    [Header("滑墙&墙跳")]
    [SerializeField] private float wallJumpGraceTime = 0.12f;       //松朝向键后，墙跳还能跳的时长（缓冲
    [SerializeField] private float wallCheckDist = 0.25f;   //墙检测射线长度
    [SerializeField] private float wallSlideMaxFall = -2f;  //墙滑下落速度上限
    [SerializeField] private float wallSlideGravity = 0.4f; //墙滑时的重力缩放（在UpdateGravity里调整）
    [SerializeField] private float wallJumpSpeed = 12f;     //墙跳水平速度
    [SerializeField] private float wallJumpForceTime = 0.16f;   //墙跳强制移动窗口(期间忽视其他输入)
    [SerializeField] private LayerMask wallMask;    //检测墙层，platform/ground？


    private bool dashPressed;   //Update里面采集判断要不要dash，一帧一次

    private float stateTime;    //当前状态计时（目前主要针对dash）

    public bool IsInvincible => isInvincible;

    //为了Visual独立


    private Rigidbody2D rb;
    private bool isGrounded;
    //private bool jumpPressed;

    //jumping buffer timers
    private float coyoteTimer;      //土狼时间剩余
    private float jumpBufferTimer;  //缓冲剩余
    private float invincibleTimer;
    private float wallJumpForceTimer;   //墙跳强制移动剩余时间（计时器）
    private float wallJumpGraceTimer;   //
    //public float moveX;

    //dash状态
    private int dashes;
    private bool isDashing;
    private bool isInvincible;
    private Vector2 dashDirection;
    private float dashCooldownTimer;
    private Vector3 baseScale;
    private GhostTrail ghost;
    //为引用可穿越平台的下落时间
    private PlatformPenetration platformPenetration;
    //墙状态（跳&滑）
    private int wallSlide;      //哪一侧有墙：-1左/+1右/0无
    private int wallJumpDir;    //墙跳弹离方向（= -wallSlide）
    private int lastWallSide;   //离开的最后一个墙的方向 -1 or 1
    private bool isDead;        //死亡中 不准输入，暂停物理

    //[SerializeField] SpriteRenderer deathPic;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
        ghost = GetComponentInChildren<GhostTrail>();
        platformPenetration = GetComponent<PlatformPenetration>();
        baseScale = visual.localScale;
        dashes = maxDashes;
        if (!GameState.hasSpawnPoint)
        {
            GameState.spawnPoint = transform.position;
            GameState.hasSpawnPoint = true;
        }
        RecalculateStats();

    }

    void Update()
    {
        if (isDead) return;     //死亡不跑输入
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
        if (isDead) return;      //死亡不跑物理
        UpdateGravity();

        //check if we're on the ground
        //isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) || Physics2D.OverlapCircle(groundCheckRight.position, groundCheckRadius, groundLayer);
        //if (isGrounded) Debug.Log(isGrounded);
        if (isDead) Debug.Log(isDead);
        //地面检测:脚底一小段向下

        //土狼时间： 在地面就充满，离开就开始计时
        if (isGrounded) coyoteTimer = coyoteTime;
        else coyoteTimer -= Time.deltaTime;

        //维护墙跳宽容,上墙充满缓冲时间，离墙才开始减
        if (currentState == PlayerState.WallSlide)
        {
            lastWallSide = wallSlide;
            wallJumpGraceTimer = wallJumpGraceTime;
        }
        else
        {
            wallJumpGraceTimer -= Time.deltaTime;
        }

        if (currentState == PlayerState.Ground) dashes = maxDashes;

        if (!TryStartDash()) TryStartJump();

        switch (currentState)
        {
            case PlayerState.Ground: UpdateGround(); break;
            case PlayerState.Jump: UpdateJump(); break;
            case PlayerState.Fall: UpdateFall(); break;
            case PlayerState.Dash: UpdateDash(); break;
            case PlayerState.WallSlide: UpdateWallSlide(); break;
            case PlayerState.WallJump: UpdateWallJump(); break;

        }

        //jumpPressed = false;
        anim.SetBool("IsGrounded", isGrounded);
        anim.SetFloat("VelocityY", rb.velocity.y);
        anim.SetBool("IsDashing", currentState == PlayerState.Dash);
        anim.SetBool("IsWallSliding", currentState == PlayerState.WallSlide);
    }


    //输入优化，防转向动画卡顿
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


    //切换角色朝向，包括墙上的方向
    void UpdateCharacterFacing(float horizontalMoveLastFrame)
    {
        if (currentState == PlayerState.WallSlide && wallSlide != 0)
        {
            //墙在右边就反转（原图朝左）
            sr.flipX = wallSlide == -1;
            anim.SetFloat("Speed", 0f);
            return;
        }
        //若不在墙上就专注于地上行走
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

    //拉伸dash残影
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


    //每fixed帧更新玩家重量：dash/jump上升/fall下落时/上升变重力
    //扒在墙上也改变重力（慢慢滑下来）
    private void UpdateGravity()
    {

        //更新重力
        //dash重力优先于其他重力逻辑/包括wallslide
        if (currentState == PlayerState.Dash)
        {
            rb.gravityScale = dashGravity;
        }

        else if (currentState == PlayerState.WallSlide)
        {
            rb.gravityScale = wallSlideGravity;
        }

        //dash、墙落以外定这一帧的重力
        //下落中
        else if (rb.velocity.y < 0f)
        {
            rb.gravityScale = baseGravity * fallMutiplier;    //下落：加倍
        }
        //上升中且没按住空格，加大重力使其快速落
        else if (rb.velocity.y > 0f && !jumpHeld)
        {
            rb.gravityScale = baseGravity * jumpCutMultiplier; //上升时松开空格，猛增重力剪短上升过程
        }
        //按住空格就不变
        else
        {
            rb.gravityScale = baseGravity;  //其余时间正常
        }
    }


    //每帧优先检测dash的函数，判断是否要dash并切换状态
    private bool TryStartDash()
    {
        //在此消耗update搜集的dashPressed
        if (!dashPressed) return false;
        dashPressed = false;
        //尝试看是否要dash 看是否按了K，目前不能在dash状态，且dashcd要转好
        //if (currentState != PlayerState.Dash && dashCooldownTimer <= 0f)
        if (currentState != PlayerState.Dash && dashes > 0 &&
  dashCooldownTimer <= 0f)
        {
            dashes--;
            //满足便在此切换为dashState，返回true
            ChangeState(PlayerState.Dash);
            return true;
        }
        //上述条件有一个不满足就返回本帧无法dash
        return false;
    }

    //尝试是否要jump了，并切换状态
    //现在加上wallJump
    private bool TryStartJump()
    {

        if (jumpBufferTimer <= 0f) return false;
        if (currentState == PlayerState.Dash) return false;  //dash的时候跳不了
        //jump与jumpbuffer和cotyoteTimer绑定，每次跳了后面会清0，因此靠这两判断能否跳
        //当然dash过程无法切换到jump，要先”落地“才行
        //地面/土狼跳的时候的跳（贴墙的时候不触及这里的逻辑）
        //不需要jumpBufferTimer的判断吗？对，上面判断jumpBufferTimer不为正就直接返回false了

        //第一种情况，上面排除不能跳和dash的时候，地面/土狼跳的时候跳
        if (currentState != PlayerState.WallSlide && (isGrounded || coyoteTimer > 0f))
        {
            //类比上面的tryDash
            ChangeState(PlayerState.Jump);
            return true;
        }

        //第二种情况，墙跳：相邻有墙，按着朝墙方向才让跳
        wallSlide = GetWallSlide();
        //中间变量记录上次的wallSlide防止被消耗
        //int side = wallSlide != 0 ? wallSlide : lastWallSide;
        //1 挨着墙:按住朝墙 或 宽容窗口内,都算
        if (wallSlide != 0 && (horizontalMoveLastFrame == wallSlide || wallJumpGraceTimer > 0f))
        //if (wallSlide != 0)
        {
            //朝着扒墙的反方向跳出去
            wallJumpDir = -wallSlide;
            wallJumpGraceTimer = 0f;    //消耗缓冲时间，防止连跳
            ChangeState(PlayerState.WallJump);
            return true;
        }
        //2已离墙:只有宽容窗口能救,不再允许"按住方向"判定(那会拿旧值误发)
        else if (wallJumpGraceTimer > 0f && lastWallSide != 0)
        {
            wallJumpDir = -lastWallSide;
            wallJumpGraceTimer = 0f;
            ChangeState(PlayerState.WallJump);
            return true;
        }
        return false;       //都不满足，buffer留着，后续帧满足在消费（在UpdateJump等里面）
    }

    //检测墙在哪里，用的地方给wallSile参数赋值 -1在左 1在右 0表没有
    private int GetWallSlide()
    {
        //在左边
        if (Physics2D.Raycast(wallCheckLeft.position, Vector2.left, wallCheckDist, wallMask)) return -1;
        //在右边
        if (Physics2D.Raycast(wallCheckRight.position, Vector2.right, wallCheckDist, wallMask)) return 1;
        //都没有
        return 0;
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
        //
        else if (next == PlayerState.WallJump)
        {
            rb.velocity = new Vector2(wallJumpDir * wallJumpSpeed, jumpForce);
            jumpBufferTimer = 0f;   //消耗缓冲
            coyoteTimer = 0f;       //消耗土狼
            wallJumpForceTimer = wallJumpForceTime;
        }
    }


    //更新地面速度，若是离地就切换到fall状态
    private void UpdateGround()
    {
        //在地板上时更新速度
        rb.velocity = new Vector2(horizontalMoveLastFrame * moveSpeed, rb.velocity.y);
        //当走出平台就切换到fallstate
        if (!isGrounded && rb.velocity.y < 0f) ChangeState(PlayerState.Fall);
    }

    //切换jump的速度，若是y向速度变低切换到fall状态
    private void UpdateJump()
    {
        //更新跳状态，更新速度
        rb.velocity = new Vector2(horizontalMoveLastFrame * moveSpeed, rb.velocity.y);
        //当y向变为非正时切换为fall
        if (rb.velocity.y <= 0) ChangeState(PlayerState.Fall);
    }

    //更新fall速度，若是触地了切换到Ground状态
    private void UpdateFall()
    {
        //更新下落状态速度
        rb.velocity = new Vector2(horizontalMoveLastFrame * moveSpeed, rb.velocity.y);
        //触地了，切换到GroudState
        if (isGrounded)
        {
            ChangeState(PlayerState.Ground);
            return;
        }

        //墙滑，下落中 且 按住向墙 且 射线命中
        if (rb.velocity.y <= 0f)
        {
            wallSlide = GetWallSlide();
            if (wallSlide != 0 && horizontalMoveLastFrame == wallSlide)
            //if (wallSlide != 0)
            {
                ChangeState(PlayerState.WallSlide);
            }
        }
    }

    //
    private void UpdateWallSlide()
    {
        //Debug.Log($"inWallSlide, vy ={rb.velocity.y}, wallSide ={wallSlide}");
        //每帧查有没有墙，没有就下去
        wallSlide = GetWallSlide();
        //水平锁0，下落到上限
        rb.velocity = new Vector2(0f, MathF.Max(rb.velocity.y, wallSlideMaxFall));
        //触地了就换到Groun
        if (isGrounded)
        {
            ChangeState(PlayerState.Ground);
            return;
        }
        //松开键 转向 没墙了 就转到fall
        if (wallSlide == 0 || horizontalMoveLastFrame != wallSlide)
        //if (wallSlide == 0)
        {
            ChangeState(PlayerState.Fall);
        }
    }

    private void UpdateWallJump()
    {
        //强制walljump窗口内忽略输入，弹离结束后恢复空中操控
        if (wallJumpForceTimer > 0f)
        {
            wallJumpForceTimer -= Time.deltaTime;
            rb.velocity = new Vector2(wallJumpDir * wallJumpSpeed, rb.velocity.y);
        }
        //
        else
        {
            rb.velocity = new Vector2(horizontalMoveLastFrame * moveSpeed, rb.velocity.y);
            if (isGrounded) ChangeState(PlayerState.Ground);
            else if (rb.velocity.y <= 0f) ChangeState(PlayerState.Fall);
        }
    }


    //dash状态计时，更新dash速度，若是触地切换至fall，若是在空中，根据y向速度切换到jump/fall
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

    public void TakeDamage()
    {
        if (isInvincible) return;    //dash无敌中不受伤害
        if (isDead) return;      //已死亡不再受伤，防止反复触发
        isDead = true;  //直接死
        rb.velocity = Vector2.zero;
        rb.gravityScale = 0f;       //身体悬空停住
        StartCoroutine(DeathRoutine());
    }


    IEnumerator DeathRoutine()
    {
        //死亡表现 压扁缩小最后消失
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            visual.localScale = baseScale * Mathf.Lerp(1f, 0.1f, t / 0.2f);
            yield return null;
        }
        sr.enabled = false;
        yield return new WaitForSeconds(0.3f);  //停顿
        Respawn();
    }

    // 重生:复位位置/物理/状态机/缓冲/dash/视觉/场景可复位物
    void Respawn()
    {
        //位置与物理
        rb.position = GameState.spawnPoint;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        horizontalMoveLastFrame = 0f;   // 重生后不带前世输入
        //状态机与缓冲
        currentState = PlayerState.Ground;
        isDashing = false;
        isInvincible = false;
        invincibleTimer = 0f;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        wallJumpGraceTimer = 0f;
        wallJumpForceTimer = 0f;
        dashes = maxDashes;
        //视觉
        sr.enabled = true;
        visual.localScale = Vector3.zero;   //
        StartCoroutine(SpawnPop());
        anim.SetBool("IsDashing", false);
        anim.SetBool("IsWallSliding", false);
        //场景可复位物(坠落平台下一步实现,现在先留遍历)
        foreach (MonoBehaviour mb in FindObjectsOfType<MonoBehaviour>())
            if (mb is IResettable r) r.ResetLevelObject();
        //复活(放最后,顺序别乱)
        isDead = false;
    }

    //出生弹入:0 → 1.2 过冲 → 1(纯代码,配合死亡压扁很自然)
    IEnumerator SpawnPop()
    {
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float s = (t < 0.15f)
                ? Mathf.Lerp(0f, 1.2f, t / 0.15f)          // 弹起
                : Mathf.Lerp(1.2f, 1f, (t - 0.15f) / 0.10f); // 回落
            visual.localScale = baseScale * s;
            yield return null;
        }
        visual.localScale = baseScale;
    }

    //装备效果
    //加到列表里触发重算（面向接口不关心是Item/buff）
    public void ApplyEffect(IItemEffect effect)
    {
        activeEffects.Add(effect);
        effect.Apply(this);
        RecalculateStats();
    }

    //卸下效果，按照itemId移除 + 重算
    public void RemoveEffect(string itemId)
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (activeEffects[i].ItemId == itemId)
            {
                activeEffects[i].Remove(this);  //
                activeEffects.RemoveAt(i);
            }
        }
        RecalculateStats();
    }

    public bool HasEffect(string itemId)
    {
        foreach (var e in activeEffects) if (e.ItemId == itemId) return true;
        return false;

    }

    //从基础SO重算当前参数，每个已装备效果的修改器乘法叠加
    private void RecalculateStats()
    {
        moveSpeed = baseStats.moveSpeed;
        jumpForce = baseStats.jumpForce;
        baseGravity = baseStats.baseGravity;
        maxDashes = baseStats.maxDashes;

        foreach (IItemEffect e in activeEffects)
        {
            moveSpeed *= e.MoveSpeedMul;
            jumpForce *= e.JumpForceMul;
            baseGravity *= e.GravityMul;
            maxDashes += e.DashBonus;
        }
        if (maxDashes < 1) maxDashes = 1;
    }

}
