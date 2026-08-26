# StitchERP Backend

The backend targets .NET 8 and uses ASP.NET Core Web API, Entity Framework Core, and the MySQL EF Core provider.

## Run locally

```powershell
dotnet restore
dotnet run --project .\src\StitchERP.Api\StitchERP.Api.csproj --launch-profile http
```

Useful endpoints:

- `http://localhost:5251/swagger`
- `http://localhost:5251/health`
- `http://localhost:5251/health/database`
- `http://localhost:5251/api/v1/info`

## MySQL configuration

Set `ConnectionStrings:DefaultConnection` in user secrets or an environment-specific configuration file. Do not commit credentials.

Example for a local MySQL 8 database:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=stitcherp;User=stitcherp;Password=<local-password>;"
  }
}
```

Run the database scripts before enabling the connection:

1. `database/schema/001_core_schema.sql`
2. `database/schema/002_p2p_o2c_transactions.sql`
3. `database/schema/003_identity_notifications.sql`
4. `database/seed/001_development_seed.sql`
5. `database/validation/001_core_validation.sql`

With no connection string, the API remains runnable in local demo mode and `/health/database` reports `not_configured`.
