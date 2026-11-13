using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menú principal: Iniciar juego (abre la cinemática), Configuración, Créditos y Salir
public class MainMenu : MonoBehaviour
{
    [Header("Botones")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Paneles")]
    [SerializeField] private GameObject mainButtons;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button creditsBackButton;

    [Header("Configuración")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeValue;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vsyncToggle;

    [Header("Fundido")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private float fadeTime = 0.6f;

    private bool leaving;

    private void Start()
    {
        playButton.onClick.AddListener(StartGame);
        settingsButton.onClick.AddListener(() => OpenPanel(settingsPanel, volumeSlider));
        creditsButton.onClick.AddListener(() => OpenPanel(creditsPanel, creditsBackButton));
        quitButton.onClick.AddListener(QuitGame);
        settingsBackButton.onClick.AddListener(ClosePanels);
        creditsBackButton.onClick.AddListener(ClosePanels);

        volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
        vsyncToggle.SetIsOnWithoutNotify(GameSettings.VSync);
        UpdateVolumeLabel(GameSettings.Volume);

        volumeSlider.onValueChanged.AddListener(v => { GameSettings.Volume = v; UpdateVolumeLabel(v); });
        fullscreenToggle.onValueChanged.AddListener(v => GameSettings.Fullscreen = v);
        vsyncToggle.onValueChanged.AddListener(v => GameSettings.VSync = v);

        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        mainButtons.SetActive(true);
        Select(playButton);

        if (fade != null) StartCoroutine(Fade(1f, 0f));
    }

    private void Update()
    {
        if (leaving) return;

        bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (back && (settingsPanel.activeSelf || creditsPanel.activeSelf)) ClosePanels();

        // Si el mouse hace clic en el fondo se pierde la selección; la recuperamos para el teclado
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            if (settingsPanel.activeSelf) Select(volumeSlider);
            else if (creditsPanel.activeSelf) Select(creditsBackButton);
            else Select(playButton);
        }
    }

    private void StartGame()
    {
        if (leaving) return;
        leaving = true;
        StartCoroutine(LeaveTo(SceneLoader.CharacterSelect));
    }

    private void QuitGame()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OpenPanel(GameObject panel, Selectable first)
    {
        mainButtons.SetActive(false);
        settingsPanel.SetActive(panel == settingsPanel);
        creditsPanel.SetActive(panel == creditsPanel);
        Select(first);
    }

    private void ClosePanels()
    {
        bool wasSettings = settingsPanel.activeSelf;
        GameSettings.Save();
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        mainButtons.SetActive(true);
        Select(wasSettings ? settingsButton : creditsButton);
    }

    private void UpdateVolumeLabel(float v)
    {
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    private static void Select(Selectable s)
    {
        if (s != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    private IEnumerator LeaveTo(string scene)
    {
        if (fade != null) yield return Fade(0f, 1f);
        SceneManager.LoadScene(scene);
    }

    private IEnumerator Fade(float from, float to)
    {
        fade.blocksRaycasts = true;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(from, to, t / fadeTime);
            yield return null;
        }
        fade.alpha = to;
        fade.blocksRaycasts = to > 0.5f;
    }
}
