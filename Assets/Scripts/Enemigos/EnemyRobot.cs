using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyRobot : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    [SerializeField] private float maxHealth = 30f;

    [Header("Patrulla")]
    [SerializeField] private float patrolDistance = 3f;
    [SerializeField] private float patrolSpeed = 2f;

    [Header("Detección")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Disparo")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 20f;
    [Tooltip("Segundos que dura la parte de disparos del sonido de ráfaga (sin contar el eco final). La animación de disparo se ajusta a esta duración y cada cuadro con fogonazo lanza una bala.")]
    [SerializeField] private float rafagaDuracion = 1.8f;
    [SerializeField] private float burstPause = 2f;
    [Tooltip("Al detectar al jugador espera un tiempo al azar entre estos dos valores (segundos) antes de la primera ráfaga, para que el jugador alcance a cubrirse o moverse")]
    [SerializeField, Min(0f)] private float reaccionMinima = 1f;
    [SerializeField, Min(0f)] private float reaccionMaxima = 2f;

    [Header("Animación")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool walkSpriteFacesLeftByDefault = false;
    [SerializeField] private bool shootSpriteFacesLeftByDefault = true;

    [Header("Ajuste visual (corrige el salto entre animaciones)")]
    [Tooltip("Transform que contiene el SpriteRenderer/Animator. Si lo dejas vacío, usa este mismo GameObject (sin corrección posible).")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Vector2 walkVisualOffset = Vector2.zero;
    [SerializeField] private Vector2 shootVisualOffset = Vector2.zero;

    [Header("Sonidos")]
    [Tooltip("Suena al empezar cada ráfaga")]
    [SerializeField] private AudioClip rafagaSound;

    private static readonly int IsShootingHash = Animator.StringToHash("IsShooting");
    private const string DisparoStateName = "Disparo";

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 spawnPosition;
    private int patrolDirection = 1;
    private Transform player;
    private bool facingLeft;
    private float fireCooldown;
    private bool bursting;
    private float burstEndTime;
    private float health;
    private Vector3 firePointLocal;
    private AudioSource audioSource;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (visualRoot == null) visualRoot = transform;
        spriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = visualRoot.GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (firePoint != null) firePointLocal = firePoint.localPosition;
        // Fuente propia: si el robot muere a mitad de la ráfaga, el sonido se corta junto con las balas
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        spawnPosition = transform.position;
        health = maxHealth;
    }

    private void Update()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        bool loVeia = player != null;
        player = (hit != null && hit.CompareTag("Player")) ? hit.transform : null;

        // Recién lo detecta: apunta un momento antes de disparar
        if (player != null && !loVeia && !bursting)
            fireCooldown = Mathf.Max(fireCooldown, Random.Range(reaccionMinima, reaccionMaxima));

        if (bursting)
        {
            // Una ráfaga empezada se termina aunque el jugador salga del radio
            if (player != null) FacePlayer();
            else rb.linearVelocity = Vector2.zero;

            if (Time.time >= burstEndTime)
            {
                bursting = false;
                fireCooldown = burstPause;
            }
        }
        else if (player != null)
        {
            FacePlayer();

            fireCooldown -= Time.deltaTime;
            if (fireCooldown <= 0f) StartBurst();
        }
        else
        {
            Patrol();
        }

        UpdateAnimator();
    }

    private void StartBurst()
    {
        bursting = true;
        burstEndTime = Time.time + rafagaDuracion;

        if (rafagaSound != null)
        {
            audioSource.clip = rafagaSound;
            audioSource.Play();
        }

        if (animator == null) return;

        // Ajusta la velocidad para que la animación completa dure lo mismo que los disparos del sonido
        animator.speed = 1f;
        animator.Play(DisparoStateName, 0, 0f);
        animator.Update(0f);
        float clipLength = animator.GetCurrentAnimatorStateInfo(0).length;
        animator.speed = clipLength / rafagaDuracion;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool shootingMode = bursting || player != null;
        animator.SetBool(IsShootingHash, shootingMode);

        if (!shootingMode)
        {
            animator.speed = 1f;
        }
        else if (!bursting)
        {
            // Entre ráfagas se queda apuntando en el primer cuadro, que no tiene fogonazo
            animator.speed = 0f;
            animator.Play(DisparoStateName, 0, 0f);
        }
    }

    // Lo llaman los eventos de animación de RobotPesado_Disparo en los cuadros con fogonazo
    public void DisparoDesdeAnimacion()
    {
        if (bursting) Shoot();
    }

    private void FacePlayer()
    {
        facingLeft = player.position.x < transform.position.x;
        ApplyVisual(shootSpriteFacesLeftByDefault, shootVisualOffset);
        rb.linearVelocity = Vector2.zero;
    }

    private void Patrol()
    {
        float leftLimit = spawnPosition.x - patrolDistance;
        float rightLimit = spawnPosition.x + patrolDistance;

        if (transform.position.x <= leftLimit) patrolDirection = 1;
        else if (transform.position.x >= rightLimit) patrolDirection = -1;

        rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);
        facingLeft = patrolDirection < 0;
        ApplyVisual(walkSpriteFacesLeftByDefault, walkVisualOffset);
    }

    private void ApplyVisual(bool spriteFacesLeftByDefault, Vector2 offset)
    {
        spriteRenderer.flipX = spriteFacesLeftByDefault ? !facingLeft : facingLeft;

        if (facingLeft) offset.x = -offset.x;
        visualRoot.localPosition = offset;

        // El cañón debe quedar del lado hacia el que mira el robot
        if (firePoint != null)
        {
            float x = Mathf.Abs(firePointLocal.x);
            firePoint.localPosition = new Vector3(facingLeft ? -x : x, firePointLocal.y, firePointLocal.z);
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null || firePoint == null) return;

        Vector2 direction = facingLeft ? Vector2.left : Vector2.right;

        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bulletScript = bulletObj.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetDirection(direction);
            bulletScript.SetSpeed(bulletSpeed);
            bulletScript.SetIsEnemyBullet(true);
        }
    }

    public void TakeDamage(float amount)
    {
        health -= amount;

        if (health <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnValidate()
    {
        if (reaccionMaxima < reaccionMinima) reaccionMaxima = reaccionMinima;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.cyan;
        Vector3 pos = Application.isPlaying ? (Vector3)spawnPosition : transform.position;
        Gizmos.DrawLine(pos + Vector3.left * patrolDistance, pos + Vector3.right * patrolDistance);
    }
}
