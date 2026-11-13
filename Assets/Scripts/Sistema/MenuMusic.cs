using UnityEngine;
using UnityEngine.SceneManagement;

// Música de los menús (menú principal, configuración, selección de personaje).
// Sobrevive al cambio entre escenas de menú, así que no se reinicia al pasar del menú a la selección.
// Cada vez que empieza a sonar elige una pista al azar, y al terminar una pasa a otra distinta.
// Al entrar a una escena que no es de menú (cinemática, carga, gameplay) se desvanece y se destruye.
public class MenuMusic : MonoBehaviour
{
    private static MenuMusic instance;
    private static int lastTrack = -1;   // para no repetir la misma pista al volver al menú

    [SerializeField] private AudioClip[] tracks;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.35f;
    [SerializeField] private float fadeInTime = 2f;
    [SerializeField] private float fadeOutTime = 1.5f;
    [Tooltip("Escenas donde suena esta música")]
    [SerializeField] private string[] menuScenes = { SceneLoader.MainMenu, SceneLoader.CharacterSelect };

    private AudioSource source;
    private float fade;          // 0..1, multiplica el volumen
    private float fadeSpeed;     // positivo = subiendo, negativo = bajando
    private bool leaving;

    private void Awake()
    {
        // Si ya suena desde una escena anterior, esta copia sobra
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.priority = 0;
        source.ignoreListenerPause = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (instance == this) PlayRandom();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (System.Array.IndexOf(menuScenes, scene.name) >= 0) return;

        // Escena de gameplay o cinemática: se apaga suavemente
        leaving = true;
        fadeSpeed = -1f / Mathf.Max(0.01f, fadeOutTime);
    }

    private void Update()
    {
        fade = Mathf.Clamp01(fade + fadeSpeed * Time.unscaledDeltaTime);
        source.volume = volume * fade;

        if (leaving)
        {
            if (fade <= 0f) Destroy(gameObject);
            return;
        }

        // Al terminar una pista, sigue con otra al azar
        if (!source.isPlaying && source.clip != null && Application.isFocused)
            PlayRandom();
    }

    private void PlayRandom()
    {
        if (tracks == null || tracks.Length == 0) return;

        int index = Random.Range(0, tracks.Length);
        if (tracks.Length > 1 && index == lastTrack)
            index = (index + 1 + Random.Range(0, tracks.Length - 1)) % tracks.Length;
        lastTrack = index;

        source.clip = tracks[index];
        source.Play();
        fade = 0f;
        fadeSpeed = 1f / Mathf.Max(0.01f, fadeInTime);
    }
}
