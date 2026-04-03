# Технічна співбесіда: Full Stack .NET Engineer (AdaptiveCash)

**Тривалість**: 1 година 30 хвилин

---

## Блок 1: .NET платформа та основи C# (20 хв)

### Q1 (5 хв): Версії .NET
**Питання**: Ви працювали з .NET 5 по .NET 8. Які конкретні фічі або покращення ви реально почали використовувати при переході між версіями? Чи змінили якісь із них те, як ви структуруєте код?

**Оптимальна відповідь**:
- **.NET 6**: Minimal APIs для простих сервісів, `global using` для зменшення шуму, `file-scoped namespaces`, `DateOnly`/`TimeOnly` — зручно для фінансових систем
- **.NET 7**: `required` модифікатор для властивостей (гарантує ініціалізацію), покращення продуктивності LINQ, `INumber<T>` для generic math
- **.NET 8**: Primary constructors для класів (менше boilerplate в DI-сервісах), `FrozenDictionary`/`FrozenSet` для read-only колекцій конфігурації, `TimeProvider` для тестування часу, Native AOT для окремих мікросервісів
- **Nullable reference types** (з'явились у .NET 5 як opt-in) — увімкнув на всіх проєктах, значно зменшило NullReferenceException у runtime
- Головне: не просто оновив версію, а переглянув як пишу код — менше церемоніального коду, більше виразності

---

### Q2 (5 хв): IEnumerable vs IQueryable
**Питання**: Поясніть різницю між `IEnumerable<T>` та `IQueryable<T>`. У вашій роботі з EF Core — що станеться, якщо метод репозиторію поверне `IEnumerable<T>` замість `IQueryable<T>`, а далі по ланцюгу додається `.Where()` фільтр? Чому це важливо для системи, що обробляє великі обсяги банківських транзакцій?

**Оптимальна відповідь**:
- `IEnumerable<T>` — працює в пам'яті (LINQ-to-Objects). Фільтрація виконується на клієнті після завантаження ВСІХ записів із бази
- `IQueryable<T>` — будує Expression Tree, який транслюється у SQL-запит. Фільтрація виконується на сервері бази даних
- **Проблема**: якщо репозиторій повертає `IEnumerable<T>`, а наступний сервіс додає `.Where(x => x.ClientId == 42)`, то EF Core спочатку завантажить ВСІ записи в пам'ять і лише потім відфільтрує. Для таблиці з мільйонами банківських транзакцій це означає OutOfMemoryException або катастрофічну деградацію продуктивності
- **Ризик IQueryable**: якщо "витікає" за межі репозиторію, downstream код може додавати довільні фільтри, що ускладнює контроль над SQL-запитами. Компроміс: повертати `IQueryable` тільки в inter-repository шарі, а на рівні сервісу — матеріалізувати через `.ToListAsync()`

---

### Q3 (5 хв): DI Lifetimes
**Питання**: Поясніть lifecycles dependency injection у .NET: Transient, Scoped, Singleton. У вашій multi-tenant системі контекст тенанта (AccountId) визначався для кожного запиту. Який lifetime мав цей сервіс? Що піде не так, якщо його випадково зареєструвати як Singleton?

**Оптимальна відповідь**:
- **Transient**: новий екземпляр на кожен `Resolve()`. Для легких stateless сервісів
- **Scoped**: один екземпляр на scope (зазвичай = HTTP request у ASP.NET Core). Для DbContext, tenant context
- **Singleton**: один екземпляр на весь час життя додатку. Для HttpClient factories, конфігурації
- Tenant context — **обов'язково Scoped**, бо прив'язаний до конкретного HTTP-запиту (конкретного користувача конкретного банку)
- **Captive dependency problem**: якщо Singleton-сервіс залежить від Scoped tenant context, він "захопить" перший створений екземпляр і буде використовувати його для ВСІХ запитів. Результат: всі користувачі бачать дані першого банку, що зайшов після старту додатку — **критична вразливість безпеки** для банківської платформи
- ASP.NET Core кидає `InvalidOperationException` при `ValidateScopes = true` (увімкнено за замовчуванням у Development), але в Production ця перевірка вимкнена — баг може пройти в прод

---

### Q4 (5 хв): Async/Await
**Питання**: Ваші інтеграційні сервіси робили багато асинхронних HTTP-викликів. Поясніть, як async/await працює під капотом — що таке state machine? Яка різниця між `Task.Run` та природно асинхронним I/O-bound викликом? Коли ви б використовували кожен?

**Оптимальна відповідь**:
- Компілятор перетворює `async` метод у **state machine** (клас з полями для локальних змінних та switch-case для кожного `await`). Кожен `await` — це точка, де метод може "призупинитися" і повернути потік у thread pool
- **Природний async I/O** (`HttpClient.GetAsync`, `DbCommand.ExecuteReaderAsync`): використовує I/O Completion Ports (Windows) / epoll (Linux). Потік НЕ блокується — він повертається в пул і стає доступним для інших запитів
- **Task.Run**: бере потік із thread pool і блокує його на час виконання. Підходить для CPU-bound операцій (обчислення, парсинг великих JSON)
- Для HTTP-викликів до банківських API — **завжди природний async**, бо ми чекаємо відповіді від зовнішньої системи (I/O-bound). `Task.Run` тут навпаки шкідливий — марно витрачає потік
- **Deadlock**: класична проблема — `.Result` або `.Wait()` на async-методі в контексті з SynchronizationContext (ASP.NET classic, WPF). В ASP.NET Core цього немає (no sync context), але у WPF/Blazor Server — все ще актуально

---

## Блок 2: Архітектура та enterprise-проєктування (20 хв)

### Q5 (5 хв): Repository + UoW поверх EF Core
**Питання**: Ви згадували патерни Generic Repository та Unit of Work поверх EF Core. Є поширена думка, що DbContext вже реалізує обидва. Яка ваша позиція — чи використали б ви ці патерни знову на новому проєкті?

**Оптимальна відповідь**:
- **Факт**: `DbContext` = Unit of Work (`SaveChanges`), `DbSet<T>` = Repository
- **Аргументи ЗА додатковий шар**: абстракція від ORM (якщо потрібно замінити EF Core на NHibernate — як у AdaptiveCash), тестування (легше мокати інтерфейс ніж DbContext), обмеження доступних операцій (не даємо сервісам робити довільні запити)
- **Аргументи ПРОТИ**: leaky abstraction (EF Core специфіка все одно "протікає" — `Include`, `AsNoTracking`, `IQueryable`), подвійний рівень абстракції додає складність без цінності, Generic Repository часто стає "god interface" з методами, які не потрібні конкретному use case
- **Зріла позиція**: на новому проєкті я б використовував **специфічні репозиторії** (не generic) з чітко визначеними методами під конкретні use cases. Це дає абстракцію без generic anti-pattern. Unit of Work окремо — лише якщо потрібно координувати кілька репозиторіїв в одній транзакції

---

### Q6 (5 хв): Multi-tenant ізоляція
**Питання**: У вашій multi-tenant системі всі запити фільтрувалися по AccountId. Як саме це було реалізовано? Як ви запобігали тому, щоб розробник випадково написав запит, який пропускає фільтр тенанта?

**Оптимальна відповідь**:
- **EF Core Global Query Filters**: `modelBuilder.Entity<Order>().HasQueryFilter(o => o.AccountId == _tenantId)` — автоматично додає `WHERE AccountId = @tenantId` до КОЖНОГО запиту. Розробник не може "забути" фільтр
- Можна обійти через `IgnoreQueryFilters()` — але це явний opt-out, який видно на code review
- **Scoped DbContext**: tenant ID встановлюється в конструкторі DbContext через `ITenantProvider` (Scoped сервіс, що читає `ClaimsPrincipal` з `HttpContext`)
- **Для Dapper/raw SQL**: спеціальний `TenantAwareConnection` wrapper, який автоматично додає `@AccountId` параметр
- **Додаткові запобіжники**: кастомний Roslyn Analyzer або архітектурні тести (NetArchTest), які перевіряють, що всі запити до tenant-aware таблиць проходять через фільтрований DbContext
- **В AdaptiveCash контексті**: якщо один банк побачить дані іншого — це катастрофа. Тому global query filters + тести + code review — мінімально необхідний набір

---

### Q7 (5 хв): Resilience та Circuit Breaker
**Питання**: AdaptiveCash інтегрується з банківськими системами. Якщо зовнішня система починає відповідати повільно або падає — як запобігти деградації всієї платформи? Чи знайомі з патернами Circuit Breaker або бібліотекою Polly?

**Оптимальна відповідь**:
- **Circuit Breaker** (Polly / `Microsoft.Extensions.Http.Resilience` в .NET 8): три стани — Closed (нормальна робота), Open (після N помилок — відразу повертає fallback, не чекає таймауту), Half-Open (періодично пробує, чи зовнішня система відновилась)
- **Retry з exponential backoff**: `RetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))` — 2с, 4с, 8с. З jitter для уникнення thundering herd
- **Timeout**: обов'язково обмежити час очікування (наприклад, 30с для банківського API). Без цього повільна зовнішня система може вичерпати всі потоки thread pool
- **Bulkhead Isolation**: обмежити кількість одночасних запитів до зовнішньої системи (наприклад, SemaphoreSlim на 10 одночасних). Один повільний партнер не займе всі з'єднання
- **Health checks**: `IHealthCheck` для кожної інтеграції — моніторинг через `/health` endpoint
- **Fallback**: для некритичних інтеграцій — повернути кешовані дані або degraded response

---

### Q8 (5 хв): Дизайн модуля
**Питання**: Спроєктуйте модуль обробки заявок на інкасацію від кількох банківських клієнтів: валідація за бізнес-правилами, виклик зовнішнього банківського API, збереження результату. Як обробити ситуацію, коли зовнішній виклик падає після валідації?

**Оптимальна відповідь**:
- **Шари**: Controller → Application Service → Domain Validators → Repository + External API Client
- **Saga / Compensating Transaction**: валідація проходить → зовнішній виклик падає → записуємо заявку зі статусом `Failed` (не `Validated`) → повертаємо клієнту відповідь "прийнято, очікує підтвердження"
- **Outbox Pattern**: замість прямого виклику API зберігаємо "намір" в outbox-таблицю в тій же транзакції. Фоновий worker підхоплює і виконує виклик. Якщо падає — автоматичний retry
- **Ідемпотентність**: кожна заявка має `IdempotencyKey`. Повторний виклик з тим самим ключем не створює дублікат
- **Статусна модель**: `Received → Validated → Processing → Confirmed/Failed → Completed` (як у C4 діаграмі стану — state machine)
- **Audit trail**: КОЖНА зміна статусу записується (регуляторна вимога FinTech)

---

## Блок 3: База даних та ORM (15 хв)

### Q9 (5 хв): Bulk Insert + MERGE
**Питання**: Пройдіться по потоку: як дані потрапляють у тимчасову таблицю, що робить MERGE, як обробляєте конфлікти? Як цей підхід застосувати до банківських транзакцій?

**Оптимальна відповідь**:
```sql
-- 1. Створення тимчасової таблиці
CREATE TABLE #TempOrders (ExternalId NVARCHAR(50), Amount DECIMAL(18,2), ...);

-- 2. Bulk Insert (SqlBulkCopy в C#) — тисячі записів за секунди
-- SqlBulkCopy.WriteToServer(dataTable)

-- 3. MERGE з основною таблицею
MERGE Orders AS target
USING #TempOrders AS source ON target.ExternalId = source.ExternalId
WHEN MATCHED THEN UPDATE SET target.Amount = source.Amount, target.UpdatedAt = GETUTCDATE()
WHEN NOT MATCHED THEN INSERT (ExternalId, Amount, ...) VALUES (source.ExternalId, source.Amount, ...);
```
- **Конфлікти**: `WHEN MATCHED AND target.UpdatedAt < source.UpdatedAt` — оновлювати лише якщо source новіший (optimistic concurrency)
- **Для банківських транзакцій**: додатково — транзакційність (BEGIN TRAN / COMMIT), ідемпотентність через unique constraint на бізнес-ключі, аудит кожної зміни через OUTPUT clause у MERGE

---

### Q10 (5 хв): N+1 проблема
**Питання**: Що таке проблема N+1 запитів у EF Core? Як виявляєте та вирішуєте?

**Оптимальна відповідь**:
- **Що**: `var orders = dbContext.Orders.ToList(); foreach(var o in orders) { var client = o.Client.Name; }` → 1 запит для orders + N запитів для кожного client (lazy loading)
- **Рішення**: `.Include(o => o.Client)` (eager loading) — один JOIN-запит. Або проєкція `.Select(o => new { o.Id, ClientName = o.Client.Name })` — ще ефективніше
- **Split Queries**: `.AsSplitQuery()` в EF Core 5+ — замість одного великого JOIN робить кілька окремих запитів (корисно при Include кількох колекцій — уникає cartesian explosion)
- **Виявлення**: `optionsBuilder.LogTo(Console.WriteLine)` або MiniProfiler, або кастомний `IInterceptor` що рахує кількість запитів на scope
- **Конкретний приклад**: завантаження замовлень з товарами та клієнтами — без Include було 500+ запитів, з Include — 1-3

---

### Q11 (5 хв): NHibernate vs EF Core
**Питання**: Стек AdaptiveCash включає NHibernate. Чим NHibernate відрізняється від EF Core? Як плануєте освоїти?

**Оптимальна відповідь**:
- **Session vs DbContext**: NHibernate використовує `ISession` (аналог DbContext). `ISession.Flush()` ≈ `SaveChanges()`, але flush може відбутись автоматично перед запитом (auto-flush)
- **Mapping**: XML-маппінг (hbm.xml) або Fluent NHibernate (аналог Fluent API в EF Core). Менше конвенцій — більше явної конфігурації
- **Запити**: HQL (Hibernate Query Language — подібний до SQL, але оперує об'єктами), Criteria API (type-safe builder), QueryOver (fluent wrapper над Criteria), або LINQ (через NHibernate.Linq, але менш зрілий ніж у EF Core)
- **Переваги NHibernate**: кращий second-level cache, batch fetching strategies, зріліша підтримка database-agnostic коду (Oracle + MS SQL)
- **План навчання**: офіційна документація + існуючий код проєкту як reference + пара pet-task'ів для набуття hands-on досвіду за перший спринт
- **Чесно**: не працював з NHibernate, але ORM-концепції однакові. Головна різниця — в API та конфігурації, не в парадигмі

---

## Блок 4: Frontend та розробка SPA (10 хв)

### Q12 (5 хв): Продуктивність великих таблиць
**Питання**: Як забезпечуєте продуктивність при відображенні тисяч рядків у React-додатку? Різниця між client-side та server-side пагінацією? Як запобігти непотрібним рендерам?

**Оптимальна відповідь**:
- **Virtual scrolling (Windowing)**: використання бібліотек типу `react-window` або `react-virtualized` (або `ag-Grid`). Рендерить лише видимі рядки (~20-30 DOM елементів), решта підвантажуються або замінюються при скролі. DOM залишається легким.
- **Server-side**: дані завантажуються порціями з API (`skip/take`), фільтрація/сортування виконується на сервері. Для 100k+ записів — єдиний масштабований варіант.
- **Client-side**: завантажити всі дані одразу, фільтрація у браузері. Підходить для <5k записів.
- **Оптимізація рендерів у React**: використання `React.memo` для компонентів рядків таблиці, `useMemo` для мемоізації фільтрацій та важких обчислень, `useCallback` для прокидання колбеків дій як пропсів.
- **Додатково**: debounce на пошукових інпутах (300ms), використання `React Query` або `SWR` для кешування завантажених сторінок.

---

### Q13 (5 хв): React Hooks: useEffect, useReducer, useState, useContext
**Питання**: Поясніть ваш підхід до роботи з хуками. Коли краще використати `useReducer` замість `useState`? Які найпоширеніші помилки ви бачили при використанні `useEffect`? У яких випадках `useContext` стає проблемою для продуктивності?

**Оптимальна відповідь**:
- **useState vs useReducer**: `useState` підходить для простих примітивних станів (наприклад, `isOpen`). `useReducer` ідеальний, коли стан має складну структуру об'єкта (де кілька полів змінюються разом), або коли наступний стан містить складну логіку переходу (як кінцевий автомат).
- **useEffect та помилки**: Найчастіша проблема — використання `useEffect` для похідних даних (краще просто обчислювати під час рендеру або юзати `useMemo`) чи для синхронізації двох хуків `useState`. Інші болі — зламаний масив залежностей (stale closures / lint warnings), нескінченні рендери через об'єкти в залежностях, та забуті cleanup функції (витоки пам'яті через підписки/таймери).
- **useContext та продуктивність**: Відмінно працює для ініціалізації (роль, тема). Але КОЖНА зміна в Context Provider змушує ре-рендеритися ВСІ компоненти, які на нього підписані, навіть якщо їхня конкретна змінна не змінилася.
- **Як вирішити**: Розділяти контексти (наприклад, State Context окремо, Dispatch Context окремо) або використовувати інструменти типу Zustand, де підписка йде через атомарні селектори, що запобігає непотрібним рендерам.

---

## Блок 5: Лайв-кодинг (15 хв)

### Q14 (15 хв): Практичне завдання

Кандидат отримує репозиторій `adaptive-cash-interview` та має імплементувати `CashOrderProcessingService.ProcessBatchAsync()`.

**Базове завдання** (5 тестів):
- Алгоритмічна складність (O(N) vs O(N^2)), стан-гонка лімітів, thread-safety колекцій.

**Додаткові запитання після кодингу**:

1. *Як модифікувати, якщо ліміти різні для кожного клієнта?*
   → Вже реалізовано через `GetClientDailyLimitAsync` — fallback на default

2. *Два одночасні запити для одного клієнта — обидва проходять ліміт. Як?*
   → Оптимістичний/песимістичний lock на рівні БД. `SELECT ... WITH (UPDLOCK, HOLDLOCK)` або версіонність рядка. На рівні додатку — distributed lock (Redis) або serializable transaction

3. *Як покрити тестами?*
   → Mock репозиторій через Moq. Тест-кейси: happy path, кожен тип rejection, boundary cases (exactly at limit), mixed batch, running totals, empty batch, null input

**Опціонально (AI-челлендж)**: Генерація фронтенд-дашборду з використанням AI-інструментів. Див. `frontend-challenge/README.md`

---

## Блок 6: CI/CD, тестування та процеси (10 хв)

### Q15 (5 хв): CI/CD Pipeline
**Питання**: Опишіть досвід з пайплайнами. Які етапи очікуєте для enterprise FinTech платформи?

**Оптимальна відповідь**:
1. **Build**: `dotnet restore` → `dotnet build` → компіляція фронтенду
2. **Test**: unit tests → integration tests → code coverage gate (мінімум 80%)
3. **Static Analysis**: SonarQube/Roslyn analyzers — code smells, security vulnerabilities
4. **Security Scan**: OWASP dependency check, secret scanning
5. **Package**: Docker image / publish artifact
6. **Deploy to Staging**: автоматичний деплой, smoke tests
7. **Approval Gate**: manual approval для Production (обов'язково для банківських систем)
8. **Deploy to Production**: blue/green або canary deployment
9. **Post-deploy**: health checks, rollback trigger при помилках
- **Azure DevOps**: YAML pipelines, environments з approval policies, variable groups для секретів

### Q16 (5 хв): Тестування
**Питання**: Як підходите до тестування? Які тест-кейси написали б для сервісу з кодинг-завдання?

**Оптимальна відповідь**:
- **Фреймворки**: xUnit (prefer) або NUnit, Moq для мокування, FluentAssertions для читабельних перевірок
- **Unit тести для ProcessBatchAsync**:
  - Happy path: один валідний order → accepted
  - Валідація: negative amount → rejected, unsupported currency → rejected
  - Ліміт: exceeded → rejected, exactly at limit → rejected, below limit → accepted
  - Running total: два orders одного клієнта, другий перевищує → перший accepted, другий rejected
  - Persistence: accepted orders → `SaveOrdersAsync` called, all rejected → NOT called
  - Edge: empty batch → empty result, null → ArgumentNullException
- **Мокаємо**: `ICashOrderRepository` через Moq. `ILogger` — mock або `NullLogger`
- **Integration тести**: `WebApplicationFactory<Program>` для end-to-end через HTTP, in-memory database або Testcontainers для реальної БД

---

## Розподіл часу

| Блок | Тема | Тривалість |
|------|------|-----------|
| 1 | .NET платформа та основи C# | 20 хв |
| 2 | Архітектура та enterprise-проєктування | 20 хв |
| 3 | База даних та ORM | 15 хв |
| 4 | Frontend та розробка SPA | 10 хв |
| 5 | Лайв-кодинг | 15 хв |
| 6 | CI/CD, тестування та процеси | 10 хв |
| — | **Загалом** | **90 хв** |

---

## Рекомендації щодо оцінювання

Ключове питання: чи здатен кандидат перейти від marketplace/e-commerce до enterprise FinTech. Зверніть увагу на:

1. **Глибина за прескріном**: чи є реальне розуміння "чому", а не лише "що"
2. **Data consistency та concurrency**: чи мислить на рівні банківської платформи
3. **Здатність до навчання**: чесність щодо NHibernate/Oracle/FinTech gaps + план освоєння
4. **Якість trade-off аргументації** у Блоці 2
5. **Конкурентність** у follow-up до кодинг-завдання — найсильніший сигнал

### Шкала оцінки

| Рівень | Опис |
|--------|------|
| **Strong Hire** | Відповідає глибше ніж оптимальні відповіді, демонструє trade-off мислення, помічає проблеми продуктивності без підказок |
| **Hire** | Покриває більшість оптимальних відповідей, проходить базовий кодинг |
| **Lean Hire** | Покриває основи, кодинг працює, потребує менторства |
| **No Hire** | Не може пояснити DI lifetimes або async, кодинг не компілюється, поверхневі відповіді |
