# Divvy Client Application

This is the client-side application for the Divvy project, built using Angular and TypeScript. Divvy is a shared expense management PWA that helps groups track, split, and settle expenses across billing cycles.

## Features

- **Authentication**: Login, signup, MFA, biometric (WebAuthn), and password reset flows.
- **Dashboard**: Overview of outstanding obligations across active cycles.
- **Cycles**: Create and manage expense cycles; track member contributions and settlements.
- **Management**: Admin tools for managing users, groups, and terms.
- **Notifications**: In-app and push notification support via VAPID.
- **Offline Queue**: Captures actions while offline and syncs when connectivity is restored.
- **PWA**: Installable progressive web app with service worker support.

## Getting Started

### Prerequisites

- Node.js (version 18 or later)
- Angular CLI (install via npm: `npm install -g @angular/cli`)
- The Divvy API running at `http://localhost:5000` (see `server/` for setup)

### Installation

1. Clone the repository:
   ```
   git clone <repository-url>
   cd 6299/client
   ```

2. Install dependencies:
   ```
   npm install
   ```

3. Configure the API URL in `src/environments/environment.ts` if needed.

### Running the Application

To start the development server, run:
```
ng serve
```
Navigate to `http://localhost:4300/` in your web browser to view the application.

### Building for Production

To build the application for production, use:
```
ng build --configuration production
```
The output will be stored in the configured `outputPath` (default: `C:/Publish/app`).

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any enhancements or bug fixes.

## License

This project is licensed under the MIT License. See the LICENSE file for details.