# ЛР5: карта source → sink

Автор: Воропаєв Артем Максимович, КБ-41. Сценарій 2-A, рівень «Добре».
Корінь шляхів нижче — корінь репозиторію. Стенд: Development, localhost:5080, окрема БД securelab_lr05.

| Етап | Incident.Description | IncidentComment.Text |
|---|---|---|
| Source | Поле description форми `src/SecureLab.Api/Client/browser-lab.html`; POST /api/incidents у browser-lab.js | Локальний навчальний seed у `src/SecureLab.Api/Data/DbSeeder.cs`: публічний коментар 30000000-0000-0000-0000-000000000005 до I1 |
| Binding/transformations | `CreateIncidentRequest`, POST у `Scaffolding/Lab02Endpoints.cs`; перевірка довжини до 4000, Trim(), правила severity/title/date. Для marker Trim не змінює рядок | Seed записує канонічний marker у Text без перетворення на HTML. Це окремий seed-дослід, а не вигаданий HTTP POST коментаря |
| Storage | EF Core SaveChangesAsync → incidents.description | EF Core seed → incident_comments.text, is_internal=false |
| Access | GET /api/incidents/{id}, authentication та `IncidentAccess.Visible` обмежують вибірку | Коментарі отримуються лише разом із доступним батьківським інцидентом; внутрішні коментарі мають додатковий фільтр |
| Response | `Application/Incidents/IncidentQueries.cs`, GetDetailsAsync → `IncidentDetailsResponse.Description` | Той самий query → `IncidentCommentResponse.Text` у Comments |
| DTO | Обидва DTO оголошено в `Presentation/Contracts/IncidentResponses.cs`; JSON залишається даними | JSON-екранування не робить рядок придатним для innerHTML після JSON.parse |
| Sink до | `browser-lab.js`: description.innerHTML = current.description | paragraph.innerHTML = comment.text |
| Sink після | `renderPlainText(description, current.description)` | createElement('p') → renderPlainText(paragraph, comment.text) → append |
| Спільний helper | `Client/safe-render.js`: element.textContent = value ?? "" | Той самий helper; жодного розбору HTML |

## Першопричина та межа виправлення

Дані проходили серверну валідацію й зберігалися в PostgreSQL, але innerHTML повторно тлумачив їх як розмітку. Для опису й коментарів контракт дозволяє лише звичайний текст. Тому виправлено обидва місця виведення через textContent, а не заблоковано слово script. Назва вже виводилася через textContent; значення textarea встановлюється через value. Після помилки читання попередня картка очищується.

Marker змінює тільки атрибут прихованого локального елемента #lab05-marker-target; не читає cookie, паролі чи інші дані. Стан до містив img та data-lab05-marker=executed. Після — точний текст marker, нуль img і відсутній атрибут. Окремий браузерний контроль із bypass_csp підтвердив, що безпечним став сам DOM sink.

## Межі довіри

Збереження в БД не перетворює введений текст на довірений HTML. Валідація довжини й бізнес-правил не замінює безпечний DOM API. CSP є додатковим захистом; CORS визначає доступність відповіді для іншого browser origin, а authorization — право користувача читати конкретний інцидент.

Докази й відтворення: [browser-security-evidence.md](browser-security-evidence.md).
