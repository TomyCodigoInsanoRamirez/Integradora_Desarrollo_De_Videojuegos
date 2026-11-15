using UnityEngine;
using UnityEngine.UI;

// Barra de vida del jefe, arriba al centro, solo visible durante la pelea.
// Con sprites asignados usa el marco metálico, el relleno y el retrato del Centinela;
// sin ellos, una barra simple (fondo oscuro + relleno) del mismo estilo que el resto del HUD.
// Crea su propio Canvas; la arena la muestra y la oculta.
public class JefeBarraVida : MonoBehaviour
{
    [Header("Sprites (opcionales)")]
    [SerializeField] private Sprite marco;
    [Tooltip("Relleno en blanco/gris: se tiñe con los colores de abajo")]
    [SerializeField] private Sprite relleno;
    [SerializeField] private Sprite retrato;
    [Tooltip("Zona interior del marco donde va el relleno, en pixeles del sprite del marco (izq, abajo, der, arriba)")]
    [SerializeField] private Vector4 interiorMarco = new Vector4(49, 19, 49, 20);

    [Header("Tamaño y colores")]
    [SerializeField] private Font fuente;   // Vacío = fuente por defecto de Unity
    [SerializeField] private float ancho = 900f;
    [SerializeField] private float alto = 14f;
    [SerializeField] private float tamanoRetrato = 84f;
    [SerializeField] private float margenSuperior = 50f;
    [SerializeField] private Color colorVida = new Color(0.9f, 0.12f, 0.1f);
    [SerializeField] private Color colorVidaFase2 = new Color(1f, 0.42f, 0.12f);
    [SerializeField] private Color colorDanio = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField] private float duracionFundido = 0.5f;

    private CanvasGroup grupo;
    private Image imgRelleno;
    private Image imgDanio;
    private Image imgRetrato;
    private Text texto;
    private JefeBase jefe;
    private float objetivoAlpha;
    private float fraccion = 1f;
    private float fraccionDanio = 1f;
    private float esperaDanio;
    private float sacudida;

    private void Awake()
    {
        CrearUI();
        grupo.alpha = 0f;
    }

    public void Mostrar(JefeBase nuevoJefe)
    {
        if (jefe != null) jefe.VidaCambio -= AlCambiarVida;
        jefe = nuevoJefe;
        if (jefe != null)
        {
            jefe.VidaCambio += AlCambiarVida;
            texto.text = jefe.Nombre.ToUpper();
            fraccion = fraccionDanio = jefe.VidaMaxima > 0f ? jefe.Vida / jefe.VidaMaxima : 1f;
        }
        objetivoAlpha = 1f;
    }

    public void Ocultar()
    {
        objetivoAlpha = 0f;
    }

    private void OnDestroy()
    {
        if (jefe != null) jefe.VidaCambio -= AlCambiarVida;
    }

    private void AlCambiarVida(float vida, float maxima)
    {
        float nueva = maxima > 0f ? Mathf.Clamp01(vida / maxima) : 0f;
        if (nueva < fraccion) { esperaDanio = 0.35f; sacudida = 1f; }   // la parte blanca se queda un momento y luego baja
        else fraccionDanio = nueva;                                     // al reiniciar se llena de golpe
        fraccion = nueva;
    }

    private void Update()
    {
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, objetivoAlpha, Time.unscaledDeltaTime / Mathf.Max(0.01f, duracionFundido));

        if (esperaDanio > 0f) esperaDanio -= Time.deltaTime;
        else fraccionDanio = Mathf.MoveTowards(fraccionDanio, fraccion, Time.deltaTime * 0.6f);

        imgRelleno.fillAmount = fraccion;
        imgDanio.fillAmount = Mathf.Max(fraccion, fraccionDanio);
        imgRelleno.color = jefe != null && jefe.EnFase2 ? colorVidaFase2 : colorVida;

        // El retrato tiembla un instante con cada golpe
        if (imgRetrato != null)
        {
            sacudida = Mathf.MoveTowards(sacudida, 0f, Time.deltaTime * 5f);
            imgRetrato.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(Time.time * 70f) * 4f * sacudida, 0f);
        }
    }

    private void CrearUI()
    {
        Font f = fuente != null ? fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bool conMarco = marco != null;
        bool conRetrato = retrato != null;
        // Con marco, el alto sale de la proporción del sprite
        float altoBarra = conMarco ? ancho * marco.rect.height / marco.rect.width : alto;
        float desplazamiento = conRetrato ? tamanoRetrato + 14f : 0f;

        var canvasGO = new GameObject("JefeBarraVida_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;   // debajo de los indicadores y de la intro
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        grupo = canvasGO.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.interactable = false;

        // Contenedor arriba al centro: [retrato] [nombre / barra]
        var cont = new GameObject("Contenedor", typeof(RectTransform)).GetComponent<RectTransform>();
        cont.SetParent(canvasGO.transform, false);
        cont.anchorMin = cont.anchorMax = new Vector2(0.5f, 1f);
        cont.pivot = new Vector2(0.5f, 1f);
        cont.anchoredPosition = new Vector2(0f, -margenSuperior);
        cont.sizeDelta = new Vector2(ancho + desplazamiento, Mathf.Max(altoBarra + 40f, conRetrato ? tamanoRetrato : 0f));

        // Retrato a la izquierda
        if (conRetrato)
        {
            var marcoRetrato = new GameObject("Retrato", typeof(RectTransform)).GetComponent<RectTransform>();
            marcoRetrato.SetParent(cont, false);
            marcoRetrato.anchorMin = marcoRetrato.anchorMax = new Vector2(0f, 0f);
            marcoRetrato.pivot = new Vector2(0f, 0f);
            marcoRetrato.anchoredPosition = Vector2.zero;
            marcoRetrato.sizeDelta = new Vector2(tamanoRetrato, tamanoRetrato);
            imgRetrato = CrearImagen(marcoRetrato, "Imagen", Color.white);
            imgRetrato.sprite = retrato;
            imgRetrato.preserveAspect = true;
            Estirar(imgRetrato.rectTransform);
        }

        // Zona de la barra (a la derecha del retrato)
        var zona = new GameObject("Barra", typeof(RectTransform)).GetComponent<RectTransform>();
        zona.SetParent(cont, false);
        zona.anchorMin = new Vector2(0f, 0f);
        zona.anchorMax = new Vector2(1f, 1f);
        zona.offsetMin = new Vector2(desplazamiento, 0f);
        zona.offsetMax = Vector2.zero;

        // Nombre
        var textoGO = new GameObject("Nombre", typeof(RectTransform), typeof(Text));
        textoGO.transform.SetParent(zona, false);
        texto = textoGO.GetComponent<Text>();
        texto.font = f;
        texto.fontSize = 26;
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.LowerLeft;
        texto.color = Color.white;
        texto.raycastTarget = false;
        var sombra = textoGO.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.6f);
        sombra.effectDistance = new Vector2(2f, -2f);
        var rtTexto = texto.rectTransform;
        rtTexto.anchorMin = new Vector2(0f, 0f);
        rtTexto.anchorMax = new Vector2(1f, 0f);
        rtTexto.pivot = new Vector2(0.5f, 0f);
        rtTexto.anchoredPosition = new Vector2(conMarco ? 6f : 0f, altoBarra + 2f);
        rtTexto.sizeDelta = new Vector2(0f, 34f);

        // Barra: fondo (marco o rectángulo oscuro) y, dentro, la parte dañada y la vida
        RectTransform barra = CrearImagen(zona, "Fondo", conMarco ? Color.white : new Color(0f, 0f, 0f, 0.55f)).rectTransform;
        if (conMarco) barra.GetComponent<Image>().sprite = marco;
        barra.anchorMin = new Vector2(0f, 0f);
        barra.anchorMax = new Vector2(1f, 0f);
        barra.pivot = new Vector2(0.5f, 0f);
        barra.anchoredPosition = Vector2.zero;
        barra.sizeDelta = new Vector2(0f, altoBarra);

        // Márgenes del interior: proporcionales al sprite del marco, o 2 px en la barra simple
        Vector2 min = new Vector2(2f, 2f), max = new Vector2(-2f, -2f);
        if (conMarco)
        {
            float k = ancho / marco.rect.width;
            min = new Vector2(interiorMarco.x * k, interiorMarco.y * k);
            max = new Vector2(-interiorMarco.z * k, -interiorMarco.w * k);
        }
        imgDanio = CrearRelleno(barra, "Danio", colorDanio, min, max);
        imgRelleno = CrearRelleno(barra, "Vida", colorVida, min, max);

        // Línea fina bajo la barra (solo en la versión simple; el marco ya tiene borde)
        if (!conMarco)
        {
            var linea = CrearImagen(zona, "Linea", new Color(1f, 1f, 1f, 0.35f)).rectTransform;
            linea.anchorMin = new Vector2(0f, 0f);
            linea.anchorMax = new Vector2(1f, 0f);
            linea.pivot = new Vector2(0.5f, 1f);
            linea.anchoredPosition = new Vector2(0f, -4f);
            linea.sizeDelta = new Vector2(0f, 2f);
        }
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Image CrearImagen(Transform padre, string nombre, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Image CrearRelleno(RectTransform barra, string nombre, Color color, Vector2 min, Vector2 max)
    {
        var img = CrearImagen(barra, nombre, color);
        // Image.Filled necesita un sprite: el relleno del arte o uno blanco creado en código
        img.sprite = relleno != null ? relleno : SpriteBlanco();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = min;
        rt.offsetMax = max;
        return img;
    }

    private static Sprite spriteBlanco;
    private static Sprite SpriteBlanco()
    {
        if (spriteBlanco == null)
            spriteBlanco = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return spriteBlanco;
    }
}
