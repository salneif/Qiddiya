# شاشة تحميل (Loading) للعبة قِدّية — متطلبات التنفيذ

> **✅ نُفّذت.** هذا المستند صار سجلًّا للقرارات لا قائمة مهام. المرجع العملي الآن
> هو `Assets/Osama/Scripts/README.md` فقرة **١٣. شاشة التحميل — `LoadingOverlay`**،
> وفيه الضبط والصور وحالات الفشل. اختلف عن الخطة شيئان:
> - **لا يوجد بريفاب**: الشاشة تبني كانفسها بالكود، فلا ربط يدوي ولا مرجع يسقط من البلد.
> - **الصور انتقلت إلى `Assets/Osama/Resources/Loading/`** لتدخل البناء دائمًا.

## السياق ولماذا نحتاجها

الانتقال بين المشاهد اليوم يتم بتعتيم أسود فقط: `ScreenFader` في المشهد الحالي يعتّم
ثم يُحمَّل المشهد التالي، وفي الوجهة يفتح `ScreenFader` آخر من السواد. لا يوجد أي شيء
يخبر اللاعب إلى أين هو ذاهب ولا كم بقي، والانتقال يبدو توقّفًا لا انتقالًا.

المطلوب: شاشة تحميل تظهر فوق كل شيء أثناء الانتقال، **تتغيّر صورتها حسب الوجهة**
(الهب / ستيم تاون / التوايلايت / السيرك)، وفيها **بار تقدّم** تركض فوقه **شخصية علي**
كأنها هي التي تقطع الطريق إلى المرحلة التالية.

قرارات متفق عليها مع أسامة:
- **شاشة Overlay** تعيش فوق المشاهد، لا مشهد Loading مستقل (لا تغيير في قائمة البناء).
- **سبرايت 2D متحرك** لعلي (صور متتابعة يوفّرها أسامة).
- **الصور يوفّرها أسامة**: وصلت الأربع كلها (الهب، ستيم، التوايلايت، السيرك).
- **البار استعراضي بمدة ثابتة ≈ 4 ثوانٍ**، لا يتبع نسبة التحميل الحقيقية.

---

## القيود التي يجب احترامها

1. **المستودع مشترك** بين Ali و Osama و Sultan و Razan و Abeer، وكل السكربتات في
   تجميعة واحدة `Assembly-CSharp`. **أي اسم صنف مكرر يكسر البناء للجميع.**
   كل الملفات الجديدة تحت `Assets/Osama/` فقط، ولا تُعدَّل مشاهد أو سكربتات غيره.
   الأسماء المقترحة متحقَّق أنها غير مستخدمة: `LoadingOverlay`، `LoadingScreen`،
   `LoadingBar`، `SceneTransition`.
2. **`ScreenFader` لا يعيش بين المشاهد** (`Assets/Osama/Scripts/UI/ScreenFader.cs`،
   بلا `DontDestroyOnLoad`)، ولكل مشهد نسخته الخاصة، و`fadeInOnStart` مفعّل فيها
   كلها. فشاشة التحميل **لا يصلح أن تكون كائنًا عاديًا في المشهد**.
3. **الشيء الوحيد الذي يعيش بين المشاهد اليوم** هو `GameProgress`
   (`Assets/Osama/Scripts/Progress/GameProgress.cs`، `DontDestroyOnLoad` في L101،
   مع `Instance` ينشئ نفسه عند أول طلب).
4. **ستة نداءات `SceneManager.LoadScene` متزامنة** موجودة في المشروع (سكربتات علي
   وعبير وملفات في جذر Assets). المتزامن لا يمكن إظهار شاشة تحميل معه إطلاقًا.
   لا نلمسها، لكن نعرف أن شاشة التحميل ستظهر فقط في المسارات التي تمر بـ
   `LevelPortal` و`SceneLoader` و`ScreenFader`.

---

## نقاط الدخول الموجودة (يجب إعادة استخدامها لا استبدالها)

