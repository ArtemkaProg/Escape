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

## Що ще потрібно до Windows-гри

1. Експортувати матеріали, Mesh, Sprite, Animator і зв'язки компонентів із Unity assets.
2. Декомпілювати DLL **локально**, адаптувати скрипти до Unity 6.
3. Відновити прив'язки ресурсів, шейдери, фізику, керування та сцени.
4. Протестувати редакторську збірку та Windows EXE.

Це дослідницька реконструкція без претензій на авторство оригінальної гри.
