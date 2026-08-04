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

## سكربتات قديمة يُستغنى عنها

`Main_Menu_AB` · `MENU` · `Load_Scene_Script` · `MainMenuController` · `Pause_Menu` · `Slider_Script` · `HoverScript`

لا تحذفها فورًا — مشاهد بقية الفريق مرتبطة بها. انقل كل مشهد للنظام الجديد ثم احذفها في النهاية.
