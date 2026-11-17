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

## Етап 5: ETC2 — перевірені вхідні дані та інструменти

**Перевірено безпосередньо на витягнутих із наданого APK файлах:**

- Усі 12 `RecoverySource/RawTextures/*.ktx` мають правильний заголовок KTX1, `GL_COMPRESSED_RGBA8_ETC2_EAC = 0x9278` та правильні розміри блоків (16 байтів на 4×4 пікселі).
- Усі 12 Texture ID, на які посилаються 58 відсутніх спрайтів, присутні; нестачі KTX-даних немає.
- Готовий ZIP `Escape_2015_Stage5_ETC2_Local_Data.zip` з 12 KTX-файлами (тільки локально, **не в публічному GitHub**), архів перевірено на цілісність.
- Написано `tools/decode_etc2_stage5.py`: перевірка KTX, конвертація в PNG, оновлення локального `escape_sprite_manifest.json` після успішної конвертації; спочатку спроба швидкого `texture2ddecoder`, потім запасний `ETCImagePlugin`.
- Додано меню Unity: **Escape Recovery → 4 - Restore ETC2 sprites (visual pass 2)**. Воно зберігає окрему сцену `Escape_Sprites_PASS2.unity` без перезапису PASS1.
- Код стороннього декодера `ETCImagePlugin` збережено зі згадкою оригінального автора у `tools/vendor/THIRD_PARTY_NOTICE.txt`.

**Ще НЕ підтверджено:** фактичну конвертацію всіх 12 файлів на Python-середовищі користувача, коректну орієнтацію отриманих PNG, успішну компіляцію нового меню Unity та відновлення всіх 5 718 SpriteRenderer у сцені. Результат — прогноз, доки не перевіримо Console Unity.

Джерела для інструментів:
- https://github.com/K0lb3/texture2ddecoder — нативний декодер, Python API `decode_etc2a8`.
- https://github.com/FluffyJay1/ETCImagePlugin — Python-декодер на базі Pillow (сторонній код).

## Етап 6 — Camera + Sorting Layer із серіалізованого APK

**Підтверджено:** у Unity 4.6.5f1 level0 розташовано 8 Camera-компонентів, кожен має 104 байти в серіалізованій структурі. Ідентифікатори камери та оригінального GameObject зіставлено з JSON ієрархії. З бінарних полів прочитано enabled, clearFlags, backgroundColor, normalizedViewport, near/far, fieldOfView, orthographic, orthographicSize, depth, cullingMask і renderingPath.

