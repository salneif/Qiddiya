using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// أصوات مؤثّرات اللون: قطرات، أزياء، نبضات. ثنائيّة الأبعاد، من مخزون مصادر واحد
/// يعيش بين المشاهد، والمقاطع من <c>Osama/Resources/Chroma/Sfx</c> بالاسم.
///
/// <b>تحترم سلايدر المؤثّرات</b> (<c>vol_sfx</c>): لا ميكسر في المشروع، فسلايدر
/// المؤثّرات في الإعدادات لا يصل لأيّ صوت — إلا هذه. والرئيسي يطبّقه
/// <c>AudioManager</c> على كل شيء عبر <see cref="AudioListener.volume"/>.
///
/// مقطعٌ غائب لا يكسر شيئًا: لا صوت، وتحذيرٌ واحد لكل اسم.
/// </summary>
public static class ChromaSfx
{
    private const string Folder = "Chroma/Sfx/";
    private const int Voices = 10;
    private const string SfxVolumeKey = "vol_sfx";   // نفس مفتاح AudioManager

    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private static readonly HashSet<string> missing = new HashSet<string>();
    private static AudioSource[] voices;
    private static int next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        clips.Clear();
        missing.Clear();
        voices = null;
        next = 0;
    }

    /// <summary>المقطع بالاسم (بلا امتداد)، أو null إن غاب.</summary>
    public static AudioClip Clip(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (clips.TryGetValue(name, out AudioClip clip) && clip != null) return clip;
        if (missing.Contains(name)) return null;

        clip = Resources.Load<AudioClip>(Folder + name);
        if (clip == null)
        {
            missing.Add(name);
            Debug.LogWarning($"[ChromaSfx] ما لقيت Osama/Resources/{Folder}{name}");
            return null;
        }
        clips[name] = clip;
        return clip;
    }

    /// <summary>
    /// يشغّل مقطعًا مرّة. <paramref name="pitch"/> يغيّر الطبقة (والمدّة معها) — نغمة
    /// واحدة تصير سلّمًا كاملًا بـ<see cref="Semitones"/>. يرجع المصدر أو null.
    /// </summary>
    public static AudioSource Play(string name, float volume = 1f, float pitch = 1f)
    {
        AudioClip clip = Clip(name);
        if (clip == null || volume <= 0f) return null;

        AudioSource source = Voice();
        if (source == null) return null;

        source.Stop();
        source.clip = clip;
        source.pitch = Mathf.Clamp(pitch, 0.25f, 3f);
        source.volume = Mathf.Clamp01(volume * SfxVolume);
        source.Play();
        return source;
    }

    /// <summary>نسبة الطبقة لنقل النغمة بهذا العدد من أنصاف الدرجات.</summary>
    public static float Semitones(float steps) => Mathf.Pow(2f, steps / 12f);

    /// <summary>سلايدر المؤثّرات من الإعدادات (١ إن لم يُحفظ بعد).</summary>
    public static float SfxVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));

    /// <summary>مصدرٌ فارغ، أو الأقدم إن كانت كلها تعمل.</summary>
    private static AudioSource Voice()
    {
        if (voices == null || voices[0] == null)
        {
            var host = new GameObject("ChromaSfx") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(host);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                AudioSource s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.ignoreListenerPause = true;   // أصوات الخزانة تعمل واللعبة موقوفة
                voices[i] = s;
            }
        }

        for (int i = 0; i < Voices; i++)
        {
            int k = (next + i) % Voices;
            if (!voices[k].isPlaying) { next = (k + 1) % Voices; return voices[k]; }
        }
        AudioSource oldest = voices[next];
        next = (next + 1) % Voices;
        return oldest;
    }
}
