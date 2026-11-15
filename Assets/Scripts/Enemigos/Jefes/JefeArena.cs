using System.Collections;
using UnityEngine;

// Arena del jefe del Nivel 1. Va en un objeto con un Collider2D en modo trigger que cubre la
// entrada de la arena. Cuando el jugador entra:
//   cierra las barreras, abre y fija la cámara a la arena, muestra la barra de vida,
//   cambia la música y activa al jefe.
// Si el jugador muere y reaparece en el checkpoint, todo vuelve al estado inicial.
// Al derrotar al jefe: suelta monedas y un kit de reparación, abre las barreras,
// libera la cámara y muestra el fin del nivel.
[RequireComponent(typeof(Collider2D))]
public class JefeArena : MonoBehaviour
{
    [Header("Jefe")]
    [SerializeField] private JefeBase jefe;

    [Header("Límites de la arena (X)")]
    [SerializeField] private float limiteIzquierdo = 393f;
    [SerializeField] private float limiteDerecho = 415f;
    [Tooltip("Altura del suelo de la arena")]
    [SerializeField] private float sueloY = 0f;

    [Header("Barreras")]
    [SerializeField] private JefeBarrera[] barreras;

    [Header("Cámara durante la pelea")]
    [Tooltip("Tamaño ortográfico (5 es el normal del nivel)")]
    [SerializeField] private float tamanoCamara = 6.2f;
    [Tooltip("Altura del centro de la cámara")]
    [SerializeField] private float centroYCamara = 3.7f;
    [SerializeField] private float duracionZoom = 1.2f;

    [Header("Música")]
    [Tooltip("Música del jefe (si está vacía, sigue la música normal del nivel)")]
    [SerializeField] private AudioClip musicaJefe;
    [Range(0f, 1f)] [SerializeField] private float volumenMusica = 0.25f;
    [SerializeField] private float fundidoMusica = 1.5f;
    [SerializeField] private GameplayMusic musicaNivel;

    [Header("UI")]
    [SerializeField] private JefeBarraVida barraVida;
    [SerializeField] private NivelCompletado nivelCompletado;

    [Header("Botín al derrotarlo")]
    [SerializeField] private GameObject prefabMoneda;
    [SerializeField] private int cantidadMonedas = 12;
    [SerializeField] private GameObject prefabKitReparacion;
    [Tooltip("Segundos desde la muerte del jefe hasta que suelta el botín")]
    [SerializeField] private float esperaBotin = 1.6f;

    [Header("Salida del nivel (después de ganar)")]
    [Tooltip("Muro invisible del final del nivel: se corre a la derecha para que el jugador pueda salir por la compuerta")]
    [SerializeField] private Transform limiteNivel;
    [SerializeField] private float xLimiteTrasVictoria = 428f;
    [Tooltip("Al pasar esta X (ya fuera de cuadro, del otro lado de la compuerta) empieza el final")]
    [SerializeField] private float xSalida = 415.6f;
    [Tooltip("Segundos desde que sale de cuadro hasta el fundido a negro")]
    [SerializeField] private float esperaTrasSalir = 1.5f;

