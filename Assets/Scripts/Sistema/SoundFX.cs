using UnityEngine;

public static class SoundFX
{
    // Reproduce el sonido en un objeto temporal para que no se corte si quien lo dispara se destruye
    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        GameObject go = new GameObject("SFX_" + clip.name);
        AudioSource source = go.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.PlayOneShot(clip, volume);
        Object.Destroy(go, clip.length + 0.1f);
    }

    public static void PlayRandom(AudioClip[] clips, float volume = 1f)
    {
        if (clips == null || clips.Length == 0) return;

        Play(clips[Random.Range(0, clips.Length)], volume);
    }
}
