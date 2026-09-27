using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// يجعل تفاعلات سلطان تعمل بيد التحكّم — أبوابه ورافعاته وأزراره.
///
/// خمسة سكربتات في ستيم وفي مرحلة سلطان بالسيرك تقرأ
/// <c>Input.GetKeyDown(KeyCode.E)</c> من نظام الإدخال القديم. ونظام الإدخال القديم
/// <b>لا يرى يد التحكّم كحرف</b> ولا يمكن حشو ضغطة فيه من داخل اللعبة: لا خريطة علي
/// ولا <c>InteractInput</c> يصلان إليه. فباليد وحدها كان اللاعب يقف أمام الباب ولا
/// يُفتح — وبعض هذي الأبواب طريقٌ إلى بقيّة المرحلة.
///
/// فهذا يُكمل عنها: كل واحدة منها تحسب بنفسها «اللاعب قريب» في حقل خاص؛ نقرأ ذلك
/// الحقل، وحين يُضغط <b>المربّع</b> نُشغّل تفاعلها. والكيبورد لا يمرّ من هنا أبدًا —
/// السكربت يقرأه بنفسه، فقراءتنا معه تعني تفاعلين في ضغطة.
///
/// ولا نلمس ملفًا لأحد: الوصول كله <b>بالاسم عبر الانعكاس</b>. ما غاب من السكربتات
/// يُتجاهل بصمت — أكثر السينات ما فيها هذي الأشياء — وما تغيّرت أسماؤه يطبع سطرًا
/// واحدًا ويُترك. ويُركّب نفسه تلقائيًا فلا تعديل على مشاهد أحد.
///
/// الأصل أن تُوصل هذي السكربتات بنظام الإدخال الجديد عند صاحبها؛ هذا جسر حتى يصير.
/// </summary>
[DisallowMultipleComponent]
public class LegacyInteractBridge : MonoBehaviour
{
    /// <summary>كيف يُشغَّل تفاعل هذا السكربت — لأن بعضها بلا دالّة تُنادى.</summary>
    private enum Trigger
    {
        /// <summary>له دالّة خاصة نناديها كما يناديها هو.</summary>
        Method,

        /// <summary>منطقه داخل <c>Update</c> نفسه: نقلب راية الفتح ونشغّل الصوت.</summary>
        Door,
    }

    /// <summary>وصف سكربت واحد: أين يخزّن «اللاعب قريب»، وكيف يُشغَّل.</summary>
    private readonly struct Recipe
    {
        public readonly string TypeName;
        public readonly string RangeField;
        public readonly Trigger How;
        public readonly string MethodName;
        public readonly string OpenField;
        public readonly string[] SoundFields;

        public Recipe(string typeName, string rangeField, Trigger how, string methodName,
                      string openField = null, string[] soundFields = null)
        {
            TypeName = typeName;
            RangeField = rangeField;
            How = how;
            MethodName = methodName;
            OpenField = openField;
            SoundFields = soundFields;
        }
    }

    /// <summary>
    /// الوصفات. حقول المدى كلها خاصة، وبعضها مؤقّت عشري (<c>_stayTime</c>) لا راية:
    /// السكربت يكتبه في <c>OnTriggerStay</c> ويطرح منه كل إطار، فموجبٌ = قريب.
    /// </summary>
    private static readonly Recipe[] Recipes =
    {
        // رافعة غرفة البخار — لها toggle() كاملة
        new Recipe("Lever", "_playerInRange", Trigger.Method, "toggle"),

        // زرّ لغز الحيوانات، وزرّ التماثيل — لكلٍّ دالّته
        new Recipe("PlateButton", "_rangeTimer", Trigger.Method, "press"),
        new Recipe("PedestalButton", "_rangeTimer", Trigger.Method, null),

        // الأبواب: لا دالّة، المنطق سطران داخل Update
        new Recipe("Door", "_stayTime", Trigger.Door, "playClip",
                   "_isOpen", new[] { "openClip", "closeClip" }),
        new Recipe("DoorLever", "_stayTime", Trigger.Door, null,
                   "_isOpen", new[] { "openSound" }),
    };

    private const BindingFlags Any =
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static LegacyInteractBridge instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("LegacyInteractBridge") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<LegacyInteractBridge>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    /// <summary>سكربت واحد في السين، وقد ربطنا حقوله مسبقًا.</summary>
    private readonly struct Target
    {
        public readonly MonoBehaviour Script;
        public readonly FieldInfo Range;
        public readonly Recipe Recipe;
        public readonly MethodInfo Method;
        public readonly FieldInfo Open;
        public readonly FieldInfo[] Sounds;

        public Target(MonoBehaviour script, FieldInfo range, Recipe recipe,
                      MethodInfo method, FieldInfo open, FieldInfo[] sounds)
        {
            Script = script;
            Range = range;
            Recipe = recipe;
            Method = method;
            Open = open;
            Sounds = sounds;
        }

        /// <summary>هل اللاعب قريب الآن؟ راية، أو مؤقّت موجب.</summary>
        public bool InRange
        {
            get
            {
                object value = Range.GetValue(Script);
                if (value is bool flag) return flag;
                if (value is float timer) return timer > 0f;
                return false;
            }
        }
    }

    private readonly List<Target> targets = new List<Target>();
    private bool scanned;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    /// <summary>سين جديد = أشياء جديدة، فنُعيد المسح مرّة واحدة.</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => scanned = false;

    private void LateUpdate()
    {
        if (!scanned) Scan();
        if (targets.Count == 0) return;

        // المربّع وحده. الكيبورد يقرأه كل سكربت بنفسه
        if (!InteractInput.PadPressed) return;

        foreach (Target target in targets)
        {
            if (target.Script == null || !target.Script.isActiveAndEnabled) continue;
            if (!target.InRange) continue;

            Fire(target);
        }
    }

