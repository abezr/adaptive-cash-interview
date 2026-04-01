# C4 Model: Container Diagram

## AdaptiveCash Platform — Containers

This diagram shows the major containers (deployable units) that make up the AdaptiveCash platform.

```mermaid
C4Container
    title AdaptiveCash Platform — Container Diagram

    Person(bankOperator, "Bank Operator", "Manages cash orders")
    Person(partnerUser, "Partner User", "Submits orders")

    System_Boundary(adaptiveCash, "AdaptiveCash Platform") {
        Container(clientPortal, "Client Portal SPA", "React / Angular", "Bank operator dashboard for managing orders, limits, and reports")
        Container(partnerPortal, "Partner Portal SPA", "React / Angular", "Partner-facing portal for order submission and tracking")
        Container(webApi, "Web API", "ASP.NET Core", "REST API serving portals and external integrations. Contains all business logic.")
        Container(backgroundWorker, "Background Worker", ".NET Worker Service", "Processes async tasks: scheduled syncs, report generation, notifications")
        ContainerDb(database, "Database", "MS SQL / Oracle", "Stores orders, clients, limits, audit trail, configurations")
        ContainerDb(cache, "Cache", "Redis", "Session data, frequently accessed configs, rate limiting")
    }

    System_Ext(bankCore, "Core Banking System", "Order confirmation and settlement")
    System_Ext(partnerApi, "Partner Integration API", "External partner systems")
    System_Ext(emailService, "Email / SMS Gateway", "Notifications")

    Rel(bankOperator, clientPortal, "Uses", "HTTPS")
    Rel(partnerUser, partnerPortal, "Uses", "HTTPS")
    Rel(clientPortal, webApi, "API calls", "HTTPS / JSON")
    Rel(partnerPortal, webApi, "API calls", "HTTPS / JSON")
    Rel(partnerApi, webApi, "Submits orders", "REST API")
    Rel(webApi, database, "Reads/Writes", "ADO.NET / NHibernate")
    Rel(webApi, cache, "Reads/Writes", "StackExchange.Redis")
    Rel(webApi, bankCore, "Confirms orders", "REST / SOAP")
    Rel(backgroundWorker, database, "Reads/Writes", "ADO.NET / NHibernate")
    Rel(backgroundWorker, emailService, "Sends notifications", "SMTP / REST")
    Rel(backgroundWorker, webApi, "Internal API calls", "HTTPS")
```

## Container Descriptions

| Container | Technology | Responsibility |
|-----------|-----------|----------------|
| **Client Portal SPA** | React or Angular + TypeScript | Bank operator interface: dashboards, order management, limit configuration, reports |
| **Partner Portal SPA** | React or Angular + TypeScript | Partner interface: order submission, status tracking, delivery scheduling |
| **Web API** | ASP.NET Core 8, REST | Central business logic: order processing, validation, limit enforcement, auth, audit |
| **Background Worker** | .NET Worker Service | Async processing: scheduled tasks, notification dispatching, report generation |
| **Database** | MS SQL Server or Oracle | Persistent storage for all domain data |
| **Cache** | Redis | Performance optimization: session state, hot configurations, rate limiting counters |

## Key Design Decisions

1. **Monolithic API with modular internals** — The Web API is a single deployable unit but internally organized by feature modules. This simplifies deployment for banking customers who prefer on-premise installations.
2. **Dual SPA portals** — Separate portals for bank operators and partners to enforce different access models and UX flows.
3. **Background Worker** — Decoupled from the API to handle long-running or scheduled operations without blocking request processing.
4. **Database agnosticism** — NHibernate is used as ORM specifically to support both MS SQL and Oracle, as different bank clients use different database engines.
