using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public Rigidbody2D rb;

    [Header("Animation")]
    [SerializeField] private Animator anim;

    [Header("Hurt Overlay")]
    [Tooltip("Default seconds to keep the 'hurt' animation playing after a wrong answer.")]
    [SerializeField] private float hurtOverlaySecondsDefault = 3f;

    private Vector2 moveDirection;
    private Vector2 lastMoveDirection = Vector2.down; // default facing front

    // timed overlay state
    private float hurtOverlayUntil = -1f;

    void Update()
    {
        // Always allow input & movement
        ProcessInputs();

        if (moveDirection != Vector2.zero)
            lastMoveDirection = moveDirection;

        UpdateAnimator();
    }

    void FixedUpdate()
    {
        // Movement is never blocked
        rb.velocity = moveDirection * moveSpeed;
    }

    void ProcessInputs()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        moveDirection = new Vector2(moveX, moveY).normalized;
    }

    // ---------- Animator driving ----------
    void UpdateAnimator()
    {
        bool moving = moveDirection != Vector2.zero;

        if (moving)
        {
            ClearIdleFlags();
            // choose dominant axis so diagonals don’t flicker
            if (Mathf.Abs(moveDirection.x) > Mathf.Abs(moveDirection.y))
            {
                SetMoveFlags(left: moveDirection.x < 0, right: moveDirection.x > 0, forward: false, backward: false);
            }
            else
            {
                SetMoveFlags(left: false, right: false, forward: moveDirection.y < 0, backward: moveDirection.y > 0);
            }
        }
        else
        {
            ClearMoveFlags();

            if (Mathf.Abs(lastMoveDirection.x) > Mathf.Abs(lastMoveDirection.y))
            {
                SetIdleFlags(front: false, back: false, left: lastMoveDirection.x < 0, right: lastMoveDirection.x > 0);
            }
            else
            {
                SetIdleFlags(front: lastMoveDirection.y < 0, back: lastMoveDirection.y > 0, left: false, right: false);
            }
        }

        // Apply the timed HURT overlay *on top* of whatever the move/idle flags are
        bool hurtActive = Time.time < hurtOverlayUntil;
        anim.SetBool("hurt", hurtActive);
    }

    // Helpers to ensure mutual exclusivity (no two bools true at once)
    void SetMoveFlags(bool left, bool right, bool forward, bool backward)
    {
        anim.SetBool("left", left);
        anim.SetBool("right", right);
        anim.SetBool("forward", forward);
        anim.SetBool("backward", backward);
    }

    void ClearMoveFlags() => SetMoveFlags(false, false, false, false);

    void SetIdleFlags(bool front, bool back, bool left, bool right)
    {
        anim.SetBool("idle", front);
        anim.SetBool("idle_back", back);
        anim.SetBool("idle_left", left);
        anim.SetBool("idle_right", right);
    }

    void ClearIdleFlags() => SetIdleFlags(false, false, false, false);

    public void isDead(bool die) => anim.SetBool("die", die);

    // Keep for backward compatibility (direct control if you need it elsewhere)
    public void isHurt(bool hurt) => anim.SetBool("hurt", hurt);


    public void HurtOverlay(float seconds = -1f)
    {
        if (seconds <= 0f) seconds = hurtOverlaySecondsDefault;
        float until = Time.time + seconds;
        // Extend if already active so overlapping hurts don't shorten it
        if (until > hurtOverlayUntil) hurtOverlayUntil = until;
    }
}
