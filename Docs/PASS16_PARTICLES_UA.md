# PASS16: оригінальні емітери середовища та нафти

18.11.2026, Unity 6000.0.58f2, Windows x64.

Сцена: Assets/Scenes/Escape_Zone003_PC_PARTICLES_PASS16.unity.
Збірка: Builds/Windows-PASS16/Escape-PASS16.exe.
База — підтверджений PASS15; старі сцени не перезаписуються.

## Джерела та ідентифікація

Zone003 містить п'ять legacy EllipsoidParticleEmitter/ParticleAnimator/
ParticleRenderer: два PSL-LongTrailSmoke, два PSL-AfterDestroyFire та
PSL-AfterDestroySmoke. Остання назва оманлива: renderer фактично посилається
на матеріал OilParticles та чорну текстуру Point64. Це емітер нафтового
викиду біля конструкції після помп. Його native localVelocity спрямована
вгору, постійна сила — вниз, rndForce додається щокадру. Увімкнення
GameObject оригінальним треком Zone003 відбувається на 8.166667 source с.
Назви AfterDestroyOil в інших місцях level0 не є підставою додавати нові
ефекти до цих чотирьох помп: вони належать іншим зонам.

Розміри native записів перевірено: emitter 116, animator 90, renderer 104
байти. Прочитано обидва bool перед minSize, п'ять Color32, sizeGrow,
вектори швидкості та сили, damping, ellipsoid, rotation, sheet 8×8 або
1×1, sorting layer/order та матеріали через правильні external file ID.
Нульові tangentVelocity, rotation axes, minEmitterRange та damping=1
перевірені для всіх п'яти систем; вони не замінені довільними значеннями.

Схема Unity5 release використана лише як незалежна звірка порядку полів:
її 64-bit PPtr та renderer header не застосовувалися як зміщення Unity4.
Матеріали й shader тексти перевірено безпосередньо в приватному архіві.
Текстуру диму RGBA4444 прочитано з R у старших чотирьох бітах слова:
отримано білий RGB і змінну alpha. Це виправляє попереднє помилкове
припущення раннього загального декодера про протилежний порядок.
Point64 — оригінальний чорний RGBA32 із прозорим краєм.

## Перенесення

Нові native ParticleSystem використовують source min/max розміру, енергії,
емісії, ellipsoid і world space, випадкове обертання, angular velocity,
п'ять рівномірних кольорових ключів, сили та original sheet cycles.
Початковий native random seed зафіксований для повторюваних перевірок;
послідовність Unity4 RNG не відновлена.

У shader відтворено три різні режими: Mobile/Particles/Alpha Blended,
двоступеневий fixed-function multiply Blend DstColor SrcColor та
Unlit/Transparent Cutout. Нафтовий shader, як і джерело, ігнорує vertex
color; оригінальний cutoff збережено. Приватні PNG й матеріали створено
окремо, без переписування раніше декодованих ресурсів.

У Unity6 всі осі velocity/force module повинні мати однаковий mode кривих.
Навіть нульовий діапазон однієї осі збережено як TwoConstants: змішування
Constant та TwoConstants виявилося некоректним у native simulation.
Це перевірено фактичними траєкторіями крапель, а не лише читанням полів.

EscapeLegacyParticles явно просуває симуляцію реальним часом польоту
(ZoneTime / source zone speed). Пауза, смерть і завершення PC harness
не просувають частинки; restart очищає їх і скидає seed. Оригінальний
трек activeSelf керує нафтою. Нових таймерів нафти не вигадано.
Для компіляції потрібен вбудований com.unity.modules.particlesystem@1.0.0,
доданий через Unity Package Manager.

## Межі відповідності

Це перенесення legacy систем на native Unity6, а не бітово тотожний
емулятор Unity4. Шум сили та випадкова емісія використовують новий рушій;
число нафтових крапель залежить від кроку симуляції. Візуальна форма
sizeGrow перенесена за mapping старого Legacy Particle System Updater:
для додатного grow використовується maxEnergy, тому системи з різним
часом життя можуть відрізнятися від старої інтеграції. Цю різницю не
маскували під підтверджену тотожність. Порядок окремих випадкових
частинок і точний кадр їхнього народження не порівняно з оригінальним Player.

TakeoffSmokes ракети (дві інші legacy системи) не ввімкнено довільно:
для них ще потрібен оригінальний запуск/зліт. SurvivorDeathBlood та
інші modern системи не оголошені перенесеними. Люди, їхній HereAndAlive,
атракція, круглі ореоли, лінії, посадка й лічильник ще лишаються окремою
наступною роботою, разом із потрібними треками SetIA/Follow/RadialForce.

## Перевірки

- Play Mode просував реальні кадри. Окремо звірено native параметри
  емітерів із приватною source таблицею.
- Усі п'ять систем генерують частинки при кроках 30/60/144 FPS;
  перевірено скінченні позиції, додатні розміри та перехід крапель до
  швидкості вниз після викиду вгору. Оригінальний gate нафти перевірено
  до й після 8.166667 с.
- За три секунди отримано 29/14 частинок диму та 19/19 вогню;
  нафти 339/339/390 для 30/60/144 FPS. Це не заявка на однаковий RNG.
- Пауза не змінює позицій і remainingLifetime; reset залишає нуль частинок.
- Знято кожну систему окремо та контрольний кадр без неї. Понад 10
  пікселів змінюються для кожної; фактично 22652–107536 пікселів
  у контрольних 960×540 кадрах. Дим, пожежі й нафтова дуга перевірені
  візуально. Приватні докази: Reports/pass16-emitter-*.png.
- Windows x64 build завершено успішно, 0 build errors. Player-регресії
  30/60/144 FPS пройшли: рух, нахил, маршрут, камера, пауза/відновлення,
  native зіткнення/смерть, restart, completion і відмальовані кадри.
- Початкову помилку відсутнього ParticleSystem module та невідповідність
  mode осей виправлено. Після цих виправлень, Play Mode і build нових
  помилок у консолі немає. Старий журнал не очищали.
- Локальні звіти: Reports/pass16-build.txt, PASS16-player,
  pass16-verification.txt і журнал PASS16 Editor.

Відтворення: tools/recover_particles_pass16.py --project з локальним
архівом, потім EscapeParticlesPass16.Create. Потрібен PASS15.
Create створює унікальний шлях нової сцени; Build збирає базову PASS16.
Всі APK-похідні залишаються ignored; публікується лише авторський код.

Довідкові джерела:
- https://github.com/AssetRipper/TypeTreeDumps
- https://gist.github.com/karljj1/58f16e936d13a94ae6a2f741fd271d91
- https://docs.unity3d.com/kr/6000.0/ScriptReference/ParticleSystem-forceOverLifetime.html
