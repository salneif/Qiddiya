# نظام القوائم والصوت — Chromafall

كل شيء تحت `Assets/_Core/`. لا تعدّل سكربتات القوائم القديمة المتفرقة، هذا النظام يستبدلها.

---

## 0) أولاً: أضف المشاهد لقائمة البناء (بدون هذا لن يعمل أي زر Play)

`File > Build Profiles > Scene List` وأضف بالترتيب:

1. `Assets/Abeer/Scenes/Hub-Menu.unity`
2. `Assets/Abeer/Scenes/Loading.unity`
3. `Assets/Abeer/Scenes/Hub.unity`
4. باقي مشاهد المراحل

> حاليًا القائمة فيها `SampleScene` و `Loading` فقط، ولهذا كل استدعاءات
> `LoadScene(0)` و `LoadScene(1)` و `LoadScene("L1")` في السكربتات القديمة تفشل.

---

## 1) AudioMixer (مرة واحدة للمشروع)

1. `Assets > Create > Audio > Audio Mixer` وسمّه `MainMixer` داخل `Assets/_Core/Audio/`.
2. في نافذة Audio Mixer أنشئ مجموعتين تحت Master: `Music` و `SFX`.
3. لكل مجموعة: اختر المجموعة، ثم في الـ Inspector كليك يمين على حقل **Volume** > `Expose ... to script`.
4. من قائمة **Exposed Parameters** (أعلى يمين نافذة الميكسر) أعد التسمية إلى:
   - `MasterVolume`
   - `MusicVolume`
   - `SFXVolume`
5. كل `AudioSource` في اللعبة: عيّن حقل **Output** إلى `Music` أو `SFX`.

---

## 2) AudioManager (في مشهد Hub-Menu فقط)

كائن فارغ اسمه `AudioManager` + سكربت `AudioManager`:

| الحقل | القيمة |
|---|---|
| Mixer | `MainMixer` |
| Master/Music/SFX Param | اتركها كما هي |
| Default Hover Clip | صوت المرور على الزر |
| Default Click Clip | صوت الضغط |
| Default Back Clip | صوت الرجوع |

ينتقل تلقائيًا بين المشاهد (`DontDestroyOnLoad`) ويحفظ مستويات الصوت في `PlayerPrefs`.

---

## 3) UIPanel — على كل لوحة

ركّب `UIPanel` على: `Main-Menu` و `Settings_UI` و `Settings_UI2` و `Credits-UI` و `Credits_UI` و `Pause_Holder`.

| اللوحة | First Selected | Hide Previous | Pause Game | Block Back |
|---|---|---|---|---|
| `Main-Menu` | `Play_Button` | ✔ | ✘ | **✔** |
| `Settings_UI` / `Settings_UI2` | أول سلايدر | ✔ | ✘ | ✘ |
| `Credits-UI` / `Credits_UI` | `Back_Button` | ✔ | ✘ | ✘ |
| `Pause_Holder` | `Resume_Button` | ✘ | **✔** | ✘ |

> **First Selected إجباري** — بدونه الكنترولر لا يستطيع التنقل بين الأزرار إطلاقًا.

---

## 4) MenuManager — كائن فارغ في كل مشهد فيه واجهة

**في Hub-Menu:**

- Root Panel = `Main-Menu`
- Close On Start = `Settings_UI`, `Settings_UI2`, `Credits-UI`, `Credits_UI`, `Pause_Holder`
- Manage Cursor = ✘
- On Cancel At Root = فارغ

**في مشاهد اللعب (Hub / Full_Steam / …):**

- Root Panel = فارغ
- Close On Start = `Pause_Holder`
- Manage Cursor = ✔
- On Cancel At Root → `MenuManager.Open` مع تمرير لوحة `Pause_Holder`

### ربط الأزرار (OnClick)

| الزر | الدالة |
|---|---|
| `Settings_Button` | `MenuManager.Open` → `Settings_UI` |
| `Credits_Button` | `MenuManager.Open` → `Credits-UI` |
| `Back_Button` / `Back_Button1` | `MenuManager.Back` |
| `Resume_Button` | `MenuManager.CloseAll` |

### التحكم الجاهز

