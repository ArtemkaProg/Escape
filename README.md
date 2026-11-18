# Escape (2015) — відновлення гри з APK

> **PASS13, 18.11.2026:** виправлено хибні контакти біля помп: відновлено 12 прив’язок колайдерів і оригінальні індивідуальні фази/швидкості чотирьох помп. Повернуто безпечні контакти з людьми. [Звіт PASS13](Docs/PASS13_PUMP_CONTACTS_UA.md).


> **PASS12, 18.11.2026:** додано перевірений Windows-прототип окремої Zone003: оригінальний маршрут і темп, 3D-колайдери, смерть, повторний запуск, пауза та завершення зони. [Звіт PASS12](Docs/PASS12_PC_PLAYABLE_UA.md) · [Стан повного відновлення](Docs/RESTORATION_ROADMAP_UA.md). Повний порт гри ще не завершений.


> **PASS11, 18.11.2026:** позначення атласу ракети як ETC2 у попередніх етапах було помилковим. Оригінальний формат 54 — ASTC RGBA 4×4. Графіку ракети виправлено в окремій сцені; параметри руху прочитані з level0, Windows-прототип перевірено. Див. [звіт PASS11](Docs/PASS11_ROCKET_RECOVERY_UA.md).


**Статус:** Unity 6 проєкт-основа для дослідження; це **не готовий порт гри для Windows** і не офіційні вихідні файли розробників.

Цей публічний репозиторій містить **власні інструменти реконструкції, Unity-скрипти та звіти**. Первинна гра Escape (2015) та її ресурси належать відповідним правовласникам; оригінальні APK, DLL, текстури, звуки, IL-дизасембльований код та інші витягнуті бінарні дані сюди **не публікуються**.

## Факти, перевірені в APK

- Версія рушія: **Unity 4.6.5f1**, Mono/C#; наявна `Assembly-CSharp.dll`.
- У `level0`: **34 993 GameObject**, **34 651 Transform**, **342 RectTransform**, **12 318 MonoBehaviour**.
- Відновлений локальний архів містить **51 PNG**, **12 аудіофайлів**, **12 текстур KTX**, 13 оригінальних керованих DLL, сирі бінарні об'єкти, об'єктні індекси й структурні JSON.
- Відновлений **IL** — це ще **не C# вихідні файли**.

Деталі: [Docs/RECOVERY_REPORT_UA.md](Docs/RECOVERY_REPORT_UA.md).

## Підготовка локального Unity-проєкту

1. Клонуй репозиторій: `git clone https://github.com/ArtemkaProg/Escape.git`.
2. Завантаж локально архів `Escape_2015_Unity6_Recovery_Project_FULL.zip`, створений під час аналізу APK та переданий у чаті.
3. У корені репозиторію виконай:

   ```powershell
   py -3 tools/restore_local_archive.py "C:\path\to\Escape_2015_Unity6_Recovery_Project_FULL.zip"
   ```

   На Linux/macOS — `python3`. Інструмент перевіряє шляхи ZIP і витягує дані з `RecoveredEscapeUnity/`. За замовчуванням наявні файли не перезаписує.

4. Відкрий корінь репозиторію у Unity Hub (Unity 6), установи Windows Build Support.
5. У меню Unity натисни **Escape Recovery → 1 - Rebuild original hierarchy (35k objects)**. Це відтворить імена, трансформації та батьківські зв'язки, але **не матеріали, меші, логіку, колайдери та фізику**.
6. **Escape Recovery → 2 - Build Windows x64 scaffold** створює лише діагностичний EXE, **не оригінальну гру**.

Детальніше: [Docs/BUILD_WINDOWS_UA.md](Docs/BUILD_WINDOWS_UA.md).

## Структура

- `Assets/Editor/EscapeSceneRebuilder.cs` — імпортер ієрархії для Unity 6.
- `Assets/Scripts/EscapeRecoveryNotice.cs` — позначення діагностичного стенда.
- `Packages/`, `ProjectSettings/` — базова конфігурація Unity.
- `tools/restore_local_archive.py` — локальна інсталяція даних із підготовленого ZIP.
- `Docs/` — перевірені результати, обмеження та план наступних робіт.


## Етап 2 — перша графіка (SpriteRenderer)

**Підтверджено аналізом APK:** у `level0` є **5 722 SpriteRenderer**, **5 718** з них мають посилання на один із **89** спрайтів. Доступні PNG покривають **2 539** SpriteRenderer, інші **3 179** використовують ETC2, ще 4 не мають спрайта.

**Важливо:** ці цифри означають, що посилання можна прочитати, **а не те, що всі вони вже відображаються в Unity**. Крок нижче створює першу неповну графічну версію.

1. Перевір, що локальний повний архів відновлення вже імпортований у корінь проєкту: є `RecoverySource/OriginalUnityData/level0`, `Reports/textures.csv` і `Assets/RecoveredTextures/`.
2. Створи локальну мапу спрайтів:

   ```powershell
   py -3 tools/extract_sprite_manifest.py
   ```

   Або розпакуй виданий у чаті `Escape_2015_Stage2_Sprite_Metadata.zip` у корінь Unity-проєкту. У ньому вже є `Assets/StreamingAssets/escape_sprite_manifest.json`.
