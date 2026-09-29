using UnityEngine;

/// <summary>
/// جسد القطرة المرئي: كرةٌ، وهالةٌ مضيئة خلفها، وحلقةٌ للذهبية والكبيرة. بلا
/// كولايدر — لا تصطدم بشيء ولا يصيبها شعاعُ أحد — وبلا سكربتٍ لكل قطرة: الحقل يحرّكها
/// كلها من حلقةٍ واحدة.
///
/// <b>تُعاد ولا تُهدم</b>: قطرةٌ جُمعت تُطفأ وتنتظر في المخزون، والدفعة التالية
/// تأخذها. فلا إنشاء ولا هدم في كل التقاط.
/// </summary>
internal sealed class ChromaDropView
{
    private readonly GameObject root;
    private readonly Transform orb, glow, ring;
    private readonly MeshRenderer orbRenderer, glowRenderer, ringRenderer;
    private readonly bool isFace;

    /// <summary>الوجه بطاقةٌ مربّعة والكرة قطرها متر — الوجه أعرض ليُقرأ من بعيد.</summary>
    private const float FaceScale = 2f;
    /// <summary>كم يأخذ الوجه من لون قطرته: لمسةٌ من القوس، والوجه يبقى وجهه.</summary>
    private const float FaceTint = 0.45f;

    private ChromaDropView(GameObject root, Transform orb, Transform glow, Transform ring,
                           MeshRenderer orbRenderer, MeshRenderer glowRenderer, MeshRenderer ringRenderer,
                           bool isFace)
    {
        this.isFace = isFace;
        this.root = root;
        this.orb = orb;
        this.glow = glow;
        this.ring = ring;
        this.orbRenderer = orbRenderer;
        this.glowRenderer = glowRenderer;
        this.ringRenderer = ringRenderer;
    }

    /// <summary>هل ما زال حيًّا؟ يموت مع سينه.</summary>
    public bool Alive => root != null;

    public static ChromaDropView Create(Transform parent)
    {
        var root = new GameObject("Drop");
        root.transform.SetParent(parent, false);

        var orbGo = new GameObject("Orb");
        orbGo.transform.SetParent(root.transform, false);
        Material face = ChromaDropArt.Face;
        MeshRenderer orbRenderer = face != null
            ? ChromaDropArt.Renderer(orbGo, ChromaDropArt.Card, face)
            : ChromaDropArt.Renderer(orbGo, ChromaDropArt.Ball, ChromaDropArt.Orb);

        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(root.transform, false);
        MeshRenderer glowRenderer = ChromaDropArt.Renderer(glowGo, ChromaDropArt.Card, ChromaDropArt.Glow);

        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(root.transform, false);
        MeshRenderer ringRenderer = ChromaDropArt.Renderer(ringGo, ChromaDropArt.Card, ChromaDropArt.Ring);
        ringGo.SetActive(false);

        root.SetActive(false);
        return new ChromaDropView(root, orbGo.transform, glowGo.transform, ringGo.transform,
                                  orbRenderer, glowRenderer, ringRenderer, face != null);
    }

    public void Show(bool withRing)
    {
        if (root == null) return;
        if (ring.gameObject.activeSelf != withRing) ring.gameObject.SetActive(withRing);
        if (!root.activeSelf) root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null && root.activeSelf) root.SetActive(false);
    }

    /// <summary>
    /// الكرة في موضعها وحجمها، تدور حول نفسها ببطء — ضوؤها مخبوزٌ فيها، فجانبها
    /// المضيء يلفّ معها ويُرى الدوران على كرةٍ لا نقش عليها. والهالة والحلقة تواجهان
    /// الكاميرا، والحلقة تدور حول محور النظر.
    /// </summary>
    public void Place(Vector3 at, float size, float glowSize, float ringSize, Quaternion facing, float spin)
    {
        if (root == null) return;
        root.transform.position = at;
        float s = isFace ? size * FaceScale : size;
        orb.localScale = new Vector3(s, s, s);
        orb.localRotation = Quaternion.Euler(0f, spin, 0f);
        glow.localScale = new Vector3(glowSize, glowSize, glowSize);
        if (ring.gameObject.activeSelf) ring.localScale = new Vector3(ringSize, ringSize, ringSize);
        Face(facing, spin);
    }

    /// <summary>
    /// الهالة والحلقة نحو الكاميرا وحدهما — للبعيدة التي لا تُحرَّك. بطاقةٌ تدور الكاميرا
    /// حولها تُرى من حافّتها فتختفي، وهالتها هي ما يُرى منها في العالم الرمادي.
    /// </summary>
    public void Face(Quaternion facing, float spin)
    {
        if (root == null) return;
        glow.rotation = facing;
        if (ring.gameObject.activeSelf) ring.rotation = facing * Quaternion.Euler(0f, 0f, spin);
    }

    public void Paint(Color orbColor, Color glowColor, Color ringColor)
    {
        ChromaDropArt.Tint(orbRenderer, isFace ? Color.Lerp(Color.white, orbColor, FaceTint) : orbColor);
        ChromaDropArt.Tint(glowRenderer, glowColor);
        if (ring != null && ring.gameObject.activeSelf) ChromaDropArt.Tint(ringRenderer, ringColor);
    }
}
