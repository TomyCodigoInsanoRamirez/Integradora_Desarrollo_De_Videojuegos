using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Vida del jugador. Al morir:
//  - si ya tocó un Checkpoint, reaparece ahí con la vida llena (lo maneja el Checkpoint);
//  - si no, se reinicia la escena.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    [Header("Muerte y reaparición")]
    [Tooltip("Segundos desde que muere hasta que se reinicia la escena (si no hay checkpoint)")]
    [SerializeField] private float esperaReinicio = 1.5f;
    [Tooltip("Segundos de invulnerabilidad al reaparecer (parpadea mientras dura)")]
    [SerializeField] private float invulnerabilidadAlReaparecer = 1.5f;

    private float health;
    private float invulnerableHasta;
    private SpriteRenderer spriteRenderer;

    public float Vida => health;
    public float VidaMaxima => maxHealth;
    public bool EstaMuerto { get; private set; }

    private void Awake()
    {
        health = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        // Parpadeo mientras es invulnerable
        if (spriteRenderer != null && !EstaMuerto)
            spriteRenderer.enabled = Time.time >= invulnerableHasta || Mathf.Repeat(Time.time * 12f, 1f) > 0.35f;
    }

    public void TakeDamage(float amount)
    {
        if (EstaMuerto || Time.time < invulnerableHasta) return;

        health -= amount;

        if (health <= 0f)
        {
            health = 0f;
            Morir();
        }
    }

    // Recupera vida sin pasar del máximo (kit de reparación)
    public void Curar(float amount)
    {
        if (EstaMuerto) return;
        health = Mathf.Min(maxHealth, health + amount);
    }

    // Lo llama el Checkpoint después de mover al jugador al punto de reaparición
    public void Revivir()
    {
        EstaMuerto = false;
        health = maxHealth;
        invulnerableHasta = Time.time + invulnerabilidadAlReaparecer;
    }

    private void Morir()
    {
        EstaMuerto = true;
        Debug.Log("El jugador ha muerto");

        if (Checkpoint.Activo != null)
        {
            Checkpoint.Activo.Reaparecer(this);
            return;
        }

        // Sin checkpoint: se oculta y se reinicia el nivel. La corrutina corre en un objeto aparte
        // porque el jugador se desactiva.
        gameObject.SetActive(false);
        var reinicio = new GameObject("ReinicioEscena").AddComponent<ReinicioEscena>();
        reinicio.Iniciar(esperaReinicio);
    }

    // Recarga la escena actual tras una espera
    private class ReinicioEscena : MonoBehaviour
    {
        public void Iniciar(float espera) => StartCoroutine(Reiniciar(espera));

        private IEnumerator Reiniciar(float espera)
        {
            yield return new WaitForSeconds(espera);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
