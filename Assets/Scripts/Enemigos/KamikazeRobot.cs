using UnityEngine;

// Robot kamikaze: al ver al jugador corre hacia él, se detiene un instante a "cargar" (aviso con el láser del ojo),
// se lanza en arco y explota al tocar al jugador o al caer. Si lo destruyen antes, también explota.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class KamikazeRobot : MonoBehaviour, IDamageable
{
    private enum State { Esperando, Corriendo, Cargando, Lanzado, Explotado }

    [Header("Vida")]
    [SerializeField] private float maxHealth = 20f;

    [Header("Persecución")]
    [SerializeField] private float detectionRadius = 14f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float runSpeed = 6.5f;
    [Tooltip("Variación aleatoria de la velocidad por robot (0.12 = ±12%), para que las estampidas se dispersen")]
    [SerializeField] private float speedVariation = 0.12f;
    [Tooltip("A esta distancia horizontal deja de correr y se prepara para lanzarse")]
    [SerializeField] private float triggerDistance = 4.5f;

    [Header("Lanzamiento")]
    [Tooltip("Tiempo agachado antes de saltar: es el aviso para que el jugador reaccione")]
    [SerializeField] private float chargeTime = 0.55f;
    [SerializeField] private Vector2 leapVelocity = new Vector2(15f, 6.5f);
    [SerializeField] private float leapGravityScale = 3.5f;
    [Tooltip("Si en este tiempo no ha tocado nada, explota en el aire")]
    [SerializeField] private float maxLeapTime = 1.0f;

    [Header("Explosión")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float explosionRadius = 2.8f;
    [Tooltip("Daño en el centro de la explosión; baja hasta la mitad en el borde del radio")]
    [SerializeField] private float explosionDamage = 35f;
    [SerializeField] private float contactDistance = 1.3f;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private AudioClip alertSound;

    [Header("Animación")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private const string RunState = "Kamikaze_Correr";
    private const string ChargeState = "Kamikaze_Carga";
    private const string LeapState = "Kamikaze_Salto";

    private Rigidbody2D rb;
    private Collider2D col;
    private Transform player;
    private State state = State.Esperando;
    private float health;
    private float stateTime;
    private int dir = -1;   // -1 izquierda, 1 derecha
    private float baseGravity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        health = maxHealth;
        baseGravity = rb.gravityScale;
        runSpeed *= 1f + Random.Range(-speedVariation, speedVariation);
    }

    private void Update()
    {
        if (state == State.Explotado) return;
        stateTime += Time.deltaTime;

        // Cae fuera del nivel (p. ej. al agua del río)
        if (transform.position.y < -8f) { Destroy(gameObject); return; }

        FindPlayer();

        switch (state)
        {
            case State.Esperando:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (player != null) Enter(State.Corriendo);
                break;

            case State.Corriendo:
                if (player == null) { Enter(State.Esperando); break; }
                float dx = player.position.x - transform.position.x;
                Face(dx);
                rb.linearVelocity = new Vector2(dir * runSpeed, rb.linearVelocity.y);
                if (Mathf.Abs(dx) <= triggerDistance) Enter(State.Cargando);
                break;

            case State.Cargando:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (player != null) Face(player.position.x - transform.position.x);
                if (stateTime >= chargeTime) Enter(State.Lanzado);
                break;

            case State.Lanzado:
                if (stateTime >= maxLeapTime) Explode();
                break;
        }

        // Si llega a tocar al jugador en cualquier momento, explota
        if (player != null && state != State.Esperando &&
            Vector2.Distance(col.bounds.center, player.GetComponent<Collider2D>().bounds.center) <= contactDistance)
        {
            Explode();
        }
    }

    private void FindPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        player = (hit != null && hit.CompareTag("Player")) ? hit.transform : null;
    }

    private void Enter(State next)
    {
        state = next;
        stateTime = 0f;

        switch (next)
        {
            case State.Esperando:
            case State.Corriendo:
                Play(RunState);
                if (animator != null) animator.speed = next == State.Esperando ? 0f : 1f;
                break;

            case State.Cargando:
                Play(ChargeState);
                SoundFX.Play(alertSound);
                break;

            case State.Lanzado:
                Play(LeapState);
                rb.gravityScale = leapGravityScale;
                rb.linearVelocity = new Vector2(dir * leapVelocity.x, leapVelocity.y);
                break;
        }
    }

    private void Play(string stateName)
    {
        if (animator == null) return;
        animator.speed = 1f;
        animator.Play(stateName, 0, 0f);
    }

    private void Face(float dx)
    {
        if (Mathf.Abs(dx) > 0.05f) dir = dx < 0f ? -1 : 1;
        // Los cuadros miran a la derecha
        if (spriteRenderer != null) spriteRenderer.flipX = dir < 0;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Durante el salto, cualquier choque (suelo, plataforma, jugador) lo detona.
        // Se ignoran los primeros instantes para que no explote al despegar del suelo.
        if (state == State.Lanzado && stateTime > 0.15f) Explode();
    }

    public void TakeDamage(float amount)
    {
        if (state == State.Explotado) return;
        health -= amount;
        if (health <= 0f) Explode();
    }

    private void Explode()
    {
        if (state == State.Explotado) return;
        state = State.Explotado;

        Vector2 center = col.bounds.center;
        if (explosionPrefab != null) Instantiate(explosionPrefab, center, Quaternion.identity);
        SoundFX.Play(explosionSound);
        CameraFollow.Shake(0.35f, 0.35f);

        // Daño al jugador según la distancia
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, explosionRadius, playerLayer))
        {
            if (!hit.CompareTag("Player")) continue;
            PlayerHealth hp = hit.GetComponent<PlayerHealth>();
            if (hp == null) continue;
            float d = Vector2.Distance(center, hit.bounds.ClosestPoint(center));
            float k = 1f - Mathf.Clamp01(d / explosionRadius) * 0.5f;
            hp.TakeDamage(explosionDamage * k);
            break;
        }

        rb.gravityScale = baseGravity;
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, explosionRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position + Vector3.left * triggerDistance, transform.position + Vector3.right * triggerDistance);
    }
}
