using UnityEngine;

// Animación simple por cuadros para objetos pequeños (moneda, kit, bala, efectos) sin necesidad
// de un Animator Controller. Cambia el sprite del SpriteRenderer a la velocidad indicada.
[RequireComponent(typeof(SpriteRenderer))]
public class AnimacionSprites : MonoBehaviour
{
    [SerializeField] private Sprite[] cuadros;
    [SerializeField] private float cuadrosPorSegundo = 10f;
    [SerializeField] private bool bucle = true;
    [Tooltip("Al terminar (sin bucle) destruye el objeto: para efectos de un solo uso")]
    [SerializeField] private bool destruirAlTerminar = false;
    [Tooltip("Empieza en un cuadro al azar, para que varios objetos iguales no se muevan sincronizados")]
    [SerializeField] private bool inicioAleatorio = true;

    private SpriteRenderer sr;
    private float t;

    public void Configurar(Sprite[] nuevos, float fps, bool enBucle)
    {
        cuadros = nuevos;
        cuadrosPorSegundo = fps;
        bucle = enBucle;
        t = 0f;
        Aplicar();
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (inicioAleatorio && bucle && cuadros != null && cuadros.Length > 0)
            t = Random.Range(0f, cuadros.Length / Mathf.Max(0.01f, cuadrosPorSegundo));
        Aplicar();
    }

    private void Update()
    {
        if (cuadros == null || cuadros.Length == 0) return;
        t += Time.deltaTime;
        int total = cuadros.Length;
        int i = Mathf.FloorToInt(t * cuadrosPorSegundo);
        if (!bucle && i >= total)
        {
            if (destruirAlTerminar) { Destroy(gameObject); return; }
            i = total - 1;
        }
        sr.sprite = cuadros[bucle ? i % total : Mathf.Min(i, total - 1)];
    }

    private void Aplicar()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (cuadros != null && cuadros.Length > 0) sr.sprite = cuadros[0];
    }
}