3. Відкрий `Assets/Scenes/Escape_Structure_RECONSTRUCTED.unity`.
4. Меню Unity: **Escape Recovery → 3 - Restore 2D SpriteRenderers (visual pass 1)**.
5. Зачекай закінчення імпорту. Unity створить локальні PNG у `Assets/RecoveredSprites/` і окрему сцену `Assets/Scenes/Escape_Sprites_PASS1.unity`.

Обмеження: **ETC2 (3 179 рендерерів), MeshRenderer, MeshFilter, Animator, камери, sorting layer і логіка гри** на цьому кроці не відновлюються. Первинна сцена **не перезаписується**. Кольори та `sortingOrder` із SpriteRenderer зчитані з APK, але користувацькі назви шарів сортування ще не встановлені. Код інструмента не компілювався у Unity Editor у середовищі аналізу — перевір помилки Console. Якщо зіставлення ієрархії не збігається, імпортер зупиниться до зміни сцени.

## Етап 5 — ETC2 для Windows: ще 58 спрайтів

**Мета:** декодувати 12 оригінальних ETC2_RGBA8 KTX-текстур у локальні PNG та додати їх до вже створеної сцени `Escape_Sprites_PASS1`. Інструменти: `tools/decode_etc2_stage5.py` і `Escape Recovery → 4 - Restore ETC2 sprites (visual pass 2)`.

1. **Збережи** створені тобою сцени, закрий Unity, онови репозиторій: `git pull origin main`.
2. Переконайся, що 12 KTX є в одному з місць:
   - `RecoverySource/RawTextures/*.ktx`
   - `RecoveredEscapeUnity/RecoverySource/RawTextures/*.ktx` (якщо папка залишилася всередині проєкту).
   
   Якщо їх немає, розпакуй **Escape_2015_Stage5_ETC2_Local_Data.zip**, виданий у чаті, **прямо в корінь** репозиторію `Escape/`; у ньому вже правильна структура папок. Не додавай KTX у публічний GitHub.
3. У терміналі Git Bash / PowerShell, у папці `Escape`, виконай:

   ```bash
   py -3 -m pip install Pillow texture2ddecoder
   py -3 tools/decode_etc2_stage5.py --dry-run
   py -3 tools/decode_etc2_stage5.py
   ```

   **Один раз** інсталюй ці Python-пакети. Якщо `texture2ddecoder` недоступний, достатньо `Pillow`: скрипт має повільніший запасний Python-декодер (`tools/vendor/ETCImagePlugin.py`, із зазначенням авторства). Стиснені текстури залишаються локальними.
4. Після успішної конвертації з'являться 12 додаткових PNG у `Assets/RecoveredTextures/`; локальний `Assets/StreamingAssets/escape_sprite_manifest.json` буде оновлено.
5. Відкрий Unity та **`Assets/Scenes/Escape_Sprites_PASS1.unity`**. Обери **`Escape Recovery → 4 - Restore ETC2 sprites (visual pass 2)`**.
6. Unity має зберегти результат у новій **`Assets/Scenes/Escape_Sprites_PASS2.unity`**, не перезаписуючи сцени `PASS1` чи `STRUCTURE`. Перевір Console, якщо є помилки.

**Точність і обмеження:** підтверджено цілісність усіх 12 KTX та відповідність усім 58 Sprite ID; саму конвертацію й Stage 5 редакторський скрипт у Unity на цьому комп'ютері ще не виконували. Кількість відновлених SpriteRenderer — очікувана, а не підтверджена успішною Unity-збіркою. Повне відтворення сцени потребує відновлення мешів, камер, шейдерів, пріоритетів шарів, анімацій та логіки.

## Етап 6 — оригінальні камери й числові Sorting Layer ID (PASS3)

**Перевірено за APK:** у level0 є 8 камер (із налаштуваннями проєкції, розміру, depth, cullingMask, фону, viewport); у SpriteRenderer є 5 числових Sorting Layer ID. **Назви та порядок нестандартних Sorting Layers НЕ відомі**, тому використовуються тимчасові RecoveredLayer_XX.

**Як виконати на ПК (Unity 6.0.58f2):**

1. **Збережи проєкт**, закрий редактор і виконай у Git Bash `git pull origin main`.
2. Завантаж із чату **Escape_2015_Stage6_Cameras_Sorting_Metadata.zip** та розпакуй у корінь `D:\Unity Projects\Escape`, об'єднавши папку Assets. Переконайся, що з'явився `Assets/StreamingAssets/escape_camera_manifest.json`.
   - **Альтернатива без ZIP:** за наявності локальних `RecoverySource/OriginalUnityData/level0` і `RecoverySource/Tools/unity4_reader.py` (або відповідних файлів усередині RecoveredEscapeUnity) виконай `py -3 tools/extract_camera_manifest.py`.
3. Відкрий `Assets/Scenes/Escape_Sprites_PASS2.unity`, а не STRUCTURE чи PASS1.
4. Вибери **Escape Recovery → 5 - Restore original cameras and sorting IDs (pass 3)**. Скрипт збереже окрему сцену `Assets/Scenes/Escape_Cameras_PASS3.unity`. Якщо Unity повідомить про нерозпізнані Sorting Layer ID, перезапусти редактор та повтори команду з PASS2.
5. Із відкритою `Escape_Cameras_PASS3.unity` виконай **Escape Recovery → 6 - Preview Zone001 only (diagnostic)**. Це створить `Assets/Scenes/Escape_Zone001_PREVIEW.unity`, де тимчасово приховані інші зони.
6. Подивися **Game View (без Play Mode)** та Console. Покажи скриншот для перевірки.

