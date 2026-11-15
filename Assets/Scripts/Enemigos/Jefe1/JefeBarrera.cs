using System.Collections;
using UnityEngine;

// Compuerta de la arena del jefe: baja desde arriba para cerrar el paso y sube para abrirlo.
// El collider solo bloquea mientras está cerrada.
public class JefeBarrera : MonoBehaviour
{
    [Tooltip("Parte que se mueve (sprite + collider). Su posición en la escena es la de CERRADA")]
    [SerializeField] private Transform puerta;
    [Tooltip("Cuánto sube la puerta al abrirse")]
    [SerializeField] private float alturaAbierta = 11f;
    [SerializeField] private float duracionCerrar = 0.35f;
    [SerializeField] private float duracionAbrir = 1.2f;
    [SerializeField] private AudioClip sonidoCerrar;
    [SerializeField] private AudioClip sonidoAbrir;

    [Header("Golpes del jefe")]
    [SerializeField] private SpriteRenderer spritePuerta;
    [SerializeField] private Sprite spriteNormal;
    [Tooltip("Instante del choque (chispas)")]
    [SerializeField] private Sprite spriteChispas;
    [Tooltip("Cómo queda después del primer choque")]
    [SerializeField] private Sprite spriteAbollada;
    [SerializeField] private float duracionChispas = 0.3f;
    [SerializeField] private AudioClip sonidoGolpe;

    private Collider2D col;
    private Vector3 posCerrada;
    private Coroutine anim;

    public bool Cerrada { get; private set; }

    private void Awake()
    {
        if (puerta == null) puerta = transform;
        col = puerta.GetComponent<Collider2D>();
        posCerrada = puerta.localPosition;
        AbrirAlInstante();
    }

    // El jefe chocó contra la compuerta: chispas un instante y queda abollada
    public void Golpear()
    {
        if (!Cerrada || spritePuerta == null) return;
        if (golpe != null) StopCoroutine(golpe);
        golpe = StartCoroutine(RutinaGolpe());
        SoundFX.Play(sonidoGolpe);
    }

    private Coroutine golpe;

    private IEnumerator RutinaGolpe()
    {
        Vector3 basePos = puerta.localPosition;
        if (spriteChispas != null) spritePuerta.sprite = spriteChispas;
        float t = 0f;
        while (t < duracionChispas)
        {
            t += Time.deltaTime;
            puerta.localPosition = basePos + new Vector3(Mathf.Sin(t * 90f) * 0.06f * (1f - t / duracionChispas), 0f, 0f);
            yield return null;
        }
        puerta.localPosition = basePos;
        if (spriteAbollada != null) spritePuerta.sprite = spriteAbollada;
        golpe = null;
    }

    public void AbrirAlInstante()
    {
        if (golpe != null) { StopCoroutine(golpe); golpe = null; }
        if (spritePuerta != null && spriteNormal != null) spritePuerta.sprite = spriteNormal;
        if (anim != null) StopCoroutine(anim);
        Cerrada = false;
        puerta.localPosition = posCerrada + Vector3.up * alturaAbierta;
        if (col != null) col.enabled = false;
    }

    public void Cerrar()
    {
        if (Cerrada) return;
        Cerrada = true;
        if (col != null) col.enabled = true;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Mover(posCerrada, duracionCerrar, true));
        SoundFX.Play(sonidoCerrar);
    }

    public void Abrir()
    {
        if (!Cerrada) return;
        Cerrada = false;
        if (col != null) col.enabled = false;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Mover(posCerrada + Vector3.up * alturaAbierta, duracionAbrir, false));
        SoundFX.Play(sonidoAbrir);
    }

    private IEnumerator Mover(Vector3 destino, float duracion, bool golpe)
    {
        Vector3 desde = puerta.localPosition;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duracion);
            // Al cerrar cae acelerando (golpe seco); al abrir sube suave
            k = golpe ? k * k : Mathf.SmoothStep(0f, 1f, k);
            puerta.localPosition = Vector3.LerpUnclamped(desde, destino, k);
            yield return null;
        }
        puerta.localPosition = destino;
        if (golpe) CameraFollow.Shake(0.2f, 0.2f);
        anim = null;
    }
}
