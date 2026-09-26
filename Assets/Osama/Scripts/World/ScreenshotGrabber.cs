using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// يلتقط صورة للشاشة بدقة مضاعفة أثناء اللعب — لصور شاشات التحميل وغيرها.
///
/// <c>ScreenCapture.CaptureScreenshot</c> بـ<see cref="superSize"/> يرسم المشهد
/// بدقة أكبر من الشاشة فعليًا ثم يحفظه، فيطلع أكبر وأوضح من أي قصّ للشاشة — وليس
/// تكبيرًا لصورة صغيرة. نافذة Game عندك ١٩٢٠×١٠٨٠ مع <c>superSize = 2</c> تعني
/// صورة ٣٨٤٠×٢١٦٠ حقيقية.
///
/// ضعه على أي كائن في السين، شغّل، وقف على المنظر الذي تريده، واضغط الزر.
/// المسار الكامل يُطبع في الكونسول.
/// </summary>
[DisallowMultipleComponent]
public class ScreenshotGrabber : MonoBehaviour
{
    [Header("الالتقاط")]
    [Tooltip("مضاعف الدقة: 2 = ضعف طول وعرض نافذة Game. أكبر من 4 يخنق الذاكرة")]
    [Range(1, 4)] [SerializeField] private int superSize = 2;

    [Tooltip("زر الالتقاط أثناء اللعب")]
    [SerializeField] private Key captureKey = Key.F9;

    [Header("الحفظ")]
    [Tooltip("مجلد داخل مجلد المشروع (بجانب Assets) — يُنشأ إن لم يوجد")]
    [SerializeField] private string folder = "Screenshots";

    [Tooltip("بداية اسم الملف — يُضاف إليه اسم السين والوقت")]
    [SerializeField] private string prefix = "shot";

    [Header("النظافة")]
    [Tooltip("يخفي واجهات السين لحظة الالتقاط فتطلع الصورة بلا أزرار ولا تلميحات")]
    [SerializeField] private bool hideCanvases = true;

    private void Update()
    {
        if (captureKey == Key.None || Keyboard.current == null) return;
        if (Keyboard.current[captureKey].wasPressedThisFrame) Capture();
    }

    /// <summary>يلتقط صورة الآن — اربطه بزر أو نادِه من أي حدث.</summary>
    [ContextMenu("التقط صورة الآن")]
    public void Capture()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ScreenshotGrabber] الالتقاط يعمل أثناء التشغيل فقط.", this);
            return;
        }

        StartCoroutine(CaptureRoutine());
    }

    private System.Collections.IEnumerator CaptureRoutine()
    {
        Canvas[] hidden = null;

        if (hideCanvases)
        {
            hidden = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude,
                                               FindObjectsSortMode.None);
            foreach (Canvas canvas in hidden)
                if (canvas != null) canvas.enabled = false;

            // إطار كامل حتى يختفي ما أخفيناه فعلًا قبل أن تُرسم الصورة
            yield return new WaitForEndOfFrame();
        }

        string dir = Path.Combine(Application.dataPath, "..", folder);
        Directory.CreateDirectory(dir);

        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string stamp = DateTime.Now.ToString("HHmmss");
        string path = Path.GetFullPath(Path.Combine(dir, $"{prefix}_{scene}_{stamp}.png"));

        ScreenCapture.CaptureScreenshot(path, superSize);

        // الالتقاط يكتمل بعد نهاية الإطار، والملف لا يوجد قبلها
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        Debug.Log($"[ScreenshotGrabber] حفظت الصورة (×{superSize}):\n{path}");

        if (hidden != null)
            foreach (Canvas canvas in hidden)
                if (canvas != null) canvas.enabled = true;
    }
}