**Захист попередніх результатів:** PASS2 і PASS3 не перезаписуються цими редакторськими командами. Перед зміною ProjectSettings/TagManager.asset зберігається копія в UserSettings/TagManager_before_Escape_PASS3.asset. Нові локальні файли проігноровані Git.

**Що ще не зроблено:** не відновлено оригінальний порядок/імена шарів, MeshRenderer/MeshFilter і матеріали, Sprite pivot/geometry, Animator, MonoBehaviour, логіку зміни зон та поведінку камер. Код PASS3 ще не перевірявся запуском Unity Editor в середовищі аналізу.

## Етап 7 — оригінальні pivots і полігональна форма спрайтів (PASS4)

**Результат повторного читання APK:** у `sharedassets1.assets` є 89 Sprite з **702 оригінальними вершинами**. Дев'ять Sprite використовують полігональну геометрію замість простого прямокутника; їх застосовано 59 разів у сцені. Чотири Sprite мають нестандартні pivot: `gyroLight` (34 використання), `SurvivorHands` (631), `SurvivorBody` (631), `Laser` (520). Разом **1 816 використань** з неправильним pivot у попередньому імпортері.

1. Збережи Unity-проєкт, закрий редактор і в корені Escape виконай `git pull origin main`.
2. Розпакуй локальний архів **Escape_2015_Stage7_Sprite_Geometry.zip** із чату **в корінь** `D:\Unity Projects\Escape`, щоб з'явився `Assets/StreamingAssets/escape_sprite_geometry.json`. Усі попередні PNG `Assets/RecoveredSprites/escape_sprite_*.png` мають залишитися.
   - Якщо ZIP недоступний, відтворення метаданих доступне з локальних оригінальних Unity-файлів: `py -3 tools/extract_sprite_geometry.py`.
3. Відкрий Unity 6.0.58f2 і сцену **`Assets/Scenes/Escape_Zone001_PREVIEW.unity`** (або `Escape_Cameras_PASS3.unity` для всіх зон).
4. Обери **Escape Recovery → 7 - Fix sprite pivots and polygons (pass 4)**.
5. Сцена буде збережена під новим ім'ям **`Assets/Scenes/Escape_Zone001_GRAPHICS_PASS4.unity`** (або `Escape_Cameras_GRAPHICS_PASS4.unity`). Подивися Game View, без запуску Play Mode.

**Як інструмент працює:** робить копію кожного з 89 наявних PNG-спрайтів у `Assets/RecoveredSpritesV2/` та встановлює оригінальні pivot із UV-трансформації. Для 9 полігональних Sprite замість прямокутного SpriteRenderer малює точно декодовані вершини/індекси через MeshFilter + MeshRenderer і `Sprites/Default`, зберігаючи індивідуальні кольори та Sorting Layer. Оригінальні PNG і PASS1/PASS2/PASS3/Zone001_PREVIEW не змінюються.

**Ще не перевірено в Unity Editor на нашому комп'ютері:** компіляція інструмента і візуальний результат; перевір і надішли Console за потреби. 2D Mesh-рендер може мати відмінності шейдерів; передбачається, що оригінальні Rect/UV дані відтворено точно. Це все ще не playable-порт — оригінальні програмні компоненти, runtime-керування зонами й камерами, фізика та решта матеріалів потребують відновлення.


## Етап 8 — 514 оригінальних Mesh, MeshFilter і базові матеріали (PASS5)

**Підтверджено повторним розбором APK:** у вихідних Unity 4.6.5f1 серіалізованих даних є **514 Mesh** (260 у level0 і 254 у sharedassets1.assets), **3 781 MeshFilter**, **3 810 MeshRenderer** та **33 Material**. Всі 514 Mesh декодовано у 1 194 188 байтів компактної приватної геометрії: 29 785 вершин / 17 920 трикутників. Перевірені кількості, індекси, оригінальні локальні координати, нормалі, UV і вершинні кольори. Зв'язки MeshFilter → Mesh та Renderer → матеріал зчитані безпосередньо з level0. П'ять MeshFilter мають порожнє посилання m_Mesh.

**Важливо:** це перший прохід геометрії, **не точне відтворення матеріалів**. Оригінальні Shader/RenderQueue/TextureBindings і анімації потребують додаткового перенесення. Поки використовується спрощений Sprites/Default із базовим _Color та вершинним кольором; текстури 3D-моделей можуть не відображатися. Зокрема велика кількість моделей використовує оригінальний матеріал Black.mat.

### Як запустити

