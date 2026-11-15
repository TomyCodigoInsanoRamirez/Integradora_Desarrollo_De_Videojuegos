using UnityEngine;

// Música de fondo del gameplay: alterna las pistas con un fundido cruzado, entra con fade-in
// y se queda a un volumen de ambiente por debajo de los efectos (disparos, golpes, pasos).
// El volumen general de Configuración se aplica encima a través de AudioListener.volume.
public class GameplayMusic : MonoBehaviour
{
    [SerializeField] private AudioClip[] tracks;

    [Tooltip("Volumen de la música. Las pistas vienen muy fuertes (~-16 dB), 0.2 las deja como ambiente")]
    [Range(0f, 1f)] [SerializeField] private float volume = 0.2f;
    [SerializeField] private float fadeInTime = 4f;
    [SerializeField] private float crossfadeTime = 5f;
    [SerializeField] private bool randomStart = true;

    private AudioSource[] sources;
    private int active;        // índice del AudioSource que suena
    private int trackIndex;
    private float fadeT;       // progreso del fundido actual (0..1)
    private float fadeDuration;

    // Silencio temporal (por ejemplo durante la música del jefe): multiplica el volumen
    private float silencio = 1f;
    private float silencioObjetivo = 1f;
    private float silencioVelocidad = 1f;

    // Baja (true) o recupera (false) la música del nivel con un fundido de 'duracion' segundos
    public void Silenciar(bool silenciar, float duracion)
    {
        silencioObjetivo = silenciar ? 0f : 1f;
        silencioVelocidad = 1f / Mathf.Max(0.01f, duracion);
    }

    private void Awake()
    {
        sources = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;   // 2D
            s.priority = 0;        // que nunca la corten otros sonidos
            s.volume = 0f;
            sources[i] = s;
        }
    }

    private void Start()
    {
        if (tracks == null || tracks.Length == 0) { enabled = false; return; }

        trackIndex = randomStart ? Random.Range(0, tracks.Length) : 0;
        // Con una sola pista la dejamos en loop y no hace falta fundido cruzado
        sources[0].loop = tracks.Length == 1;
        StartTrack(0, tracks[trackIndex], fadeInTime);
    }

    private void Update()
    {
        AudioSource cur = sources[active];
        AudioSource prev = sources[1 - active];
        silencio = Mathf.MoveTowards(silencio, silencioObjetivo, silencioVelocidad * Time.unscaledDeltaTime);
        float vol = volume * silencio;

        // Avance del fundido: sube la pista actual y baja la anterior
        if (fadeT < 1f)
        {
            fadeT = fadeDuration > 0f ? Mathf.Clamp01(fadeT + Time.unscaledDeltaTime / fadeDuration) : 1f;
            float k = Mathf.SmoothStep(0f, 1f, fadeT);
            cur.volume = vol * k;
            if (prev.isPlaying)
            {
                prev.volume = vol * (1f - k);
                if (fadeT >= 1f) prev.Stop();
            }
        }
        else
        {
            cur.volume = vol;   // permite ajustar el volumen en vivo desde el Inspector
        }

        // Cerca del final de la pista, empieza la siguiente
        if (tracks.Length > 1 && cur.clip != null && cur.isPlaying &&
            cur.time >= cur.clip.length - crossfadeTime)
        {
            trackIndex = (trackIndex + 1) % tracks.Length;
            StartTrack(1 - active, tracks[trackIndex], crossfadeTime);
        }
        else if (tracks.Length > 1 && !cur.isPlaying && fadeT >= 1f)
        {
            // Por si la pista terminó sin fundido (p. ej. al volver de una pausa larga)
            trackIndex = (trackIndex + 1) % tracks.Length;
            StartTrack(1 - active, tracks[trackIndex], 1f);
        }
    }

    private void StartTrack(int sourceIndex, AudioClip clip, float fade)
    {
        active = sourceIndex;
        AudioSource s = sources[active];
        s.clip = clip;
        s.volume = 0f;
        s.Play();
        fadeT = 0f;
        fadeDuration = fade;
    }
}