    private bool activa;
    private bool terminada;
    private AudioSource musica;
    private Coroutine fundido;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        musica = gameObject.AddComponent<AudioSource>();
        musica.playOnAwake = false;
        musica.loop = true;
        musica.spatialBlend = 0f;
        musica.priority = 0;
        musica.volume = 0f;
        if (musicaNivel == null) musicaNivel = FindAnyObjectByType<GameplayMusic>();
        if (jefe != null) jefe.ConfigurarArena(limiteIzquierdo, limiteDerecho);
    }

    private void OnEnable()
    {
        Checkpoint.AlReaparecer += AlReaparecerJugador;
        if (jefe != null) { jefe.Derrotado += AlDerrotarJefe; jefe.ChocoConPared += AlChocarJefe; }
    }

    private void OnDisable()
    {
        Checkpoint.AlReaparecer -= AlReaparecerJugador;
        if (jefe != null) { jefe.Derrotado -= AlDerrotarJefe; jefe.ChocoConPared -= AlChocarJefe; }
    }

    // La embestida terminó contra una pared: se golpea la compuerta de ese lado
    private void AlChocarJefe(float direccion)
    {
        float centro = (limiteIzquierdo + limiteDerecho) * 0.5f;
        foreach (JefeBarrera b in barreras)
        {
            if (b == null) continue;
            bool lado = direccion < 0f ? b.transform.position.x < centro : b.transform.position.x > centro;
            if (lado) b.Golpear();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activa || terminada || jefe == null || !other.CompareTag("Player")) return;
        IniciarPelea(other.transform);
    }

    private void IniciarPelea(Transform jugador)
    {
        activa = true;
        foreach (JefeBarrera b in barreras) if (b != null) b.Cerrar();
        CameraFollow.FijarArena(limiteIzquierdo, limiteDerecho, centroYCamara, tamanoCamara, duracionZoom);
        if (barraVida != null) barraVida.Mostrar(jefe);
        CambiarMusica(true);
        jefe.ConfigurarArena(limiteIzquierdo, limiteDerecho);
        jefe.Activar(jugador);
    }

    // El jugador murió en la pelea y reapareció en el checkpoint: se reinicia todo
    private void AlReaparecerJugador()
    {
        if (!activa || terminada) return;
        activa = false;
        foreach (JefeBarrera b in barreras) if (b != null) b.AbrirAlInstante();
        CameraFollow.LiberarArena(0.3f);
        if (barraVida != null) barraVida.Ocultar();
        CambiarMusica(false);
        jefe.Reiniciar();
    }

    private void AlDerrotarJefe()
    {
        if (terminada) return;
        terminada = true;
        StartCoroutine(Victoria());
    }

    private IEnumerator Victoria()
    {
        if (barraVida != null) barraVida.Ocultar();
        CambiarMusica(false);

        yield return new WaitForSeconds(esperaBotin);
        SoltarBotin();

        // Se abren las compuertas y se libera el paso; la cámara se queda fija en la arena,
        // así el jugador sale de cuadro al cruzar la compuerta derecha
        foreach (JefeBarrera b in barreras) if (b != null) b.Abrir();
        if (limiteNivel != null) limiteNivel.position = new Vector3(xLimiteTrasVictoria, limiteNivel.position.y, limiteNivel.position.z);
        IndicadoresUI.CambiarFinNivel(xSalida + 1f);   // la flecha "Avanza" señala la salida si se queda quieto

        // Espera a que cruce la salida
        while (true)
        {
            GameObject jugador = GameObject.FindWithTag("Player");
            if (jugador != null && jugador.transform.position.x >= xSalida) break;
            yield return null;
        }

        yield return new WaitForSeconds(esperaTrasSalir);
        if (nivelCompletado != null) nivelCompletado.Mostrar();
    }

    private void SoltarBotin()
    {
        Vector3 origen = jefe.transform.position + Vector3.up * 2.5f;
        // Que no caiga fuera de la arena
        origen.x = Mathf.Clamp(origen.x, limiteIzquierdo + 2f, limiteDerecho - 2f);

        if (prefabMoneda != null)
        {
            for (int i = 0; i < cantidadMonedas; i++)
            {
                GameObject m = Instantiate(prefabMoneda, origen, Quaternion.identity);
                var r = m.GetComponent<Recogible>();
                if (r != null) r.Lanzar(new Vector2(Random.Range(-5f, 5f), Random.Range(7f, 12f)), sueloY + Random.Range(0.3f, 0.6f));
            }
        }

        if (prefabKitReparacion != null)
        {
            GameObject k = Instantiate(prefabKitReparacion, origen, Quaternion.identity);
            var r = k.GetComponent<Recogible>();
            if (r != null) r.Lanzar(new Vector2(0f, 10f), sueloY + 0.4f);
        }
    }

    // Cambia entre la música del nivel y la del jefe con un fundido
    private void CambiarMusica(bool aJefe)
    {
        if (musicaJefe == null) return;   // sin clip asignado se queda la música normal
        if (musicaNivel != null) musicaNivel.Silenciar(aJefe, fundidoMusica);
        if (aJefe)
        {
            musica.clip = musicaJefe;
            if (!musica.isPlaying) musica.Play();
        }
        if (fundido != null) StopCoroutine(fundido);
        fundido = StartCoroutine(FundirMusica(aJefe ? volumenMusica : 0f));
    }

    private IEnumerator FundirMusica(float destino)
    {
        float desde = musica.volume;
        float t = 0f;
        while (t < fundidoMusica)
        {
            t += Time.unscaledDeltaTime;
            musica.volume = Mathf.Lerp(desde, destino, t / fundidoMusica);
            yield return null;
        }
        musica.volume = destino;
        if (destino <= 0f) musica.Stop();
        fundido = null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawLine(new Vector3(limiteIzquierdo, sueloY - 1f), new Vector3(limiteIzquierdo, sueloY + 10f));
        Gizmos.DrawLine(new Vector3(limiteDerecho, sueloY - 1f), new Vector3(limiteDerecho, sueloY + 10f));
        Gizmos.DrawLine(new Vector3(limiteIzquierdo, sueloY), new Vector3(limiteDerecho, sueloY));
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(xSalida, sueloY - 1f), new Vector3(xSalida, sueloY + 6f));
    }
}
