/// <summary>
/// هويّة العلم — ثلاثة أعلام، واحد لكل مرحلة.
///
/// الرقم مهم: يُحفظ في <see cref="GameProgress"/> كقناع بتّات، فلا تغيّر الأرقام
/// بعد أول حفظ وإلا صار العلم المزروع علمًا آخر.
/// </summary>
public enum FlagId
{
    [UnityEngine.InspectorName("بدون")]
    None = 0,

    [UnityEngine.InspectorName("علم ستيم تاون")]
    Steam = 1,

    [UnityEngine.InspectorName("علم التوايلايت")]
    Twilight = 2,

    [UnityEngine.InspectorName("علم السيرك")]
    Circus = 3,
}
