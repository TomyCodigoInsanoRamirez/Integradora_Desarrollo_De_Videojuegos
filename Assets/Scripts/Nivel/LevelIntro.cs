using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Entrada del nivel: arranca en negro total, muestra el título, se desvanece el negro
// y la cámara hace un pequeño zoom in hasta su tamaño normal.
// Crea su propio Canvas al iniciar, así que basta con tenerlo en un objeto de la escena.
public class LevelIntro : MonoBehaviour
{
    [Header("Texto")]
    [SerializeField] private string titulo = "Nivel 1";
    [SerializeField] private string subtitulo = "Inicio de la misión";
    [SerializeField] private Font fuente;               // Vacío = fuente por defecto de Unity
    [SerializeField] private int tamanoTitulo = 72;
    [SerializeField] private int tamanoSubtitulo = 30;

    [Header("Tiempos (segundos)")]
    [SerializeField] private float esperaInicial = 0.3f;    // Negro total antes de que aparezca el texto
    [SerializeField] private float aparicionTexto = 0.8f;
    [SerializeField] private float textoVisible = 1.4f;
    [SerializeField] private float desvanecerNegro = 1.6f;
    [SerializeField] private float desvanecerTexto = 0.8f;

    [Header("Zoom de cámara")]
    [Tooltip("Cuánto más abierta empieza la cámara (1.15 = 15% más lejos)")]
    [SerializeField] private float zoomInicial = 1.15f;
    [SerializeField] private float duracionZoom = 2.6f;

    [Header("Jugador")]
    [Tooltip("El jugador no se puede mover mientras la pantalla está en negro")]
    [SerializeField] private bool bloquearJugador = true;

    private CanvasGroup negro;
    private CanvasGroup textos;

    private void Awake()
    {
        CrearUI();
        negro.alpha = 1f;
        textos.alpha = 0f;
    }

    private IEnumerator Start()
    {
        Camera cam = Camera.main;
        float tamanoFinal = cam != null ? cam.orthographicSize : 5f;
        if (cam != null) cam.orthographicSize = tamanoFinal * zoomInicial;

        // El PlayerSpawner cambia el jugador en Awake, así que aquí ya está el definitivo
        Behaviour control = null;
        if (bloquearJugador)
        {
            GameObject jugador = GameObject.FindWithTag("Player");
            if (jugador != null) control = jugador.GetComponent<PlayerController>();
            if (control != null) control.enabled = false;
        }

        yield return Esperar(esperaInicial);
        yield return Fundido(textos, 0f, 1f, aparicionTexto);
        yield return Esperar(textoVisible);

        if (control != null) control.enabled = true;

        // El negro se va mientras la cámara se acerca; el texto se apaga un poco después
        StartCoroutine(Zoom(cam, tamanoFinal * zoomInicial, tamanoFinal, duracionZoom));
        StartCoroutine(Fundido(textos, 1f, 0f, desvanecerTexto, desvanecerNegro * 0.35f));
        yield return Fundido(negro, 1f, 0f, desvanecerNegro);

        yield return Esperar(desvanecerTexto);
        Destroy(negro.transform.parent.gameObject);
    }

    private static IEnumerator Esperar(float segundos)
    {
        float t = 0f;
        while (t < segundos) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static IEnumerator Fundido(CanvasGroup grupo, float desde, float hasta, float duracion, float retraso = 0f)
    {
        if (retraso > 0f) yield return Esperar(retraso);
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            grupo.alpha = Mathf.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / duracion));
            yield return null;
        }
        grupo.alpha = hasta;
    }

    private static IEnumerator Zoom(Camera cam, float desde, float hasta, float duracion)
    {
        if (cam == null) yield break;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duracion), 3f);   // ease out: frena al final
            cam.orthographicSize = Mathf.Lerp(desde, hasta, k);
            yield return null;
        }
        cam.orthographicSize = hasta;
    }

    private void CrearUI()
    {
        Font f = fuente != null ? fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGO = new GameObject("IntroNivel_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Negro a pantalla completa
        var negroGO = new GameObject("Negro", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        negroGO.transform.SetParent(canvasGO.transform, false);
        Estirar(negroGO.GetComponent<RectTransform>());
        negroGO.GetComponent<Image>().color = Color.black;
        negro = negroGO.GetComponent<CanvasGroup>();
        negro.blocksRaycasts = false;

        // Textos (encima del negro, con su propio fundido)
        var textosGO = new GameObject("Textos", typeof(RectTransform), typeof(CanvasGroup));
        textosGO.transform.SetParent(canvasGO.transform, false);
        Estirar(textosGO.GetComponent<RectTransform>());
        textos = textosGO.GetComponent<CanvasGroup>();
        textos.blocksRaycasts = false;

        CrearTexto(textosGO.transform, "Titulo", titulo.ToUpper(), f, tamanoTitulo, FontStyle.Bold, new Vector2(0f, 40f), Color.white);
        CrearTexto(textosGO.transform, "Subtitulo", subtitulo, f, tamanoSubtitulo, FontStyle.Normal, new Vector2(0f, -30f), new Color(0.78f, 0.82f, 0.88f));

        // Línea fina entre título y subtítulo
        var linea = new GameObject("Linea", typeof(RectTransform), typeof(Image));
        linea.transform.SetParent(textosGO.transform, false);
        var rt = linea.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420f, 2f);
        rt.anchoredPosition = new Vector2(0f, 2f);
        linea.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);
    }

    private static void CrearTexto(Transform padre, string nombre, string contenido, Font f, int tamano, FontStyle estilo, Vector2 pos, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Text), typeof(Shadow));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1600f, tamano * 1.6f);
        rt.anchoredPosition = pos;
        var txt = go.GetComponent<Text>();
        txt.text = contenido;
        txt.font = f;
        txt.fontSize = tamano;
        txt.fontStyle = estilo;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        var sombra = go.GetComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.8f);
        sombra.effectDistance = new Vector2(3f, -3f);
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
