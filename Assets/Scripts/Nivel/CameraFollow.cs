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
