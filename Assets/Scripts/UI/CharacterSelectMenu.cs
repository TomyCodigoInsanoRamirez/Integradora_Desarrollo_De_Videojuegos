using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menú de selección: resalta el personaje actual (etiqueta amarilla + retrato iluminado),
// se navega con mouse o teclado (A/D o flechas) y se confirma con clic, Enter o Espacio.
public class CharacterSelectMenu : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public Button button;          // Botón sobre el retrato
        public Image portrait;         // Retrato del personaje
        public Image tag;              // Etiqueta P1..P4
        public Sprite tagSelected;     // Versión amarilla
        public Sprite tagIdle;         // Versión gris
        public RectTransform plate;    // Placa con el nombre
    }

    [SerializeField] private CharacterRoster roster;
    [Tooltip("Escena que se abre después de elegir personaje")]
    [SerializeField] private string nextScene = SceneLoader.Cinematic;

    [Header("Ranuras (mismo orden que el CharacterRoster)")]
    [SerializeField] private Slot[] slots;

    [Header("Estilo")]
    [SerializeField] private Color idlePortraitColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color selectedPortraitColor = Color.white;
    [SerializeField] private float selectedScale = 1.04f;
    [SerializeField] private float animSpeed = 12f;
    [SerializeField] private float confirmDelay = 0.45f;

    [Header("Transición")]
    [Tooltip("Duración del fundido a negro antes de pasar a la cinemática")]
    [SerializeField] private float fadeOutTime = 1.2f;

    private int current;
    private Image fadeOverlay;
    private bool confirming;
    private Vector2 lastMousePos;
    private Canvas canvas;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();

        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            if (slots[i].button != null)
                slots[i].button.onClick.AddListener(() => Select(index));
        }

        current = Mathf.Clamp(CharacterSelection.SelectedIndex, 0, Mathf.Max(0, slots.Length - 1));
        ApplyVisualsInstant();
    }

    private void Update()
    {
        if (confirming || slots == null || slots.Length == 0) return;

        HandleKeyboard();
        HandleGamepad();
        HandleMouseHover();
        AnimateSlots(Time.unscaledDeltaTime);
    }

    private void HandleKeyboard()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Move(-1);
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Move(1);

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            Select(current);

        if (kb.escapeKey.wasPressedThisFrame) BackToMenu();
    }

    private void HandleGamepad()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return;

        if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) Move(-1);
        if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) Move(1);
        if (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) Select(current);
        if (pad.buttonEast.wasPressedThisFrame) BackToMenu();
    }

    // Esc / B regresa al menú principal
    private void BackToMenu()
    {
        if (confirming) return;
        confirming = true;
        SceneManager.LoadScene(SceneLoader.MainMenu);
    }

    // Solo cambia la selección cuando el mouse se mueve, para no pelear con el teclado
    private void HandleMouseHover()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 pos = mouse.position.ReadValue();
        if ((pos - lastMousePos).sqrMagnitude < 0.01f) return;
        lastMousePos = pos;

        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].portrait == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(slots[i].portrait.rectTransform, pos, cam))
            {
                SetCurrent(i);
                break;
            }
        }
    }

    private void Move(int dir)
    {
        SetCurrent((current + dir + slots.Length) % slots.Length);
    }

    private void SetCurrent(int index)
    {
        if (index == current) return;
        current = index;
        UpdateTags();
    }

    // Lo llaman los botones de cada personaje con su índice en la lista
    public void Select(int index)
    {
        if (confirming) return;
        if (roster == null || index < 0 || index >= roster.characters.Length) return;

        current = index;
        UpdateTags();
        CharacterSelection.SelectedIndex = index;
        StartCoroutine(ConfirmAndLoad(index));
    }

    private IEnumerator ConfirmAndLoad(int index)
    {
        confirming = true;

        // Pequeño "golpe" visual en el retrato elegido antes de cambiar de escena
        Slot s = slots[index];
        RectTransform rt = s.portrait != null ? s.portrait.rectTransform : null;
        float t = 0f;
        while (t < confirmDelay)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / confirmDelay);
            if (rt != null)
                rt.localScale = Vector3.one * (selectedScale + Mathf.Sin(k * Mathf.PI) * 0.06f);
            if (s.portrait != null)
                s.portrait.color = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.6f), Mathf.PingPong(t * 8f, 1f));
            yield return null;
        }

        // Fundido suave a negro; la escena de la cinemática también empieza en negro
        Image overlay = GetFadeOverlay();
        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeOutTime);
            overlay.color = new Color(0f, 0f, 0f, k * k * (3f - 2f * k));   // SmoothStep
            yield return null;
        }
        overlay.color = Color.black;
        yield return null;   // asegura que se dibuje un cuadro totalmente negro

        SceneManager.LoadScene(nextScene);
    }

    // Capa negra encima de todo el Canvas (se crea al vuelo, no hace falta configurarla en la escena)
    private Image GetFadeOverlay()
    {
        if (fadeOverlay != null) return fadeOverlay;

        Transform root = canvas != null ? canvas.transform : transform;
        var go = new GameObject("FundidoNegro", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = root.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.SetAsLastSibling();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        fadeOverlay = go.GetComponent<Image>();
        fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        fadeOverlay.raycastTarget = true;   // bloquea clics durante el fundido
        return fadeOverlay;
    }

    private void UpdateTags()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Slot s = slots[i];
            if (s.tag == null) continue;
            Sprite sprite = i == current ? s.tagSelected : s.tagIdle;
            if (sprite != null) s.tag.sprite = sprite;
        }
    }

    private void AnimateSlots(float dt)
    {
        float k = 1f - Mathf.Exp(-animSpeed * dt);
        for (int i = 0; i < slots.Length; i++)
        {
            Slot s = slots[i];
            bool sel = i == current;

            if (s.portrait != null)
            {
                s.portrait.color = Color.Lerp(s.portrait.color, sel ? selectedPortraitColor : idlePortraitColor, k);
                RectTransform rt = s.portrait.rectTransform;
                rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * (sel ? selectedScale : 1f), k);
            }

            if (s.plate != null)
                s.plate.localScale = Vector3.Lerp(s.plate.localScale, Vector3.one * (sel ? 1.06f : 1f), k);
        }
    }

    private void ApplyVisualsInstant()
    {
        UpdateTags();
        for (int i = 0; i < slots.Length; i++)
        {
            Slot s = slots[i];
            bool sel = i == current;
            if (s.portrait != null)
            {
                s.portrait.color = sel ? selectedPortraitColor : idlePortraitColor;
                s.portrait.rectTransform.localScale = Vector3.one * (sel ? selectedScale : 1f);
            }
            if (s.plate != null) s.plate.localScale = Vector3.one * (sel ? 1.06f : 1f);
        }
    }
}
