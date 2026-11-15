using UnityEngine;

// Hitbox del panel de refrigeración en la espalda del Centinela. Solo está activo mientras el jefe
// está aturdido: así también se le puede disparar por detrás y el daño entra con el multiplicador de aturdido.
// Va en un hijo con un Collider2D en modo trigger; se coloca siempre del lado contrario a donde mira el jefe.
[RequireComponent(typeof(Collider2D))]
public class JefePanelDebil : MonoBehaviour, IDamageable
{
    private JefeCentinela jefe;
    private Collider2D col;
    private float xLocal;

    private void Awake()
    {
        if (col != null) return;   // ya se inicializó desde Configurar
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        xLocal = Mathf.Abs(transform.localPosition.x);
    }

    public void Configurar(JefeCentinela duenio)
    {
        jefe = duenio;
        if (col == null) Awake();
    }

    public void Activar(bool activo)
    {
        if (col != null) col.enabled = activo;
    }

    // El panel está en la espalda: si el jefe mira a la izquierda, queda a la derecha
    public void Orientar(bool jefeMiraIzquierda)
    {
        Vector3 p = transform.localPosition;
        p.x = jefeMiraIzquierda ? xLocal : -xLocal;
        transform.localPosition = p;
    }

    public void TakeDamage(float amount)
    {
        if (jefe != null) jefe.RecibirDanioPanel(amount);
    }
}
