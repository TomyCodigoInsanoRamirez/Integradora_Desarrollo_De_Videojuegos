using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla de carga: carga en segundo plano la escena pendiente y muestra el progreso
public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private RectTransform barFill;   // Se estira de 0 a 1 en X (anchorMax)
    [SerializeField] private Text percentText;
    [SerializeField] private Text loadingText;
    [SerializeField] private Text tipText;
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private float minDisplayTime = 2f;
    [SerializeField, TextArea] private string[] tips =
    {
        "Dispara con Enter. Mantén W o la flecha arriba para apuntar hacia arriba.",
        "Agáchate con S para esquivar los disparos de los robots.",
        "Presiona 1 para golpear con el tubo cuando los robots estén cerca.",
        "Cada integrante del escuadrón tiene su propio estilo de combate.",
        "ADO-342 aprende de cada enfrentamiento. No te confíes.",
    };

    private float shown;

    private IEnumerator Start()
    {
        if (tipText != null && tips.Length > 0) tipText.text = tips[Random.Range(0, tips.Length)];
        SetProgress(0f);
        if (fade != null) yield return Fade(1f, 0f, 0.35f);

        AsyncOperation op = SceneManager.LoadSceneAsync(SceneLoader.PendingScene);
        if (op == null) yield break;
        op.allowSceneActivation = false;

        float start = Time.unscaledTime;
        while (true)
        {
            float real = Mathf.Clamp01(op.progress / 0.9f);              // Unity se detiene en 0.9 hasta activar
            float timeGate = Mathf.Clamp01((Time.unscaledTime - start) / minDisplayTime);
            float target = Mathf.Min(real, timeGate);
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 1.5f);
            SetProgress(shown);
            AnimateDots();

            if (shown >= 1f && op.progress >= 0.9f) break;
            yield return null;
        }

        if (fade != null) yield return Fade(0f, 1f, 0.35f);
        op.allowSceneActivation = true;
    }

    private void SetProgress(float p)
    {
        if (barFill != null) barFill.anchorMax = new Vector2(p, barFill.anchorMax.y);
        if (percentText != null) percentText.text = Mathf.RoundToInt(p * 100f) + "%";
    }

    private void AnimateDots()
    {
        if (loadingText == null) return;
        int dots = (int)(Time.unscaledTime * 3f) % 4;
        loadingText.text = "CARGANDO" + new string('.', dots);
    }

    private IEnumerator Fade(float from, float to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        fade.alpha = to;
    }
}
