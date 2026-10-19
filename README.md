# StockTrack — C# backend + frontend

One project: ASP.NET Core (.NET 8) API + SQLite, and the HTML/CSS/JS pages served from `wwwroot/`.

## Run it (VS Code)

1. Install the **.NET 8 SDK** and the **C# Dev Kit** (or **C#**) extension.
2. In VS Code: **File → Open Folder →** `StockTrack.Api`.
3. Press **F5** (or run `dotnet run` in the terminal).
4. Open **http://localhost:5080** — the database (`stocktrack.db`) is created and seeded on first run.

## Demo accounts

| Role  | Username | Password |
|-------|----------|----------|
| Admin | admin    | admin123 |
| Staff | staff1   | staff123 |
| Staff | staff2   | staff123 |

## Layout

```
Program.cs / Endpoints.cs   startup, auth cookie, role policies, REST routes
Services/                   AuthService, UserService, InventoryService, ActivityService
Data/                       AppDb (SQLite) + Seeder (demo data)
Models/Entities.cs          tables + DTOs
wwwroot/                    frontend; js/api.js replaces db.js + auth.js
```

To start fresh: stop the app, delete `stocktrack.db`, run again (or Settings → Reset Demo Data).
