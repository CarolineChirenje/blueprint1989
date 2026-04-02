# Vitara Push Notifications Reference

All push notifications fall into two delivery types:

| Type | How it works | Survives closed browser? |
|------|-------------|--------------------------|
| **VAPID server-sent** | Server sends via Web Push Protocol; browser push service delivers it | ✅ Yes |
| **Local SW scheduled** | Service worker fires a `showNotification` after a `setTimeout` | ❌ No — requires tab/browser to be open |

Each server-sent notification also creates an **in-app Notification row** in the database (via `NotificationService.CreateAsync`), which appears in the in-app notification bell.

---

## Server-sent VAPID Notifications

### 1. Assessment Alert — CarerNotified
| Field | Value |
|-------|-------|
| **Trigger** | Assessment saved with outcome `CarerNotified` (BGL still low after max treatment cycles) |
| **Sent to** | All carers linked to the care recipient |
| **Title** | `Assessment Alert` |
| **Body** | `{careRecipientName}: {hypoglycemia/hyperglycemia} assessment requires your attention. BGL: X.X mmol/L.` |
| **Deep link** | `/features/assessment/{id}` |
| **Source** | `AssessmentController.cs` → `POST /api/assessment` |
| **NotificationType** | `AssessmentSeverity (3)` |

---

### 2. Assessment Alert — EmergencyRequiredCarerNotified
| Field | Value |
|-------|-------|
| **Trigger** | Assessment saved with outcome `EmergencyRequiredCarerNotified` (critical hypoglycemia — reading returned "LOW") |
| **Sent to** | All carers linked to the care recipient |
| **Title** | `Assessment Alert` |
| **Body** | `{careRecipientName}: Assessment completed. BGL: X.X mmol/L. Immediate attention is required.` |
| **Deep link** | `/features/assessment/{id}` |
| **Source** | `AssessmentController.cs` → `POST /api/assessment` |
| **NotificationType** | `AssessmentSeverity (3)` |

---

### 3. Blood Pressure Alert — Critical
| Field | Value |
|-------|-------|
| **Trigger** | Blood pressure session saved with `Severity == Critical` |
| **Sent to** | All carers linked to the care recipient |
| **Title** | `Critical Blood Pressure Alert` |
| **Body** | `{careRecipientName}: {systolic}/{diastolic} mmHg — {category label} (Critical). Immediate attention required.` |
| **Deep link** | `/blood-pressure/{id}` |
| **Source** | `BloodPressureController.cs` → `POST /api/blood-pressure` |
| **NotificationType** | `BloodPressureAlert (not yet in enum — check NotificationType.cs)` |

---

### 4. Incident Alert — High or Critical BGL
| Field | Value |
|-------|-------|
| **Trigger** | Incident saved with `Severity >= High` **and** BGL state is not Normal |
| **Sent to** | All carers linked to the care recipient |
| **Title** | `High {Hypoglycemia/Hyperglycemia} Alert` or `Critical {Hypoglycemia/Hyperglycemia} Alert` |
| **Body** | `BGL reading of X.X mmol/L recorded. Immediate attention may be required.` |
| **Deep link** | `/features/incident/entries/{id}` |
| **Source** | `IncidentController.cs` → `POST /api/incident` |
| **NotificationType** | `IncidentSeverity (2)` |

---

### 5. BGL Recheck Reminder (Server-scheduled background timer)
| Field | Value |
|-------|-------|
| **Trigger** | 2-hour ketone monitoring timer started in the hyperglycemia assessment flow; `BgTimerHostedService` polls every 30 s and fires when the scheduled time passes |
| **Sent to** | The user who started the timer (support worker / carer) |
| **Title** | `BGL Recheck Reminder` |
| **Body** | `Time to recheck blood glucose for {careRecipientName}. Please perform a ketone test.` |
| **Deep link** | `/features/incident/entries?careRecipientId={id}` |
| **Source** | `PushController.cs` → `POST /api/push/bg-timer/schedule` (called from frontend `start2HourTimer()`) · `BgTimerHostedService.cs` (delivers it) |
| **NotificationType** | `BgTimerReminder (6)` |
| **Cancellation** | `DELETE /api/push/bg-timer/cancel` — called automatically when user skips the timer or countdown reaches zero while app is open |

---

### 6. Urgent Ketone Alert (Manual)
| Field | Value |
|-------|-------|
| **Trigger** | Support worker manually triggers via `POST /api/notification/ketone-alert` with a ketone reading ≥ 0.6 mmol/L |
| **Sent to** | All carers linked to the care recipient |
| **Title** | `⚠️ Urgent: High Ketone Reading` |
| **Body** | `Support worker is recording a ketone level of X.X mmol/L. Please check in immediately.` |
| **Deep link** | `/features/incident` |
| **Source** | `NotificationController.cs` → `POST /api/notification/ketone-alert` |
| **NotificationType** | `IncidentSeverity (2)` |
| **Role required** | `SupportOrHigher` |

---

### 7. Delink Request Approved — Carer
| Field | Value |
|-------|-------|
| **Trigger** | Admin approves a delink request |
| **Sent to** | The carer being delinked |
| **Title** | `Delink Request Approved` |
| **Body** | `Your link to {careRecipientName} has been removed by an administrator.` |
| **Deep link** | `/features/profile` |
| **Source** | `DelinkRequestService.cs` → `ApproveOrDenyAsync()` |
| **NotificationType** | `DelinkApproved (7)` |