| الملف | ما يفعله الآن | كيف نستفيد منه |
|---|---|---|
| `Assets/Osama/Scripts/Progress/LevelPortal.cs` L241-276 | `GoRoutine`: حدث بدء → تجميد اللاعب → تأخير → `GameProgress.SetLastScene` → `fader.FadeOutAndLoad(sceneName)` | نقطة الانتقال الأساسية بين المراحل — تُوجَّه إلى شاشة التحميل بدل النداء المباشر |
| `Assets/Osama/Scripts/UI/ScreenFader.cs` L44 `FadeOutAndLoad(string)` | يعتّم ثم `SceneManager.LoadSceneAsync` ويهمل الـ`AsyncOperation` | يظل للتعتيم داخل المشهد، وتتولّى الشاشة التحميل نفسه |
| `Assets/_Core/Scenes/SceneLoader.cs` L46 `LoadScene(string)` | قفل `isLoading` ثابت + فحص وجود السين + تعتيم اختياري ثم تحميل غير متزامن | مسار القائمة الرئيسية؛ يوجَّه لنفس الشاشة |
| `Assets/Osama/Scripts/Progress/GameProgress.cs` L134 `SetLastScene` | يسجّل السين المغادَر ليقرأه `PlayerSpawnRouter` في الوجهة | يبقى كما هو؛ الشاشة لا تمسّ التقدّم |
| `Assets/Osama/Scripts/Progress/PlayerSpawnRouter.cs` L44-63 | ينقل اللاعب لنقطة الدخول في `Start()` بعد تفعيل المشهد | سبب مهم لإبقاء الشاشة ظاهرة **بعد** التفعيل بإطار أو إطارين حتى تختفي قفزة النقل |

أسماء المشاهد وترتيبها في `ProjectSettings/EditorBuildSettings.asset`:
`Hub-Menu`(0) • `Intro`(1) • `Steam_Final`(2) • `Hub`(3) • `AliLvl2_SultanVersion`(4) • `Main_Circus`(5).
(`Assets/Abeer/Scenes/Loading.unity` موجود لكنه **معطّل** في القائمة وشاشته أنميشن
جاهز بلا بار — لا يُستخدم ولا يُحذف.)

---

## المطلوب بناؤه

### ١. `LoadingOverlay` — الشاشة نفسها (Osama/Scripts/UI)

سكربت واحد + بريفاب كانفس، يعيش بين المشاهد:

- `DontDestroyOnLoad`، ونسخة واحدة فقط (`Instance` ساكن + حذف أي نسخة زائدة،
  ومصفّر عبر `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` مثل
  `GameProgress.ResetStatics` في L83).
- **ينشئ نفسه عند أول طلب** بنفس نمط `GameProgress.Instance`: يحمّل بريفابه من
  `Assets/Osama/Resources/LoadingOverlay.prefab` عبر `Resources.Load`. هكذا لا
  يحتاج أي مشهد أن يضيف شيئًا، ولا نعدّل مشاهد الآخرين.
- `Canvas` بوضع `Screen Space - Overlay` و`sortingOrder` عالٍ (مثلًا 5000) ليغطي
  `ScreenFader` وكل واجهات المشاهد، مع `CanvasScaler` = `Scale With Screen Size`
  بمرجع 1920×1080.
- واجهة برمجية واحدة:
  `public static void Go(string sceneName)` — تعرض الشاشة، تختار الصورة حسب الوجهة،
  تشغّل البار، تحمّل السين، ثم تخفي نفسها.
- المتغيّرات القابلة للضبط في البريفاب: `barDuration` (افتراضي 4)،
  `fadeInDuration` (0.25)، `fadeOutDuration` (0.35)، `holdAfterActivate` (0.15).

### ٢. تسلسل الانتقال المطلوب بالضبط

1. تظهر الشاشة بتلاشٍ داخل 0.25 ث (`CanvasGroup.alpha` 0→1) وتحجب الإدخال
   (`blocksRaycasts = true`).
2. تُختار صورة الوجهة وتُعرض، ويبدأ البار من الصفر.
3. `SceneManager.LoadSceneAsync(sceneName)` مع **`allowSceneActivation = false`**.
4. البار يعبّي **خطيًا على 4 ثوانٍ بزمن غير متأثر بالتوقف** (`Time.unscaledDeltaTime`،
   لأن `Time.timeScale` قد يكون صفرًا عند الانتقال من قائمة).
5. لا يُسمح بالتفعيل إلا عند تحقّق الشرطين: انتهاء الأربع ثوانٍ **و**
   `operation.progress >= 0.9f` (يونيتي يقف عند 0.9 حتى يُسمح بالتفعيل).
6. `allowSceneActivation = true`، ثم انتظار `operation.isDone`.
7. انتظار `holdAfterActivate` (≈0.15 ث) ليمرّ `Start()` في الوجهة — فيتم نقل اللاعب
   بـ`PlayerSpawnRouter` ويبدأ `ScreenFader` الخاص بها — ثم تختفي الشاشة بتلاشٍ.
