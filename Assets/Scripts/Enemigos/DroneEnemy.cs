using UnityEngine;

// Dron enemigo: vuela por encima del jugador, se coloca delante de él en diagonal, baja el arma
// y lo ametralla hacia abajo. Mientras dispara se queda quieto (es el momento de meterse debajo
// y dispararle hacia arriba). Si el jugador sale del ángulo de tiro, guarda el arma y se reacomoda.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class DroneEnemy : MonoBehaviour, IDamageable
{
    private enum State { Reposicionando, Apuntando, Disparando, Guardando, Muerto }

    [Header("Vida")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private GameObject deathExplosionPrefab;
    [SerializeField] private float deathExplosionScale = 0.6f;
    [SerializeField] private AudioClip deathSound;

    [Header("Vuelo")]
    [SerializeField] private float detectionRadius = 16f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float flySpeed = 6f;
    [Tooltip("Altura del dron (borde superior) sobre los pies del jugador")]
    [SerializeField] private float hoverHeight = 5.6f;
    [Tooltip("Distancia horizontal que guarda respecto al jugador para dispararle en diagonal")]
    [SerializeField] private float preferredDistance = 1.6f;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobSpeed = 3f;

    [Header("Disparo")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 16f;
    [SerializeField] private float fireInterval = 0.2f;
    [SerializeField] private float burstDuration = 1.2f;
    [Tooltip("Dispersión aleatoria de cada bala, en grados (para que no todas acierten)")]
    [SerializeField] private float spread = 5f;
    [SerializeField] private float cooldown = 1.1f;
    [Tooltip("Al detectar al jugador espera un tiempo al azar entre estos dos valores (segundos) antes de poder disparar, para que el jugador alcance a reaccionar")]
    [SerializeField, Min(0f)] private float reaccionMinima = 1f;
    [SerializeField, Min(0f)] private float reaccionMaxima = 2f;
    [Tooltip("Boca del cañón respecto al dron mirando a la derecha (unidades locales del Visual)")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0.30f, -1.15f);
    [Tooltip("Ángulos permitidos por debajo de la horizontal (grados): fuera de este rango no puede apuntar")]
    [SerializeField] private Vector2 aimAngleRange = new Vector2(35f, 88f);
    [SerializeField] private AudioClip burstSound;
    [Tooltip("Zumbido de acercamiento. Quien crea el dron (EnemySpawnPoint, el jefe) lo empieza antes de que aparezca; si no, suena al aparecer")]
    [SerializeField] private AudioClip aproximacionSound;
    [Range(0f, 1f)] [SerializeField] private float aproximacionVolumen = 0.8f;

    public AudioClip AproximacionSound => aproximacionSound;
    public float AproximacionVolumen => aproximacionVolumen;

    [Header("Animación")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private const string FlyState = "Dron_Volar";
    private const string AimState = "Dron_Apuntar";
    private const string FireState = "Dron_Disparo";
    private const string StowState = "Dron_Guardar";

    private Rigidbody2D rb;
    private Transform player;
    private Collider2D playerCol;
    private AudioSource audioSource;
    private bool aproximacionAsignada;
    private State state = State.Reposicionando;
    private float health;
    private float stateTime;
    private float fireTimer;
    private float cooldownTimer;
    private bool facingLeft = true;
    private Vector2 homePosition;
    private float bobPhase;
    private float hitFlash;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        health = maxHealth;
        homePosition = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    // Quien crea el dron le pasa el sonido que ya venía sonando (empezó antes de que apareciera)
    public void UsarSonidoAcercamiento(SonidoAcercamiento sonido)
    {
        aproximacionAsignada = true;
        if (sonido != null) sonido.Seguir(transform);
    }

    private void Start()
    {
        // Nadie le pasó el sonido: lo empieza él mismo al aparecer
        if (!aproximacionAsignada && aproximacionSound != null)
        {
            var s = SonidoAcercamiento.Crear(aproximacionSound, transform.position, aproximacionVolumen);
            if (s != null) s.Seguir(transform);
        }
    }

    private void Update()
    {
        if (state == State.Muerto) return;
        stateTime += Time.deltaTime;
        cooldownTimer -= Time.deltaTime;
        bool loVeia = player != null;
        FindPlayer();

        // Recién lo detecta: le da un momento al jugador antes de poder dispararle
        if (player != null && !loVeia)
            cooldownTimer = Mathf.Max(cooldownTimer, Random.Range(reaccionMinima, reaccionMaxima));

        if (player != null) facingLeft = player.position.x < transform.position.x;
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = facingLeft;   // los cuadros miran a la derecha
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 6f);
            spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f), hitFlash);
        }

        switch (state)
        {
            case State.Reposicionando:
                if (player != null && cooldownTimer <= 0f && AtTarget() && CanAim(out _)) Enter(State.Apuntando);
                break;

            case State.Apuntando:
                if (stateTime >= 0.25f) Enter(State.Disparando);
                break;

            case State.Disparando:
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0f)
                {
                    fireTimer = fireInterval;
                    if (CanAim(out Vector2 dir)) Shoot(dir);
                    else { Enter(State.Guardando); break; }
                }
                if (stateTime >= burstDuration) Enter(State.Guardando);
                break;

            case State.Guardando:
                if (stateTime >= 0.33f) { cooldownTimer = cooldown; Enter(State.Reposicionando); }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (state == State.Muerto) return;

        // Solo se desplaza mientras se reacomoda; al apuntar y disparar se queda quieto (con un leve vaivén)
        Vector2 target = state == State.Reposicionando ? Target() : (Vector2)rb.position;
        float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobAmplitude;
        Vector2 next = Vector2.MoveTowards(rb.position, target, flySpeed * Time.fixedDeltaTime);
        next.y += (bob - Mathf.Sin((Time.time - Time.fixedDeltaTime) * bobSpeed + bobPhase) * bobAmplitude);
        rb.MovePosition(next);
    }

    private Vector2 Target()
    {
        if (player == null) return homePosition;
        // Se queda del lado en el que está y a la distancia preferida
        float side = transform.position.x >= player.position.x ? 1f : -1f;
        return new Vector2(player.position.x + side * preferredDistance, player.position.y + hoverHeight);
    }

    private bool AtTarget() => Vector2.Distance(rb.position, Target()) < 0.35f;

    private Vector2 Muzzle()
    {
        Vector3 s = spriteRenderer != null ? spriteRenderer.transform.lossyScale : Vector3.one;
        Vector2 o = new Vector2(muzzleOffset.x * s.x * (facingLeft ? -1f : 1f), muzzleOffset.y * s.y);
        return (Vector2)transform.position + o;
    }

    // ¿El jugador está dentro del ángulo de tiro (hacia abajo y hacia el frente)?
    private bool CanAim(out Vector2 dir)
    {
        dir = Vector2.down;
        if (player == null) return false;
        Vector2 chest = playerCol != null ? (Vector2)playerCol.bounds.center : (Vector2)player.position + Vector2.up;
        Vector2 d = chest - Muzzle();
        float below = Mathf.Atan2(-d.y, Mathf.Abs(d.x)) * Mathf.Rad2Deg;   // grados por debajo de la horizontal
        bool inFront = (d.x < 0f) == facingLeft;
        dir = d.normalized;
        return inFront && below >= aimAngleRange.x && below <= aimAngleRange.y;
    }

    private void Shoot(Vector2 dir)
    {
        if (bulletPrefab == null) return;
        dir = Quaternion.Euler(0f, 0f, Random.Range(-spread, spread)) * dir;
        GameObject b = Instantiate(bulletPrefab, Muzzle(), Quaternion.identity);
        Bullet bullet = b.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.SetIsEnemyBullet(true);
            bullet.SetSpeed(bulletSpeed);
            bullet.SetDirection(dir);
        }
    }

    private void Enter(State next)
    {
        state = next;
        stateTime = 0f;
        switch (next)
        {
            case State.Reposicionando: Play(FlyState); audioSource.Stop(); break;
            case State.Apuntando: Play(AimState); break;
            case State.Disparando:
                Play(FireState);
                fireTimer = 0f;
                if (burstSound != null) { audioSource.clip = burstSound; audioSource.Play(); }
                break;
            case State.Guardando: Play(StowState); audioSource.Stop(); break;
        }
    }

    private void Play(string s)
    {
        if (animator != null) animator.Play(s, 0, 0f);
    }

    private void FindPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        if (hit != null && hit.CompareTag("Player")) { player = hit.transform; playerCol = hit; }
        else { player = null; playerCol = null; }
    }

    public void TakeDamage(float amount)
    {
        if (state == State.Muerto) return;
        health -= amount;
        hitFlash = 1f;
        if (health > 0f) return;

        state = State.Muerto;
        audioSource.Stop();
        if (deathExplosionPrefab != null)
        {
            Vector3 c = GetComponent<Collider2D>().bounds.center;
            GameObject e = Instantiate(deathExplosionPrefab, c, Quaternion.identity);
            e.transform.localScale = Vector3.one * deathExplosionScale;
        }
        SoundFX.Play(deathSound);
        CameraFollow.Shake(0.15f, 0.2f);
        Destroy(gameObject);
    }

    private void OnValidate()
    {
        if (reaccionMaxima < reaccionMinima) reaccionMaxima = reaccionMinima;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        if (Application.isPlaying) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(Muzzle(), 0.15f); }
    }
}
