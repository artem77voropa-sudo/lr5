# ЛР5: докази безпеки браузера

Воропаєв Артем Максимович, КБ-41; 2-A; рівень «Добре».

## Стенд і Git-стан

- Windows, VS Code, .NET SDK 10.0.403, PostgreSQL 18.4 (Docker), Google Chrome (точна версія в JSON-протоколах).
- API/client: http://localhost:5080; дозволений origin http://localhost:4173; недозволений http://localhost:4174. Це різні origins, але один site.
- Development, БД securelab_lr05. Дані попередніх ЛР не очищувалися.
- Робоча гілка lab/5-browser-xss; release lab-05-start-v1; base_commit 8daa3ca2da6061aac67a3ed5e97886c56d4df716 (v0.4.0). Receipt: `.scaffolds/lab-05.json`.
- Vulnerable commit: `36bc4fb467cf101162d348149e143635b6b91640` — scaffold із двома innerHTML та marker-коментар.
- Fixed commit: `6cc3d66bb737bdddc8b4a961b044f709b1a1368c` — safe helper, CORS, CSP та результати перевірок. Контрольні суми перевірених файлів наведено в `evidence/lr05/verified-files.json`.
- Репозиторій для передавання: [artem77voropa-sudo/lr5](https://github.com/artem77voropa-sudo/lr5). Завершальне оформлення: документаційний коміт → v0.5.0 → main → push гілок і тегу. Код лишається кодом зазначеного fixed commit; передавання перевіряється за віддаленими refs.
- Навчальний vulnerable state запускається тільки локально. Обидва коміти створив автор роботи.

## Finding XSS-05-01

| До | Причина | Виправлення | Повторна перевірка | Залишковий ризик |
|---|---|---|---|---|
| Description і Comment.Text створювали img та виконували marker | Недовірений рядок передано в innerHTML | Єдиний renderPlainText на textContent для обох полів | Той самий marker є текстом, img=0; виконання відсутнє навіть при bypass CSP у тестовому браузері | Нові sinks потребують review; HTML, URL та JavaScript-контексти мають інші правила |

Карта двох потоків: [source-to-sink.md](source-to-sink.md). Marker зберігається в `tests/browser/lab05-marker.txt`. Він лише встановлює локальний DOM-атрибут; жодного читання секретів чи зовнішніх hosts.

## REG-XSS-01

Передумови: локальний seed, Alice, /browser-lab.html. Для «до» — vulnerable snapshot; для retest — fixed. Для опису створити інцидент з унікальною назвою, Low і канонічним marker. POST має 201, наступний GET — 200. У Network response description містить вихідний рядок; GetDetailsAsync читає його з PostgreSQL. Для коментаря прочитати I1=20000000-0000-0000-0000-000000000001: marker уже міститься в публічному seed-коментарі.

До: обидва sinks створюють img, прихований target має атрибут executed. Після: Description містить точний marker як textContent без дочірніх елементів; коментар має нуль img; атрибут відсутній. Негативний контроль підтверджує здатність marker працювати в початковому стані, а не лише його відсутність після fix.

Фактичні протоколи: [до](evidence/lr05/before-browser.json), [після](evidence/lr05/fixed-browser.json). Зображення: [опис до](evidence/lr05/before-description.png), [опис після](evidence/lr05/fixed-description.png), [коментар до](evidence/lr05/before-comment.png), [коментар після](evidence/lr05/fixed-comment.png).

Окремий [storage-api.json](evidence/lr05/storage-api.json) зіставляє пряме читання PostgreSQL через psql та API для обох полів із канонічним marker. Для опису створено окрему синтетичну fixture та видалено після спостереження; для коментаря прочитано публічний seed. Це прямий DB/API-контроль, окремий від браузерного DOM-доказу.

Позитивна регресія: апостроф, одинарні/подвійні лапки, `<b>звичайний текст</b> </> &`, Unicode та 4000 символів Ж залишаються точним текстом. Bob отримує 404 на інцидент Alice, Morgan — 200. CSP не замінила доступ до ресурсу.

## REG-CORS-01

Обидві probe-сторінки виконують fetch до абсолютної адреси API. Проста проба: GET /health. Preflight: той самий GET із X-Lab-Cors-Probe. Cookie: GET /api/incidents із credentials:include після входу Alice на API origin.

| Стан | Origin | Простий GET | Preflight | Cookie fetch |
|---|---|---|---|---|
| До CORS | 4173 і 4174 | JS не читає відповідь | Немає дозволу; actual request не виконується | JS не читає відповідь |
| Fixed | 4173 | Читається, 200 | OPTIONS 204, дозволені GET і X-Lab-Cors-Probe; actual GET 200 | Читається, 200; точний origin і Allow-Credentials:true |
| Fixed | 4174 | HTTP-запит може дійти до API, JS не читає відповідь | OPTIONS без Allow-Origin; браузер не надсилає actual GET | JS не читає відповідь |
| Тимчасово без AllowCredentials | 4173 | Читається | Читається без credentials | Браузер не надає відповідь JavaScript |

Протокол короткого негативного контролю: [no-credentials-browser.json](evidence/lr05/no-credentials-browser.json). Після досліду Program.cs відновлено побайтно; повторний fixed-протокол підтверджує працюючий cookie flow. У network-фактах є лише cookiePresent, значення cookie не записуються.

Прямий HTTP-клієнт із Origin:4174 отримує 200 від /health без Allow-Origin: це доказ браузерної межі CORS, а не відсутності HTTP-відповіді. Анонімний browser context на дозволеному 4173 читає 401 захищеного endpoint: CORS не створює principal.

Політика Lab05BrowserProbe зареєстрована й застосована лише в Development, дозволяє тільки 4173, GET, X-Lab-Cors-Probe та credentials. Порядок: UseRouting → UseCors → UseAuthentication → UseAuthorization → UseRateLimiter. У Production навчальна CORS policy відсутня; є окремий інтеграційний тест.

## CSP та headers

Enforcement header: `default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'`.

Власні HTML-сторінки використовують зовнішні JS/CSS того самого origin; inline handlers та eval не потрібні. Вхід через UI, створення/читання інциденту та сторінки /, /browser-lab.html, /auth-lab.html перевірені під enforcement. Контроль new Image() із localhost:4173/x.png дав securitypolicyviolation img-src, disposition=enforce. Він не є XSS-доказом і не підмінює перевірку DOM.

- nosniff забороняє MIME-sniffing; не виправляє HTML sink самостійно.
- no-referrer не передає адресу поточної сторінки в Referer.
- frame-ancestors 'none' забороняє вбудовування сторінки у frame (захист від clickjacking).
- /scalar — вузький Development-only виняток із CSP через окремі вимоги документаційного UI. У Production endpoints Scalar/OpenAPI не реєструються. Основні клієнти винятку не мають.

## Відтворення й підсумок

Інтеграційні тести: `dotnet test tests/SecureLab.Api.Tests -c Release` після налаштування приватних ConnectionStrings__SecureLab і SeedUsers__Password та reset окремої навчальної БД. Фактичний результат: **90 passed, 0 failed**, [final-tests.txt](evidence/lr05/final-tests.txt).

Автоматичні browser-перевірки та залежності описано в [tests/browser/README.md](../tests/browser/README.md). Протоколи before/fixed є перевірками очікувань відповідного стану, а не твердженням, що vulnerable state безпечний. Після перевірок створені сценарієм інциденти видалено; seed-коментар залишено для демонстрації.

Повторювані ручні REG-XSS-01/REG-CORS-01 наведено вище. Рівень «Відмінно» не заявлено: окремий report-only цикл не виконувався. Автоматизація DOM додана як перевірка якості доброго рівня.

## Короткий review та межі

XSS — виконання тексту як коду в браузері; тут усунуто textContent. CORS — право іншого origin читати response у браузері. CSRF — небажана дія з автоматично доданими credentials; CORS не є повним CSRF-захистом, окремий CSRF-механізм у цій ЛР не додавався. Authorization — серверна перевірка права суб'єкта на ресурс; її збережено.

Локальний HTTP cookie-профіль призначений для лабораторії, не є production HTTPS-конфігурацією. Успадковані від ЛР4 маршрути, зокрема окремий пошук, не оголошуються повністю переаудитованими цією роботою. Докази обмежені зазначеними версіями, даними й маршрутами.
