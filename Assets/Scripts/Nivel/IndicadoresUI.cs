using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Flechas de ayuda en pantalla:
//  - "Avanza": parpadea si el jugador se queda quieto mucho tiempo y no hay enemigos a la vista.
//  - "Advertencia": parpadea en el borde de la pantalla por donde van a aparecer enemigos.
//    La piden EnemySpawnPoint y KamikazeStampede con IndicadoresUI.Advertir(...).
// Crea su propio Canvas; basta con tenerlo en un objeto de la escena y asignarle los dos sprites.
public class IndicadoresUI : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite flechaAvanza;
    [SerializeField] private Sprite flechaAdvertencia;

    [Header("Flecha Avanza")]
    [Tooltip("Segundos sin moverse para que aparezca la flecha")]
    [SerializeField, Min(0.5f)] private float tiempoInmovil = 4f;
    [SerializeField] private float anchoAvanza = 170f;
    [Tooltip("No aparece si hay enemigos a esta distancia horizontal del borde de la cámara o menos")]
    [SerializeField] private float margenEnemigos = 2f;
    [Tooltip("Pasando esta X ya no hay a dónde avanzar (final del nivel)")]
    [SerializeField] private float xFinNivel = 198f;

    [Header("Flecha Advertencia")]
    [SerializeField] private float anchoAdvertencia = 140f;

    [Header("Parpadeo")]
    [Tooltip("Parpadeos por segundo")]
    [SerializeField] private float frecuencia = 2.5f;

    private struct Aviso { public Vector3 posicion; public float fin; }
    private static readonly List<Aviso> avisos = new List<Aviso>();

    private static IndicadoresUI instancia;

    // Cambia hasta dónde hay "a dónde avanzar" (por ejemplo, al abrirse la salida tras el jefe)
    public static void CambiarFinNivel(float x)
    {
        if (instancia != null) instancia.xFinNivel = x;
    }

    // Muestra la flecha de advertencia apuntando hacia 'posicionMundo' durante 'duracion' segundos
    public static void Advertir(Vector3 posicionMundo, float duracion)
    {
        avisos.Add(new Aviso { posicion = posicionMundo, fin = Time.time + duracion });
    }

    private RectTransform canvasRect;
    private Image imgAvanza;
    private readonly List<Image> imgsAdvertencia = new List<Image>();
    private Transform jugador;
    private Rigidbody2D jugadorRb;
    private Behaviour jugadorControl;
    private float quietoDesde;
    private float siguienteRevisionEnemigos;
    private bool enemigosCerca;

    private void Awake()
    {
        instancia = this;
        avisos.Clear();
        var canvasGO = new GameObject("Indicadores_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;   // por debajo de la intro del nivel
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = canvasGO.GetComponent<RectTransform>();

        imgAvanza = CrearImagen("Avanza", flechaAvanza, anchoAvanza);
        quietoDesde = Time.time;
    }

    private Image CrearImagen(string nombre, Sprite sprite, float ancho)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRect, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;
        float alto = sprite != null ? ancho * sprite.rect.height / sprite.rect.width : ancho * 0.6f;
        img.rectTransform.sizeDelta = new Vector2(ancho, alto);
        img.enabled = false;
        return img;
    }

    private void Update()
    {
        BuscarJugador();
        bool encendido = Mathf.Repeat(Time.unscaledTime * frecuencia, 1f) < 0.6f;

        avisos.RemoveAll(a => Time.time > a.fin);
        ActualizarAdvertencias(encendido);
        ActualizarAvanza(encendido);
    }

    private void BuscarJugador()
    {
        if (jugador != null && jugador.gameObject.activeInHierarchy) return;
        // El PlayerSpawner puede reemplazar al jugador al iniciar
        GameObject p = GameObject.FindWithTag("Player");
        jugador = p != null ? p.transform : null;
        jugadorRb = p != null ? p.GetComponent<Rigidbody2D>() : null;
        jugadorControl = p != null ? p.GetComponent<PlayerController>() : null;
    }

    // ---------- Avanza ----------
    private void ActualizarAvanza(bool encendido)
    {
        bool moviendose = jugadorRb == null || Mathf.Abs(jugadorRb.linearVelocity.x) > 0.2f;
        bool sinControl = jugadorControl != null && !jugadorControl.enabled;   // p. ej. durante la intro
        if (moviendose || sinControl) quietoDesde = Time.time;

        if (Time.time >= siguienteRevisionEnemigos)
        {
            siguienteRevisionEnemigos = Time.time + 0.4f;
            enemigosCerca = HayEnemigosALaVista();
        }

        bool mostrar = jugador != null
                       && Time.time - quietoDesde >= tiempoInmovil
                       && !enemigosCerca
                       && avisos.Count == 0
                       && jugador.position.x < xFinNivel;

        imgAvanza.enabled = mostrar && encendido;
        if (!mostrar) return;

        // A la derecha de la pantalla, con un pequeño empujón hacia adelante
        float mitadAncho = canvasRect.rect.width * 0.5f;
        float empujon = Mathf.Sin(Time.unscaledTime * 6f) * 12f;
        imgAvanza.rectTransform.anchoredPosition = new Vector2(mitadAncho - anchoAvanza * 0.5f - 60f + empujon, 40f);
    }

    private bool HayEnemigosALaVista()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        float mitad = cam.orthographicSize * cam.aspect + margenEnemigos;
        float cx = cam.transform.position.x;

        foreach (var e in FindObjectsByType<EnemyRobot>(FindObjectsSortMode.None))
            if (Mathf.Abs(e.transform.position.x - cx) <= mitad) return true;
        foreach (var e in FindObjectsByType<DroneEnemy>(FindObjectsSortMode.None))
            if (Mathf.Abs(e.transform.position.x - cx) <= mitad) return true;
        foreach (var e in FindObjectsByType<KamikazeRobot>(FindObjectsSortMode.None))
            if (Mathf.Abs(e.transform.position.x - cx) <= mitad) return true;
        return false;
    }

    // ---------- Advertencia ----------
    private void ActualizarAdvertencias(bool encendido)
    {
        while (imgsAdvertencia.Count < avisos.Count)
            imgsAdvertencia.Add(CrearImagen("Advertencia", flechaAdvertencia, anchoAdvertencia));

        Camera cam = Camera.main;
        for (int i = 0; i < imgsAdvertencia.Count; i++)
        {
            Image img = imgsAdvertencia[i];
            if (i >= avisos.Count || cam == null) { img.enabled = false; continue; }

            img.enabled = encendido;
            Vector3 vp = cam.WorldToViewportPoint(avisos[i].posicion);
            RectTransform rt = img.rectTransform;
            Vector2 tam = canvasRect.rect.size;
            float margen = anchoAdvertencia * 0.5f + 40f;

            if (vp.x > 0.95f || vp.x < 0.05f)
            {
                // Fuera de la pantalla: en el borde de ese lado, apuntando hacia afuera
                bool derecha = vp.x > 0.5f;
                float x = derecha ? tam.x * 0.5f - margen : -tam.x * 0.5f + margen;
                float y = (Mathf.Clamp(vp.y, 0.25f, 0.8f) - 0.5f) * tam.y;
                rt.anchoredPosition = new Vector2(x, y);
                rt.localEulerAngles = Vector3.zero;
                rt.localScale = new Vector3(derecha ? 1f : -1f, 1f, 1f);
            }
            else
            {
                // Dentro de la pantalla (p. ej. un dron arriba): encima del punto, apuntando hacia él
                float x = (vp.x - 0.5f) * tam.x;
                float y = (Mathf.Clamp(vp.y + 0.15f, 0.2f, 0.88f) - 0.5f) * tam.y;
                rt.anchoredPosition = new Vector2(x, y);
                rt.localEulerAngles = new Vector3(0f, 0f, -90f);
                rt.localScale = Vector3.one;
            }
        }
    }

    private void OnDestroy()
    {
        avisos.Clear();
    }
}
