using UnityEngine;

// Objeto que el jugador recoge al tocarlo: monedas o kit de reparación.
// Puede salir disparado al aparecer (botín de un jefe) y flota un poco mientras espera.
[RequireComponent(typeof(Collider2D))]
public class Recogible : MonoBehaviour
{
    public enum Tipo { Moneda, KitReparacion }

    [SerializeField] private Tipo tipo = Tipo.Moneda;
    [Tooltip("Monedas que da (Moneda) o vida que recupera (KitReparacion)")]
    [SerializeField] private float cantidad = 1f;
    [SerializeField] private AudioClip sonido;
    [Tooltip("Efecto que aparece al recogerlo (por ejemplo el destello de la moneda)")]
    [SerializeField] private GameObject efectoRecoger;

    [Header("Movimiento")]
    [SerializeField] private float gravedad = 25f;
    [Tooltip("Altura del suelo donde se detiene al caer")]
    [SerializeField] private float sueloY = 0f;
    [SerializeField] private float flotarAmplitud = 0.12f;
    [SerializeField] private float flotarVelocidad = 3f;
    [Tooltip("Segundos antes de poder recogerlo (para que se vea el botín salir)")]
    [SerializeField] private float esperaRecoger = 0.4f;

    private Vector2 velocidad;
    private bool enSuelo;
    private float baseY;
    private float creado;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        creado = Time.time;
        enSuelo = true;
        baseY = transform.position.y;
    }

    // Lanza el objeto con una velocidad inicial (cae hasta 'suelo')
    public void Lanzar(Vector2 velocidadInicial, float suelo)
    {
        velocidad = velocidadInicial;
        sueloY = suelo;
        enSuelo = false;
    }

    private void Update()
    {
        if (!enSuelo)
        {
            velocidad.y -= gravedad * Time.deltaTime;
            Vector3 p = transform.position + (Vector3)(velocidad * Time.deltaTime);
            if (p.y <= sueloY && velocidad.y < 0f)
            {
                p.y = sueloY;
                enSuelo = true;
                baseY = sueloY;
            }
            transform.position = p;
            return;
        }

        Vector3 pos = transform.position;
        pos.y = baseY + (Mathf.Sin(Time.time * flotarVelocidad) * 0.5f + 0.5f) * flotarAmplitud;
        transform.position = pos;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time - creado < esperaRecoger || !other.CompareTag("Player")) return;

        if (tipo == Tipo.Moneda)
        {
            if (GameManager.Instance != null) GameManager.Instance.AgregarMonedas(Mathf.RoundToInt(cantidad));
        }
        else
        {
            var hp = other.GetComponent<PlayerHealth>();
            if (hp == null) return;
            hp.Curar(cantidad);
        }

        SoundFX.Play(sonido);
        if (efectoRecoger != null)
        {
            Vector3 centro = GetComponent<Collider2D>().bounds.center;
            Instantiate(efectoRecoger, centro, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
