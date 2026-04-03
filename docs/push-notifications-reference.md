# Divvy Push Notifications Reference

All push notifications fall into two delivery types:

| Type | How it works | Survives closed browser? |
|------|-------------|--------------------------|
| **VAPID server-sent** | Server sends via Web Push Protocol; browser push service delivers it | ✅ Yes |
| **Local SW scheduled** | Service worker fires a `showNotification` after a `setTimeout` | ❌ No — requires tab/browser to be open |

Each server-sent notification also creates an **in-app Notification row** in the database (via `NotificationService.CreateAsync`), which appears in the in-app notification bell.

---

## Server-sent VAPID Notifications

### 1. Payment Due
| Field | Value |
|-------|-------|
| **Trigger** | A member obligation is calculated when a cycle closes |
| **Sent to** | The debtor member |
| **Title** | `Payment Due` |
| **Body** | `You owe {creditorName} {amount} in cycle {cycleName}.` |
| **Deep link** | `/cycles/{id}/obligations` |
| **Source** | `ExpenseCycleController.cs` → `POST /api/expense-cycle/{id}/close` |
| **NotificationType** | `PaymentDue (2)` |

---

### 2. Payment Received
| Field | Value |
|-------|-------|
| **Trigger** | A payment is confirmed by Admin or creditor |
| **Sent to** | The creditor member |
| **Title** | `Payment Received` |
| **Body** | `{debtorName} has paid you {amount}.` |
| **Deep link** | `/payments/{id}` |
| **Source** | `PaymentController.cs` → `PATCH /api/payment/{id}/confirm` |
| **NotificationType** | `PaymentReceived (3)` |

---

### 3. Cycle Created
| Field | Value |
|-------|-------|
| **Trigger** | A new expense cycle is created |
| **Sent to** | All members added to the cycle |
| **Title** | `New Expense Cycle` |
| **Body** | `{cycleName} is now active. Start adding expenses.` |
| **Deep link** | `/cycles/{id}` |
| **Source** | `ExpenseCycleController.cs` → `POST /api/expense-cycle` |
| **NotificationType** | `CycleCreated (4)` |

---

### 4. System Restart
| Field | Value |
|-------|-------|
| **Trigger** | An Admin triggers a system restart via `POST /api/system/restart` |
| **Sent to** | All users |
| **Title** | `System Restart` |
| **Body** | `Divvy will restart shortly for maintenance. Please save your work.` |
| **Deep link** | (none) |
| **Source** | `SystemController.cs` → `POST /api/system/restart` |
| **NotificationType** | `SystemRestart (5)` |

---

### 5. General
| Field | Value |
|-------|-------|
| **Trigger** | Any ad-hoc notification sent by the system or admin |
| **Sent to** | Targeted user(s) |
| **Title** | `Divvy` |
| **Body** | Custom message |
| **Deep link** | Optional |
| **Source** | `NotificationService.CreateAsync` called directly |
| **NotificationType** | `General (1)` |

---

## Test Push Notifications

Use the push test panel at `/push-test` (Admin only in production) to send a test push of any type to your own account. Select notification type and optionally provide custom title and body.

- Dev: Available to all roles.
- Production: Requires `Admin` or `SuperAdmin`.
