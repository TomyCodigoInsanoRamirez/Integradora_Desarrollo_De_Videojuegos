using UnityEngine;

// Sonido de algo que se acerca desde fuera de la pantalla (por ejemplo un dron).
// Puede empezar ANTES de que exista el enemigo: arranca en una posición fija (el punto de aparición)
// y, cuando el enemigo aparece, se le pega con Seguir(). Mientras suena:
//   - el volumen sube conforme la fuente se acerca al borde de la cámara (bajo lejos, completo en pantalla),
//   - se oye del lado por el que viene (paneo estéreo).
// Primero suena el clip completo (la llegada) y después repite en bucle un tramo estable del clip,
// con fundido cruzado, mientras el objeto siga vivo. Si el objeto se destruye, se apaga con un fundido corto.
public class SonidoAcercamiento : MonoBehaviour
{
    [Header("Volumen y lado")]
    [Tooltip("Distancia fuera de la pantalla a la que el sonido queda al volumen mínimo")]
    [SerializeField] private float distanciaAtenuacion = 10f;
    [Range(0f, 1f)] [SerializeField] private float volumenMinimo = 0.12f;
    [Tooltip("Cuánto se carga el sonido al lado de donde viene (0 = centrado, 1 = todo a un lado)")]
    [Range(0f, 1f)] [SerializeField] private float paneo = 0.75f;
    [SerializeField] private float fundidoSalida = 0.2f;

    [Header("Bucle mientras el objeto vive")]
    [Tooltip("Tramo del clip (segundos) que se repite después de la llegada. Debe ser la parte pareja del sonido")]
    [SerializeField] private float bucleInicio = 0.3f;
    [SerializeField] private float bucleFin = 1.9f;
    [Tooltip("Duración del fundido cruzado entre repeticiones")]
    [SerializeField] private float cruce = 0.25f;
    [Tooltip("Si en este tiempo nunca llegó a seguir a nadie, se apaga solo")]
    [SerializeField] private float maxSinSeguir = 6f;

    private AudioSource[] fuentes;
    private int activa;              // fuente que suena "de frente"
    private float pesoActiva = 1f;   // 0..1 durante el fundido cruzado
    private bool cruzando;
    private bool enBucle;            // ya terminó la llegada y está repitiendo el tramo
    private AudioClip clip;

    private Transform objetivo;
    private bool seguia;
    private Vector3 posicion;
    private float volumen = 1f;
    private float fundido = 1f;
    private bool apagando;
    private float creado;
    private Camera cam;

    // Crea el sonido en 'posicionInicial' y lo empieza a reproducir
    public static SonidoAcercamiento Crear(AudioClip clip, Vector3 posicionInicial, float volumen)
    {
        if (clip == null) return null;
        var go = new GameObject("SonidoAcercamiento_" + clip.name);
        var s = go.AddComponent<SonidoAcercamiento>();
        s.clip = clip;
        s.posicion = posicionInicial;
        s.volumen = volumen;
        s.creado = Time.time;
        s.fuentes = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            var f = go.AddComponent<AudioSource>();
            f.playOnAwake = false;
            f.spatialBlend = 0f;   // el efecto de distancia y lado se calcula aquí
            f.clip = clip;
            s.fuentes[i] = f;
        }
        // El tramo del bucle no puede salirse del clip
        s.bucleFin = Mathf.Min(s.bucleFin, clip.length);
        s.bucleInicio = Mathf.Clamp(s.bucleInicio, 0f, s.bucleFin - s.cruce * 2f);
        s.Actualizar();
        s.fuentes[0].Play();
        return s;
    }

    // A partir de ahora el sonido sale de este objeto (y se repite mientras viva)
    public void Seguir(Transform t)
    {
        objetivo = t;
        seguia = t != null;
    }

    // Lo apaga con un fundido (por ejemplo si el dron al final no llega a aparecer)
    public void Apagar()
    {
        apagando = true;
    }

    // Lo mueve mientras todavía no hay objeto que seguir (el punto de aparición se puede correr)
    public void MoverA(Vector3 p)
    {
        if (!seguia) posicion = p;
    }

    private void Update()
    {
        // El objeto que seguía desapareció (murió), o nunca apareció: fundido y fuera
        if (seguia && objetivo == null) apagando = true;
        if (!seguia && Time.time - creado > maxSinSeguir) apagando = true;
        if (apagando)
        {
            fundido -= Time.deltaTime / Mathf.Max(0.01f, fundidoSalida);
            if (fundido <= 0f) { Destroy(gameObject); return; }
        }

        if (!RepetirSiHaceFalta()) return;
        Actualizar();
    }

    // Cuando la fuente activa llega al final de su tramo, la otra arranca en el inicio del bucle y se cruzan.
    // Devuelve false si ya no queda nada sonando (el objeto se destruye).
    private bool RepetirSiHaceFalta()
    {
        AudioSource actual = fuentes[activa];
        float fin = enBucle ? bucleFin : clip.length;

        if (!cruzando && !apagando && actual.isPlaying && actual.time >= fin - cruce)
        {
            int otra = 1 - activa;
            fuentes[otra].time = bucleInicio;
            fuentes[otra].Play();
            activa = otra;
            pesoActiva = 0f;
            cruzando = true;
            enBucle = true;
        }

        if (cruzando)
        {
            pesoActiva = Mathf.MoveTowards(pesoActiva, 1f, Time.deltaTime / Mathf.Max(0.01f, cruce));
            if (pesoActiva >= 1f)
            {
                fuentes[1 - activa].Stop();
                cruzando = false;
            }
        }

        // Ya no queda nada sonando (por ejemplo al apagarse): fuera
        if (!fuentes[0].isPlaying && !fuentes[1].isPlaying) { Destroy(gameObject); return false; }
        return true;
    }

    private void Actualizar()
    {
        if (objetivo != null) posicion = objetivo.position;
        if (cam == null) cam = Camera.main;

        float k = 1f, pan = 0f;
        if (cam != null)
        {
            Vector3 c = cam.transform.position;
            float mitadAlto = cam.orthographicSize;
            float mitadAncho = mitadAlto * cam.aspect;
            float dx = posicion.x - c.x;
            float dy = posicion.y - c.y;

            // Cuánto le falta para entrar a la pantalla (0 si ya está dentro)
            float fuera = Mathf.Max(0f, Mathf.Abs(dx) - mitadAncho, Mathf.Abs(dy) - mitadAlto);
            float cercania = 1f - Mathf.Clamp01(fuera / Mathf.Max(0.01f, distanciaAtenuacion));
            // Curva suave: al principio se oye lejano y crece más rápido al acercarse al borde
            k = Mathf.Lerp(volumenMinimo, 1f, cercania * cercania);
            pan = Mathf.Clamp(dx / (mitadAncho * 1.2f), -1f, 1f) * paneo;
        }

        float baseVol = volumen * k * fundido;
        fuentes[activa].volume = baseVol * (cruzando ? pesoActiva : 1f);
        fuentes[1 - activa].volume = cruzando ? baseVol * (1f - pesoActiva) : 0f;
        fuentes[0].panStereo = fuentes[1].panStereo = pan;
    }
}
