using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ذراع القارب في التوايلايت يعمل من أيّ مكان بعد أوّل مرورٍ به — نُصلحه من الخارج.
///
/// <c>A_BoatLever</c> (علي) يجعل "يمكن التفاعل" صحيحًا في <c>OnTriggerStay</c> ولا يُرجعه أبدًا —
/// لا <c>OnTriggerExit</c>. فمن مرّ بجانب الذراع مرّة صار كل ضغطِ تفاعلٍ في المرحلة كلها يقلب
/// الألواح ويُسمع صوت الذراع. هنا: ما دام اللاعب خارج منطقة الذراع يُطفأ ذلك كل إطار، ويعيده
/// الذراع نفسه حين يعود إليه.
///
/// بالاسم عبر الانعكاس، بلا سطرٍ في سكربت علي. يُركّب نفسه ويسكت في كل مشهدٍ بلا ذراع.
/// </summary>
[DisallowMultipleComponent]
public class BoatLeverGuard : MonoBehaviour
{
    private const string LeverScript = "A_BoatLever";
    private const string CanInteract = "_canInterAct";
    private static readonly object No = false;

    private sealed class Lever
    {
        public Component script;
        public FieldInfo flag;
        public Collider[] zones;
    }

    private static BoatLeverGuard instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("BoatLeverGuard") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<BoatLeverGuard>();
        DontDestroyOnLoad(host);
        instance.Scan();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private readonly List<Lever> levers = new List<Lever>();
    private CharacterController player;
    private float nextPlayerScan;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Scan();
    }

    private void Scan()
    {
        levers.Clear();
        player = null;
        foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (script == null || script.GetType().Name != LeverScript) continue;
            FieldInfo flag = script.GetType().GetField(CanInteract, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (flag == null || flag.FieldType != typeof(bool)) continue;

            var zones = new List<Collider>();
            foreach (Collider c in script.GetComponents<Collider>()) if (c.isTrigger) zones.Add(c);
            if (zones.Count > 0) levers.Add(new Lever { script = script, flag = flag, zones = zones.ToArray() });
        }
    }

    private void LateUpdate()
    {
        if (levers.Count == 0) return;
        if (player == null)
        {
            if (Time.unscaledTime < nextPlayerScan) return;
            nextPlayerScan = Time.unscaledTime + 1f;
            GameObject p = PlayerLocator.Find("Player");
            player = p != null ? p.GetComponentInParent<CharacterController>() : null;
            if (player == null) return;
        }

        Bounds body = player.bounds;
        foreach (Lever lever in levers)
        {
            if (lever.script == null) continue;
            bool inside = false;
            foreach (Collider zone in lever.zones)
                if (zone != null && zone.enabled && zone.gameObject.activeInHierarchy && zone.bounds.Intersects(body)) { inside = true; break; }
            if (!inside) lever.flag.SetValue(lever.script, No);
        }
    }
}
