# Architecture of Vitara PWA

## Overview
Vitara is a Progressive Web Application (PWA) designed to facilitate the management of BGL entries and comments by school administrators and parents. The application is built using Angular for the front end and .NET for the backend API, ensuring a robust and scalable solution.

## Architecture Components

### Frontend
- **Framework**: Angular
- **Language**: TypeScript
- **Structure**:
  - **Modules**:
    - `AdminModule`: Handles functionalities for school administrators, including login and BGL entry management.
    - `ParentModule`: Allows parents to add comments related to BGL entries.
  - **Services**:
    - `AuthService`: Manages user authentication and session management.
    - `BglService`: Handles operations related to BGL entries and comments.
    - `ExportService`: Manages the generation of Excel sheets for BGL history.
  - **Models**:
    - `BglEntry`: Represents a BGL entry with properties such as value, timestamp, and comments.
    - `Comment`: Represents a comment with properties like content and timestamp.
    - `User`: Represents user data including username and password.

### Backend
- **Framework**: .NET (Latest LTS)
- **Language**: C# 11
- **Structure**:
  - **Controllers**:
    - `AuthController`: Handles authentication requests.
    - `BglController`: Manages BGL entries and related operations.
    - `ParentController`: Manages comments from parents.
    - `ExportController`: Handles the generation and export of Excel sheets.
  - **Models**:
    - `User`: Represents user data for authentication.
    - `BglEntry`: Represents BGL entries in the backend.
    - `Comment`: Represents comments submitted by parents.
  - **Services**:
    - `AuthService`: Implements authentication logic.
    - `BglService`: Manages BGL-related operations.
    - `ExportService`: Handles Excel generation and integration with Google Drive.

## Data Flow
1. **User Authentication**: Users log in through the Angular frontend, which communicates with the `AuthController` in the backend.
2. **BGL Entry Management**: Administrators can enter BGL values and comments, which are processed by the `BglController` and stored in the database.
3. **Parent Comments**: Parents can submit comments via the `ParentController`, which are also stored in the database.
4. **Excel Generation**: The `ExportService` generates an Excel sheet containing all BGL history, which can be uploaded to a shared Google Drive.

## Deployment
The application is designed to be deployed as a PWA, allowing users to install it on their devices and access it offline. The backend API can be hosted on cloud platforms that support .NET applications.

## Conclusion
Vitara provides a comprehensive solution for managing BGL entries and comments, leveraging modern web technologies to ensure a seamless user experience for both administrators and parents.