# Frontend Challenge: AdaptiveCash Dashboard (Optional)

## 🎯 Goal

Using AI tools (Cursor, GitHub Copilot, ChatGPT, etc.), generate a frontend dashboard for the AdaptiveCash Order Processing Service. The dashboard should provide an ergonomic visualization of the system's current state, workflow, and data flow.

**Time**: Use up to half of the remaining interview time (~7-8 minutes).
**AI tools**: Allowed and encouraged for this part.

## 📋 Requirements

Build a single-page dashboard (React, Angular, or Vue — your choice) that displays:

### 1. Order Processing KPI Cards
- Total orders processed today
- Number of accepted orders
- Number of rejected orders
- Current daily volume (total amount across all clients)

### 2. Workflow Visualization
Show the order lifecycle state machine from the C4 Component diagram:
```
Received → Validated → Processing → Confirmed → Completed
                ↘ Rejected
```
Display the count of orders in each state. Use color-coding:
- 🟢 Green: Validated, Confirmed, Completed
- 🟡 Yellow: Received, Processing
- 🔴 Red: Rejected, Failed

### 3. Data Flow Diagram
Interactive visualization showing how data flows through the system:
- API Gateway → Validation → Limit Check → Persistence → Audit Trail
- Show real-time status indicators for each component (healthy/degraded/down)

### 4. Client Activity Table
Sortable table with columns:
- Bank Client ID
- Client Name
- Orders Today
- Volume Today
- Daily Limit
- Utilization % (with progress bar)

### 5. Recent Activity Feed
Live-updating list of the latest orders with:
- Timestamp
- Client
- Amount + Currency
- Status (accepted/rejected)
- Rejection reason (if applicable)

## 🎨 Design Reference

See the mockup images in the `mockups/` directory:

- `mockups/dashboard_main.png` — Main dashboard layout with KPIs, charts, and data table
- `mockups/dashboard_dataflow.png` — System architecture and data flow monitoring view

### Design Guidelines
- **Dark mode** with navy background (#0f172a)
- **Accent colors**: Cyan (#22d3ee) for primary, Emerald (#10b981) for success, Rose (#f43f5e) for errors
- **Glassmorphism** cards with subtle backdrop blur
- **Modern typography**: Inter or similar sans-serif font
- Use mock/sample data — no real API integration needed

## 📊 Sample Data

Use this mock data for the dashboard:

```typescript
const mockOrders = [
  { id: "ORD-001", bankClientId: 1, clientName: "PrivatBank", amount: 150000, currency: "UAH", status: "Validated", timestamp: "2024-06-15T09:30:00Z" },
  { id: "ORD-002", bankClientId: 2, clientName: "Raiffeisen", amount: 75000, currency: "EUR", status: "Validated", timestamp: "2024-06-15T09:31:00Z" },
  { id: "ORD-003", bankClientId: 1, clientName: "PrivatBank", amount: 500000, currency: "UAH", status: "Rejected", reason: "Daily limit exceeded", timestamp: "2024-06-15T09:32:00Z" },
  { id: "ORD-004", bankClientId: 3, clientName: "UkrSibbank", amount: 200000, currency: "USD", status: "Processing", timestamp: "2024-06-15T09:33:00Z" },
  { id: "ORD-005", bankClientId: 4, clientName: "OTP Bank", amount: -5000, currency: "EUR", status: "Rejected", reason: "Invalid amount", timestamp: "2024-06-15T09:34:00Z" },
  { id: "ORD-006", bankClientId: 2, clientName: "Raiffeisen", amount: 120000, currency: "EUR", status: "Completed", timestamp: "2024-06-15T09:35:00Z" },
  { id: "ORD-007", bankClientId: 5, clientName: "Crédit Agricole", amount: 300000, currency: "CHF", status: "Confirmed", timestamp: "2024-06-15T09:36:00Z" },
  { id: "ORD-008", bankClientId: 1, clientName: "PrivatBank", amount: 50000, currency: "USD", status: "Validated", timestamp: "2024-06-15T09:37:00Z" },
];

const mockClients = [
  { id: 1, name: "PrivatBank", ordersToday: 45, volumeToday: 3200000, dailyLimit: 5000000, currency: "UAH" },
  { id: 2, name: "Raiffeisen", ordersToday: 23, volumeToday: 890000, dailyLimit: 1000000, currency: "EUR" },
  { id: 3, name: "UkrSibbank", ordersToday: 12, volumeToday: 450000, dailyLimit: 500000, currency: "USD" },
  { id: 4, name: "OTP Bank", ordersToday: 8, volumeToday: 120000, dailyLimit: 500000, currency: "EUR" },
  { id: 5, name: "Crédit Agricole", ordersToday: 15, volumeToday: 780000, dailyLimit: 1000000, currency: "CHF" },
];

const systemHealth = {
  apiGateway: { status: "healthy", latencyMs: 12, uptimePercent: 99.98 },
  validationService: { status: "healthy", latencyMs: 3, uptimePercent: 99.99 },
  orderProcessor: { status: "healthy", latencyMs: 45, uptimePercent: 99.95 },
  auditTrailService: { status: "healthy", latencyMs: 8, uptimePercent: 99.99 },
  database: { status: "healthy", latencyMs: 5, uptimePercent: 99.97 },
  cache: { status: "degraded", latencyMs: 120, uptimePercent: 98.50 },
};
```

## ✅ Evaluation Criteria

| Criterion | Weight |
|-----------|--------|
| Visual quality and UX | 30% |
| Effective use of AI tools | 25% |
| Data presentation clarity | 25% |
| Code organization | 20% |

This is about demonstrating your ability to rapidly prototype a professional-looking UI with AI assistance, not about writing every line by hand.
