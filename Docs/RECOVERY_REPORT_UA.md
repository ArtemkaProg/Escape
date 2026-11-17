# Звіт з відновлення Escape (2015) з APK — факти / обмеження

Дата перевірки: 17 листопада 2026 року. Дані з конкретного завантаженого APK.

## Джерело (перевірено з файлу)
- Назва: `Escape_1.0_APKPure.apk`
- SHA-256: `16bca583c219aa461aa5fa5b91b0ee84fe2a97712c96d51bce104f343a49255f`
- Обсяг: 45 173 365 байтів.
- Unity `4.6.5f1`: вбудована назва версії у `mainData`, `level0`, `sharedassets0.assets`, `sharedassets1.assets`.
- Знайдено `Managed/Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`, `libmono.so` ARMv7/x86 — Mono/C# підтверджено.
- У `level0` — 102 870 записів Unity; в усіх 4 Unity-файлах разом — 104 623 записи.
- У `level0`: 34 993 `GameObject`, 34 651 `Transform`, 342 `RectTransform`, 12 318 `MonoBehaviour`.
- 711 описів `MonoScript` у `sharedassets0.assets`.

## Автоматично витягнуто та перевірено
- **51 PNG**: текстури зі збережених `Texture2D` (формати RGBA32, ARGB32, RGB24, RGBA4444, Alpha8).
- **12 KTX**: оригінальні ETC2_RGBA8 стиснені текстури; **не декодовано в PNG**, але їхні блоки збережено.
- **2 Texture2D порожні** (0×0).
- **12 аудіофайлів**: 9 WAV, 3 MP3; усі 12 розпізнано через `ffprobe`.
- **Код**: 13 оригінальних DLL із APK скопійовано без змін. 6 сторонніх та ігрових збірок оброблено Python IL-дизасемблером.
- `2DColliderGen_Runtime.dll`: 11 типів, 78 методів, 74 з IL-тілами.
- `Assembly-CSharp-firstpass.dll`: 39 типів, 372 методи, 372 з IL-тілами.
- `Assembly-CSharp.dll`: 934 типи, 5 236 методів, 4 524 з IL-тілами.
- `CodingJar.AdditiveScenes.dll`: 5 типів, 18 методів, 18 з IL-тілами.
- `DarkArtsStudios.ScreenshotCreator.Runtime.dll`: 3 типи, 8 методів, 8 з IL-тілами.
- `P31RestKit.dll`: 35 типів, 164 методи, 146 з IL-тілами.
- У `Assembly-CSharp.dll`: 1 322 методи з простору назв `RocketChips`.
- **Ієрархія**: з 34 993 GameObject витягнуто імена, активність, layer, tag та списки компонентів; з 34 993 Transform/RectTransform — локальні position/rotation/scale та parent ID.
- **Сирі об'єкти**: AnimationClip: 152, AnimatorController: 108, Avatar: 10, Font: 3, Material: 33, Mesh: 514, Shader: 13, Sprite: 89.
- **Індекс** у локальному `Reports/unity_object_index.csv`: об'єкт, тип, Unity-файл, offset і розмір у байтах.

## Що відновлено частково
- Ієрархія доступна локально у `Assets/StreamingAssets/escape_hierarchy.json` й відтворюється імпортером Editor у Unity 6.
- У raw Unity assets є Mesh, Sprite, Material, Animator, Collider, Script-компоненти, але **не всі вони перетворені в редаговані Unity assets**.
- Код у локальному `RecoverySource/DisassembledIL/` — **MSIL-дизасембльована логіка**, не вихідні `.cs`.
- У APK немає оригінального редакторського проєкту; його метадані, `.prefab`, `.meta` та частина редакторських налаштувань не повертаються автоматично.

## Статус ПК-порту
- **Unity-проєкт-основа:** створено під Unity 6 (`Assets`, `Packages`, `ProjectSettings`) з ресурсами, звітами та редакторським скриптом.
- **Windows EXE:** **не зібрано**; Unity Editor/Windows Build Support у робочому середовищі не встановлено. Є пункт меню для збірки діагностичного EXE, а не оригінальної гри.
- **Грається як оригінал:** **ні**. Потрібно адаптувати C# API, відновити рендеринг, матеріали, сцени, Animator, колайдери, script/object зв'язки та Android-ввід.
- Сцена `level0` збережена в оригінальному форматі Unity 4.6.5f1 для подальшого експорту.

## Припущення / ризики
- Android-залежні класи (Google Play Games, Ads, touch) **можуть** вимагати заміни; точну поведінку порту не тестували.
- Структурний імпортер не гарантує коректного вигляду сцени, бо не імпортує оригінальні рендерери та матеріали.
- Конвертація RGBA4444 ґрунтується на Unity TextureFormat; деякі PNG перевірено візуально, але не всі спрайти валідовано.

## Як користуватися
Прочитайте `Docs/BUILD_WINDOWS_UA.md`. Локальні скрипти розбору — у `RecoverySource/Tools/`.


## Другий етап: аналіз SpriteRenderer і підготовка часткової графіки (17.11.2026)

- Зі серіалізованих даних `level0` вилучено 5 722 записи `SpriteRenderer`; 5 718 містять валідні посилання на оригінальні `Sprite` у `sharedassets1.assets`, 4 — порожні.
- Визначено 89 унікальних `Sprite`, їхні `m_Texture`, `m_Rect`, `textureRect`, `pixelsPerUnit`; а також GO ID, стан `enabled`, колір і `sortingOrder` рендерерів.
- 31 Sprite має вже декодовані PNG-джерела — їх використовують 2 539 `SpriteRenderer`.
- 58 Sprite посилаються на 12 стиснених текстур ETC2 — їх використовують 3 179 `SpriteRenderer`.
- Створено **самостійно написаний** редакторський інструмент `Assets/Editor/EscapeSpritePass.cs` і локальний екстрактор `tools/extract_sprite_manifest.py`.
- Локально сформовано `Assets/StreamingAssets/escape_sprite_manifest.json`, перевірено посилання на всі 34 993 GameObject, відсутність невалідних Sprite ID та межі crop для доступних PNG.
- Інструмент зіставляє Unity-сцену зі структурною ієрархією за кореневими іменами й порядком дочірніх об'єктів; при невідповідності відмовляється від імпорту.
- Результат імпорту редактор має зберегти **окремою** сценою `Assets/Scenes/Escape_Sprites_PASS1.unity`; вихідна структура залишається.
- **Не перевірено в редакторі:** чи C# імпортер компілюється і чи коректно видно всі спрайти в Unity 6.0.58f2. Результати аналізу APK ≠ завершений візуальний порт.
- **Залишається:** декодувати ETC2, відновити Mesh/Material, повноцінний SortingLayer, оригінальні камери, Sprite pivot/geometry для частини атласів, Animator, MonoBehaviour та управління.