1. Збережи Unity-проєкт і закрий Editor. У Git Bash, в корені Escape, виконай `git pull origin main`.
2. Завантаж з цього чату **Escape_2015_Stage8_Meshes_Local.zip** й розпакуй у корінь `D:\Unity Projects\Escape`. Архів містить приватні `RecoverySource/MeshData/*.meshbin`, `Assets/StreamingAssets/escape_mesh_manifest.json` і `tools/extract_mesh_stage8.py`.
3. Перевір, що під `RecoverySource/MeshData/` з'явилось **514 файлів** і є `Assets/StreamingAssets/escape_mesh_manifest.json`. Дані з APK не додавай у публічний репозиторій.
4. Відкрий **`Assets/Scenes/Escape_Zone001_GRAPHICS_PASS4.unity`** у Unity 6.0.58f2. Обери **Escape Recovery → 8 - Restore original meshes (pass 5)**.
5. Інструмент читає локальні файли, створює Mesh і спрощені Material у `Assets/RecoveredOriginalMeshes` і `Assets/RecoveredOriginalMaterials`, а результат зберігає в **`Assets/Scenes/Escape_Meshes_PASS5.unity`**. Попередню сцену він не перезаписує. MeshRenderer створюється на окремому дочірньому GameObject, щоб не конфліктувати зі SpriteRenderer (помилка попереднього етапу).
6. Відкрий нову сцену PASS5 і вибери **Escape Recovery → 9 - Preview Zone003 meshes (diagnostic)**. Це створює нову `Assets/Scenes/Escape_Zone003_MESH_PREVIEW.unity`, залишає активною лише Zone003 і ставить **діагностичну**, а НЕ оригінальну камеру за межами її моделей. Вкладка **Game** дозволить побачити структуру; 16:9 корисно для цього тесту.

**Чому Zone003:** оригінальна Zone003 містить приблизно 1 276 MeshFilter, Endless — 1 143, а Zone001 майже не містить статичних мешів. Тому саме Zone003 дозволить перевірити результат імпорту.

За наявності вихідного `RecoverySource/OriginalUnityData` і `unity4_reader.py` в архіві також є власний локальний Python-екстрактор, який може повторно відтворити файли Stage8. **Реальний запуск коду PASS5 в Unity Editor ще не перевірено**, тому у разі помилок надішли повний лог з Console.


## Етап 9 — відновлення _MainTex матеріалів (PASS6, частково)

**Перевірено безпосередньо в APK:** 33 серіалізованих Material. **23** Material містять _MainTex з доступними PNG-джерелами, ще **2** посилаються на порожні текстури шрифтів і **8** не мають _MainTex. У Stage 8 відновлено переважно базові кольори, а не оригінальні зв'язки з текстурами. Серед TextureID є промислові та природні фони.

**Це не повне перенесення шейдерів.** Новий інструмент лише створює окремі Material із Sprites/Default, прив'язує оригінальну _MainTex, базовий _Color та UV scale/offset. Зміни стосуються тільки MeshRenderer, які посилаються на такі Material. Матеріали без PNG залишаються незміненими; alpha cutout, custom shaders, blend modes, pixel snapping та runtime логіка не відновлені.

1. **Збережи Scene в Unity (Ctrl+S), закрий Unity Editor.** Відкрий Git Bash у корені Escape й виконай `git pull origin main`.
2. Розпакуй приватний ZIP **Escape_2015_Stage9_Materials_Metadata.zip** із чату прямо в `D:\Unity Projects\Escape`, об'єднавши папку Assets. Перевір: `Assets/StreamingAssets/escape_material_manifest.json`.
3. Переконайся, що є PNG з попередніх етапів у `Assets/RecoveredTextures`. Додаткових зовнішніх пакетів не потрібно.
4. Відкрий Unity та вже створену **`Escape_Zone003_CAMERA_MATCH.unity`** (у ній ракурс камери наближений до Scene).
5. Обери **Escape Recovery → 11 - Restore original material textures (pass 6)**.
6. Unity збереже нову сцену **`Escape_Zone003_MATERIALS_PASS6.unity`** (за потреби з числовим суфіксом), а копії матеріалів — у `Assets/RecoveredMaterialTexturesStage9/`. Порівняй Game із CAMERA_MATCH.

Оригінальні Material у `Assets/RecoveredOriginalMaterials` і старі сцени не редагуються. Публічний репозиторій не містить оригінальні матеріали чи зображення з комерційної гри.

**Статус перевірки:** дані 33 Material та їхні текстурні посилання перевірені на реальному APK і зіставлені з індексом витягнутих текстур. Код Editor Stage 9 опублікований, але **ще не запускався у користувацькому Unity 6.0.58f2**. Якщо Unity виведе помилку, надішли Console. Деякі матеріали взагалі не використовуються в Zone003, тому зміни можуть бути невеликими.

## Що ще потрібно до Windows-гри

1. Експортувати матеріали, Mesh, Sprite, Animator і зв'язки компонентів із Unity assets.
2. Декомпілювати DLL **локально**, адаптувати скрипти до Unity 6.
3. Відновити прив'язки ресурсів, шейдери, фізику, керування та сцени.
4. Протестувати редакторську збірку та Windows EXE.

Це дослідницька реконструкція без претензій на авторство оригінальної гри.

## Діагностика: Scene виглядає правильно, а Game — дрібно або неправильно

Це може бути **різницею камер**, а не помилкою імпорту Mesh. На Stage 8 команда Zone003 створює **діагностичну** ортографічну камеру, яка вміщує ВСІ Mesh зони в кадр. Scene View використовує вільну камеру редактора й може бути значно ближче до об'єктів. Не плутайте цей Game View із завершеною грою.

**Перевірка без переробки геометрії:**

1. Закрий Unity, виконай `git pull origin main` і відкрий `Assets/Scenes/Escape_Zone003_MESH_PREVIEW.unity`.
2. Перейди у вкладку **Scene**, встанови ракурс і масштаб, за якого моделі виглядають правильно.
3. Без запуску Play виконай **Escape Recovery → 10 - Copy Scene view to Game camera (diagnostic)**.
4. Інструмент збереже окрему сцену `Escape_Zone003_CAMERA_MATCH.unity` (якщо файл існує, додасть номер), скопіює кут, розташування й орто/перспективну проєкцію камери Scene. Відкрий вкладку **Game** і порівняй зображення.

