# Project Tallify

Project Tallify is a real-time event scoring and tallying system designed for competitions such as beauty pageants and talent shows. It automates the judging process, provides live tally updates via SignalR, and generates high-quality PDF reports for final results.

## Project Overview

- **Purpose**: Automate judging, scoring, and real-time result aggregation for multi-round competitions.
- **Main Technologies**:
  - **Framework**: .NET 8.0 (ASP.NET Core MVC)
  - **Database**: MySQL 8.0 (via Pomelo.EntityFrameworkCore.MySql)
  - **Real-time**: ASP.NET Core SignalR
  - **Reporting**: QuestPDF (for PDF generation)
  - **Authentication**: Cookie-based authentication with BCrypt hashing.
  - **Frontend**: Razor Views, Vanilla CSS, jQuery for AJAX interactions.
  - **Testing**: Katalon Studio project located in the `TALLIFY/` directory.

## Architecture

The project follows a standard ASP.NET Core MVC architecture with a dedicated service layer:

- **Controllers**: Located in `/Controllers`. Key controllers include `AuthController`, `EventsController`, `JudgeController`, and `NotificationsController`.
- **Models**: Located in `/Models`. The database context is `TallifyDbContext` in `/Models/Data/`.
- **Services**: Business logic is encapsulated in `/Services`:
  - `ScoringService`: Core logic for score calculations, rankings, and tallying.
  - `ReportService`: Generates PDF summaries using QuestPDF.
  - `NotificationService`: Manages real-time alerts and state changes via SignalR.
  - `SmtpEmailSender`: Handles email communications.
- **Hubs**: `/Hubs/NotificationHub.cs` facilitates real-time communication between the management dashboard and judges.
- **Migrations**: Database schema evolution is tracked in `/Migrations`.

## Building and Running

### Prerequisites
- .NET 8 SDK
- MySQL Server 8.0

### Local Setup
1.  **Configuration**: Update the `TallifyDb` connection string in `appsettings.json` or `appsettings.Development.json`.
2.  **Database Migration**:
    ```bash
    dotnet ef database update
    ```
3.  **Run Application**:
    ```bash
    dotnet run
    ```
    The application typically starts at the Login screen (`/Auth/Login`).

### Docker Setup
The project includes a `docker-compose.yml` for easy environment setup:
```bash
docker-compose up --build
```

### Testing
Automated functional tests are located in the `TALLIFY/` folder. This is a Katalon Studio project.

## Development Conventions

- **Dependency Injection**: Use the built-in DI container. Register services in `Program.cs` (typically as `Transient` or `Scoped`).
- **Real-time Updates**: Use the `NotificationHub` and `INotificationService` to push state changes (e.g., score submissions) to connected clients.
- **Controller Logic**: Keep controllers thin. Move complex business logic, calculations, and data transformations to the appropriate `Service` classes.
- **Data Transfer**: Use DTOs (found in `Models/EventDtos.cs`, `Models/ManageDtos.cs`, etc.) for passing data between the client and server.
- **Styling**: Maintain UI consistency using the Vanilla CSS files in `wwwroot/css/`.
- **AJAX**: Most interactive elements in the Judge and Manage views are powered by jQuery AJAX calls to controller actions.
