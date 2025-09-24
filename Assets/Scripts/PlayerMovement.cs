using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerMovement : MonoBehaviour
{
   

    public float moveSpeed;
    public Rigidbody2D rb;
    private Vector2 moveDirection;

    #region Animation Variables
    [Header("Animation")]
    [SerializeField] private Animator anim;
    #endregion


    void Update()
    {
        ProcessInputs();

        if (moveDirection != Vector2.zero)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            
            
        }


        // --- Animation Handling ---
        anim.SetBool("forward", moveDirection.y < 0);
        anim.SetBool("backward", moveDirection.y > 0);
        anim.SetBool("left", moveDirection.x < 0);
        anim.SetBool("right", moveDirection.x > 0);
        anim.SetBool("idle", moveDirection.x == 0 && moveDirection.y == 0);
    }

    void FixedUpdate()
    {
        Move();
    }

    void ProcessInputs()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        moveDirection = new Vector2(moveX, moveY).normalized;
    }

    void Move()
    {
        rb.velocity = new Vector2(moveDirection.x * moveSpeed, moveDirection.y * moveSpeed);
    }



    
}