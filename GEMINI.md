# Project Tallify

Tallify is a web-based event management and scoring system designed for organizers to manage contests, rounds, criteria, and contestants, while providing a platform for judges to submit real-time scores.

## Project Overview

- **Purpose:** Event management, scoring, and real-time tallying.
- **Architecture:** ASP.NET Core 8.0 MVC with a Service Layer for business logic.
- **Key Features:**
    - Organizer Dashboard & Event Management.
    - Judge Portal for score submission.
    - Multiple Scoring Logics (Weighted Average, Point Based).
    - Real-time Live Tally & Notifications (SignalR).
    - PDF Report Generation (QuestPDF).
    - Automated Testing (Katalon Studio).

## Technology Stack

- **Backend:** .NET 8.0 (C#)
- **Database:** MySQL (Entity Framework Core with Pomelo provider)
- **Frontend:** Razor Views, Vanilla CSS, JavaScript (jQuery for some interactions)
- **Real-time Communication:** SignalR
- **Security:** BCrypt.Net-Next for password hashing, Cookie-based Authentication
- **Reporting:** QuestPDF
- **Containerization:** Docker & Docker Compose
- **Testing:** Katalon Studio (located in the `TALLIFY/` directory)

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- MySQL Server
- (Optional) Docker & Docker Compose
- (Optional) Katalon Studio (for running automated tests)

### Configuration

1.  Copy `.env.example` to `.env` and fill in the required environment variables.
2.  Update the connection string in `appsettings.json` if necessary (it currently points to `localhost`).

### Building and Running

1.  **Restore dependencies:**
    ```bash
    dotnet restore
    ```
2.  **Update database:**
    ```bash
    dotnet ef database update
    ```
3.  **Run the application:**
    ```bash
    dotnet run
    ```
    The application will typically be available at `http://localhost:5000` or `https://localhost:5001`.

### Testing

- Automated tests are located in the `TALLIFY/` directory and are intended to be run with Katalon Studio.
- Unit/Integration tests (if added in the future) should be placed in a separate `.Tests` project.

## Development Conventions

- **Services:** Place complex business logic in the `Services/` directory. Services should be registered in `Program.cs` and injected into controllers.
- **Asynchronous Code:** Always prefer `async/await` for I/O-bound operations (DB queries, file access, email sending).
- **Models:**
    - Entities are located in `Models/`.
    - DTOs and ViewModels should be used for data transfer between layers or to the view.
- **Frontend:**
    - CSS files are modularized in `wwwroot/css/`.
    - JavaScript logic is separated by feature in `wwwroot/js/`.
- **SignalR:** Use `NotificationHub` for any real-time updates.
- **Migrations:** Use Entity Framework Core Migrations for any database schema changes.

## Directory Structure

- `Controllers/`: MVC Controllers.
- `Hubs/`: SignalR Hub definitions.
- `Migrations/`: EF Core database migrations.
- `Models/`: Database entities and application models.
- `Properties/`: Project settings and launch configurations.
- `Services/`: Business logic services (Scoring, Reporting, Email, etc.).
- `TALLIFY/`: Katalon Studio test project.
- `Views/`: Razor View files.
- `wwwroot/`: Static assets (CSS, JS, Images, Libs).
- `Program.cs`: Application entry point and configuration.
- `ProjectTallify.csproj`: Project definition and dependencies.
- `appsettings.json`: Configuration settings.
- `Dockerfile` / `docker-compose.yml`: Containerization setup.
