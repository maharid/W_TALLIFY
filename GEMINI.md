# ProjectTallify

ProjectTallify is a comprehensive event management and scoring system designed for organized competitions, such as pageants or talent shows. It facilitates event coordination, judge participation, real-time scoring, and automated reporting.

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK
- MySQL Server (or Docker)
- SMTP Server (for email features, e.g., Gmail)

### Building and Running
1.  **Database Setup:**
    - Ensure a MySQL instance is running.
    - Update the connection string in `appsettings.json` or via environment variables.
    - Apply migrations:
      ```bash
      dotnet ef database update
      ```
2.  **Run the Application:**
    ```bash
    dotnet run
    ```
    The application will be available at `http://localhost:5022` (or the port configured in `Properties/launchSettings.json`).

3.  **Docker (Optional):**
    ```bash
    docker-compose up --build
    ```

### Configuration
Environment variables can be managed via `appsettings.json`, `.env.example` (if using a loader), or system environment variables. Key sections include:
- `ConnectionStrings:TallifyDb`: MySQL connection string.
- `EmailSettings`: SMTP configuration for system emails.

## 🏗️ Architecture & Technology Stack

-   **Backend:** ASP.NET Core 8.0 MVC
-   **Database:** MySQL with Entity Framework Core (Pomelo provider)
-   **Real-time:** SignalR for live notifications and scoring updates
-   **Reporting:** QuestPDF for generating PDF result summaries
-   **Authentication:** Cookie-based authentication for Organizers; PIN-based access for Judges
-   **Security:** SHA256 hashing for passwords/tokens (using `BCrypt.Net-Next` as per project file, though `AuthController` uses a custom SHA256 helper)
-   **Frontend:** Razor Views, Vanilla JavaScript, CSS, Bootstrap, and jQuery

## 📂 Project Structure

-   `Controllers/`: Handles incoming requests (Auth, Events, Judge, etc.)
-   `Models/`: Contains data entities (`Event.cs`, `Contestant.cs`, `Judge.cs`, `Score.cs`) and DTOs
-   `Services/`: Business logic implementations:
    -   `ScoringService.cs`: Logic for tallying and ranking
    -   `ReportService.cs`: PDF generation logic
    -   `NotificationService.cs`: SignalR wrapper for notifications
-   `Views/`: Razor templates for the UI
-   `wwwroot/`: Static assets (CSS, JS, images, uploads)
-   `TALLIFY/`: Automation testing project (Katalon Studio/Gradle)
-   `Hubs/`: SignalR Hub definitions (`NotificationHub.cs`)
-   `Migrations/`: EF Core database migration history

## 🛠️ Development Conventions

### Coding Style
-   Follow standard C# and .NET naming conventions (PascalCase for classes/methods).
-   Use Dependency Injection (DI) for services in controllers.
-   Asynchronous programming (`async`/`await`) is preferred for I/O operations (database, email).

### Database Management
-   All schema changes must be done via EF Core Migrations.
-   Avoid raw SQL queries unless absolutely necessary for performance.

### Testing
-   **Manual Testing:** Most features involve complex multi-user flows (Organizer vs. Judge).
-   **Automation:** The `TALLIFY` directory contains Gradle-based automation tests using the Katalon plugin.

## 📝 Key Commands
-   **Add Migration:** `dotnet ef migrations add <MigrationName>`
-   **Update Database:** `dotnet ef database update`
-   **Build:** `dotnet build`
-   **Clean:** `dotnet clean`

## 💡 Important Notes
-   **Email:** Password resets and signup verifications require a valid SMTP configuration in `appsettings.json`.
-   **Environment:** Ensure `ASPNETCORE_ENVIRONMENT` is set to `Development` during local work to see detailed error pages.