| الإدخال | النتيجة |
|---|---|
| `ESC` / زر `B` (Xbox) / `O` (PS) | رجوع خطوة واحدة |
| زر `Start` / `Options` | فتح لوحة الإيقاف |
| `L1` / `R1` أو `Q` / `E` | تبديل تبويبات الإعدادات |

---

## 5) SceneLoader — على نفس كائن MenuManager

| الحقل | القيمة |
|---|---|
| Scene Name | `Hub` (أو أول مرحلة) |
| Fader | كائن `ScreenFader` إن وُجد |
| Delay Before Load | `0.15` ليكتمل صوت الزر |

ربط الأزرار:

| الزر | الدالة |
|---|---|
| `Play_Button` | `SceneLoader.LoadTargetScene` |
| `Quit_Button` / `Exit_Button` | `SceneLoader.QuitGame` |
| زر العودة للقائمة من داخل اللعبة | `SceneLoader.LoadScene` → `Hub-Menu` |
| زر إعادة المحاولة | `SceneLoader.ReloadCurrentScene` |

يرفض التحميل ويطبع خطأ واضحًا في الـ Console إذا كان المشهد غير مضاف لقائمة البناء.

---

## 6) الصوت والسلايدرات

- على كل `Slider` في الإعدادات: سكربت `VolumeSlider` → اختر `Channel` وعيّن `Value Label` (نص النسبة).
- على كل زر: سكربت `UIButtonFeedback` بدل `HoverScript` القديم.
  يعمل مع الماوس **والكنترولر** (الصوت يشتغل عند تحديد الزر بالعصا، لا عند المرور بالماوس فقط).
- `Slider_Script` القديم يُحذف من السلايدرات — `VolumeSlider` يقوم بعمله ويزيد.

---

## 7) تبويبات الإعدادات (General / Controls)

`UITabGroup` على كائن الإعدادات الأب:

- Tab 0 → Content: محتوى General، Header: نص `General`، First Selected: أول سلايدر
- Tab 1 → Content: محتوى Controls، Header: نص `Controls`، First Selected: أول عنصر
- زر `L1_Button` → `UITabGroup.Previous`
- زر `R1_Button` → `UITabGroup.Next`

---

## 8) الزر يعلق منتفخًا بعد الرجوع من الإعدادات — السبب والحل

**السبب في الأنميشن، لا في السكربتات.**

في كل كنترولر زر (`Assets/Abeer/Animations-Abeer/Play_Button .controller` وإخوانه):

| الكليب | يكتب `m_LocalScale`؟ |
|---|---|
| `Highlighted` | ✔ `(2.71, 3.03, 1.74)` |
| `Normal` / `Selected` / `Pressed` / `Disabled` | ✘ **فارغة تمامًا** |

كليب `Normal` بلا أي منحنى، فيونيتي لا يرجّع الحجم من الكليب بل من **القيمة الافتراضية**
التي التقطها الأنميتور لحظة ربطه بالكائن.

وهنا تنفجر المشكلة: إخفاء اللوحة يجمّد الأنميتور على حجم `Highlighted`. وعند إظهارها
يُعيد الأنميتور الربط **والزر لا يزال منتفخًا**، فيلتقط الحجم المنتفخ كقيمة افتراضية
جديدة. من تلك اللحظة لا شيء يعيده أبدًا.

**الحل المطبَّق في `UIPanel`:** التقاط الحجم/الموضع/الزخارف الأصلية مرة واحدة قبل أي
تحويم (`CaptureBaseline`)، وإرجاعها **قبل** `Animator.Rebind()` لا بعده. الترتيب هو كل
شيء — `Rebind` وحده كان يثبّت العطل لا يصلحه.

> **لا تركّب `UIButtonHoverEffect` ولا `UIButtonFeedback` على هذه الأزرار.** كلاهما
> يكتب `transform.localScale` كل إطار، والأنميتور يكتبه أيضًا بعد `Update` — فيتنازعان.
> هما بديلان لأزرار **بلا** أنميتور (`Transition = None`).

**حل جذري بديل** (لو أردت التخلص من المصدر): افتح كل كنترولر زر، اختر حالة `Normal`،
وفي نافذة Animation أضف مفتاحًا لـ`Scale` بالحجم الطبيعي. عندها لا يحتاج أي كود.

---

## 9) الماوس + الكيبورد + الكنترولر معًا

