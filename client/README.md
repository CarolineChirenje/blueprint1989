# Vitara Client Application

This is the client-side application for the Vitara project, built using Angular and TypeScript. The application is designed for school administrators and parents to manage BGL entries and comments effectively.

## Features

- **Admin Login**: School administrators can log in to the application.
- **BGL Entry Management**: Administrators can enter BGL values along with comments, which are automatically timestamped.
- **Parent Comments**: A dedicated section for parents to add their comments.
- **Excel Export**: The application can generate an Excel sheet containing all history, which can be updated to a shared Google Drive.

## Getting Started

### Prerequisites

- Node.js (version 14 or later)
- Angular CLI (install via npm: `npm install -g @angular/cli`)
- A compatible web browser

### Installation

1. Clone the repository:
   ```
   git clone <repository-url>
   cd Vitara/client
   ```

2. Install dependencies:
   ```
   npm install
   ```

### Running the Application

To start the development server, run:
```
ng serve
```
Navigate to `http://localhost:4200/` in your web browser to view the application.

### Building for Production

To build the application for production, use:
```
ng build --prod
```
The output will be stored in the `dist/` directory.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any enhancements or bug fixes.

## License

This project is licensed under the MIT License. See the LICENSE file for details.