8. إعادة `blocksRaycasts = false` وإخفاء الكانفس (لا تدميره).

> ملاحظة حرجة: **لا** يُنادى `SceneLoader.isLoading` ولا يُعتمد عليه، لأنه يُحرَّر عند
> `sceneLoaded` أي قبل أن تنتهي شاشتنا (`SceneLoader.cs` L32-40).

### ٣. صور الوجهات

- جدول في البريفاب: كل عنصر = اسم السين (نص) + صورة (`Sprite`) + عنوان يظهر للاعب.
- الربط المطلوب:

| اسم السين | الصورة | العنوان المقترح |
|---|---|---|
| `Hub` | صورة الجزر الثلاث | الجزيرة الرئيسية |
| `Steam_Final` | غرفة الساعة | ستيم تاون |
| `AliLvl2_SultanVersion` | شارع الفطر المضيء | التوايلايت |
| `Main_Circus` | مدخل الكرنفال ليلًا | السيرك |
| غير ذلك | صورة افتراضية | — |

- **الصور جاهزة فعلًا** في `Assets/Osama/UI/Loading/`: `Loading_Hub.png` و
  `Loading_Steam.png` و`Loading_Twilight.png` و`Loading_Circus.png` — مقصوصة
  مركزيًا إلى 16:9 ومصدَّرة 1920×1080، وملفات الاستيراد مكتوبة
  (`Texture Type = Sprite (2D and UI)`، بلا Mip Maps، `Max Size = 2048`).
- العرض بـ`Image` مع `Preserve Aspect`، وخلفية سوداء خلفها تملأ ما زاد عن الشاشة.

### ٤. البار وشخصية علي

- البار: صورتان متراكبتان (مسار + تعبئة) بـ`Image.type = Filled`، `Fill Method = Horizontal`،
  و`fillAmount` = نسبة التقدّم.
- **علي يركض فوقه**: `Image` بسبرايت متحرك، موضعه الأفقي يتبع نسبة التقدّم بالضبط
  (`anchoredPosition.x = fill * barWidth`)، فيبدأ من أول البار وينتهي عند آخره.
- الأنميشن: تبديل الصور المتتابعة بمعدّل ثابت (**12 إطار/ث** = دورة كاملة كل ⅔ ثانية،
  أي ست دورات ركض خلال الأربع ثوانٍ) عبر عدّاد في `Update` بـ`Time.unscaledDeltaTime`،
  لا `Animator` — أبسط وأخف ولا يحتاج Controller. تسلسل حلقي: 1→8 ثم يعود للأول.
- ✅ **إطارات علي جاهزة**: `Assets/Osama/UI/Loading/Run_01.png` … `Run_08.png`
  (+ ملفات `.meta`)، مقسومة من شيت الركض الذي وفّره أسامة:
  - ثمانية إطارات، كل واحد **171×256 بكسل**، خلفية شفافة تمامًا (لا شطرنج ولا إطار).
  - كلها على **لوحة واحدة بنفس المقاس**، والشخصية **محاذاة على خط أرض واحد**
    ومتوسّطة أفقيًا — فلا ترجف الأقدام أثناء التشغيل.
  - مستوردة `Sprite (2D and UI)`، `Pixels Per Unit = 100`، بلا Mip Maps.
  - في `Assets/Osama/UI/` لا في `Resources`، فتُربط في البريفاب مباشرة (مصفوفة
    `Sprite[] runFrames` بالترتيب 01→08). البريفاب داخل `Resources` يحملها معه للبلد.
- ارتفاع علي على الشاشة ≈ 90–110 بكسل (يعني `RectTransform` بنسبة عرض/ارتفاع 171:256)،
  وقاعدته تلامس أعلى البار تمامًا.
- سبرايتات عبير (`Assets/Abeer/Pic/Frame1-Child*.png`) **لم تعد مطلوبة** — بديل قديم فقط.
- نص تحت البار: عنوان الوجهة + نسبة مئوية اختيارية.

### ٥. توجيه مسارات الانتقال إلى الشاشة

- `LevelPortal.GoRoutine` (L274-275): بدل `fader.FadeOutAndLoad(sceneName)` →
  `LoadingOverlay.Go(sceneName)`. يبقى التعتيم المحلي إن وُجد **قبل** ظهور الشاشة
  (خيار `useLocalFadeFirst`) أو يُستغنى عنه، والافتراضي: الشاشة وحدها.