    private void Fire(Target target)
    {
        try
        {
            if (target.Recipe.How == Trigger.Method)
            {
                if (target.Method != null) target.Method.Invoke(target.Script, null);
                else Pedestal(target);

                return;
            }

            Door(target);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LegacyInteractBridge] ما نفع تشغيل " +
                             $"{target.Recipe.TypeName}: {e.Message}", target.Script);
        }
    }

    /// <summary>
    /// الباب بلا دالّة: نقلب <c>_isOpen</c> وهو يدوّر نفسه في <c>Update</c> كما يفعل
    /// عادة، ثم نشغّل نفس الصوت الذي كان يشغّله.
    /// </summary>
    private static void Door(Target target)
    {
        bool open = !(bool)target.Open.GetValue(target.Script);
        target.Open.SetValue(target.Script, open);

        if (target.Sounds == null || target.Sounds.Length == 0) return;

        // بابٌ له دالّة صوت تأخذ الكليب، وآخر له AudioSource يُشغَّل عند الفتح فقط
        if (target.Method != null)
        {
            FieldInfo clipField = open ? target.Sounds[0]
                                       : target.Sounds[Mathf.Min(1, target.Sounds.Length - 1)];
            target.Method.Invoke(target.Script, new[] { clipField.GetValue(target.Script) });
            return;
        }

        if (!open) return;

        if (target.Sounds[0].GetValue(target.Script) is AudioSource source) source.Play();
    }

    /// <summary>
    /// زرّ التماثيل: التفاعل ليس دالّة بل نداء على المنصّة التي يحملها، ولا يُضغط إلا
    /// إن قبلت. فنمشي كما يمشي هو بالضبط: <c>CanInteract</c> ثم <c>Cycle()</c>.
    /// </summary>
    private static void Pedestal(Target target)
    {
        FieldInfo field = target.Script.GetType().GetField("pedestal", Any);
        object pedestal = field?.GetValue(target.Script);
        if (pedestal == null) return;

        var can = pedestal.GetType().GetProperty("CanInteract", Any);
        if (can != null && can.GetValue(pedestal) is bool ok && !ok) return;

        var cycle = pedestal.GetType().GetMethod("Cycle", Any, null, System.Type.EmptyTypes, null);
        if (cycle == null) return;

        bool pressed = cycle.Invoke(pedestal, null) is bool result && result;
        if (!pressed) return;

        // الغمزة البصرية وصوت الضغطة، كما يفعلهما بنفسه
        var timer = target.Script.GetType().GetField("_pressTimer", Any);
        var duration = target.Script.GetType().GetField("pressDuration", Any);
        if (timer != null && duration != null) timer.SetValue(target.Script, duration.GetValue(target.Script));

        target.Script.GetType().GetMethod("playPress", Any, null, System.Type.EmptyTypes, null)
              ?.Invoke(target.Script, null);
    }

    /// <summary>
    /// مسح مرّة واحدة لكل سين. هذي أشياء ثابتة في المشهد من بدايته، فلا داعي لتكرار
    /// المرور على كل سكربتات السين كل إطار.
    /// </summary>
    private void Scan()
    {
        scanned = true;
        targets.Clear();

        foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (script == null) continue;

            System.Type type = script.GetType();
            foreach (Recipe recipe in Recipes)
            {
                if (type.Name != recipe.TypeName) continue;

                Target? bound = Bind(script, type, recipe);
                if (bound.HasValue) targets.Add(bound.Value);
                break;
            }
        }
    }

    private static Target? Bind(MonoBehaviour script, System.Type type, Recipe recipe)
    {
        FieldInfo range = type.GetField(recipe.RangeField, Any);
        if (range == null || (range.FieldType != typeof(bool) && range.FieldType != typeof(float)))
        {
            Warn(script, $"ما لقيت حقل المدى \"{recipe.RangeField}\" في {recipe.TypeName}.");
            return null;
        }

        MethodInfo method = null;
        if (!string.IsNullOrEmpty(recipe.MethodName))
        {
            method = FindMethod(type, recipe.MethodName);
            if (method == null)
            {
                Warn(script, $"ما لقيت \"{recipe.MethodName}()\" في {recipe.TypeName}.");
                return null;
            }
        }

        FieldInfo open = null;
        FieldInfo[] sounds = null;

        if (recipe.How == Trigger.Door)
        {
            open = type.GetField(recipe.OpenField, Any);
            if (open == null || open.FieldType != typeof(bool))
            {
                Warn(script, $"ما لقيت راية الفتح \"{recipe.OpenField}\" في {recipe.TypeName}.");
                return null;
            }

            if (recipe.SoundFields != null)
            {
                sounds = new FieldInfo[recipe.SoundFields.Length];
                for (int i = 0; i < sounds.Length; i++)
                    sounds[i] = type.GetField(recipe.SoundFields[i], Any);
            }
        }

        return new Target(script, range, recipe, method, open, sounds);
    }

    /// <summary>أول دالّة بهذا الاسم بلا نظر لمعاملاتها — منها ما يأخذ كليبًا.</summary>
    private static MethodInfo FindMethod(System.Type type, string name)
    {
        foreach (MethodInfo candidate in type.GetMethods(Any))
            if (candidate.Name == name) return candidate;

        return null;
    }

    private static void Warn(MonoBehaviour script, string why) =>
        Debug.LogWarning($"[LegacyInteractBridge] {why} تفاعله باليد ما بيشتغل، " +
                         $"والكيبورد يعمل كما كان.", script);
}