| المشكلة | الحل المطبَّق |
|---|---|
| زرّان بارزان معًا (واحد بالماوس وواحد محدَّد بالكنترولر) | `UISelectOnHover` — يُركَّب **تلقائيًا** على كل زر داخل `UIPanel` |
| زر يبقى بارزًا والمؤشر بعيد عنه | `MenuManager` يتتبّع الجهاز القائد؛ حين يقود الماوس لا يفرض تحديدًا، و`UISelectOnHover` يلغيه عند خروج المؤشر |
| صوت التحويم لا يُسمع بالكنترولر (مربوط على `EventTrigger > PointerEnter`) | `UISelectOnHover.OnSelect` ينادي الـ`EventTrigger` وحده — لا الكائن كله، وإلا ظنّ الـ`Button` أن المؤشر فوقه فعلق في `Highlighted` |

**لا يحتاج أي عمل في الـ Inspector** — التركيب تلقائي من خانة
`UIPanel > Auto Sync Mouse And Controller`.

### فخ: الزر يصير "ناشفًا" لا يكبر إطلاقًا

ترتيب أولويات `Selectable` (من `Selectable.cs:609`):

```
Disabled ← Pressed ← Selected ← Highlighted ← Normal
```

**`Selected` تسبق `Highlighted`.** فبمجرد أن يصير الزر محدَّدًا — بالكنترولر، أو
بالماوس بعد إضافة `UISelectOnHover` — لا يدخل `Highlighted` أبدًا. وكليب `Selected`
في كنترولرات عبير **فارغ** بينما كل التكبير في `Highlighted` وحده، فيختفي أي تفاعل.

هذا يفسّر أيضًا لماذا لم يكن للكنترولر أي إبراز قبل ذلك أصلًا.

**الحل في `UIPanel.RedirectEmptySelectedClip`:** يجعل زناد `Selected` يشير لحالة
`Highlighted`. لكن **بشرط** أن يكون `Selected` فارغًا و`Highlighted` مليئًا:

| الكنترولر | `Highlighted` | `Selected` | يُحوَّل؟ |
|---|---|---|---|
| `Play` / `Quit` / `Credits` / `Options` / `Back` / `Resume` / `Exit` | مليء | فارغ | ✔ |
| `L1_Button` / `R1_Button` | **فارغ** | **مليء** | ✘ (معكوسان — تعميم التحويل يكسرهما) |

### فخ ثانٍ: عنصر التحديد وحده يُنفّذ أمرًا

`L1_Button` و `R1_Button` في الإعدادات عليهما `EventTrigger > Select → SetActive`
(مرّتان لكل واحد). أي أن **مجرد تحديدهما يبدّل التبويب بلا أي كليك**.

فلا يُركَّب عليهما `UISelectOnHover` وإلا صار مرور المؤشر فوقهما يقلب التبويبات وحده.
`UIPanel.SelectionHasSideEffects` يكتشفهما تلقائيًا ويتخطاهما — يعملان بالكليك
وبالكنترولر فقط.

> القاعدة العامة: **لا تربط أمرًا على `Select`**، اربطه على `PointerClick` أو `Submit`.
> `Select` تعني "وقع عليه المؤشر/التحديد"، لا "اختاره اللاعب".

### الأصوات

- صوت التحويم مربوط على `EventTrigger > PointerEnter` — أي بالماوس فقط أصلًا.
  `UISelectOnHover.OnSelect` يناديه عند تنقّل الكنترولر ليُسمع هناك أيضًا.
- **لا يُشغَّل** عند تحديد يفرضه فتح/إغلاق لوحة (`MenuManager.SelectionIsSilent`)،
  وإلا سمعت نغمة زر عند بدء المشهد وعند كل فتح لوحة.
- `MenuManager.ForceSelect` لا يعيد تحديد ما هو محدَّد أصلًا، فلا يتكرّر الصوت.
- ⚠️ مصادر الصوت `Audio-Button-Hovor (1)` و `Audio-Button-Holder (2)` مكرّرة: نسخة تحت
  `Pause-UI` (مطفأة مع اللوحة) ونسخة في الجذر. `PlayOneShot` على مصدر داخل كائن مطفأ
  **لا يُصدر صوتًا**، فأزرار لوحة الإيقاف صامتة. اربطها بنسخة الجذر.

