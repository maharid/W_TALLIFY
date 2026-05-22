# ProjectTallify

ProjectTallify is a sophisticated event management and scoring system designed for organized competitions, such as pageants, talent shows, and corporate events. It streamlines the entire lifecycle of an event—from registration and judging to real-time tallying and automated PDF reporting.

## 🚀 Quick Start

### Prerequisites
- **.NET 8.0 SDK**
- **MySQL Server** (or a compatible instance like MariaDB/Docker)
- **SMTP Server** for email features (e.g., Gmail App Passwords, Mailtrap)
- **Katalon Studio** (Optional, for automated testing)

### Building and Running
1.  **Database Configuration:**
    - Update `ConnectionStrings:TallifyDb` in `appsettings.json` with your MySQL credentials.
    - Ensure `SslMode=none;AllowPublicKeyRetrieval=True;` is set if using local development MySQL.
2.  **Apply Migrations:**
    ```powershell
    dotnet ef database update
    ```
3.  **Run the Application:**
    ```powershell
    dotnet run
    ```
    The application typically listens on `http://localhost:5022`.

4.  **Docker (Optional):**
    ```bash
    docker-compose up --build
    ```
    This will spin up both the application and a MySQL database container.

### Configuration
Key settings are managed in `appsettings.json`:
- `EmailSettings`: SMTP host, port, credentials, and sender info.
- `ConnectionStrings`: Database connection details.

## 🏗️ Architecture & Technology Stack

-   **Backend:** ASP.NET Core 8.0 MVC (C#)
-   **Database:** MySQL with Entity Framework Core (using `Pomelo.EntityFrameworkCore.MySql`)
-   **Real-time:** SignalR for live scoring updates and notifications.
-   **Reporting:** QuestPDF for high-quality, code-defined PDF generation.
-   **Authentication:** 
    -   **Organizers:** Cookie-based authentication with session management.
    -   **Judges:** Secure PIN/Token-based access via unique invitation links.
-   **Security:** SHA256 hashing for sensitive tokens; `BCrypt.Net-Next` for password hashing.
-   **Frontend:** Razor Views, Vanilla JavaScript, jQuery, and custom CSS (Vanilla CSS + Bootstrap).

## 📂 Project Structure

-   `Controllers/`: Request handlers (e.g., `EventsController` for management, `AuthController` for login).
-   `Models/`: 
    -   `Data/`: `TallifyDbContext.cs` (EF Core context).
    -   Entities: `Event.cs`, `Contestant.cs`, `Judge.cs`, `Score.cs`, `Round.cs`, `Criteria.cs`.
    -   `DTOs`: Data transfer objects for API requests and view models.
-   `Services/`: Business logic implementations:
    -   `ScoringService.cs`: Core logic for `WeightedAverage` and `PointBased` scoring.
    -   `ReportService.cs`: Logic for generating PDF summaries and score sheets.
    -   `NotificationService.cs`: SignalR wrapper for pushing real-time alerts.
    -   `SmtpEmailSender.cs`: Handles system emails (invites, password resets).
-   `Views/`: Razor templates, organized by controller.
-   `wwwroot/`: Static assets (JS, CSS, images, uploads).
-   `Hubs/`: SignalR Hub definitions (`NotificationHub.cs`).
-   `TALLIFY/`: Katalon Studio project for automated E2E testing.
-   `Migrations/`: EF Core database schema history.

## 🛠️ Development Conventions

### Coding Style
-   **C#:** PascalCase for classes, methods, and public properties; camelCase for private fields (with `_` prefix).
-   **Asynchronous:** Always use `async`/`await` for I/O-bound operations (DB, Email, Files).
-   **Dependency Injection:** All services and the DbContext must be injected via constructors.
-   **Error Handling:** Use custom middleware for global exception handling (`Home/Error`).

### Scoring Systems
1.  **Weighted Average (WA):** Scores are averaged across judges and then weighted based on criteria percentages.
2.  **Point-Based (PB):** Simple summation of judge points or Rank Aggregation (where lower rank sum wins).
3.  **Derived Criteria:** Supports carrying over scores from previous rounds (e.g., "Preliminary Score" in a "Top 5" round).

### Database Management
-   **Migrations Only:** Never modify the database schema manually. Always use `dotnet ef migrations add <Name>`.
-   **Auditing:** Critical actions (event start/end, judge verification) must be logged to the `AuditLogs` table.

## 📝 Key Commands
-   **Add Migration:** `dotnet ef migrations add <MigrationName>`
-   **Update Database:** `dotnet ef database update`
-   **Remove Last Migration:** `dotnet ef migrations remove`
-   **Run Tests:** `cd TALLIFY && ./gradlew katalonTest` (Requires Gradle and Katalon setup)

## 💡 System Logic Notes
-   **Judge Invitations:** Judges must verify their email via a unique token before they can access the scoring portal.
-   **Round Management:** Only one round is typically "Active" at a time to prevent scoring confusion.
-   **Live Tally:** The Organizer's dashboard polls/listens for SignalR updates to show real-time rankings as judges submit scores.
-   **File Storage:** Uploaded photos (contestants/organizers) are stored in `wwwroot/uploads/` and cleaned up if orphaned for >30 days.
