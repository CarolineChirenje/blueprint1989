# Export & Reports

## Overview

The Export & Reports feature provides data export capabilities for clinical and nutritional records. It generates Excel (`.xlsx`) workbooks using **ClosedXML** and supports optional upload to **Google Drive**. Five dedicated report types are available: Diabetes Incidents, Blood Pressure Incidents, BGL Assessments, Meal Entries (Bolus History), and a combined All-Data report. All reports are scoped to a specific Care Recipient and filtered by a date range supplied by the caller.

The Google Drive integration uses OAuth 2.0 with offline access (refresh tokens), enabling server-side background uploads without requiring human interaction at export time. Drive credentials are persisted in the database against the requesting user's profile.

---

## Role Access

| Role | Access |
|---|---|
| SuperAdmin | All CRs |
| Administrator | All CRs |
| Carer | Linked CRs only |
| SupportWorker | Linked CRs only |
| CareRecipient | Own data only |
| HealthCareProvider | Linked CRs only |

---

## Backend

### Controller — `ExportController`

**File:** `server/src/Vitara.Api/Controllers/ExportController.cs`

Base route: `/api/export`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/incident` | `AllRoles` | Export Diabetes Incident report |
| GET | `/bp-incident` | `AllRoles` | Export BP Incident report |
| GET | `/assessment` | `AllRoles` | Export BGL Assessment report |
| GET | `/meal-entry` | `AllRoles` | Export Meal Entry (Bolus History) report |
| POST | `/upload-to-drive` | `AllRoles` | Upload the most recent export to Google Drive |
| GET | `/drive-status` | `AllRoles` | Check whether Drive credentials are saved |

All GET export endpoints accept the following query parameters:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `careRecipientId` | `int` | Yes | Target CR |
| `startDate` | `DateTime` | No | Filter from date (inclusive) |
| `endDate` | `DateTime` | No | Filter to date (inclusive) |

The response is a file download with `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.

---

### Service — `ExportService`

**File:** `server/src/Vitara.Api/Services/ExportService.cs`

#### Key Methods

| Method | Description |
|---|---|
| `GenerateIncidentReportAsync(careRecipientId, startDate, endDate)` | Builds `.xlsx` for diabetes incidents |
| `GenerateBpIncidentReportAsync(careRecipientId, startDate, endDate)` | Builds `.xlsx` for BP incidents |
| `GenerateAssessmentReportAsync(careRecipientId, startDate, endDate)` | Builds `.xlsx` for BGL assessments |
| `GenerateMealEntryReportAsync(careRecipientId, startDate, endDate)` | Builds `.xlsx` for meal entries |
| `UploadToGoogleDriveAsync(userId, fileBytes, fileName)` | Uploads bytes to user's Google Drive |
| `GetDriveStatusAsync(userId)` | Returns whether Drive OAuth tokens are stored |

#### Excel Report Generation (ClosedXML)

Each `Generate*Async` method:

1. Queries the database using EF Core, filtered by `careRecipientId`, `startDate`, and `endDate`.
2. Creates a new `XLWorkbook` with a named worksheet.
3. Writes a styled header row (bold, background colour).
4. Iterates all data rows, writing each column.
5. Auto-fits column widths.
6. Saves the workbook to a `MemoryStream` and returns the byte array.

#### Incident Report Columns

`Date | Time | Type | BGL Reading | Ketone Reading | Symptoms | Actions Taken | Notes | Recorded By`

#### BP Incident Report Columns

`Date | Time | Systolic | Diastolic | Pulse Rate | Classification | Symptoms | Actions Taken | Notes | Recorded By`

#### BGL Assessment Report Columns

`Date | Time | BGL Reading | State | Severity | Protocol Steps | Notes | Recorded By`

#### Meal Entry Report Columns

`Date | Time | Meal Type | Carbohydrates (g) | Bolus Units | Notes | Recorded By`

File name pattern: `<Report_Type>_Report_<YYYY-MM-DD>.xlsx` (date = today).

---

### Google Drive Upload

#### Flow

1. Caller sends `POST /export/upload-to-drive` with a JSON body:
   ```json
   { "careRecipientId": 5, "reportType": "incident", "startDate": "2026-01-01", "endDate": "2026-03-31" }
   ```
