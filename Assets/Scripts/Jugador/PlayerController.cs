using System.Collections;
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
    [SerializeField] private float bulletSpeed = 30f;
    [SerializeField] private Vector2 firePointOffsetFrente = new Vector2(0.4f, 0.3f);
    [SerializeField] private Vector2 firePointOffsetArriba = new Vector2(0.15f, 0.75f);
    [SerializeField] private Vector2 firePointOffsetAgachado = new Vector2(0.4f, 0.05f);

    [Header("Golpe con Tubo")]
    [SerializeField]
    private string[] golpeTuboStates =
    {
        "GolpeTubo1", "GolpeTubo2", "GolpeTubo3", "GolpeTubo4", "GolpeTubo5"
    };
    [Tooltip("Distancia que alcanza el tubo por delante del cuerpo")]
    [SerializeField] private float golpeAlcance = 1.4f;
    [SerializeField] private float golpeDanio = 10f;

    [Header("Agachado")]
    [Tooltip("Altura del collider al agacharse, como fracción de la altura de pie (la base se queda en los pies)")]
    [Range(0.3f, 1f)]
    [SerializeField] private float alturaAgachado = 0.75f;

    [Header("Sonidos")]
    [SerializeField] private AudioClip disparoSound;
    [SerializeField] private AudioClip golpeFallidoSound;
    [SerializeField] private AudioClip[] golpeImpactoSounds;
    [Tooltip("Pasos en bucle mientras corre por el suelo")]
    [SerializeField] private AudioClip correrSound;
    [SerializeField] private AudioClip caidaSound;
    [Tooltip("Altura mínima (desde el punto más alto) para que suene la caída al aterrizar")]
    [SerializeField] private float alturaMinimaCaida = 1.5f;

    private const string DisparoStateName = "Disparar";
    private const string DisparoArribaStateName = "DispararArriba";
    private const string DisparoAgachadoStateName = "DispararAgachado";
    private const string DisparoAvanzandoStateName = "DispararAvanzando";
    private const string DisparoArribaAvanzandoStateName = "DispararArribaAvanzando";
    private const string DisparoAgachadoAvanzandoStateName = "DispararAgachadoAvanzando";
    private const string IdleStateName = "Idle";
    private const string CorrerStateName = "Correr";

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int DisparoArribaHash = Animator.StringToHash("DisparoArriba");
    private static readonly int DisparoAgachadoHash = Animator.StringToHash("DisparoAgachado");

    private Rigidbody2D rb;
    private Collider2D col;
    private float moveInput;
    private bool isGrounded;
    private bool jumpRequested;
    private bool shootRequested;
    private bool aimUp;
    private bool aimDown;
    private bool shootPoseFrozen;
    private bool jumpKeyPressed;
    private bool isGolpeando;
    private int lastGolpeIndex = -1;
    private Coroutine disparoAvanzandoRoutine;
    private string currentAvanzandoState;
    private CapsuleCollider2D capsule;
    private Vector2 capsuleSizeDePie;
    private Vector2 capsuleOffsetDePie;
    private AudioSource pasosSource;
    private float alturaMaximaEnAire;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        capsule = col as CapsuleCollider2D;
        if (capsule != null)
        {
            capsuleSizeDePie = capsule.size;
            capsuleOffsetDePie = capsule.offset;
        }
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        pasosSource = gameObject.AddComponent<AudioSource>();
        pasosSource.clip = correrSound;
        pasosSource.loop = true;
        pasosSource.playOnAwake = false;
        pasosSource.spatialBlend = 0f;
    }

    private void Update()
    {
        moveInput = 0f;
        bool movementKeyPressed = false;
        bool golpeKeyPressed = false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput += 1f;

            jumpKeyPressed = keyboard.spaceKey.wasPressedThisFrame;
            if (jumpKeyPressed) jumpRequested = true;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) shootRequested = true;
            golpeKeyPressed = keyboard.digit1Key.wasPressedThisFrame;

            aimUp = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
            aimDown = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;

            movementKeyPressed = keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame ||
                keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame ||
                keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
                keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;
        }
        else
        {
            aimUp = false;
            aimDown = false;
            jumpKeyPressed = false;
        }

        if (shootPoseFrozen && (movementKeyPressed || jumpKeyPressed))
        {
            shootPoseFrozen = false;
            if (animator != null) animator.enabled = true;
        }

        if (isGolpeando)
        {
            moveInput = 0f;
            jumpRequested = false;
            shootRequested = false;
        }
        else if (golpeKeyPressed && !shootPoseFrozen &&
            GameManager.Instance != null && GameManager.Instance.HasTubo)
        {
            StartCoroutine(GolpeConTubo());
        }

        if (moveInput > 0f) spriteRenderer.flipX = false;
        else if (moveInput < 0f) spriteRenderer.flipX = true;

        if (animator != null && !shootPoseFrozen && !isGolpeando && disparoAvanzandoRoutine == null)
        {
            animator.SetBool(IsMovingHash, moveInput != 0f);
            animator.SetBool(IsGroundedHash, isGrounded);
            animator.SetBool(DisparoArribaHash, aimUp);
            animator.SetBool(DisparoAgachadoHash, aimDown);
        }

        if (shootRequested)
        {
            shootRequested = false;
            Shoot();
        }

        UpdatePasos();
    }

    // Pausa en lugar de detener para que al volver a correr los pasos sigan donde iban
    private void UpdatePasos()
    {
        if (pasosSource.clip == null) return;

        bool corriendo = isGrounded && moveInput != 0f && !isGolpeando;
        if (corriendo && !pasosSource.isPlaying)
        {
            if (pasosSource.time > 0f) pasosSource.UnPause();
            else pasosSource.Play();
        }
        else if (!corriendo && pasosSource.isPlaying)
        {
            pasosSource.Pause();
        }
    }

    private void LateUpdate()
    {
        if (capsule == null || animator == null) return;

        // Se consulta el estado aunque el Animator esté desactivado: la pose congelada al disparar sigue siendo la última
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool agachado = state.IsName(DisparoAgachadoStateName) || state.IsName(DisparoAgachadoAvanzandoStateName);

        float altura = capsuleSizeDePie.y * (agachado ? alturaAgachado : 1f);
        float baseY = capsuleOffsetDePie.y - capsuleSizeDePie.y * 0.5f;
        capsule.size = new Vector2(capsuleSizeDePie.x, altura);
        capsule.offset = new Vector2(capsuleOffsetDePie.x, baseY + altura * 0.5f);
    }

    private void FixedUpdate()
    {
        Vector2 checkPoint = new Vector2(col.bounds.center.x, col.bounds.min.y);
        bool wasGrounded = isGrounded;
        isGrounded = false;
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkPoint, groundCheckRadius);
        foreach (Collider2D hit in hits)
        {
            // Triggers (checkpoints, zonas, objetos) y colliders que el jugador atraviesa (el jefe) no son suelo
            if (hit != null && hit != col && !hit.isTrigger && !Physics2D.GetIgnoreCollision(col, hit))
            {
                isGrounded = true;
                break;
            }
        }

        // Se mide desde el punto más alto en el aire, así cuenta igual un salto que caer de una plataforma
        if (!isGrounded)
        {
            if (wasGrounded) alturaMaximaEnAire = rb.position.y;
            else alturaMaximaEnAire = Mathf.Max(alturaMaximaEnAire, rb.position.y);
        }
        else if (!wasGrounded)
        {
            if (alturaMaximaEnAire - rb.position.y >= alturaMinimaCaida) SoundFX.Play(caidaSound);
            alturaMaximaEnAire = rb.position.y;
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
        if (animator != null)
        {
            if (moveInput != 0f)
            {
                string avanzandoState = aimUp ? DisparoArribaAvanzandoStateName
                    : aimDown ? DisparoAgachadoAvanzandoStateName
                    : DisparoAvanzandoStateName;

                if (disparoAvanzandoRoutine == null || currentAvanzandoState != avanzandoState)
                {
                    if (disparoAvanzandoRoutine != null) StopCoroutine(disparoAvanzandoRoutine);
                    currentAvanzandoState = avanzandoState;
                    disparoAvanzandoRoutine = StartCoroutine(DispararAvanzandoRoutine(avanzandoState));
                }
            }
            else
            {
                if (disparoAvanzandoRoutine != null)
                {
                    StopCoroutine(disparoAvanzandoRoutine);
                    disparoAvanzandoRoutine = null;
                    currentAvanzandoState = null;
                }

                string staticState = aimUp ? DisparoArribaStateName
                    : aimDown ? DisparoAgachadoStateName
                    : DisparoStateName;

                animator.enabled = true;
                animator.Play(staticState, 0, 0f);
                animator.Update(0f);
                animator.enabled = false;
                shootPoseFrozen = true;
            }
        }

        if (bulletPrefab == null || firePoint == null) return;

        bool facingLeft = spriteRenderer.flipX;
        Vector2 sideDirection = facingLeft ? Vector2.left : Vector2.right;
        Vector2 offset;
        Vector2 direction;

        if (aimUp)
        {
            offset = firePointOffsetArriba;
            direction = Vector2.up;
        }
        else if (aimDown)
        {
            offset = firePointOffsetAgachado;
            direction = sideDirection;
        }
        else
        {
            offset = firePointOffsetFrente;
            direction = sideDirection;
        }

        if (facingLeft) offset.x = -offset.x;
        firePoint.position = (Vector2)transform.position + offset;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        SoundFX.Play(disparoSound);

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetDirection(direction);
            bulletScript.SetSpeed(bulletSpeed);
        }
    }

    private IEnumerator DispararAvanzandoRoutine(string stateName)
    {
        animator.enabled = true;
        animator.Play(stateName, 0, 0f);
        animator.Update(0f);

        float duration = animator.GetCurrentAnimatorStateInfo(0).length;
        float elapsed = 0f;
        while (elapsed < duration && moveInput != 0f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        disparoAvanzandoRoutine = null;
        currentAvanzandoState = null;

        if (!isGolpeando)
        {
            if (moveInput != 0f)
            {
                animator.Play(CorrerStateName, 0, 0f);
            }
            else if (aimUp)
            {
                animator.Play(DisparoArribaStateName, 0, 0f);
                animator.Update(0f);
                animator.enabled = false;
                shootPoseFrozen = true;
            }
            else if (aimDown)
            {
                animator.Play(DisparoAgachadoStateName, 0, 0f);
                animator.Update(0f);
                animator.enabled = false;
                shootPoseFrozen = true;
            }
            else
            {
                animator.Play(IdleStateName, 0, 0f);
            }
        }
    }

    private IEnumerator GolpeConTubo()
    {
        if (disparoAvanzandoRoutine != null)
        {
            StopCoroutine(disparoAvanzandoRoutine);
            disparoAvanzandoRoutine = null;
            currentAvanzandoState = null;
        }

        isGolpeando = true;

        int index = NextGolpeIndex();
        string stateName = golpeTuboStates[index];

        float duration = 0f;

        if (animator != null)
        {
            animator.enabled = true;
            animator.Play(stateName, 0, 0f);
            animator.Update(0f);
            duration = animator.GetCurrentAnimatorStateInfo(0).length;
        }

        yield return new WaitForSeconds(duration);

        isGolpeando = false;

        if (animator != null)
        {
            animator.Play(moveInput != 0f ? CorrerStateName : IdleStateName, 0, 0f);
        }
    }

    // Lo llaman los eventos de animación de GolpeTubo1-5 en el cuadro en que el tubo golpea
    public void GolpeTuboImpacto()
    {
        bool facingLeft = spriteRenderer.flipX;
        Bounds b = col.bounds;
        Vector2 center = new Vector2(
            b.center.x + (facingLeft ? -1f : 1f) * (b.extents.x + golpeAlcance * 0.5f),
            b.center.y);

        bool impacto = false;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, new Vector2(golpeAlcance, b.size.y), 0f))
        {
            IDamageable enemy = hit.GetComponent<IDamageable>();
            if (enemy != null)
            {
                enemy.TakeDamage(golpeDanio);
                impacto = true;
                break;
            }
        }

        if (impacto) SoundFX.PlayRandom(golpeImpactoSounds);
        else SoundFX.Play(golpeFallidoSound);
    }

    private int NextGolpeIndex()
    {
        if (golpeTuboStates.Length <= 1) return 0;

        int index;
        do
        {
            index = Random.Range(0, golpeTuboStates.Length);
        } while (index == lastGolpeIndex);

        lastGolpeIndex = index;
        return index;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D c = col != null ? col : GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = Color.yellow;
        Vector2 checkPoint = new Vector2(c.bounds.center.x, c.bounds.min.y);
        Gizmos.DrawWireSphere(checkPoint, groundCheckRadius);

        SpriteRenderer sr = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        bool facingLeft = sr != null && sr.flipX;

        DrawFirePointGizmo(firePointOffsetFrente, facingLeft, Color.yellow, "Frente");
        DrawFirePointGizmo(firePointOffsetArriba, facingLeft, Color.cyan, "Arriba");
        DrawFirePointGizmo(firePointOffsetAgachado, facingLeft, Color.magenta, "Agachado");

        Bounds b = c.bounds;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            new Vector3(b.center.x + (facingLeft ? -1f : 1f) * (b.extents.x + golpeAlcance * 0.5f), b.center.y, 0f),
            new Vector3(golpeAlcance, b.size.y, 0f));
    }

    private void DrawFirePointGizmo(Vector2 offset, bool facingLeft, Color color, string label)
    {
        if (facingLeft) offset.x = -offset.x;
        Vector3 worldPoint = transform.position + (Vector3)offset;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(worldPoint, 0.04f);
#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(worldPoint + new Vector3(0.05f, 0.05f, 0f), label);
#endif
    }
}
