using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

// Reproduce la cinemática de inicio. Se puede saltar con Espacio (o Start en gamepad).
// Al terminar o al saltarla, pasa por la pantalla de carga hacia la escena del juego.
[RequireComponent(typeof(VideoPlayer))]
public class CinematicPlayer : MonoBehaviour
{
    [SerializeField] private string nextScene = SceneLoader.Game;
    [SerializeField] private Text skipHint;
    [Tooltip("Segundos en negro antes de que empiece el video")]
    [SerializeField] private float startDelay = 3.5f;
    [SerializeField] private float hintDelay = 1f;
    [Tooltip("Segundo del video en el que termina la cinemática (corta lo que sobra al final). 0 = reproducir hasta el final")]
    [SerializeField, Min(0f)] private float terminarEnSegundo = 0f;

    private VideoPlayer video;
    private bool finished;

    private void Awake()
    {
        video = GetComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.isLooping = false;

        // El audio pasa por un AudioSource para respetar el volumen de Configuración
        AudioSource src = GetComponent<AudioSource>();
        if (src != null)
        {
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            video.EnableAudioTrack(0, true);
            video.SetTargetAudioSource(0, src);
        }

        video.loopPointReached += _ => Finish();
        video.errorReceived += (_, msg) => { Debug.LogWarning("Error en la cinemática: " + msg); Finish(); };

        if (skipHint != null) SetHintAlpha(0f);
    }

    private void Start()
    {
        if (video.clip == null && string.IsNullOrEmpty(video.url)) { Finish(); return; }
        StartCoroutine(PlayAfterDelay());
    }

    // Pantalla en negro unos segundos (el video se va preparando mientras tanto) y luego arranca
    private IEnumerator PlayAfterDelay()
    {
        video.Prepare();
        yield return new WaitForSecondsRealtime(startDelay);
        if (finished) yield break;

        video.Play();
        if (skipHint != null) StartCoroutine(HintRoutine());
    }

    private void Update()
    {
        if (finished) return;

        // Termina antes del final del archivo (ahí la grabación muestra el cursor y el navegador)
        if (terminarEnSegundo > 0f && video.isPlaying && video.time >= terminarEnSegundo)
        {
            Finish();
            return;
        }

        bool skip = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (skip) Finish();
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        if (video != null) video.Stop();
        SceneLoader.LoadWithLoadingScreen(nextScene);
    }

    // El aviso "Presiona ESPACIO para saltar" aparece tras un momento y late suavemente
    private IEnumerator HintRoutine()
    {
        yield return new WaitForSecondsRealtime(hintDelay);
        float t = 0f;
        while (!finished)
        {
            t += Time.unscaledDeltaTime;
            float fadeIn = Mathf.Clamp01(t / 0.5f);
            SetHintAlpha(fadeIn * (0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 3f))));
            yield return null;
        }
    }

    private void SetHintAlpha(float a)
    {
        Color c = skipHint.color; c.a = a; skipHint.color = c;
    }
}
