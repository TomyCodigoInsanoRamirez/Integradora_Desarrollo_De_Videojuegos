using System;
using System.Collections;
using UnityEngine;

// Punto de control: al pasar el jugador queda guardado como el activo. Si el jugador muere
// después, reaparece aquí con la vida llena (PlayerHealth lo llama) y se avisa a quien
// necesite reiniciarse (por ejemplo la arena del jefe) con el evento AlReaparecer.
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    public static Checkpoint Activo { get; private set; }

    // Se lanza justo después de que el jugador reaparece
    public static event Action AlReaparecer;

    [Tooltip("Segundos que el jugador queda oculto antes de reaparecer")]
    [SerializeField] private float esperaReaparicion = 1.2f;
    [Tooltip("Dónde reaparece el jugador. Vacío = la posición de este objeto")]
    [SerializeField] private Transform puntoReaparicion;
    [Tooltip("Opcional: se enciende al activarse (por ejemplo una luz o bandera)")]
    [SerializeField] private GameObject indicadorActivo;
    [SerializeField] private AudioClip sonidoActivar;

    [Header("Torre (opcional)")]
    [SerializeField] private SpriteRenderer torre;
    [Tooltip("Apagado: la luz roja parpadea entre estos cuadros")]
    [SerializeField] private Sprite[] cuadrosInactivo;
    [Tooltip("Se reproduce una vez al activarse")]
    [SerializeField] private Sprite[] cuadrosActivacion;
    [Tooltip("Encendido (verde)")]
    [SerializeField] private Sprite[] cuadrosActivo;
    [SerializeField] private float cuadrosPorSegundo = 4f;
    [Tooltip("Brillo suave alrededor de la luz (se tiñe y pulsa por código)")]
    [SerializeField] private SpriteRenderer brillo;
    [SerializeField] private Color colorInactivo = new Color(1f, 0.15f, 0.1f, 0.55f);
    [SerializeField] private Color colorActivo = new Color(0.2f, 1f, 0.3f, 0.7f);

    private float tAnim;
    private bool activando;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (indicadorActivo != null) indicadorActivo.SetActive(false);
    }

    private void Update()
    {
        tAnim += Time.deltaTime;
        bool activo = Activo == this;
        Sprite[] cuadros = activando ? cuadrosActivacion : activo ? cuadrosActivo : cuadrosInactivo;

        if (torre != null && cuadros != null && cuadros.Length > 0)
        {
            int i = Mathf.FloorToInt(tAnim * cuadrosPorSegundo);
            if (activando && i >= cuadros.Length) { activando = false; tAnim = 0f; }
            else torre.sprite = cuadros[activando ? i : i % cuadros.Length];
        }

        if (brillo != null)
        {
            // Rojo que parpadea lento apagado; verde que respira encendido
            float pulso = activo ? 0.75f + 0.25f * Mathf.Sin(tAnim * 3f) : (Mathf.Sin(tAnim * 6f) > 0f ? 1f : 0.25f);
            Color c = activo || activando ? colorActivo : colorInactivo;
            c.a *= pulso;
            brillo.color = c;
        }
    }

    private void OnDestroy()
    {
        if (Activo == this) Activo = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Activo == this || !other.CompareTag("Player")) return;
        if (Activo != null && Activo.indicadorActivo != null) Activo.indicadorActivo.SetActive(false);

        Activo = this;
        activando = cuadrosActivacion != null && cuadrosActivacion.Length > 0;
        tAnim = 0f;
        if (indicadorActivo != null) indicadorActivo.SetActive(true);
        SoundFX.Play(sonidoActivar);
    }

    public void Reaparecer(PlayerHealth jugador)
    {
        StartCoroutine(RutinaReaparecer(jugador));
    }

    private IEnumerator RutinaReaparecer(PlayerHealth jugador)
    {
        jugador.gameObject.SetActive(false);
        yield return new WaitForSeconds(esperaReaparicion);

        Vector3 destino = puntoReaparicion != null ? puntoReaparicion.position : transform.position;
        jugador.transform.position = destino;
        var rb = jugador.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        jugador.Revivir();
        jugador.gameObject.SetActive(true);
        AlReaparecer?.Invoke();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 p = puntoReaparicion != null ? puntoReaparicion.position : transform.position;
        Gizmos.DrawWireCube(p + Vector3.up, new Vector3(0.6f, 2f, 0f));
    }
}
