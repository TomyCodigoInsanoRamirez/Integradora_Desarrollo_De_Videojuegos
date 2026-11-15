using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.2f;
    [Tooltip("X positivo deja al personaje a la izquierda del centro de la pantalla")]
    [SerializeField] private Vector2 offset = new Vector2(3f, 0f);
    [Tooltip("Ancho de la zona en la que el personaje se puede mover sin que la cámara lo siga en X")]
    [SerializeField] private float deadZoneWidth = 2f;

    private Vector3 velocity;
    private float focusX;
    private Vector3 smoothedPosition;
    private bool hasSmoothed;

    // Sacudida de cámara (explosiones, impactos fuertes)
    private static float shakeAmount;
    private static float shakeDuration;
    private static float shakeTime;

    public static void Shake(float amount, float duration)
    {
        // Si ya hay una sacudida más fuerte en curso, se conserva
        float remaining = shakeDuration > 0f ? shakeAmount * (1f - shakeTime / shakeDuration) : 0f;
        if (amount < remaining) return;
        shakeAmount = amount;
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTime = 0f;
    }

    // Modo arena (pelea de jefe): la cámara se abre a otro tamaño y no sale de un rango en X
    private static CameraFollow instancia;
    private Camera cam;
    private bool enArena;
    private float arenaMinX, arenaMaxX, arenaCentroY;
    private float tamanoNormal;
    private float tamanoObjetivo;
    private float velocidadZoom;   // unidades de tamaño por segundo

    // Abre la cámara a 'tamano' y la limita a [minX, maxX] con el centro vertical en 'centroY'
    public static void FijarArena(float minX, float maxX, float centroY, float tamano, float duracion)
    {
        if (instancia == null || instancia.cam == null) return;
        var c = instancia;
        if (!c.enArena) c.tamanoNormal = c.cam.orthographicSize;
        c.enArena = true;
        c.arenaMinX = minX;
        c.arenaMaxX = maxX;
        c.arenaCentroY = centroY;
        c.IniciarZoom(tamano, duracion);
    }

    // Vuelve a seguir al jugador con el tamaño de antes de la arena
    public static void LiberarArena(float duracion)
    {
        if (instancia == null || !instancia.enArena) return;
        instancia.enArena = false;
        instancia.IniciarZoom(instancia.tamanoNormal, duracion);
    }

    private void IniciarZoom(float tamano, float duracion)
    {
        tamanoObjetivo = tamano;
        velocidadZoom = Mathf.Abs(tamano - cam.orthographicSize) / Mathf.Max(0.01f, duracion);
    }

    private void Awake()
    {
        instancia = this;
        cam = GetComponent<Camera>();
        tamanoObjetivo = -1f;
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null) focusX = target.position.x + offset.x;
    }

    private void Start()
    {
        if (target != null) focusX = target.position.x + offset.x;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // La cámara solo se desplaza en X cuando el personaje sale de la zona muerta
        float desiredX = target.position.x + offset.x;
        float halfZone = deadZoneWidth * 0.5f;
        if (desiredX > focusX + halfZone) focusX = desiredX - halfZone;
        else if (desiredX < focusX - halfZone) focusX = desiredX + halfZone;

        Vector3 targetPosition = new Vector3(
            focusX,
            target.position.y + offset.y,
            transform.position.z);

        // Zoom gradual hacia el tamaño pedido (arena o el normal al salir)
        if (cam != null && tamanoObjetivo > 0f)
        {
            cam.orthographicSize = Mathf.MoveTowards(cam.orthographicSize, tamanoObjetivo, velocidadZoom * Time.deltaTime);
            if (Mathf.Approximately(cam.orthographicSize, tamanoObjetivo)) tamanoObjetivo = -1f;
        }

        // En la arena la vista no sale de sus límites; si la arena es más angosta que la vista, se centra
        if (enArena && cam != null)
        {
            float mitad = cam.orthographicSize * cam.aspect;
            float x = (arenaMaxX - arenaMinX) <= mitad * 2f
                ? (arenaMinX + arenaMaxX) * 0.5f
                : Mathf.Clamp(targetPosition.x, arenaMinX + mitad, arenaMaxX - mitad);
            targetPosition = new Vector3(x, arenaCentroY, targetPosition.z);
            focusX = x;
        }

        // El suavizado se calcula sin la sacudida, para que esta no altere el seguimiento
        if (!hasSmoothed) { smoothedPosition = transform.position; hasSmoothed = true; }
        smoothedPosition = Vector3.SmoothDamp(smoothedPosition, targetPosition, ref velocity, smoothTime);

        Vector3 shake = Vector3.zero;
        if (shakeTime < shakeDuration)
        {
            shakeTime += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(shakeTime / shakeDuration);
            shake = (Vector3)(Random.insideUnitCircle * shakeAmount * k);
        }

        transform.position = smoothedPosition + shake;
    }
}
