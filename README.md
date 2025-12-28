# Reservations

Simple reservations system for pool lanes and sessions.

Features
- Register/Login (simple session-based auth)
- Roles: Administrator, Trainer, User
- Admin can manage users and delete sessions
- Trainers can create sessions
- Users can reserve/unreserve sessions
- PostgreSQL via EF Core (Npgsql)

Quickstart (local)

Prerequisites
- .NET 8 SDK
- PostgreSQL running locally

1. Clone repository
   git clone <repo>

2. Configure DB connection
   - Edit `appsettings.json` connection string `ConnectionStrings:DefaultConnection` to point to your PostgreSQL instance.
   - Example: `Host=localhost;Database=reservations;Username=postgres;Password=postgres`

3. Apply migrations (recommended)
   - Install dotnet-ef tool (global or local):
     dotnet tool install --global dotnet-ef --version 9.0.11
   - Restore packages and add design package (if not present):
     dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.11
   - Create DB and apply migrations:
     dotnet ef database update

   Alternatively, run the SQL script `Migrations/InitialCreate.pgsql` in pgAdmin to create schema and seed data.

4. Run the app
   dotnet run
   Open the URL shown in the console (e.g. https://localhost:7132)

Default admin

- Username: `admin`
- Password: `admin`
