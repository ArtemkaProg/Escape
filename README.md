# Escape (2015) — відновлення гри з APK

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

## Що ще потрібно до Windows-гри

1. Експортувати матеріали, Mesh, Sprite, Animator і зв'язки компонентів із Unity assets.
2. Декомпілювати DLL **локально**, адаптувати скрипти до Unity 6.
3. Відновити прив'язки ресурсів, шейдери, фізику, керування та сцени.
4. Протестувати редакторську збірку та Windows EXE.

Це дослідницька реконструкція без претензій на авторство оригінальної гри.
