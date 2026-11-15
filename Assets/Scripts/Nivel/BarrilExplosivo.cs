using UnityEngine;

// Barril (o botella) de combustible: explota al recibir suficiente daño o al chocar contra él
// un jefe embistiendo. La explosión hace daño en área a enemigos (mucho a los jefes) y,
// menos, al jugador si está muy cerca.
[RequireComponent(typeof(Collider2D))]
public class BarrilExplosivo : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    [Tooltip("Con el daño de bala actual (10): 20 = el primer disparo lo daña y el segundo lo hace explotar")]
    [SerializeField] private float vida = 20f;

    [Header("Aspecto")]
    [SerializeField] private SpriteRenderer visual;
    [Tooltip("Cuadros en reposo (goteando), en bucle")]
    [SerializeField] private Sprite[] reposo;
    [SerializeField] private float cuadrosPorSegundo = 3f;
    [Tooltip("Destello del símbolo de inflamable, cada cierto tiempo")]
    [SerializeField] private Sprite destello;
    [SerializeField] private Vector2 intervaloDestello = new Vector2(1.5f, 3.5f);
    [SerializeField] private float duracionDestello = 0.15f;
    [Tooltip("Aspecto al recibir el primer daño (abollado y en llamas)")]
    [SerializeField] private Sprite danado;
    [SerializeField] private AudioClip sonidoDanado;

    [Header("Explosión")]
    [SerializeField] private float radio = 3f;
    [Tooltip("Daño a enemigos y jefes dentro del radio")]
    [SerializeField] private float danioEnemigos = 30f;
    [Tooltip("Daño al jugador si está dentro del radio")]
    [SerializeField] private float danioJugador = 20f;
    [SerializeField] private LayerMask capasAfectadas = ~0;
    [SerializeField] private GameObject prefabExplosion;
    [SerializeField] private float escalaExplosion = 1f;
    [SerializeField] private AudioClip sonidoExplosion;

    private bool explotado;
    private bool estaDanado;
    private float vidaInicial;
    private float t;
    private float proximoDestello;
    private float finDestello;
    private Vector3 posVisual;

    private void Awake()
    {
        vidaInicial = vida;
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        if (visual != null) posVisual = visual.transform.localPosition;
        t = Random.Range(0f, 2f);   // que no goteen todos al mismo tiempo
        proximoDestello = Time.time + Random.Range(intervaloDestello.x, intervaloDestello.y);
    }

    private void Update()
    {
        if (visual == null || explotado) return;
        t += Time.deltaTime;

        if (estaDanado)
        {
            // Dañado: tiembla un poco, a punto de reventar
            visual.transform.localPosition = posVisual + new Vector3(Mathf.Sin(t * 45f) * 0.02f, 0f, 0f);
            return;
        }

        if (Time.time >= proximoDestello)
        {
            finDestello = Time.time + duracionDestello;
            proximoDestello = Time.time + Random.Range(intervaloDestello.x, intervaloDestello.y);
        }
        if (destello != null && Time.time < finDestello) visual.sprite = destello;
        else if (reposo != null && reposo.Length > 0) visual.sprite = reposo[Mathf.FloorToInt(t * cuadrosPorSegundo) % reposo.Length];
    }

    public void TakeDamage(float amount)
    {
        if (explotado) return;
        vida -= amount;
        if (vida <= 0f) { Explotar(); return; }

        // Primer daño: se abolla y se prende (el siguiente lo hace explotar)
        if (!estaDanado && vida < vidaInicial)
        {
            estaDanado = true;
            if (visual != null && danado != null) visual.sprite = danado;
            SoundFX.Play(sonidoDanado);
        }
    }

    public void Explotar()
    {
        if (explotado) return;
        explotado = true;

        Vector2 centro = GetComponent<Collider2D>().bounds.center;
        if (prefabExplosion != null)
        {
            GameObject e = Instantiate(prefabExplosion, centro, Quaternion.identity);
            e.transform.localScale = Vector3.one * escalaExplosion;
        }
        SoundFX.Play(sonidoExplosion);
        CameraFollow.Shake(0.35f, 0.35f);

        // Se desactiva antes de repartir el daño para que una cadena de barriles no vuelva a dañarlo
        gameObject.SetActive(false);

        // Cada objeto recibe daño una sola vez aunque tenga varios colliders
        var yaDaniados = new System.Collections.Generic.HashSet<Object>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(centro, radio, capasAfectadas))
        {
            if (hit.CompareTag("Player"))
            {
                var hp = hit.GetComponent<PlayerHealth>();
                if (hp != null && yaDaniados.Add(hp)) hp.TakeDamage(danioJugador);
                continue;
            }

            var danio = hit.GetComponent<IDamageable>();
            if (danio == null) continue;
            // El jefe lo maneja con su propio multiplicador; los demás reciben el daño normal
            var jefe = hit.GetComponentInParent<JefeBase>();
            if (jefe != null) { if (yaDaniados.Add(jefe)) jefe.RecibirDanioExplosion(danioEnemigos); }
            else if (danio is Object o && yaDaniados.Add(o)) danio.TakeDamage(danioEnemigos);
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.8f, radio);
    }
}
