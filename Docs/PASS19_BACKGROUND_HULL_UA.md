# PASS19 — порядок фону та невеликий запас хітбокса

Сцена: `Assets/Scenes/Escape_Zone003_PC_VISUAL_POLISH_PASS19.unity`.
Windows: `Builds/Windows-PASS19/Escape-PASS19.exe`.
Unity: 6000.0.85f2.

Фоновий smoke 6420 і обидва великі промислові квади мали однаковий
прозорий render queue/sorting order. Перспективна камера порівнювала
радіальні відстані до центрів bounds. Під час руху X порядок мінявся,
хоча геометрична глибина Z залишалась сталою. Це відтворено біля
36,05% і 55,28%, що відповідає наданим парам скриншотів.

Для PASS19 ввімкнено `sortBackgroundByDepth`: камера залишається
перспективною з тим самим FOV, але прозорість сортується режимом
`TransparencySortMode.Orthographic` за глибиною. Старі сцени мають
default false, їхні параметри та ресурси не переписуються.

Перевірка заморожує дим і рухає камеру на 0,008 одиниці через точку
рівної відстані. Порівнюються ті самі текстури та ті самі частинки:

| Перехід | Стара середня зміна RGB | PASS19 |
| --- | ---: | ---: |
| 36,05% | 1,8103 | 0,3419 |
| 55,28% | 5,1874 | 0,8173 |

У PASS19 лишається звичайна зміна пікселів від руху камери; стрибка
порядку між усією хмарою і цілим краном немає. Кадри до/після:
`Reports/pass19-sort-*.png`, звіт `Reports/pass19-background.txt`.

Хітбокс: 19 solid-колайдерів (12 convex meshes, 5 boxes, 2 spheres)
стиснено до 96% у площині XY навколо центра ракети. Геометрія спрайтів
і анімацій не змінюється. MeshCollider отримують окремі локальні копії
мешів із явним Physics.BakeMesh; collider contactOffset зменшено з
0,01 до 0,002. Глобальні налаштування фізики та колайдери помп незмінені.
Тригери підбирання й OutOfScreenCollider не змінені.

Native Physics.ComputePenetration порівнює PASS18 і PASS19: 25 проб
біля краю більше не стикаються; 67 проб глибшого контакту лишаються
контактами. Габарити XY зменшилися рівно на 4%, усі 19 фізичних форм
зберігаються після завантаження в Play. `Reports/pass19-hull.txt`.

Windows regression: повна зона та нормальний цикл камер на
30/60/144 FPS, rescue і flight/collision/restart на 60 FPS, з
`-diag-job-temp-memory-leak-validation`. Результати лежать у
`Reports/PASS19-player` та `Logs/pass19-*.log`.

Відтворення перевірок: `tools/VerifyHullPass19.cs`,
`tools/VerifyBackgroundPass19.cs`, `tools/audit_pass19.py`.

Довідка API, звірена для Unity 6.0:

- [Camera.transparencySortMode](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-transparencySortMode.html)
- [TransparencySortMode.Orthographic](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/TransparencySortMode.Orthographic.html)
- [Collider.contactOffset](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider-contactOffset.html)

Попередні 25 сцен перевіряються за SHA256. Приватні копії геометрії
колайдерів і сцена PASS19 не публікуються; коміт містить авторський код,
перевірки та цей опис. Нових ігрових систем не додано.
