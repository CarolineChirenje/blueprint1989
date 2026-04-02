# Vitals Charts

This document describes the chart visualisations and vitals-tracking features added to the Vitara platform: Blood Pressure, BGL (Blood Glucose Level), Temperature, and Weight.

---

## Overview

All four vitals use a shared `VitaraChartComponent` (a Chart.js line chart wrapper) with coloured reference band lines. Each feature exposes:

- A **history page** with a Table / Chart toggle
- A **trend API endpoint** returning time-series data

---

## VitaraChartComponent

**Location:** `client/src/app/shared/components/vitara-chart/`

**Selector:** `<app-vitara-chart>`

### Inputs

| Input | Type | Description |
|---|---|---|
| `title` | `string` | Card heading |
| `labels` | `string[]` | X-axis labels (one per data point) |
| `datasets` | `VitaraChartDataset[]` | One or more data series |
| `yAxisLabel` | `string` | Y-axis label text |
| `referenceBands` | `VitaraReferenceBand[]` | Horizontal dashed reference lines |

### Interfaces

```ts
interface VitaraChartDataset {
  label: string;
  data: (number | null)[];
  color: string;   // CSS colour string
  fill?: boolean;
}

interface VitaraReferenceBand {
  y: number;
  label: string;
  color: string;
}
```

### Notes

- Uses Chart.js 4.x directly (no ng2-charts wrapper)
- A custom plugin `vitaraRefLines` draws dashed horizontal lines and labels for each `referenceBand`
- Chart is rebuilt on `ngOnChanges` whenever inputs change
- Declared in `SharedModule`; feature modules import `SharedModule` to use it

---

## Blood Pressure Chart

**Feature:** `features/blood-pressure`

### Chart Data Source

The chart reads weekly averages already loaded by `bp-history.component.ts` (`loadWeeklyAverages()`). No separate trend endpoint is needed.

### Datasets

| Dataset | Colour |
|---|---|
| Systolic (mmHg) | `#e53935` (red) |
| Diastolic (mmHg) | `#1e88e5` (blue) |

### Reference Bands

| Y value | Label | Colour |
|---|---|---|
| 90 | Hypotension | `#1565c0` |
| 120 | Normal | `#43a047` |
| 130 | Elevated | `#fb8c00` |
| 140 | Stage 1 HT | `#ef6c00` |
| 180 | Crisis | `#b71c1c` |

---

## BGL Trend Chart

**Feature:** `features/assessment`

### Trend Endpoint

```
GET /api/assessment/bgl-trend?careRecipientId={id}&days={n}
```

- Default `days`: 30
- Returns the `InitialReading` value of each Assessment in chronological order
- Role-based access: CareRecipient sees own readings; Carer/SupportWorker/HealthCareProvider see linked care recipients; Admin/SuperAdmin see all

### Datasets

| Dataset | Colour |
|---|---|
| BGL (mmol/L) | `#8e24aa` (purple) |

### Reference Bands

| Y value | Label |
|---|---|
| 2.5 | Severe Hypo |
| 3.5 | Hypo |
| 4.0 | Low Normal |
| 7.9 | Post-meal Upper |
| 10.1 | Elevated |
| 15.0 | Very High |
| 20.0 | Dangerous |

---

## Temperature Feature

**Backend route prefix:** `/api/temperature`
**Frontend route:** `/temperature`

### Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/temperature` | Record a new reading |
| `GET` | `/api/temperature` | List readings (filtered by role) |
| `GET` | `/api/temperature/{id}` | Get a single reading |
| `DELETE` | `/api/temperature/{id}` | Delete a reading |
| `GET` | `/api/temperature/trend` | Time-series for chart (`?careRecipientId&days`) |
| `GET` | `/api/temperature-classification` | List global/per-recipient classification ranges |
| `POST` | `/api/temperature-classification` | Create range (Admin+) |
| `PUT` | `/api/temperature-classification/{id}` | Update range (Admin+) |
| `DELETE` | `/api/temperature-classification/{id}` | Delete range (Admin+) |

### Classification Categories (WHO guidelines, °C)

| Category | Range | Severity |
|---|---|---|
| Hypothermia | < 35.0 | Critical |
| Below Normal | 35.0 – 36.1 | Medium |
| Normal | 36.1 – 37.2 | Normal |
| Low Fever | 37.2 – 38.3 | Low |
| Fever | 38.3 – 39.4 | Medium |
| High Fever | 39.4 – 41.1 | High |
| Hyperpyrexia | ≥ 41.1 | Critical |

These ranges are seeded as global defaults in the EF migration `AddTemperatureFeature`.

### Measurement Sites

Oral · Axillary · Rectal · Tympanic · Temporal

### Chart Reference Bands

Lines drawn at 35.0, 36.1, 37.2, 38.3, 39.4, and 41.1 °C.

---

## Weight Feature

**Backend route prefix:** `/api/weight`
**Frontend route:** `/weight`

### Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/weight` | Record a new reading (BMI calculated server-side) |
| `GET` | `/api/weight` | List readings (filtered by role) |
| `GET` | `/api/weight/{id}` | Get a single reading |
| `DELETE` | `/api/weight/{id}` | Delete a reading |
| `GET` | `/api/weight/trend` | Time-series for chart (`?careRecipientId&days`) |
| `GET` | `/api/weight/height` | Return stored HeightCm for a user |
| `PATCH` | `/api/weight/height` | Update stored HeightCm |

### BMI Calculation

BMI is calculated server-side at the time a reading is saved, using the CareRecipient's stored `HeightCm`:

```
BMI = weightKg / (heightM²)    rounded to 1 decimal place
```

If `HeightCm` is not set, `Bmi` and `BmiCategory` are `null` on the response.

### BMI Categories

| Range | Category | CSS class |
|---|---|---|
| BMI < 18.5 | Underweight | `reading-warning` |
| 18.5 ≤ BMI < 25 | Normal | `reading-normal` |
| 25 ≤ BMI < 30 | Overweight | `reading-elevated` |
| 30 ≤ BMI < 35 | Obese Class I | `reading-warning` |
| 35 ≤ BMI < 40 | Obese Class II | `reading-danger` |
| BMI ≥ 40 | Obese Class III | `reading-critical` |

### Chart

The weight trend chart shows:
- **Weight (kg)** line (always visible)
- **BMI** line (optional, toggle checkbox — only shown when HeightCm is set for some readings)

When the BMI line is enabled, BMI reference bands are drawn at 18.5, 25, 30, 35, and 40.

### Height in Profile

CareRecipients can set their height on the **My Profile** page under the *Physical Information* section. This is used for BMI calculation when recording weight. It can also be set inline on the Weight Entry form.

---

## Styling Conventions

All severity classes are shared across all vitals:

| Class | Background | Text | Use |
|---|---|---|---|
| `reading-normal` | `#e8f5e9` | `#1b5e20` | Normal / healthy |
| `reading-elevated` | `#fff8e1` | `#e65100` | Slightly above normal |
| `reading-warning` | `#fff3e0` | `#bf360c` | Clinically significant |
| `reading-danger` | `#ffebee` | `#b71c1c` | Dangerous |
| `reading-critical` | `#f3e5f5` | `#4a148c` | Critical / emergency |

These classes are applied to `.reading-badge` and `.label-badge` span elements in history tables, and to result cards in entry components.
