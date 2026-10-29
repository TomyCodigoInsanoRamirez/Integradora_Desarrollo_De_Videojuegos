using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 40f;
    [SerializeField] private float deceleration = 18f;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float groundCheckRadius = 0.15f;

    [Header("Shoot")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 15f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int DisparoHash = Animator.StringToHash("Disparo");
    private static readonly int DisparoArribaHash = Animator.StringToHash("DisparoArriba");
    private static readonly int DisparoAgachadoHash = Animator.StringToHash("DisparoAgachado");

    private Rigidbody2D rb;
    private Collider2D col;
    private float moveInput;
    private bool isGrounded;
    private bool jumpRequested;
    private bool shootRequested;
    private bool shootUpRequested;
    private bool shootDownRequested;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        moveInput = 0f;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput += 1f;

            if (keyboard.spaceKey.wasPressedThisFrame) jumpRequested = true;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) shootRequested = true;
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) shootUpRequested = true;
            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) shootDownRequested = true;
        }

        if (moveInput > 0f) spriteRenderer.flipX = false;
        else if (moveInput < 0f) spriteRenderer.flipX = true;

        if (animator != null)
        {
            animator.SetBool(IsMovingHash, moveInput != 0f);
            animator.SetBool(IsGroundedHash, isGrounded);
        }

        if (shootRequested)
        {
            shootRequested = false;
            Shoot();
        }

        if (shootUpRequested)
        {
            shootUpRequested = false;
            if (animator != null) animator.SetTrigger(DisparoArribaHash);
        }

        if (shootDownRequested)
        {
            shootDownRequested = false;
            if (animator != null) animator.SetTrigger(DisparoAgachadoHash);
        }
    }

    private void FixedUpdate()
    {
        Vector2 checkPoint = new Vector2(col.bounds.center.x, col.bounds.min.y);
        isGrounded = false;
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkPoint, groundCheckRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit != null && hit != col)
            {
                isGrounded = true;
                break;
            }
        }

        float targetSpeed = moveInput * moveSpeed;
        float rate = moveInput != 0f ? acceleration : deceleration;
        float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, rate * Time.fixedDeltaTime);
        float newY = rb.linearVelocity.y;

        if (jumpRequested)
        {
            jumpRequested = false;
            if (isGrounded)
            {
                newY = jumpForce;
                isGrounded = false;
            }
        }

        rb.linearVelocity = new Vector2(newX, newY);
    }

    private void Shoot()
    {
        if (animator != null) animator.SetTrigger(DisparoHash);

        if (bulletPrefab == null || firePoint == null) return;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Vector2 direction = spriteRenderer.flipX ? Vector2.left : Vector2.right;

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null) bulletScript.SetDirection(direction);
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D c = col != null ? col : GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = Color.yellow;
        Vector2 checkPoint = new Vector2(c.bounds.center.x, c.bounds.min.y);
        Gizmos.DrawWireSphere(checkPoint, groundCheckRadius);
    }
}
