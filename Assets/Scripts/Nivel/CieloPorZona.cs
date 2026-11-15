using UnityEngine;

// Funde una capa de cielo según la posición de la cámara (por ejemplo, del cielo nocturno del
// exterior a la pared de la nave al entrar a las instalaciones). Va en la capa que aparece,
// encima de la otra. Solo el cielo se funde: el resto de las transiciones se tapan con objetos.
[RequireComponent(typeof(SpriteRenderer))]
public class CieloPorZona : MonoBehaviour
{
    [Tooltip("X de la cámara donde empieza a aparecer")]
    [SerializeField] private float xInicio = 50f;
    [Tooltip("X de la cámara donde ya se ve completa")]
    [SerializeField] private float xFin = 62f;

    private SpriteRenderer sr;
    private Transform cam;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (cam == null) { if (Camera.main == null) return; cam = Camera.main.transform; }
        float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(xInicio, xFin, cam.position.x));
        Color c = sr.color;
        c.a = k;
        sr.color = c;
    }
}
