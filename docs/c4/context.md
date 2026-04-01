# C4 Model: Context Diagram

## System Context — AdaptiveCash Platform

This diagram shows the AdaptiveCash platform in the context of its users and external systems.

```mermaid
C4Context
    title AdaptiveCash Platform — System Context

    Person(bankOperator, "Bank Operator", "Manages cash orders, monitors operations, configures limits")
    Person(partnerUser, "Partner User", "Cash-in-transit company operator submitting orders via portal")

    System(adaptiveCash, "AdaptiveCash Platform", "Enterprise FinTech platform automating cash management for banks and CIT companies")

    System_Ext(bankCore, "Core Banking System", "Bank's core system for account management and settlement")
    System_Ext(partnerApi, "Partner Integration API", "External partner systems sending cash order requests")
    System_Ext(emailService, "Email / SMS Gateway", "Notification delivery infrastructure")
    System_Ext(regulatorySystem, "Regulatory Reporting System", "Financial regulator's data submission endpoint")

    Rel(bankOperator, adaptiveCash, "Manages orders, views dashboards, configures limits", "HTTPS")
    Rel(partnerUser, adaptiveCash, "Submits cash orders, views status", "HTTPS")
    Rel(adaptiveCash, bankCore, "Confirms orders, checks balances, settles transactions", "REST API / SOAP")
    Rel(partnerApi, adaptiveCash, "Submits batch orders", "REST API")
    Rel(adaptiveCash, emailService, "Sends notifications", "SMTP / REST")
    Rel(adaptiveCash, regulatorySystem, "Submits compliance reports", "Secure File Transfer")
```

## Key Relationships

| From | To | Interaction | Protocol |
|------|----|-------------|----------|
| Bank Operator | AdaptiveCash | Order management, dashboards | HTTPS (SPA) |
| Partner User | AdaptiveCash | Order submission, status tracking | HTTPS (SPA) |
| Partner Integration API | AdaptiveCash | Batch order submission | REST API |
| AdaptiveCash | Core Banking System | Order confirmation, settlement | REST / SOAP |
| AdaptiveCash | Email/SMS Gateway | Notifications | SMTP / REST |
| AdaptiveCash | Regulatory Reporting | Compliance reports | SFTP |

## Notes

- The platform serves **multiple bank clients** (multi-tenant architecture).
- Each bank client may have multiple partner companies using the platform.
- All interactions with external systems must be **auditable** for regulatory compliance.
- The Core Banking System integration is critical-path: if it's unavailable, orders cannot be confirmed.
