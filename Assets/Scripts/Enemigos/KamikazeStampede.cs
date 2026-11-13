using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Estampida de robots kamikaze: cuando el jugador se acerca a este punto, empiezan a salir robots
// desde fuera de la pantalla en grupos pequeños, a ritmo casi constante, hasta completar el total.
public class KamikazeStampede : MonoBehaviour
{
    [SerializeField] private GameObject kamikazePrefab;

    [Header("Activación")]
    [Tooltip("La estampida empieza cuando el jugador está a esta distancia horizontal o menos")]
    [SerializeField] private float activationDistance = 13f;
    [Tooltip("Segundos que parpadea la flecha de advertencia antes de que salga el primer grupo (0 = sin aviso)")]
    [SerializeField, Min(0f)] private float avisoPrevio = 1.5f;

    [Header("Oleadas")]
    [SerializeField] private int totalCount = 20;
    [SerializeField] private int groupSize = 3;
    [Tooltip("Segundos entre un grupo y el siguiente")]
    [SerializeField] private float groupInterval = 1.3f;
    [Tooltip("Variación aleatoria del intervalo (para que no sea mecánico)")]
    [SerializeField] private float intervalJitter = 0.25f;
    [Tooltip("Separación en tiempo entre los robots de un mismo grupo")]
    [SerializeField] private float stagger = 0.22f;
    [Tooltip("Si hay tantos vivos, espera a que caigan algunos antes del siguiente grupo")]
    [SerializeField] private int maxAlive = 8;

    [Header("Posición")]
    [Tooltip("Aparecen fuera de la pantalla, a esta distancia del borde derecho de la cámara (mayor que la mitad del ancho del robot)")]
    [SerializeField] private float offscreenMargin = 2.5f;
    [Tooltip("No aparecen más allá de esta X (borde del nivel)")]
    [SerializeField] private float maxSpawnX = 203f;
    [SerializeField] private float spawnY = 0.2f;

    private Transform player;
    private Camera cam;
    private bool started;
    private readonly List<GameObject> alive = new List<GameObject>();
    private readonly List<Collider2D> spawnedColliders = new List<Collider2D>();

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        cam = Camera.main;
    }

    private void Update()
    {
        if (started || kamikazePrefab == null) return;
        if (player == null)
        {
            // El jugador puede reemplazarse al iniciar (selección de personaje)
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }

        if (Mathf.Abs(player.position.x - transform.position.x) <= activationDistance)
        {
            started = true;
            StartCoroutine(Run());
        }
    }

    private IEnumerator Run()
    {
        if (avisoPrevio > 0f)
        {
            // Los robots llegan desde fuera de la pantalla por la derecha
            float x = transform.position.x;
            if (cam != null) x = Mathf.Max(x, cam.transform.position.x + cam.orthographicSize * cam.aspect + offscreenMargin);
            IndicadoresUI.Advertir(new Vector3(x, spawnY + 1.5f, 0f), avisoPrevio + 0.6f);
            yield return new WaitForSeconds(avisoPrevio);
        }

        int spawned = 0;
        while (spawned < totalCount)
        {
            // Espera si ya hay demasiados en pantalla
            alive.RemoveAll(g => g == null);
            while (alive.Count >= maxAlive)
            {
                yield return new WaitForSeconds(0.2f);
                alive.RemoveAll(g => g == null);
            }

            int inGroup = Mathf.Min(groupSize, totalCount - spawned);
            for (int i = 0; i < inGroup; i++)
            {
                // Si el borde del nivel ya está a la vista, no hay dónde esconderlos: espera
                float x;
                while (!SitioFueraDePantalla(i, out x)) yield return null;
                Spawn(x);
                spawned++;
                if (i < inGroup - 1) yield return new WaitForSeconds(stagger * Random.Range(0.7f, 1.3f));
            }

            yield return new WaitForSeconds(groupInterval + Random.Range(-intervalJitter, intervalJitter));
        }
    }

    // X donde aparecer sin que se vea; false si el único lugar posible ya está dentro de la pantalla
    private bool SitioFueraDePantalla(int indexInGroup, out float x)
    {
        x = transform.position.x;
        if (cam == null) { x = Mathf.Min(x, maxSpawnX); return true; }

        float cameraRight = cam.transform.position.x + cam.orthographicSize * cam.aspect;
        x = Mathf.Max(x, cameraRight + offscreenMargin + indexInGroup * 0.6f);
        x = Mathf.Min(x, maxSpawnX);
        return x >= cameraRight + offscreenMargin;
    }

    private void Spawn(float x)
    {
        GameObject k = Instantiate(kamikazePrefab, new Vector3(x, spawnY, 0f), Quaternion.identity);
        alive.Add(k);

        // Los robots de la estampida se atraviesan entre sí (si no, se empujan en fila y explotan al chocar en el salto)
        Collider2D col = k.GetComponent<Collider2D>();
        if (col != null)
        {
            spawnedColliders.RemoveAll(c => c == null);
            foreach (Collider2D other in spawnedColliders) Physics2D.IgnoreCollision(col, other, true);
            spawnedColliders.Add(col);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f);
        Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1.5f, 2f, 0f));
        Gizmos.DrawLine(transform.position + Vector3.left * activationDistance + Vector3.up * 3f,
                        transform.position + Vector3.left * activationDistance + Vector3.down * 1f);
    }
}
