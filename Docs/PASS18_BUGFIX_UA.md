# PASS18 — виправлення без нових механік

Оновлення: Unity 6000.0.85f2 встановлено та перевірено; див. [звіт перевірки](PASS18_UNITY85_UA.md). Попередній опис нижче зберігає результати діагностики 6000.0.58f2.

Сцена: `Assets/Scenes/Escape_Zone003_PC_BUGFIX_PASS18.unity`.
Windows: `Builds/Windows-PASS18/Escape-PASS18.exe`.

- Фонові камери працюють звичайним порядком `depth`: небо, перспектива,
  передній план. Видалено вкладені `Camera.Render()` з `OnPreCull`.
  Синхронізація відбувається після руху камери; фонові шари використовують
  власний frustum без occlusion culling. Збережено source FOV, паралакс і кольори.
- Лінія підганяється після всіх source progression tracks, які раніше
  перезаписували її масштаб. Довжина враховує геометрію оригінального
  спрайта та масштаб предків. Rescue виконується після оновлення дверей.
- При відриві від помпи передається швидкість анімованого руху, як у
  `RocketChips.SurvivorMovementController::set_Control`, замість нульової.
  Швидкість зберігається під час паузи.

Перевірка `tools/VerifyBugfixPass18.cs` підтвердила напрямок останнього
поштовху вперед/угору і кінці всіх дев'яти променів при 30/60/144 FPS.
Збережено кадри навколо 36%, 58%, 60–61% у `Reports/pass18-background-*.png`.
Opt-in `-escapeBugfixAudit` проходить цілу зону з рендером і перевіркою
видимості фону. Для прихованого Windows-вікна використовується явний
offscreen render поза render callbacks; після останнього кадру audit
очікує завершення тимчасових робіт перед виходом.

Перевірено кінці дев'яти ліній та поштовх помпи на 30/60/144 FPS.
П'ять Windows-прогонів (повна зона на трьох FPS, rescue і zone regression
на 60 FPS) пройшли з `-diag-job-temp-memory-leak-validation` без allocator
попереджень. Кадри фону збережені біля 36%, 58% і 60–61%.

У чистій сесії Editor 6000.0.58f2 повна зона та restart пройшли, але при
виході з Play відтворено `JobTempAlloc`: 56 байтів, native stack
`GeometryJobTasks::ScheduleSharedGeometryJobs` →
`ParticleSystemGeometryJob::ScheduleJobs` → `Camera::Render` →
`RenderPlayModeViewCameras`. Отже, кодові зміни не усувають цей native
витік повністю. Повідомлення не приховуються і Console не очищується.

Unity документує аналогічну регресію частинок UUM-114725 / UUM-113839;
release notes 6000.0.59f2 містять виправлення витоку ParticleSystem job
fences (UUM-113729). Потрібне точкове оновлення Editor і повторний прогін.
6000.0.59f2 ще не встановлено: план CLI — 4,079,987,712 байтів завантаження
без додаткових Android-модулів. Версія проєкту залишається 6000.0.58f2.

- https://issuetracker.unity.com/issues/3882/an-internal-warning-jobtempalloc-has-allocations-that-are-more-than-the-maximum-lifespan-of-4-frames-old-is-being-logged-when-interacting-with-a-particle-system-in-the-editor
- https://unity.com/releases/editor/whats-new/6000.0.59f2

Докази: `Reports/PASS18-player`, `Reports/pass18-editor.txt`,
`Reports/pass18-editor-clean.txt`, `Logs/pass18-editor-clean.log`.
Попередні 24 сцени перевірено за SHA256: без змін. Приватні сцени та
ресурси APK не публікуються.
