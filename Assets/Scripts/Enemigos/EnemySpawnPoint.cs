using UnityEngine;

// Hace aparecer un enemigo una sola vez, siempre FUERA de la pantalla, para que el jugador
// sienta que ya estaba ahí. Antes de aparecer muestra la flecha de advertencia.
public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;

    [Header("Aviso")]
    [Tooltip("La flecha de advertencia empieza cuando este punto está a esta distancia (o menos) del borde derecho de la cámara")]
    [SerializeField, Min(0f)] private float distanciaAviso = 9f;
    [Tooltip("Segundos de aviso antes de que aparezca el enemigo (si el jugador se acerca rápido, aparece antes, pero nunca a la vista)")]
    [SerializeField, Min(0f)] private float avisoPrevio = 1.5f;

    [Header("Aparición")]
    [Tooltip("Distancia mínima fuera del borde de la cámara a la que se crea el enemigo (debe ser mayor que la mitad de su ancho)")]
    [SerializeField, Min(0f)] private float margenFueraDePantalla = 3.5f;

    private Camera cam;
    private bool avisado;
    private bool aparecido;
    private float inicioAviso;
    private SonidoAcercamiento sonidoAcercamiento;

    private void Start()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        if (aparecido || enemyPrefab == null) return;
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        float bordeDerecho = cam.transform.position.x + cam.orthographicSize * cam.aspect;
        float distanciaAlBorde = transform.position.x - bordeDerecho;

        if (!avisado && distanciaAlBorde <= distanciaAviso)
        {
            avisado = true;
            inicioAviso = Time.time;
            if (avisoPrevio > 0f) IndicadoresUI.Advertir(transform.position, avisoPrevio + 0.4f);

            // Si es un dron, su zumbido empieza ya, junto con la flecha (antes de que exista)
            var dronPrefab = enemyPrefab.GetComponent<DroneEnemy>();
            if (dronPrefab != null)
                sonidoAcercamiento = SonidoAcercamiento.Crear(dronPrefab.AproximacionSound, PosicionAparicion(bordeDerecho), dronPrefab.AproximacionVolumen);
        }
        if (!avisado) return;
        if (sonidoAcercamiento != null) sonidoAcercamiento.MoverA(PosicionAparicion(bordeDerecho));

        // Aparece cuando termina el aviso, o antes si el borde de la cámara ya está por alcanzarlo
        bool terminoAviso = Time.time - inicioAviso >= avisoPrevio;
        bool casiALaVista = distanciaAlBorde <= margenFueraDePantalla;
        if (terminoAviso || casiALaVista)
        {
            // Nunca dentro de la pantalla: si la cámara ya lo alcanzó, se corre justo fuera del borde
            GameObject enemigo = Instantiate(enemyPrefab, PosicionAparicion(bordeDerecho), Quaternion.identity);
            aparecido = true;

            // El dron hereda el sonido que ya venía sonando (así no se reinicia)
            var dron = enemigo.GetComponent<DroneEnemy>();
            if (dron != null && sonidoAcercamiento != null) dron.UsarSonidoAcercamiento(sonidoAcercamiento);
        }
    }

    // Nunca dentro de la pantalla: si la cámara ya alcanzó el punto, se corre justo fuera del borde
    private Vector3 PosicionAparicion(float bordeDerecho)
    {
        float x = Mathf.Max(transform.position.x, bordeDerecho + margenFueraDePantalla);
        return new Vector3(x, transform.position.y, transform.position.z);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f);
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 2f, 0f));
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.down);
    }
}
