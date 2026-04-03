# Architecture of Divvy PWA

## Overview
Divvy is a Progressive Web Application (PWA) designed to facilitate shared expense management for households and groups. The application is built using Angular for the front end and .NET for the backend API, ensuring a robust and scalable solution.

## Architecture Components

### Frontend
- **Framework**: Angular
- **Language**: TypeScript
- **Structure**:
  - **Modules**:
    - `AuthModule`: Handles login, signup, MFA, and biometric authentication.
    - `DashboardModule`: Landing page with navigation cards and greeting.
    - `ExpenseCycleModule`: Manage expense cycles, members, and balances.
    - `ExpenseModule`: Create and view expense entries within a cycle.
    - `PaymentModule`: Submit and confirm payment obligations.
    - `ProfileModule`: User settings, MFA, biometric, notification preferences.
    - `ManagementModule`: Admin hub for users, cycles, reports, and config.
  - **Services**:
    - `AuthService`: Manages user authentication and session management.
    - `ExpenseService`: Handles expense creation and retrieval.
    - `PaymentService`: Manages payment submissions and confirmations.
    - `NotificationService`: In-app notification inbox and preferences.
    - `PushNotificationService`: Web Push subscription and dispatch.
  - **Models**:
    - `ExpenseCycle`: Represents a billing period with start/end date and member list.
    - `Expense`: An expenditure entry with category, amount, and payer.
    - `Payment`: A payment obligation between two members.
    - `MemberObligation`: A member's calculated share within a cycle.
    - `User`: Represents a user with role (SuperAdmin, Admin, Member).

### Backend
- **Framework**: ASP.NET Core (.NET 10)
- **Language**: C#
- **Structure**:
  - **Controllers**:
    - `AuthController`: Handles authentication and registration.
    - `ExpenseCycleController`: Manages expense cycles.
    - `ExpenseController`: Manages expense entries.
    - `PaymentController`: Handles payment submissions and confirmations.
    - `NotificationController`: In-app notifications.
    - `PushController`: VAPID web push subscriptions and dispatch.
    - `AppConfigController`: Runtime application configuration.
    - `SystemController`: System health and restart triggers.
  - **Models**:
    - `User`: Authentication and role data.
    - `ExpenseCycle`: A cycle with start date, end date, and status (Active/Closed).
    - `Expense`: An expense entry linked to a cycle and payer.
    - `Payment`: A payment with status (Pending/Confirmed/Rejected).
    - `MemberObligation`: A member's share within a cycle.
  - **Services**:
    - `AuthService`: Authentication and JWT issuance.
    - `ExpenseCycleService`: Cycle lifecycle and balance calculation.
    - `ExpenseService`: Expense CRUD and categorisation.
    - `PaymentService`: Payment workflow.
    - `PushNotificationSender`: VAPID push dispatch.

## Data Flow
1. **User Authentication**: Users log in through the Angular frontend, which communicates with the `AuthController` and receives a JWT.
2. **Expense Entry**: Members add expenses within an active cycle. The `ExpenseController` stores the entry and recalculates member obligations.
3. **Payment Workflow**: Members submit payments against their obligations. Admins confirm or reject via the `PaymentController`.
4. **Push Notifications**: On key events (payment due, cycle created), `PushNotificationSender` dispatches VAPID pushes to subscribed devices.

## Deployment
The application is deployed as a PWA on Ubuntu 22.04, served through Nginx as a reverse proxy. The backend API runs on Kestrel behind Nginx. TLS is handled by Let's Encrypt via Cloudflare.

## Conclusion
Divvy provides a streamlined solution for shared expense tracking, leveraging modern web technologies to deliver a fast, offline-capable Progressive Web App for both administrators and members.