2. `ExportController` calls `ExportService.UploadToGoogleDriveAsync(userId, fileBytes, fileName)`.
3. `ExportService` loads `GoogleDriveCredential` for the user from the database.
4. Constructs a `GoogleCredential` with the stored access + refresh token.
5. Initialises the `DriveService` (Google.Apis.Drive.v3).
6. If the access token is expired, the Google client library automatically refreshes it using the stored `RefreshToken` and updates the persisted record.
7. Uploads the file as `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
8. Returns the Google Drive file ID and view URL.

#### Drive Credential Model

**File:** `server/src/Vitara.Api/Models/GoogleDriveCredential.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `UserId` | `int` | FK → User |
| `AccessToken` | `string` | OAuth access token |
| `RefreshToken` | `string` | Long-lived refresh token |
| `ExpiresAt` | `DateTime` | Access token expiry |
| `CreatedAt` | `DateTime` | Row created |
| `UpdatedAt` | `DateTime` | Row updated |

---

### Google Drive Auth Controller — `GoogleDriveAuthController`

**File:** `server/src/Vitara.Api/Controllers/GoogleDriveAuthController.cs`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/google-drive/auth-url` | `AllRoles` | Returns OAuth consent URL with redirect URI |
| GET | `/google-drive/callback` | — (redirect target) | Exchanges code for tokens, saves to DB, redirects to profile |
| GET | `/google-drive/status` | `AllRoles` | Returns `{ connected: bool }` |
| DELETE | `/google-drive/disconnect` | `AllRoles` | Deletes stored credential for the user |

OAuth flow:
1. Frontend calls `GET /google-drive/auth-url`, opens the returned URL in a new browser tab.
2. User grants access on Google's consent screen.
3. Google redirects to `GET /google-drive/callback?code=...`.
4. Server exchanges `code` for tokens, saves `GoogleDriveCredential`, then sends an HTTP 302 redirect to the client profile page with a `?drive=connected` query parameter.
5. `ProfileComponent` reads the query param and shows a success snackbar.

---

## Frontend

### Export Entry Points

Exports can be triggered from two locations:

1. **Feature History Pages:** Each history component (e.g., `MealEntryHistoryComponent`, `BglHistoryComponent`) has an "Export" button that calls the relevant export endpoint directly and triggers a browser file download.

2. **My Reports Page (`/profile/reports`):** `MyReportsComponent` shows a dedicated form with a report type selector, care recipient selector, and date range pickers, then calls the export endpoint.

---

### MyReportsComponent

**File:** `client/src/app/features/profile/components/my-reports.component.ts`

#### Form Controls

| Control | Type | Notes |
|---|---|---|
| `reportType` | `MatSelect` | Incident, BP Incident, Assessment, Meal Entry |
| `careRecipientId` | `MatSelect` | Populated from linked CRs |
| `startDate` | `MatDatepicker` | Optional |
| `endDate` | `MatDatepicker` | Optional |

#### Download Flow

1. Calls `ExportService.downloadReport(type, careRecipientId, startDate, endDate)`.
2. `ExportService` calls the correct backend endpoint with `responseType: 'blob'`.
3. On success, creates an object URL, appends a hidden `<a>` tag, programmatically clicks it, then revokes the URL. This triggers a file download with the server-provided filename (read from `Content-Disposition` header).
4. Shows a success snackbar.

#### Upload to Drive Flow

1. Checks `GET /google-drive/status`. If not connected, shows "Connect Google Drive" button.
2. If connected, shows "Upload to Drive" button.
3. On upload: posts to `POST /export/upload-to-drive`, shows a loading spinner.
4. On success: shows a snackbar with a "View in Drive" link.

---

### ExportService (Angular)

**File:** `client/src/app/features/profile/services/export.service.ts` (or `core/services/export.service.ts`)

| Method | HTTP | Description |
|---|---|---|
| `downloadReport(type, careRecipientId, start, end)` | GET | Returns `Blob`, triggers browser download |
| `uploadToDrive(careRecipientId, reportType, start, end)` | POST | Returns `{ fileId, viewUrl }` |
| `getDriveStatus()` | GET | Returns `{ connected: bool }` |

---

## End-to-End Data Flow

```
User clicks "Export"
  → MyReportsComponent builds query params
  → ExportService.downloadReport() → GET /api/export/{type}?careRecipientId=X&startDate=Y&endDate=Z
  → ExportController validates CR access
  → ExportService queries DB, builds XLWorkbook (ClosedXML)
  → Returns FileContentResult (.xlsx bytes)
  → Angular creates blob URL → browser downloads file

Optional: User clicks "Upload to Drive"
  → ExportService.uploadToDrive() → POST /api/export/upload-to-drive
  → ExportService loads GoogleDriveCredential for user
  → DriveService uploads file (auto-refreshes token if expired)
  → Returns { fileId, viewUrl }
  → Snackbar shows "View in Drive" link
```