Список Camera (по об'єктах): 03_Camera_LastBackground, 02_Camera_HUD (AdventureEndScreen), CameraBackground (TutoControlScreen), 04_Camera_Flash, CameraForeground (TutoControlScreen), 01_Camera_ZoneDefault, 02_Camera_HUD (GameHUD), 02_Camera_BackgroundPerspective.

- Основна 01_Camera_ZoneDefault: ортографічна; orthographicSize=4.05; depth=0; маска cullingMask=0xFFF9FFDF; transform під ExternGameElements/GameCamera.
- GameHUD/02_Camera_HUD: ортографічна; orthographicSize=5.4; depth=100; cullingMask=0x20.
- Усі 8 камер є окремими, кожен GameObject існує в 34 993 елементах ієрархії.
- SpriteRenderer мають **п'ять унікальних числових Sorting Layer ID** з такою кількістю посилань: ID 0 — 3 560; ID -1349028731 — 1 622; ID -711865379 — 32; ID 484840917 — 3; ID 1936908687 — 505.
- *Невідомо:* назви чотирьох нестандартних шарів та порядок між ними. Вони мають тимчасові RecoveredLayer_XX, тому це **не точне відтворення порядку малювання**.
- Додано `Assets/Editor/EscapeCameraPass.cs`: відновлення 8 Camera-компонентів і числових Sorting Layer ID в окрему `Escape_Cameras_PASS3.unity`; створення окремої Zone001_PREVIEW-сцени з вимкненими сторонніми зонами.
- Підготовлено локальний архів `Escape_2015_Stage6_Cameras_Sorting_Metadata.zip` та повторюваний скрипт `tools/extract_camera_manifest.py`. Архів камер не містить оригінальних текстур або DLL і не публікувався в репозиторій.
- *Не перевірено:* компіляція C# Stage 6 у Unity 6.0.58f2 та достовірність фінального вигляду Game View. Їх потрібно перевірити на комп'ютері користувача.

## Етап 7 — оригінальні Sprite pivots і полігональна геометрія (17.11.2026)

На основі **безпосереднього повторного читання `sharedassets1.assets` з APK**:

- Оброблено **89 Sprite**: загалом **702 вершини** і **1 596 індексів трикутників**. Перевірено довжини буферів, межі індексів та UV-координати у межах вирізаних PNG.
- **9 Sprite** містять складну полігональну геометрію; разом їхні SpriteRenderer використані **59 разів** у сцені. Включно з `RocketComplete` (90 вершин), `StartControlTower` (136), `BackgroundCityDestroyed02` (44), `StartLaunchSupportBase` (53).
- **4 Sprite з нестандартним pivot**: `gyroLight` = (0,1), `SurvivorHands` = (0.5,0.1), `SurvivorBody` = (0.5,0), `Laser` = (0.5,1). Разом **1 816 SpriteRenderer** посилаються на них.
- У попередньому `EscapeSpritePass.cs` для кожного імпортованого Sprite використовувався центр за замовчуванням, а 9 полігонів відображалися прямокутними спрайтами. Це **підтверджений недолік** попереднього імпортера, але він **не пояснює автоматично усі** кольорові смуги та артефакти.
- Відновлені `vertices`, `triangles`, `UV` і pivot із бінарних Sprite записані до **локального** `escape_sprite_geometry.json`, перевірено структурну цілісність 89 записів. Створено архів `Escape_2015_Stage7_Sprite_Geometry.zip` (не публікується на GitHub).
- Додано **`Assets/Editor/EscapeSpriteGeometryPass.cs`**: копіює 89 вихідних PNG у окремий локальний каталог; встановлює рідні pivot, малює полігони через MeshFilter/MeshRenderer із `Sprites/Default`, зберігає окрему сцену `Escape_Zone001_GRAPHICS_PASS4.unity`. Вихідні ресурси та попередні сцени не перезаписує.
- Додано **`tools/extract_sprite_geometry.py`**, щоб локально повторити витягування метаданих з APK.
- **Обмеження:** без Unity Editor у середовищі аналізу компіляцію та рендер PASS4 ще не перевірено. Сприймай корекції як перевірену **структуру даних**, а ефективність відображення як гіпотезу до перегляду в Unity.


## Етап 8 — MeshDecoder + спрощені матеріали (17.11.2026)

Відтворено 514/514 оригінальних Mesh без помилок серіалізації: **260** Mesh з `level0`, **254** з `sharedassets1.assets`. Декодовано **29 785 вершин**, **53 760 індексів (17 920 трикутників)**, нормалі, UV0, RGBA32 для процедурних мешів; загальний обсяг власного формату `ESCMESH1` **1 194 188 байтів**. 512 Mesh мають один submesh, два — по два. Усі 514 приватних вихідних файлів повторно перевірено на відповідність заголовків, кількостей, індексів та довжини.

В оригінальному `level0` є **3 781 MeshFilter**, **3 810 MeshRenderer**; їхні GameObject ID, m_Mesh (fileID/pathID), матеріальні посилання (fileID/pathID) та enabled експортовано в локальний JSON. Із MeshFilter **5 мають порожні (0,0) посилання**, інші 3 776 успішно співвіднесені з оригінальними Mesh. 33 Material розпізнані з файлами, назвами, shader pointer та доступним базовим кольором. Найчастіша пара посилань на матеріал — `(1,7) Black.mat`, її використовують 3 423 Renderer-слоти.

Приватний ZIP `Escape_2015_Stage8_Meshes_Local.zip` містить 514 компактних `RecoverySource/MeshData/*.meshbin` та `Assets/StreamingAssets/escape_mesh_manifest.json`. Додано самостійно написаний `EscapeMeshPass.cs`: створення Mesh .asset, базових Material і MeshFilter/MeshRenderer на дочірніх об'єктах у новій сцені `Escape_Meshes_PASS5.unity`; діагностична Zone003-прев'ю в окремій `Escape_Zone003_MESH_PREVIEW.unity`. Порівняно з Zone001, у Zone003 значно більше MeshFilter (~1 276), тому ця зона вибрана для демонстрації.

**Не відновлено:** оригінальні shader graph, property textures/offsets, RenderQueue, кастомний blending, MeshCollider, Animator/SkinnedMeshRenderer і скрипти зміни зон. Поточні матеріали свідомо приблизні. **Unity Editor для нового коду не запускався**; C# компіляцію та вигляд PASS5 потрібно перевірити на ПК.


## Етап 9 — Material _MainTex (18.11.2026, локальний інструмент)

Повторно декодовано серіалізовані Unity 4.6.5f1 Material із sharedassets0.assets і sharedassets1.assets: **33 Material**. Із них 23 мають джерела Texture2D, декодовані як PNG (21 унікальний PNG-шлях), 2 посилаються на порожні текстури шрифтів, 8 не мають _MainTex. Оригінальні shader fileID/pathID, _MainTex fileID/pathID, _Color, _Cutoff, UV scale/offset зчитано з файлів. Посилання на текстури зіставлено з локальним індексом Reports/textures.csv. Підготовлено приватний архів Escape_2015_Stage9_Materials_Metadata.zip; перевірено його цілісність і 33 унікальні матеріальні ID.

Додано самостійно написаний Unity Editor скрипт Assets/Editor/EscapeMaterialPass.cs. Він не змінює оригінальні матеріали/сцени, а створює копії тільки матеріалів із локальним PNG та застосовує їхні _MainTex, _Color і UV scale/offset до відповідних Stage8 MeshRenderer у новій Escape_Zone003_MATERIALS_PASS6.unity.

**Не підтверджено:** виконання Stage 9 в Unity Editor користувача, правильність підсумкового шейдерного рендерингу та зміна Game View. Шейдери Unity 4, pixel-snap, RenderQueue, Cutoff/blend-режими та MaterialPropertyBlock не відновлюються. Попередньо — це часткова графічна реконструкція, не playable-порт.

## Етап 10 — перевірено оригінальні режими unlit Shader

Повторно розібрано `sharedassets1.assets` і `sharedassets0.assets` оригінального Unity 4.6.5f1: **13 Shader-об'єктів** загалом. Серед них важливі Shader ID 353/354 (непрозорий суцільний колір), 356 (непрозорий колір вершини), 357 (суцільний двосторонній колір), 358 (напівпрозорий колір вершини), 359 (напівпрозора інвертована RGB-текстура з множенням на колір вершини). Shader 359 використовується **14 Material** із доступними _MainTex, для яких у вихідних MeshRenderer є **51** матеріальний слот.

Виявлено **помилку попереднього Stage9-екстрактора:** текстовий пошук `_Color` знаходив `_ColorMask` і пропускав власне колір у `Black.mat`. Реальний `_Color` цього матеріалу збережений як **(0,0,0,1)**. У Stage10-екстракторі пошук здійснюється за **повною довжиною й назвою** властивості; всі 33 Material перевірені, 14 shader 359 виявлено.

Виведено локальний маніфест `escape_shader_manifest.json` (33 Material, оригінальні ID й кольори). На публічний GitHub додано лише **незалежно написані** ShaderLab-наближення для Unity 6 та редакторський інструмент `Assets/Editor/EscapeShaderPass.cs`. Він створює окремі матеріали й `Escape_Zone003_SHADERS_PASS7.unity`; попередні сцени/матеріали не модифікує. Оригінальний ShaderLab-код і текстури не оприлюднюються.

**Не перевірено:** компіляцію ShaderLab у Unity 6.0.58f2 та візуальний збіг у Game View. Очікується, що інвертований Shader ID 359 краще відповідатиме оригінальному способу зображення темних силуетів. Це гіпотеза про результат, а не підтверджене відновлення.
