# RaceDay – Part 2: RESTful API Development

This repository contains **Part 2** of the RaceDay Portfolio of Evidence (PROG6212): a RESTful API, built with ASP.NET Core Web API in C#, that powers the entire RaceDay system. It implements the endpoint plan approved in Part 1, connects to SQL Server via Entity Framework Core (Code-First), enforces role-based access for Organisers and Participants, and is covered by a unit test suite that runs automatically through GitHub Actions CI/CD.

## About RaceDay

RaceDay is a web-based event management system for the South African road running, walking, and cycling community. Organisers create and manage events and categories, and capture participant results. Participants browse events, enter a category, and track their personal results history.

## Roles

| Role | Can do |
|---|---|
| **Organiser** | Create, update, and delete their own events; define categories (age- or distance-based) for their events; view all enrolments for their events; capture and correct participant results. |
| **Participant** | View/update their own profile; browse events and categories; enrol in a category; view their own enrolments; view their own results. |

Role-based access is enforced **at the API level** using ASP.NET Core's `[Authorize(Roles = "...")]` attribute on every restricted endpoint — Organiser-only endpoints reject Participants (and vice-versa) with `403 Forbidden`, and all non-public endpoints reject unauthenticated requests with `401 Unauthorized`.

## Project Structure

```
RaceDay.sln
RaceDay.Api/                 ← the Web API project
  Controllers/                AuthController, UsersController, EventsController,
                               CategoriesController, EnrolmentsController, ResultsController
  Models/                     EF Core entities (Users, Events, Categories, Enrolments, Results, Payments)
  DTOs/                       Request/response objects, grouped by feature
  Data/                       RaceDayContext (DbContext) and DbSeeder (sample data)
  Program.cs                  App startup: EF Core, cookie auth, Swagger
RaceDay.Api.Tests/            ← the xUnit test project
  ControllerTests/            AuthTests, EventsTests, EnrolmentsTests, ResultsTests
  TestHelpers/                CustomWebApplicationFactory (in-memory DB), TestAuthHelper
.github/workflows/ci.yml      ← GitHub Actions: builds the solution and runs all tests on every push
docs/                         ← carried over from Part 1: ERD, endpoint plan, SQL script
```

## Setup and Run Instructions

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), SQL Server (LocalDB is fine) or SQL Server Management Studio, Visual Studio 2022 (or VS Code).

1. Clone the repository and open `RaceDay.sln` in Visual Studio (or run the steps below from a terminal in the repository root).
2. Restore dependencies:
   ```
   dotnet restore
   ```
3. Confirm the connection string in `RaceDay.Api/appsettings.json` matches your SQL Server instance. The default targets LocalDB:
   ```
   Server=(localdb)\mssqllocaldb;Database=RaceDayDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
   ```
4. Create the initial migration and database (run from the repository root):
   ```
   dotnet tool install --global dotnet-ef   # if you don't already have the EF Core CLI tool
   dotnet ef migrations add InitialCreate --project RaceDay.Api
   dotnet ef database update --project RaceDay.Api
   ```
5. Run the API:
   ```
   dotnet run --project RaceDay.Api
   ```
   In the `Development` environment, the app automatically applies any pending migrations and seeds sample data (2 Organisers, 2 Participants, 3 events, 6 categories, 3 enrolments, 1 result) matching Part 1's seed data, so Swagger has realistic data immediately.
6. Open Swagger in your browser (it opens automatically): `https://localhost:5101/swagger` (or the port shown in the console).
7. To try the API end-to-end in Swagger: call `POST /api/auth/register`, then `POST /api/auth/login` — your browser now holds a session cookie, so subsequent calls in the same Swagger tab (e.g. `GET /api/users/me`, `POST /api/events`) are authenticated as that user.

### Seeded test accounts (Development only)

| Email | Password | Role |
|---|---|---|
| thandiwe.mokoena@raceday.co.za | Password1! | Organiser |
| johan.vdm@raceday.co.za | Password1! | Organiser |
| lindiwe.dlamini@gmail.com | Password1! | Participant |
| sipho.nkosi@gmail.com | Password1! | Participant |

### Running the tests

```
dotnet test
```

The test suite uses `WebApplicationFactory<Program>` to boot the real application pipeline (routing, authentication, authorization) against an isolated **EF Core InMemory** database per test class, so tests never touch your real SQL Server database and can be run repeatably in any environment, including GitHub Actions.

Tests cover, at minimum:
- User registration and login (valid and invalid roles, duplicate email, wrong password, unknown email)
- Event management by Organisers (create, update, delete; non-owner rejection; invalid EventType rejection)
- Rejection of role-based functionality for the wrong role (Participants blocked from creating events or capturing results; Organisers blocked from enrolling)
- Event enrolments (successful enrolment with a generated race number, duplicate-enrolment conflict, category-full rejection, Organiser viewing all enrolments for their event)
- Results capture and viewing (Organiser captures a result, Participant views only their own results, duplicate-result conflict, non-owning Organiser rejection)

## Design Decisions and Deviations from the Part 1 Plan

- **Session-based (cookie) authentication, not JWT.** The brief asks for "session management to maintain the user's authenticated state and role for all subsequent requests." A cookie issued on login *is* that session: the browser (and Swagger UI, running on the same origin) automatically attaches it to every later request, and `[Authorize(Roles = "...")]` reconstructs the user's identity and role from it. See the comment in `AuthController.cs` for the full reasoning.
- **Event now also captures `Distance` and `EventType`** ("Run"/"Walk"/"Cycle"), as required by Part 2's functional requirements. The Part 1 ERD only held distance at the Category level; these two fields have been added to the `Events` table to reflect the event's overall discipline and headline distance, while Categories still hold their own specific distance for events that offer multiple distances. This is documented in `Models/Event.cs`.
- **Categories support both distance-based and age-based entries** (e.g. "10km" vs "Senior (Over 40)"), per the functional requirement to support both. `DistanceKm`, `MinAge`, and `MaxAge` are all nullable so a category can be defined by whichever is relevant.
- **The `Payments` table from Part 1 is retained in the schema** for continuity with the approved ERD/SQL script, but no endpoints were planned for it in the Part 1 API plan, so no `PaymentsController` exists in Part 2.

## Swagger

All endpoints are visible and testable directly through Swagger UI at `/swagger` with no additional tooling required. Every endpoint has XML-doc-generated descriptions of its purpose, expected request body, and possible responses (see the `[ProducesResponseType]` attributes and `/// <summary>` comments on each controller action).

## GitHub and CI/CD

- All work for Part 2 is pushed to this repository (see commit history for 20+ meaningful commits).
- `.github/workflows/ci.yml` builds the solution and runs the full `dotnet test` suite on every push and pull request to `main`.

**CI/CD green build screenshot:** *[insert screenshot of a successful GitHub Actions run here before submission]*

## Video Presentation

An unlisted YouTube video demonstrating the running API, explaining the code structure, endpoints, authentication, and unit tests is required for submission. No AI-generated voice is used in the recording.

**YouTube video link:** *[insert unlisted YouTube link here before submission]*

## Submission Checklist

- [ ] Project builds and runs with `dotnet run --project RaceDay.Api`
- [ ] All endpoints from the Part 1 plan are implemented and visible in Swagger
- [ ] Role-based access enforced at the API level (tested in `RaceDay.Api.Tests`)
- [ ] `dotnet test` passes locally
- [ ] Minimum 20 meaningful commits for this part
- [ ] GitHub Actions workflow shows a green check mark
- [ ] README updated with CI/CD screenshot and YouTube link
- [ ] GitHub repository link submitted on ARC
