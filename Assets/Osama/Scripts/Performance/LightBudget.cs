using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <b>ميزانية الأضواء</b> — أخفّ لعبة بلا لمس مشهد واحد:
///
/// <list type="bullet">
/// <item><b>الظلال</b>: ستيم تاون فيها ٢٤ ضوءًا نقطيًّا بظلٍّ حيّ، وكل ضوءٍ نقطيّ يرسم المشهد ستّ مرّات
/// (مكعّب ظلّ) كل إطار — أثقل شيءٍ في اللعبة. هنا الظلّ لأقرب <see cref="ShadowSlots"/> من الكاميرا
/// وحدها، والبعيدة بلا ظلّ (لا يُرى ظلّها أصلًا من ذلك البعد).</item>
/// <item><b>الأضواء البعيدة</b>: التوايلايت فيها ٢١٩ ضوءًا حيًّا. ما كان أبعد من
/// <see cref="FarDistance"/> م عن الكاميرا (بعد مداه) يُطفأ، ويعود حين تقترب.</item>
/// </list>
///
/// لا نلمس المخبوز. ولا نُطفئ ضوءًا يمسكه سكربت (مرجعٌ في حقل، أو سكربتٌ على كائنه — مصباح
/// <c>LampSwitch</c>، وميض <c>LightFlicker</c>، أضواء الروبوت…): تشغيله وإطفاؤه لصاحبه، ولو أعدناه
/// لأضأنا ما أطفأه. يُعاد الحساب أربع مرّات في الثانية، والفرز على عشرات الأضواء لا يكلّف شيئًا.
/// </summary>
[DisallowMultipleComponent]
public class LightBudget : MonoBehaviour
{
    /// <summary>مفتاح المقارنة: false = الأضواء كما صمّمها أصحابها.</summary>
    public static readonly bool Enabled = true;

    private const int ShadowSlots = 3;
    private const float FarDistance = 45f;
    private const float Interval = 0.25f;

    private sealed class Entry
    {
        public Light light;
        public LightShadows shadows;   // الأصليّ
        public bool culledByUs;
        public float distance;
    }

    private static LightBudget instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!Enabled || instance != null) return;
        var host = new GameObject("LightBudget") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<LightBudget>();
        DontDestroyOnLoad(host);
        instance.Scan();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        lightFields.Clear();
    }

    private static readonly Dictionary<Type, FieldInfo[]> lightFields = new Dictionary<Type, FieldInfo[]>();

    private readonly List<Entry> shadowed = new List<Entry>();
    private readonly List<Entry> plain = new List<Entry>();
    private float nextAt;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Scan();
    }

    private void Scan()
    {
        shadowed.Clear();
        plain.Clear();
        nextAt = 0f;
        HashSet<Light> scripted = Scripted();
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light == null || (light.type != LightType.Point && light.type != LightType.Spot)) continue;
            LightBakingOutput baked = light.bakingOutput;
            if (baked.isBaked && baked.lightmapBakeType == LightmapBakeType.Baked) continue;

            var entry = new Entry { light = light, shadows = light.shadows };
            if (light.shadows != LightShadows.None) shadowed.Add(entry);
            else if (!scripted.Contains(light)) plain.Add(entry);
        }
        if (shadowed.Count <= ShadowSlots) shadowed.Clear();   // تحت الميزانية أصلًا: لا شأن لنا
    }

    /// <summary>كل ضوءٍ يمسكه سكربت: حقلٌ من نوع Light أو مصفوفة/قائمة منه، أو سكربتٌ على كائنه نفسه.</summary>
    private static HashSet<Light> Scripted()
    {
        var found = new HashSet<Light>();
        foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (script == null) continue;
            Type type = script.GetType();
            if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal)) continue;

            if (script.TryGetComponent(out Light own)) found.Add(own);
            foreach (FieldInfo field in Fields(type))
            {
                object value = field.GetValue(script);
                if (value is Light one) { if (one != null) found.Add(one); }
                else if (value is IEnumerable<Light> many)
                    foreach (Light l in many) if (l != null) found.Add(l);
            }
        }
        return found;
    }

    private static FieldInfo[] Fields(Type type)
    {
        if (lightFields.TryGetValue(type, out FieldInfo[] cached)) return cached;
        var list = new List<FieldInfo>();
        for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (f.FieldType == typeof(Light) || typeof(IEnumerable<Light>).IsAssignableFrom(f.FieldType)) list.Add(f);
        return lightFields[type] = list.ToArray();
    }

    private void LateUpdate()
    {
        if (shadowed.Count == 0 && plain.Count == 0) return;
        if (Time.unscaledTime < nextAt) return;
        nextAt = Time.unscaledTime + Interval;

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 eye = cam.transform.position;

        if (shadowed.Count > 0) Shadows(eye);
        if (plain.Count > 0) Cull(eye);
    }

    /// <summary>الظلّ لأقرب الأضواء الشغّالة، والباقي بلا ظلّ.</summary>
    private void Shadows(Vector3 eye)
    {
        for (int i = shadowed.Count - 1; i >= 0; i--)
        {
            Entry e = shadowed[i];
            if (e.light == null) { shadowed.RemoveAt(i); continue; }
            bool lit = e.light.isActiveAndEnabled && e.light.intensity > 0f;
            e.distance = lit ? Vector3.Distance(eye, e.light.transform.position) : float.MaxValue;
        }
        shadowed.Sort((a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < shadowed.Count; i++)
        {
            Entry e = shadowed[i];
            LightShadows want = i < ShadowSlots ? e.shadows : LightShadows.None;
            if (e.light.shadows != want) e.light.shadows = want;
        }
    }

    /// <summary>أضواءٌ بلا ظلّ: البعيد يُطفأ، ويعود ما أطفأناه نحن حين نقترب.</summary>
    private void Cull(Vector3 eye)
    {
        for (int i = plain.Count - 1; i >= 0; i--)
        {
            Entry e = plain[i];
            if (e.light == null) { plain.RemoveAt(i); continue; }

            float reach = e.light.range;
            bool far = Vector3.Distance(eye, e.light.transform.position) - reach > FarDistance;

            if (far && e.light.enabled && !e.culledByUs)
            {
                e.light.enabled = false;
                e.culledByUs = true;
            }
            else if (!far && e.culledByUs)
            {
                e.culledByUs = false;
                if (!e.light.enabled) e.light.enabled = true;
            }
        }
    }

    private void OnDestroy()
    {
        // خروج من اللعب: كل شيء كما كان (يهمّ في المحرّر مع مشاهد مفتوحة)
        foreach (Entry e in shadowed) if (e.light != null) e.light.shadows = e.shadows;
        foreach (Entry e in plain) if (e.light != null && e.culledByUs) e.light.enabled = true;
    }
}