---

### 8. Delink Request Approved — Care Recipient
| Field | Value |
|-------|-------|
| **Trigger** | Admin approves a delink request |
| **Sent to** | The care recipient |
| **Title** | `Delink Request Approved` |
| **Body** | `Your request to remove {carerName} from your care team has been approved.` |
| **Deep link** | `/features/profile` |
| **Source** | `DelinkRequestService.cs` → `ApproveOrDenyAsync()` |
| **NotificationType** | `DelinkApproved (7)` |

---

### 9. Delink Request Denied — Care Recipient
| Field | Value |
|-------|-------|
| **Trigger** | Admin denies a delink request |
| **Sent to** | The care recipient who submitted the request |
| **Title** | `Delink Request Denied` |
| **Body** | `Your request to remove {carerName} from your care team was denied by an administrator.` |
| **Deep link** | `/features/profile` |
| **Source** | `DelinkRequestService.cs` → `ApproveOrDenyAsync()` |
| **NotificationType** | `DelinkDenied (8)` |

---

### 10. Access Removed — Carer (Admin action)
| Field | Value |
|-------|-------|
| **Trigger** | Admin manually delinks a carer via `DELETE /api/carerecipient/admin/{careRecipientId}/linked-carer/{carerId}` |
| **Sent to** | The carer |
| **Title** | `Access Removed` |
| **Body** | `You have been removed as a carer for {careRecipientName}.` |
| **Deep link** | `/features/profile` |
| **Source** | `CareRecipientController.cs` → `DelinkCarerAdmin()` |
| **NotificationType** | `CarerRemoved (4)` |

---

### 11. Carer Removed — Care Recipient (Admin action)
| Field | Value |
|-------|-------|
| **Trigger** | Admin manually delinks a carer (same action as #10, other party) |
| **Sent to** | The care recipient |
| **Title** | `Carer Removed` |
| **Body** | `{carerName} has been removed from your care team.` |
| **Deep link** | `/features/profile` |
| **Source** | `CareRecipientController.cs` → `DelinkCarerAdmin()` |
| **NotificationType** | `CareRecipientRemoved (5)` |

---

### 12. Carer Removed — Care Recipient (Self-delink)
| Field | Value |
|-------|-------|
| **Trigger** | A carer removes themselves via `DELETE /api/carerecipient/{careRecipientId}` |
| **Sent to** | The care recipient |
| **Title** | `Carer Removed` |
| **Body** | `{carerName} has removed themselves from your care team.` |
| **Deep link** | `/features/profile` |
| **Source** | `CareRecipientController.cs` → `RemoveCareRecipient()` |
| **NotificationType** | `CareRecipientRemoved (5)` |

---

### 13. Test Push (Dev / Admin only)
| Field | Value |
|-------|-------|
| **Trigger** | Manual trigger via `POST /api/push/test` |
| **Sent to** | The requesting user only |
| **Title** | Custom (provided in request body) |
| **Body** | Custom (provided in request body) |
| **Deep link** | Custom (optional) |
| **Source** | `PushController.cs` → `SendTestPush()` |
| **NotificationType** | Custom (provided in request body) |
| **Role required** | Any role in Development; `SuperAdmin` or `Administrator` in Production |

---

## Local Service Worker Notification

### 14. Ketone Recheck Reminder (Local SW fallback)
| Field | Value |
|-------|-------|
| **Trigger** | 2-hour ketone monitoring timer started; fires after the countdown completes **within the service worker** using `setTimeout` |
| **Sent to** | N/A — local browser notification only; no server involved |
| **Title** | `Vitara Reminder` |
| **Body** | `Time to recheck your ketone levels after 2 hours of monitoring.` |
| **Source** | `sw-notification.service.ts` → `scheduleNotification()` → `custom-sw.js` |
| **Note** | This fires only if the browser/service worker is still active. Notification #5 (VAPID) is the reliable fallback for closed browsers. Both are scheduled simultaneously when the 2-hour timer starts. |
| **Cancellation** | SW receives a `CANCEL_NOTIFICATION` message and clears the `setTimeout`; also cleared from `localStorage`. |

---

## NotificationType Enum Reference

Defined in `server/src/Vitara.Api/Models/NotificationType.cs` and mirrored in `client/src/app/core/services/push-notification.service.ts`:

| Value | Name | Used by |
|-------|------|---------|
| 1 | `General` | (reserved) |
| 2 | `IncidentSeverity` | Notifications #4, #6 |
| 3 | `AssessmentSeverity` | Notifications #1, #2 |
| 4 | `CarerRemoved` | Notification #10 |
| 5 | `CareRecipientRemoved` | Notifications #11, #12 |
| 6 | `BgTimerReminder` | Notification #5 |
| 7 | `DelinkApproved` | Notifications #7, #8 |
| 8 | `DelinkDenied` | Notification #9 |

---

## Adding a New Notification

1. Optionally add a new value to `NotificationType.cs` (server) and mirror it in `push-notification.service.ts` (client)
2. Call `await _pushSender.SendToUserAsync(...)` or `SendToUsersAsync(...)` from the relevant controller or service
3. The sender automatically creates an in-app notification row and dispatches VAPID push to all registered devices for the target user(s)
4. Update this document
