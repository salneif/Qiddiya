using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <b>إعادة المرحلة</b> من أوّلها — لما يعلق اللاعب أو تخرب نقطة الحفظ نفسها. زرّ «Restart» في
/// قائمة الإيقاف (<see cref="PauseMenuFix"/>) وبديلُ «Respawn» حين لا نجد نظام موت.
///
/// يعيد تحميل المشهد نفسه عبر <see cref="LoadingOverlay"/>. الوجوه المجموعة تبقى مجموعة (لا خسارة
/// ولا جمعَ مرّتين)، والعلم المحمول يبقى محمولًا، والمؤقّت والإحصائيات تبدأ من الصفر مع المشهد.
/// </summary>
public static class LevelRestart
{
    /// <summary>المراحل الثلاث — الهب والقوائم بلا إعادة.</summary>
    public static bool Available => IsLevel(SceneManager.GetActiveScene().name);

    public static bool IsLevel(string scene) => LevelPortal.FlagOf(scene) != FlagId.None;

    public static void Go()
    {
        if (Available) Load(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// يعيد تحميل أيّ مشهد لعب، والهب معه — بديل «Respawn» حيث لا نظام موت (الهب): اللاعب يظهر
    /// من جديد عند بوابته.
    /// </summary>
    public static void Reload()
    {
        if (ChromaEvents.GameplayScene) Load(SceneManager.GetActiveScene().name);
    }

    private static void Load(string scene)
    {
        if (LoadingOverlay.IsBusy) return;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
        ChromaBank.Flush();
        if (!LoadingOverlay.Go(scene)) SceneManager.LoadSceneAsync(scene);
    }
}