**Обмеження:** точне співпадіння пікселів не гарантоване через різні пропорції вікна, оверлеї редактора та різне середовище освітлення. Це **не відновлення оригінальної камери/логіки**, лише діагностика. Stage8, PASS4, матеріали й вихідні сцені залишаються без змін. Якщо і після цього текстури/кольори спотворені, продовжуємо відновлювати саме матеріали й шейдери з APK.

## Налаштування камери Zone003 під референс з оригінальної гри

Після відновлення матеріалів у `Escape_Zone003_MATERIALS_PASS6.unity` промислові декорації схожі на оригінал, але діагностична камера може показувати **не той масштаб або зміщення**, що у відео. Оригінальна камера зі старого APK не містить повної логіки руху під час гри, а Stage8-прев'ю спочатку автоматично охоплювало весь рівень.

**Рішення:** нове меню **Escape Recovery → 12 - Adjust Zone003 PC camera (preview only)** відкриває вікно для налаштування положення X/Y і масштабу ортографічної камери.

1. Збережи зміни та зроби `git pull origin main`, відкрий `Assets/Scenes/Escape_Zone003_MATERIALS_PASS6.unity`.
2. Вибери пункт **12** у меню Escape Recovery; натисни **Create separate camera preview scene**. З'явиться нова `Escape_Zone003_FRAMING_TUNED.unity` (з номером, якщо така вже є).
3. Перейди у вкладку Game, вибери **16:9 Aspect**. Повернися до вікна **Escape Camera Framing** — воно дає змогу змінювати **Camera X / Y**, **Orthographic Size**, кнопками **Left/Right/Up/Down** і **Zoom in / Zoom out**. Положення та масштаб мають змінюватися наживо; кнопка **Use current Scene view angle** теж доступна.
4. Для наближення до оригінального відео натискай **Zoom in**, що зменшує Orthographic Size; якщо потрібно, перемісти камеру X/Y так, щоб основні конструкції зайняли потрібну частину кадру.
5. Натисни **Save tuned preview** або **Ctrl+S**. Скриншот вкладки **Game** дасть змогу порівняти кадрування з референсом.

**Безпека даних:** інструмент навмисно не дозволяє редагувати камеру, доки не створено окрему FRAMING_TUNED-сцену. `MATERIALS_PASS6`, `CAMERA_MATCH`, `MESH_PREVIEW` та всі локальні матеріали залишаються без змін. Результат — **ручна PC-адаптація кадру**, не твердження про оригінальні координати/правила роботи камери Escape. Код перевірено у GitHub, але ще не компільовано на локальному Unity користувача.


## Етап 10 — відновлення режимів оригінальних шейдерів (PASS7)

**Знайдено безпосередньо в APK:** у `sharedassets1.assets` збереглися 12 Shader-об'єктів, які описують графічні режими гри. До того ж є ще один у `sharedassets0.assets`. Для матеріалу `Black.mat` вихідне значення **_Color = (0,0,0,1)**, а не біле, як у попередньому Stage9-екстракторі. Причина попередньої помилки: пошук тексту `_Color` інколи зупинявся на `_ColorMask` і не знаходив справжню властивість.

**Власні, не скопійовані з гри, шейдери Unity 6** наближають ці режими:
- `EscapeRecovery/SolidColor` — непрозорий суцільний колір із двостороннім рендерингом для `Black.mat`, оригінальний Shader ID 357.
- `EscapeRecovery/OpaqueVertexColor` — непрозорі кольори вершин, Shader ID 356.
- `EscapeRecovery/TransparentVertexColor` — напівпрозорі кольори вершин, Shader ID 358.
- `EscapeRecovery/InvertedTextureVertex` — **інверсія RGB текстури, множення на колір вершини й альфа-змішування**, Shader ID 359 (14 Material, 51 посилання у вихідних MeshRenderer).
- Непрозорі колірні шейдери 353, 354 повторно використовують `SolidColor` із властивістю `_Color` або `_Color2`. Інші шейдери частинок/шрифтів наразі не переносяться.

**Як перевірити без пошкодження прогресу:**

1. Збережи поточну `Escape_Zone003_FRAMING_TUNED.unity` через **Save tuned preview** або **Ctrl+S**, закрий Unity Editor.
2. У Git Bash із папки проєкту виконай `git pull origin main`.
3. Із цього чату завантаж **Escape_2015_Stage10_Shaders_Local.zip** та розпакуй **у корінь** `D:\Unity Projects\Escape`. Має з'явитися файл `Assets/StreamingAssets/escape_shader_manifest.json`. Альтернатива: за наявності локальних оригінальних Unity-файлів і Stage9 manifest виконай `py -3 tools/extract_shader_manifest.py`.
4. Відкрий в Unity **`Assets/Scenes/Escape_Zone003_FRAMING_TUNED.unity`** і вибери **Escape Recovery → 13 - Restore original unlit shader effects (pass 7)**.
5. Створиться **НОВА** `Assets/Scenes/Escape_Zone003_SHADERS_PASS7.unity` (або нумерована копія) та окремі матеріали в `Assets/RecoveredShaderMaterialsStage10/`. Попередні сцени, матеріали й твій підігнаний ракурс не перезаписуються.
6. У вкладці **Game** вибери **16:9** і порівняй PASS7 з `FRAMING_TUNED`. Якщо фон або силуети гірші — просто відкрий попередню сцену. Надішли скриншот і текст Console.

