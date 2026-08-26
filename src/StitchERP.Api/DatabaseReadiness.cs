using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using StitchERP.Infrastructure.Data;

namespace StitchERP.Api;

public sealed record DatabaseReadiness(string Status, string Provider, string? Message);

public static class DatabaseReadinessExtensions
{
    public static async Task<DatabaseReadiness> CheckDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<StitchErpDbContext>();
        if (db is null)
            return new DatabaseReadiness("not_configured", "MySQL", "DefaultConnection is not configured.");

        try
        {
            var connected = await db.Database.CanConnectAsync(cancellationToken);
            return connected
                ? new DatabaseReadiness("ready", "MySQL", null)
                : new DatabaseReadiness("unavailable", "MySQL", "MySQL database could not be reached.");
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException)
        {
            return new DatabaseReadiness("unavailable", "MySQL", exception.Message);
        }
    }
}
