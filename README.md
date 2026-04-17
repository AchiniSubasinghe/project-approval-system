# Blind-Match PAS

Blind-Match PAS is an ASP.NET Core Blazor application for allocating final-year project supervisors without exposing student identities during the review stage. Students submit proposals by research area, supervisors browse anonymous proposals that match their expertise, and confirmed matches reveal both parties so collaboration can begin.

## Features

- Student workspace for creating, editing, withdrawing, and tracking project proposals.
- Supervisor workspace for managing expertise areas, reviewing anonymous proposals, expressing interest, and confirming matches.
- Module leader workspace for managing users, research areas, proposals, and supervisor allocations.
- Role-based access control using ASP.NET Core Identity roles.
- EF Core SQL Server persistence with migrations and startup database seeding.
- Project matching rules for supervisor expertise, duplicate interest prevention, first-confirmed match wins, and supervisor capacity checks.
- Optional Anthropic-powered chatbot with role-aware project allocation tools.
- xUnit test coverage for core project matching behavior.

## Tech Stack

- .NET 10 / ASP.NET Core Blazor Server
- MudBlazor
- ASP.NET Core Identity
- Entity Framework Core
- SQL Server
- xUnit with SQLite in-memory test database
- Anthropic Messages API for the chatbot integration

## Requirements

- .NET 10 SDK
- SQL Server, SQL Server Express, or a compatible local SQL Server container
- Optional: `dotnet-ef` CLI tool for manual migration commands
- Optional: Anthropic API key for chatbot use

Install the EF Core CLI if you need to run migration commands manually:

```bash
dotnet tool install --global dotnet-ef
```

## Configuration

Development settings live in `appsettings.Development.json`. For local work, prefer user secrets for passwords and API keys:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=BlindMatchPas;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
dotnet user-secrets set "IdentityDefaults:ModuleLeader:Password" "ChangeMe!123"
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
```

The chatbot is optional. The app can be developed without an Anthropic key, but chatbot requests will fail until `Anthropic:ApiKey` is configured.

## Run Locally

Restore dependencies:

```bash
dotnet restore
```

Start the app:

```bash
dotnet run
```

The development launch profile uses:

- HTTP: `http://localhost:5022`
- HTTPS: `https://localhost:7220`

The app applies EF Core migrations automatically on startup through `DbInitializer.SeedAsync`.

## Database

The main database provider is SQL Server. The configured development database name is `BlindMatchPas`.

To apply migrations manually:

```bash
dotnet ef database update
```

To add a migration after changing the EF model:

```bash
dotnet ef migrations add MigrationName --output-dir Data/Migrations
```

## Seeded Development Data

In the Development environment, startup seeding creates roles, baseline research areas, and development users when the required config values exist.

| Role | Email | Password |
| --- | --- | --- |
| Module Leader | `lead@pas.local` | `ChangeMe!123` |
| Student | `student@pas.local` | `Student!123` |
| Supervisor | `supervisor@pas.local` | `Supervisor!123` |

Seeded research areas:

- Artificial Intelligence
- Web Development
- Cybersecurity
- Cloud Computing

The development supervisor is seeded with Artificial Intelligence and Web Development expertise.

## Tests

Run the test suite:

```bash
dotnet test
```

The tests use SQLite in-memory storage and cover the matching service behavior, including expertise checks, duplicate interests, match confirmation, supervisor capacity, and module leader assignment.

## Project Structure

```text
Components/                    Blazor UI pages, layouts, account pages, and shared components
Components/Pages/Admin/        Module leader screens
Components/Pages/Student/      Student proposal screens
Components/Pages/Supervisor/   Supervisor review and matching screens
Data/                          EF Core entities, DbContext, migrations, roles, and seeding
Services/                      Project matching service and chatbot services
Services/Chatbot/Tools/        Chatbot tool handlers for each role
project-approval-system.Tests/ xUnit tests
wwwroot/                       Static assets and application CSS
```

## Roles and Workflow

1. A student submits a proposal with title, abstract, technical stack, and research area.
2. A supervisor maintains expertise areas and browses matching proposals anonymously.
3. The supervisor expresses interest, moving the proposal into review.
4. The supervisor confirms the match if the project fits and capacity is available.
5. The confirmed match reveals student and supervisor details.
6. A module leader can oversee allocations and assign unmatched proposals when needed.

## Notes

- Do not use development seed passwords in production.
- Keep connection strings and API keys out of committed configuration.
- The app requires a valid `ConnectionStrings:DefaultConnection` value at startup.
