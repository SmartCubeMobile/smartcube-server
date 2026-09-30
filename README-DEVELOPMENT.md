# SmartCube: shared development

## Who owns what

| Component       | Development | Production |
|-----------------|-------------|------------|
| Server          | Dad         | Dan        |
| Application     | Dad         | Dan        |
| Website         | Dan         | Dan        |

Code moves one way: Dan **pulls** from the shared development repo into production when he decides
(`pull-from-dev.ps1`). Production never pushes anything back, and development never pushes into
production. Server/app ideas from Dan go to Dad first; you decide between you.

Urgent production fixes (data loss, server down) may be made directly in production. Dan then
sends the same fix to development as a patch for Dad to apply, so the next pull doesn't clash.

## Running the dev server on your PC

1. Copy `appsettings.Example.json` to `appsettings.json` (it's git-ignored, so secrets never get
   committed). The example points at a local database called `SmartCubeDev`. Email and the Claude key
   are blank, so password-reset emails and Smart Scan won't work in dev unless you add your own.
2. Create the dev database (structure only, no customer data). In PowerShell, from this folder:

   ```
   .\Database\Apply-Migrations.ps1 -Server "(localdb)\MSSQLLocalDB" -Database SmartCubeDev -Create
   ```

   LocalDB comes with Visual Studio. SQL Server Express works too: use `-Server ".\SQLEXPRESS"` and
   change the connection string in `appsettings.json` to match.
3. In Visual Studio choose the **SmartCube (dev)** profile and press F5. The server runs at
   `https://localhost:5001`; the website is at `/site/index.html`.
4. Register a test account on your dev site (`/site/register.html`).

## Pointing the dev app at the dev server

On the app's sign-in screen, click the **Server:** line at the bottom and enter
`https://localhost:5001`. Blank resets it to the live server. Your dev app and dev server then never
touch live users.

## Changing the database

Never change the live database by hand, and never edit a migration that has already been pulled.
Add a new numbered script instead:

```
Database\Migrations\001-add-invoice-table.sql
Database\Migrations\002-...
```

Make scripts safe to re-run where you can (`IF COL_LENGTH(...) IS NULL ALTER TABLE ...`). Run
`Apply-Migrations.ps1` on your dev database to test it. When Dan pulls, `deploy-server.ps1` runs the new
scripts on production inside a transaction; a failing script rolls back and the deploy stops.
`000-baseline.sql` is the structure production had when the shared set-up started (30 Sep 2026).

## Never commit

- `appsettings.json`, API keys, passwords, connection strings with passwords
- `App_Data\` (the live database files) or `Installers\`