**Обмеження:** за APK підтверджені числові ID шейдерів, обробка RGB/alpha, кольори та призначення матеріалів. Проте самі ShaderLab-файли у GitHub — **самостійно реалізовані сумісні наближення**, НЕ оригінальний код гри. PixelSnap, спеціальні ефекти частинок, порядок прозорих MeshRenderer, анімація та поведінка камери поки неповні. Stage 10 ще не протестований у твоєму Unity 6.0.58f2 — можлива потреба виправлень.


## Етап 11 — регульована яскравість і серпанок фону (діагностика)

Після PASS7 силуети промислових конструкцій схожі на вихідну гру, але **у відеореференсі задній план здається світлішим і менш контрастним**. Це спостереження зі скриншотів, а не доказ неправильної Gamma/Linear-конфігурації. Відсутні підтверджені дані про точні налаштування освітлення/постобробки під час оригінального геймплею, а відео на YouTube могло бути перекодоване.

Щоб нічого не псувати, додано незалежний Shader `EscapeRecovery/InvertedTextureTunable` та меню **Escape Recovery → 14 - Tune Zone003 background brightness (preview only)**. Вони працюють **виключно з копіями** матеріалів `EscapeRecovery/InvertedTextureVertex` і залишають силуетний шейдер `Black.mat` незміненим.

1. Збережи `Escape_Zone003_SHADERS_PASS7.unity`, закрий Unity Editor, виконай `git pull origin main`.
2. Відкрий сцену `Assets/Scenes/Escape_Zone003_SHADERS_PASS7.unity`.
3. Вибери **Escape Recovery → 14 - Tune Zone003 background brightness (preview only)**, натисни **Create safe background-tuning preview**.
4. Створиться окрема сцена `Assets/Scenes/Escape_Zone003_BG_TUNING.unity` (або нумерована версія) з новими локальними Material у `Assets/RecoveredBackgroundTuning/`.
5. У вкладці Game з **16:9** порівняй оригінальний YouTube-кадр і тестову сцену. Змінюй **Background brightness** (почни з 1.08), **Atmospheric haze** (почни з 0.06). Колір **Haze tint** можна також змінити. Кнопка **Reset to unadjusted Stage 10 appearance** відновлює brightness=1 та haze=0.
6. Натисни **Save background-tuning preview**. Щоб відмовитися від експерименту, відкрий SHADERS_PASS7 — він залишається незмінним.

Поточні значення 1.08 / 0.06 — лише делікатна **гіпотеза для ручного підбору**; не підтверджені оригінальними файлами APK. Оригінальний колірний простір Unity Editor, fog/post-processing і шейдерний порядок відображення ще потрібно перевіряти окремо. Інструмент створено в GitHub, але **його компіляцію/рендер у Unity на ПК користувача поки не перевірено**.

## Етап 12 — точний інвентар анімацій і керування кораблем

Після завершення візуального проходу Zone003 **не перезаписуй** створені сцени `Escape_Zone003_SHADERS_PASS7` та `Escape_Zone003_BG_TUNING`: їх уже можна використовувати як референс для порту.

З оригінальних файлів **Unity 4.6.5f1** перевірено:
- **152 AnimationClip** (151 у sharedassets1.assets та 1 у sharedassets0.assets);
- **108 AnimatorController** (107 + 1);
- **1 408 компонентів Animator** в level0 — **824** мають правильне посилання на AnimatorController, **584** залишені без контролера;
- окремі кліпи на кшталт `OilPumpAnimation`, `StartEngine`, `TakingOff`, `TryingToTakeOff`, `anim_RocketDoor_opening`;
- контролери `Rocket`, `OilPump`, `DoorController`, `EngineSound`, `RocketFuelBar`;
- класи у Assembly-CSharp.dll: `RocketInputController` (OnDown / OnUp), `RocketMovementController` (UpdateVelocity / UpdatePosition / UpdateOrientation), `RocketFuelController`, `RocketAnimatorController`.

**Це інвентар, а не готові анімації!** Імена, оригінальні fileID/pathID і зв’язки компонентів перевірені, але криві AnimationClip, графи станів AnimatorController, серіалізовані поля і реальна фізика/керування ракетою ще потребують відновлення. Не можна вважати, що однойменний кліп автоматично належить однойменному контролеру.

### Перевірка в Unity

1. Збережи поточну сцену і матеріали (Ctrl+S, File → Save Project), закрий Unity. Запусти `git pull origin main` у папці проєкту.
2. Розпакуй приватний **Escape_2015_Stage12_Animation_Inventory_Local.zip** із чату в корінь `D:\Unity Projects\Escape`. Має з’явитися `Assets/StreamingAssets/escape_animation_inventory.json`.
3. Відкрий будь-яку відновлену сцену Unity і вибери **Escape Recovery → 15 - Inspect original animations and rocket (read-only)**.
4. У новому вікні шукай `Rocket`, `OilPump`, `Engine`, `TakingOff` або `Zone003`. Перемикай вкладки Clips, Controllers, Scene Animators, Rocket Methods. Вікно не змінює сцени.
5. За наявності оригінального повного локального архіву дані можна згенерувати повторно: `py -3 tools/extract_animation_inventory.py`.