- `SceneLoader.LoadScene` (L91): نفس التوجيه، مع إبقاء كل الفحوص والقفل كما هي.
- `ScreenFader.FadeOutAndLoad` (L66): يبقى كما هو لمن يستدعيه مباشرة، ويُفضَّل
  توجيهه للشاشة أيضًا لتوحيد السلوك.
- **لا** تُعدَّل نداءات التحميل المتزامنة في ملفات الآخرين.

### ٦. حالات يجب ألا تنكسر

- الانتقال من القائمة الرئيسية (`Hub-Menu` → `Intro`) و`Time.timeScale = 0`.
- انتقال فشل لأن السين غير مضاف لقائمة البناء: تُخفى الشاشة ويُطبع الخطأ الحالي
  نفسه، ولا يتجمّد اللاعب (راجع `LevelPortal` L259-266 و`SceneLoader` L56-62).
- انتقالان متتاليان بسرعة: نداء ثانٍ أثناء العمل يُتجاهَل.
- البناء النهائي: البريفاب وصوره داخل `Resources` أو مرجعية منه، فلا يسقط شيء من
  البلد (نفس درس `Assets/Osama/Resources/OsamaGroundMarker.mat`).

---

## الملفات التي ستُنشأ أو تُعدَّل

**جديد:**
- `Assets/Osama/Scripts/UI/LoadingOverlay.cs`
- `Assets/Osama/Resources/LoadingOverlay.prefab`
- ✅ `Assets/Osama/UI/Loading/Loading_Hub|Steam|Twilight|Circus.png` (+ `.meta`) — **جاهزة**
- ✅ `Assets/Osama/UI/Loading/Run_01.png` … `Run_08.png` (+ `.meta`) — **جاهزة**،
  إطارات ركض علي 171×256 بخلفية شفافة ومحاذاة على خط أرض واحد

**موجود يُعاد استخدامه بلا تعديل:**
- الخطوط: `LiberationSans SDF` وحده داخل `Resources`. خطوط اللعبة المميّزة
  (`Assets/Abeer/Pic/DK Crayon Crumble SDF`، `Assets/Abeer/Carnevalee Freakshow SDF`)
  خارج `Resources`، فتُربط في البريفاب مباشرة لا عبر `Resources.Load`
- أفضل قالب كانفس: `Loading-UI` داخل `Assets/Abeer/Scenes/Loading.unity`
  (1920×1080، Scale With Screen Size) — باقي قوالب الكانفس في المشروع 800×600 ثابتة

**تعديل (كلها في مجلد أسامة و`_Core` المسموح):**
- `Assets/Osama/Scripts/Progress/LevelPortal.cs` — سطر التحميل فقط
- `Assets/_Core/Scenes/SceneLoader.cs` — سطر التحميل فقط
- `Assets/Osama/Scripts/UI/ScreenFader.cs` — توجيه `FadeOutAndLoad` (اختياري)
- `Assets/Osama/Scripts/README.md` — توثيق الشاشة وأعمدتها

---

## التحقق

1. **الترجمة قبل فتح يونيتي**: ترجمة `Assembly-CSharp` كاملة بمترجم يونيتي
   (Roslyn + `Library/ScriptAssemblies`) والتأكد من صفر أخطاء.
2. **من القائمة**: Play من `Hub-Menu` → زر اللعب → تظهر الشاشة بصورة الانترو/الهب،
   البار يعبّي أربع ثوانٍ وعلي يركض فوقه، ثم يدخل المشهد بلا وميض أسود مزدوج.
3. **من الهب إلى مرحلة**: ادخل بوابة ستيم → صورة ستيم تاون، وبعد الدخول لا تظهر قفزة
   نقل اللاعب (يغطّيها `holdAfterActivate`).
4. **الرجوع للهب**: من مخرج المرحلة → صورة الهب.
5. **حالة الخطأ**: غيّر اسم سين في بوابة إلى اسم غير موجود → الشاشة تختفي، ويظهر
   الخطأ في الكونسول، واللاعب يتحرك طبيعيًا.
6. **بلد حقيقي**: بناء ويندوز، والتأكد أن الصور والسبرايتات تظهر (لا تعتمد على أصول
   خارج `Resources` أو غير مرجعية من البريفاب).
