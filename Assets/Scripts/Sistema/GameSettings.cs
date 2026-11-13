using UnityEngine;

// Opciones del juego guardadas en PlayerPrefs y aplicadas al arrancar
public static class GameSettings
{
    private const string VolumeKey = "opciones_volumen";
    private const string FullscreenKey = "opciones_pantalla_completa";
    private const string VSyncKey = "opciones_vsync";

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); AudioListener.volume = Volume; }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        set { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); Screen.fullScreen = value; }
    }

    public static bool VSync
    {
        get => PlayerPrefs.GetInt(VSyncKey, 1) == 1;
        set { PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0); QualitySettings.vSyncCount = value ? 1 : 0; }
    }

    public static void Save() => PlayerPrefs.Save();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        AudioListener.volume = Volume;
        QualitySettings.vSyncCount = VSync ? 1 : 0;
#if !UNITY_EDITOR
        Screen.fullScreen = Fullscreen;
#endif
    }
}