**Наступний етап:** не просто відтворити назви, а конвертувати ключові AnimationClip (спершу Rocket/OilPump), відновити графи потрібних AnimatorController і окремо перенести оригінальну поведінку RocketInputController / RocketMovementController під керування клавіатурою ПК. Не підміняти її припущеннями без підтверджених даних.


## Етап 13 — перша справжня анімація: OilPumpAnimation (PASS8)

**Що підтверджено прямим декодуванням із APK:** оригінальний `OilPumpAnimation` у `sharedassets1.assets` (Unity 4.6.5f1, fileID 1/pathID 394) має двосекундний зациклений рух при 60 кадрах/с. Звичайні списки кривих порожні: рух закодовано в `m_MuscleClip` → `StreamedClip`. Там **14 анімованих каналів** і **3 сталі канали**, згруповані у 5 прив'язок Transform: положення `RopeAndCounterWeights/CounterWeight`, положення `RopeAndCounterWeights`, обертання `ArmPendular`, обертання й положення `ArmPendular/Plane2032`.

Числові path hash зіставлені з іменами через CRC32 і перевірені на восьми реальних ієрархіях `SubAnimRoot`: **4 OilPump у Zone003** і 4 у Zone031. За вихідними кубічними коефіцієнтами згенеровано **17 кривих по 121 відліку** з кроком 1/60 секунди. Кватерніони обертання перевірено на нормалізованість, усі вибірки скінченні, а початок і кінець циклу збігаються.

**Це відновлені значення руху, але не оригінальний AnimatorController.** Для демонстрації створюється власний простий зациклений контролер Unity 6, без оригінальних переходів станів, фізики й взаємодії корабля/помпи. Пересемплювання при 60 Гц може дещо відрізнятися від первісної інтерполяції між ключами.

### Як запустити

1. Збережи налаштування `Escape_Zone003_BG_TUNING.unity` (Ctrl+S і **File → Save Project**) та закрий Unity.
2. У папці проєкту виконай `git pull origin main`.
3. Завантаж із чату локальний архів **Escape_2015_Stage13_OilPump_Animation_Local.zip**, розпакуй у корінь `D:\Unity Projects\Escape`. З'являться `Assets/StreamingAssets/escape_oilpump_animation.json` та `tools/extract_oilpump_animation.py`. Це **дані користувацького APK**, тому архів не публікується у GitHub.
4. Відкрий **Escape_Zone003_BG_TUNING** (або `Escape_Zone003_SHADERS_PASS7`, якщо BG_TUNING недоступна).
5. Запусти **Escape Recovery → 16 - Restore OilPump animation (pass 8)**. Скрипт створить **окрему** `Assets/Scenes/Escape_Zone003_OILPUMP_ANIM_PASS8.unity`, AnimationClip і простий AnimatorController у `Assets/RecoveredOilPumpAnimationStage13/`. Він не змінює попередні сцени.
6. У новій сцені натисни **Play**. Очікується зациклений рух чотирьох помп у Zone003; ракурс і фони залишаться такими, як у збереженій сцені.
7. Подивися Console і зроби скриншот Game View. Якщо самих рухів не видно, перевір активність `Zone003/Zone/AnimationRoot/SubAnimated/OilPump*/SubAnimRoot`, наявність Animator на цих об'єктах та повідомлення про імпорт.

**Для повторного локального витягування:** якщо присутні `RecoverySource/OriginalUnityData/sharedassets1.assets`, `RecoverySource/Tools/unity4_reader.py`, та приватні маніфести Stage1/Stage12, виконай `py -3 tools/extract_oilpump_animation.py`. Скрипт входить до Stage13 ZIP; на GitHub зберігається редактор Unity, документація та правила ігнорування приватних даних.

**Перевірка:** Python-декодер виконано на вихідному APK, JSON і ZIP перевірено; Unity Editor-скрипт `Assets/Editor/EscapeOilPumpAnimationPass.cs` завантажено, але ще **не компільовано та не запускався** в Unity 6.0.58f2. Можуть знадобитися сумісні зміни після перевірки на ПК.

## Етап 14 — Rocket: оригінальні альфа-анімації і тест клавіатури (PASS9)

З оригінальних файлів APK Unity 4.6.5f1 перевірені Rocket GO #27907, Animator root #19899, AnimatorController #553. Підтверджені Clips: StartEngine #397 (1 с, 8 SpriteRenderer alpha), TakingOff #398 (2 с, 0 SpriteRenderer alpha), TryingToStartTheEngine #399 (23/60 с, 8 SpriteRenderer alpha), TryingToTakeOff #400 (1 с, 8 SpriteRenderer alpha). Всього **24 придатні до перенесення альфа-криві** та **20 інших прив'язок**, що залишаються неперенесеними. Криві з Unity4 StreamedClip просемпльовано на 60 Гц і перевірено за оригінальними CRC32 path. Дані кривих доступні лише у приватному ZIP із чату, не на GitHub.

Перевірка Assembly-CSharp.dll: RocketInputController використовує події OnDown/OnUp однієї кнопки тяги; RocketMovementController змінює yVelocity силою тяги та гравітації, інтегрує yPosition і пов'язаний із TargetTransform. **Точні параметри і логіка зон, зіткнення, звуки, паливо, частинки та state machine ще не перенесені.** Нове керування є самостійно написаним демонстраційним прототипом, а не оригінальною фізикою.

### Як запустити

