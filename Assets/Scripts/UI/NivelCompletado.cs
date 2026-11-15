using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Final del nivel: fundido a negro total, aparece el texto (título y monedas) y después se pasa
// por la pantalla de carga a la siguiente escena. Si la escena siguiente todavía no existe en la
// build (hoy no hay Nivel 2), el título es el de "fin del juego" y se vuelve al menú principal.
// Si 'alTerminar' tiene algo asignado, se llama eso en lugar de cambiar de escena (tienda, etc.).
public class NivelCompletado : MonoBehaviour
{
    [Header("Texto")]
    [Tooltip("Título cuando hay un nivel siguiente")]
    [SerializeField] private string tituloNivelCompletado = "Nivel completado";
    [Tooltip("Título cuando no hay nivel siguiente")]
    [SerializeField] private string tituloFinDelJuego = "Fin del juego";
    [SerializeField] private string subtitulo = "Monedas: {0}";
    [SerializeField] private Font fuente;   // Vacío = fuente por defecto de Unity

    [Header("Tiempos (segundos)")]
    [SerializeField] private float fundidoANegro = 1.5f;
    [SerializeField] private float esperaEnNegro = 0.4f;
    [SerializeField] private float aparicionTexto = 0.8f;
    [SerializeField] private float tiempoVisible = 3f;
    [SerializeField] private float desvanecerTexto = 0.6f;

    [Header("Después")]
    [Tooltip("Escena del siguiente nivel. Si no está en la build, se usa la de respaldo")]
    [SerializeField] private string escenaSiguiente = "Nivel2";
    [SerializeField] private string escenaRespaldo = SceneLoader.MainMenu;
    [Tooltip("Si tiene algo asignado se llama en lugar de cambiar de escena (tienda, siguiente nivel...)")]
    [SerializeField] private UnityEvent alTerminar;

    private bool mostrando;

    public bool Mostrando => mostrando;

    public void Mostrar()
    {
        if (mostrando) return;
        mostrando = true;
        StartCoroutine(Rutina());
    }

    private bool HaySiguienteNivel =>
        !string.IsNullOrEmpty(escenaSiguiente) && Application.CanStreamedLevelBeLoaded(escenaSiguiente);

    private IEnumerator Rutina()
    {
        bool hayNivel = HaySiguienteNivel;
        int monedas = GameManager.Instance != null ? GameManager.Instance.Monedas : 0;
        CrearUI(hayNivel ? tituloNivelCompletado : tituloFinDelJuego, string.Format(subtitulo, monedas),
                out CanvasGroup negro, out CanvasGroup textos);

        // La música del nivel se apaga junto con la imagen
        var musica = FindAnyObjectByType<GameplayMusic>();
        if (musica != null) musica.Silenciar(true, fundidoANegro);

        yield return Fundido(negro, 0f, 1f, fundidoANegro);

        // Ya en negro: el jugador deja de moverse
        GameObject jugador = GameObject.FindWithTag("Player");
        if (jugador != null)
        {
            var control = jugador.GetComponent<PlayerController>();
            if (control != null) control.enabled = false;
            var rb = jugador.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        yield return Esperar(esperaEnNegro);
        yield return Fundido(textos, 0f, 1f, aparicionTexto);
        yield return Esperar(tiempoVisible);
        yield return Fundido(textos, 1f, 0f, desvanecerTexto);

        if (alTerminar != null && alTerminar.GetPersistentEventCount() > 0) alTerminar.Invoke();
        else SceneLoader.LoadWithLoadingScreen(hayNivel ? escenaSiguiente : escenaRespaldo);
    }

    private static IEnumerator Esperar(float segundos)
    {
        float t = 0f;
        while (t < segundos) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static IEnumerator Fundido(CanvasGroup grupo, float desde, float hasta, float duracion)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            grupo.alpha = Mathf.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / duracion));
            yield return null;
        }
        grupo.alpha = hasta;
    }

    private void CrearUI(string textoTitulo, string textoSubtitulo, out CanvasGroup negro, out CanvasGroup textos)
    {
        Font f = fuente != null ? fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGO = new GameObject("NivelCompletado_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // encima de todo el HUD
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Negro total a pantalla completa
        var negroGO = new GameObject("Negro", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        negroGO.transform.SetParent(canvasGO.transform, false);
        var rtNegro = negroGO.GetComponent<RectTransform>();
        rtNegro.anchorMin = Vector2.zero;
        rtNegro.anchorMax = Vector2.one;
        rtNegro.offsetMin = rtNegro.offsetMax = Vector2.zero;
        negroGO.GetComponent<Image>().color = Color.black;
        negro = negroGO.GetComponent<CanvasGroup>();
        negro.alpha = 0f;

        // Textos encima del negro, con su propio fundido
        var textosGO = new GameObject("Textos", typeof(RectTransform), typeof(CanvasGroup));
        textosGO.transform.SetParent(canvasGO.transform, false);
        var rtTextos = textosGO.GetComponent<RectTransform>();
        rtTextos.anchorMin = Vector2.zero;
        rtTextos.anchorMax = Vector2.one;
        rtTextos.offsetMin = rtTextos.offsetMax = Vector2.zero;
        textos = textosGO.GetComponent<CanvasGroup>();
        textos.alpha = 0f;

        CrearTexto(textosGO.transform, textoTitulo.ToUpper(), f, 72, FontStyle.Bold, new Vector2(0f, 52f), Color.white);
        CrearTexto(textosGO.transform, textoSubtitulo, f, 30, FontStyle.Normal, new Vector2(0f, -36f), new Color(0.78f, 0.82f, 0.88f));

        var linea = new GameObject("Linea", typeof(RectTransform), typeof(Image));
        linea.transform.SetParent(textosGO.transform, false);
        var rt = linea.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420f, 2f);
        rt.anchoredPosition = new Vector2(0f, -4f);
        linea.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
    }

    private static void CrearTexto(Transform padre, string contenido, Font f, int tamano, FontStyle estilo, Vector2 pos, Color color)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);
        var t = go.GetComponent<Text>();
        t.text = contenido;
        t.font = f;
        t.fontSize = tamano;
        t.fontStyle = estilo;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.sizeDelta = new Vector2(1400f, tamano * 1.6f);
        rt.anchoredPosition = pos;
    }
}