### سلايدر الصوت لا يغيّر شيئًا

`VolumeSlider` يعمل عبر `AudioManager.Instance`. ولم يكن هناك `AudioManager` في المشهد
إطلاقًا، فكان `Instance` يساوي `null` والسلايدر لا يفعل شيئًا.

الآن **`AudioManager` يُنشئ نفسه عند أول طلب** — لا يحتاج كائنًا في المشهد.

| الحالة | Master | Music / SFX |
|---|---|---|
| بلا ميكسر (الوضع الحالي) | ✔ عبر `AudioListener.volume` — يخفض كل شيء بما فيه الموسيقى | ✘ تُحفظ فقط + تحذير في الكونسول |
| مع `AudioMixer` مربوط | ✔ | ✔ |

للفصل بين الموسيقى والمؤثرات لاحقًا: أنشئ الميكسر (قسم ١)، اربطه في خانة `Mixer`،
و**عيّن `Output` لكل `AudioSource`** — حاليًا كلها `None (Audio Mixer Group)` فلا يمر
صوت واحد عبر الميكسر.

### فيضان تحذيرات `%` في الكونسول

`%` هو حرف **`%`**، وخط `DK Crayon Crumble2 SDF` لا يحتوي عليه. كان
`VolumeSlider` يكتب `"100%"` فيُطلق تحذيرًا **عند كل إطار من تحريك السلايدر**.

صار اللاحقة في خانة `Value Suffix` وقيمتها الافتراضية فارغة. لو أردت علامة `%` فعلًا،
أضف خط احتياطي في `Fallback Font Assets` داخل إعدادات TMP بدل كتابتها مباشرة.

---

## 10) جرد ربط الأزرار في Hub-Menu (فحص فعلي للسين)

| الزر | `On Click` الحالي | الحكم |
|---|---|---|
| `Play_Button ` | PlayOneShot + `SceneLoader.LoadTargetScene` | ✔ (لكن المشهد غير مضاف للبناء) |
| `Settings_Button ` | PlayOneShot + `MenuManager.Open → Settings_UI` | ✔ |
| `Credits_Button ` | PlayOneShot + `MenuManager.Open → Credits_UI` | ✔ |
| `Back_Button` (الكرِدِتس) | PlayOneShot + `MenuManager.Back` | ✔ |
| **`Quit_Button `** | PlayOneShot **فقط** | ✘ **لا يخرج إطلاقًا** — أضف `SceneLoader.QuitGame` |
| **`Back_Button1` (إعدادات القائمة)** | `Settings_UI.SetActive` + `Menu-UI.SetActive` | ✘ يتخطى المدير — استبدلهما بـ`MenuManager.Back` |
| `Back_Button1` (إعدادات الإيقاف) | SetActive ×2 + `MenuManager.Back` | ⚠ مزدوج — احذف الـ SetActive |
| `Settings_Button 2` (إيقاف) | SetActive ×2 | ⚠ قديم |
| `Resume_Button` | `Pause-UI.SetActive` | ⚠ قديم — `MenuManager.CloseAll` |
| `Exit_Button ` | `Pause_Menu.LoadMainMenu` → `LoadScene(0)` | ✘ المشهد 0 هو `SampleScene` لا `Hub-Menu` |

**شبكة الأمان في الكود:** `UIPanel.OnEnable/OnDisable` تُصلح الحالة المرئية وترتيب اللوحات
حتى لو غُيّرت اللوحة بـ`SetActive` مباشرة. لكنها علاج للعرض فقط — الربط الصحيح أفضل.

### قائمة البناء ناقصة

`ProjectSettings/EditorBuildSettings.asset` فيه `SampleScene` و `Loading` فقط.
فـ`SceneLoader.LoadTargetScene` (المشهد `Hub`) سيرفض التحميل ويطبع خطأً واضحًا.
أضف المشاهد من `File > Build Profiles > Scene List` — القسم 0 أعلاه.

---

## سكربتات قديمة يُستغنى عنها

`Main_Menu_AB` · `MENU` · `Load_Scene_Script` · `MainMenuController` · `Pause_Menu` · `Slider_Script` · `HoverScript`

لا تحذفها فورًا — مشاهد بقية الفريق مرتبطة بها. انقل كل مشهد للنظام الجديد ثم احذفها في النهاية.