1. Збережи поточну сцену (Ctrl+S, File → Save Project), закрий Unity та у Git Bash в корені Escape виконай git pull origin main.
2. Завантаж із цього чату Escape_2015_Stage14_Rocket_Visuals_Local.zip і розпакуй у корінь D:\Unity Projects\Escape. Перевір файл Assets/StreamingAssets/escape_rocket_visual_clips.json.
3. Відкрий Escape_Zone003_OILPUMP_ANIM_PASS8 (рекомендовано) або Escape_Zone003_BG_TUNING.
4. Натисни **Escape Recovery → 17 - Restore rocket visuals and keyboard test (pass 9)**. Скрипт збереже окрему Escape_Zone003_ROCKET_TEST_PASS9.unity та створить 3 AnimationClip (альфа-канали), діагностичний AnimatorController, прив'яже оригінальний Rocket і тестовий клавіатурний компонент. Попередні сцени не змінюються.
5. Натисни Play і клацни Game для фокусу. **Space / W / Up** — тримати тягу, **R** — скинути, **1/2/3** — пробні анімації світла й індикаторів; **4** — лише статус TakingOff (оригінальні script/particle bindings ще не відтворені).

Приватний ZIP також містить tools/extract_rocket_animations.py для локального повторного витягування даних із оригінального APK. На GitHub розміщені тільки власні Assets/Editor/EscapeRocketPreviewPass.cs та Assets/Scripts/EscapeRocketKeyboardPreview.cs. **Unity Editor не запускався під час розробки коду** — компіляцію й рух потрібно перевірити на комп'ютері користувача.

## Етап 15 — ракета: автоматичний нахил і пошук пошкодженого шару графіки (PASS10)

Користувацький тест **PASS9 успішний для вертикального руху**: ракета реагує на клавіші та злітає/опускається. Водночас її кут не змінюється, а на корпусі видно чужорідні кольорові прямокутники/контури. Це дві **окремі** проблеми; наявність такої графіки не доводить, що сама структура або всі текстури зіпсовані.

**Підтверджено прямим читанням оригінального APK і IL:**
- `RocketMovementController.UpdateOrientation` викликає `ComputeWantedRotation`, а той обчислює бажаний кут: `atan2(Velocity.y, rotationInertia) * 57.29578 * angularAmplitudeReduction`; поворот об'єкта відбувається навколо його forward-вектора. Точні значення `rotationInertia` й `angularAmplitudeReduction` з оригінальних MonoBehaviour ще не зчитано.
- `ExternGameElements/Rocket` використовує окремі SpriteRenderer для `RocketBody*`, `RocketWing*`, `RocketLightGlass*`, `RocketFuelGauge*` і спрайтових `*_collider`-об'єктів.
- Усі згадані графічні деталі ракети посилаються на `SpriteAtlasTexture-1024x512`, TextureID **32**, оригінальний ETC2_RGBA8 (компресований KTX). Саме цей атлас було локально декодовано на Stage5. Поки **не підтверджено**, чи кольорові прямокутники пов'язані з декодером ETC2, імпортом PNG, масками, змішуванням прозорості або кількома шарами SpriteRenderer.

**Нові інструменти:** `Assets/Scripts/EscapeRocketOrientationDiagnostic.cs` і `Assets/Editor/EscapeRocketDiagnosticPass.cs`. Вони **не замінюють** оригінальні ресурси, старі сцени чи керування: створюється лише нова `Escape_Zone003_ROCKET_DIAGNOSTIC_PASS10.unity` з нахилом за *підтвердженою формою формули* і **демонстраційними числовими значеннями**. Перемикачі дозволяють тимчасово приховати лише групу скла/індикаторів та групу collider-масок під час Play, щоб знайти джерело артефактів.

### Що зробити

1. Заверши Play Mode, збережи `Escape_Zone003_ROCKET_TEST_PASS9` (Ctrl+S), закрий Unity, виконай `git pull origin main`.
2. Відкрий `Escape_Zone003_ROCKET_TEST_PASS9.unity`. Вибери **Escape Recovery → 18 - Diagnose rocket graphics and tilt (pass 10)**.
3. В окремій сцені **Escape_Zone003_ROCKET_DIAGNOSTIC_PASS10** натисни Play. Утримуй Space/W/Up, щоб перевірити нахил. **T** вмикає/вимикає його.
4. Натисни **G** — тимчасово сховається скло та індикатори. Зроби скриншот і натисни **G** повторно. Потім окремо натисни **C**, щоб приховати SpriteRenderer на `*_collider` (лише для тесту), і зроби другий скриншот. **C** повторно повертає їх. **R** скидає положення.
5. Надішли результати G та C. Якщо кольорові артефакти зникають тільки від G, наступним етапом виправляємо саме атлас/шейдер скла й паливних індикаторів; якщо від C — перевіряємо зайві collider sprite layers. Якщо не зникають — повертаємося до ETC2 PNG/UV/матеріалів основного корпусу. Оригінальні текстури не переписуємо без підтвердження.

**Статус:** вихідна формула нахилу, оригінальна структура, TextureID 32 і код попереднього тесту підтверджені. Команда №18 і демонстраційний нахил нові, тому компіляцію та поведінку Unity 6.0.58f2 потрібно ще перевірити на ПК. Це **не завершене відновлення фізики та візуалу ракети**, а ізольована діагностика й перший крок до правильного нахилу.
