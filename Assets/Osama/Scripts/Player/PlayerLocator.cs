using UnityEngine;

/// <summary>
/// يجد اللاعب الحقيقي بين كل الكائنات التي تحمل وسمه.
///
/// <see cref="GameObject.FindGameObjectWithTag"/> يرجّع <b>أي واحد</b> من الكائنات
/// الموسومة، وترتيبه في البلد يختلف عن المحرر. فلو كان في السين كائن آخر موسوم
/// Player بالغلط (مثل Checkpoint)، يشتغل كل شيء في المحرر ثم في البلد تمسك
/// السكربتات الكائن الخطأ: الرافعة لا تُمسك لأن "اللاعب" بعيد، والفئران لا تقتل
/// لأن "اللاعب" بلا <see cref="PlayerKillable"/>.
///
/// هنا نفضّل من عليه <see cref="PlayerKillable"/>، ثم من عليه جسم متحرك.
/// ولو في السين كائن واحد فقط بالوسم، يرجّعه كما هو — نفس السلوك القديم حرفيًا.
/// </summary>
public static class PlayerLocator
{
    /// <summary>اللاعب الحقيقي بين الكائنات الفعّالة الموسومة بـ <paramref name="tag"/>، أو null.</summary>
    public static GameObject Find(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;

        GameObject[] tagged = GameObject.FindGameObjectsWithTag(tag);
        if (tagged.Length == 0) return null;
        if (tagged.Length == 1) return tagged[0];

        GameObject best = null;
        int bestScore = -1;
        foreach (var go in tagged)
        {
            int score = Score(go);
            if (score > bestScore)
            {
                best = go;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>كم يبدو هذا الكائن لاعبًا فعليًا؟ الأعلى يفوز، والتعادل للأول.</summary>
    private static int Score(GameObject go)
    {
        if (go.GetComponent<PlayerKillable>() != null) return 4;
        if (go.GetComponentInParent<PlayerKillable>() != null) return 3;
        if (go.GetComponentInParent<CharacterController>() != null) return 2;

        var rb = go.GetComponentInParent<Rigidbody>();
        if (rb != null && !rb.isKinematic) return 1;

        // Checkpoint وأي منطقة ثابتة موسومة بالغلط
        return 0;
    }
}
