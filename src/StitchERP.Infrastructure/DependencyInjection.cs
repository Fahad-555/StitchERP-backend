using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using StitchERP.Infrastructure.Data;

namespace StitchERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ResolveConnectionString(configuration);

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<StitchErpDbContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
        }

        return services;
    }

    private static string? ResolveConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(configured) || !configured.Contains("${{", StringComparison.Ordinal))
            return configured;

        var host = Environment.GetEnvironmentVariable("MYSQLHOST");
        var port = Environment.GetEnvironmentVariable("MYSQLPORT");
        var database = Environment.GetEnvironmentVariable("MYSQLDATABASE");
        var user = Environment.GetEnvironmentVariable("MYSQLUSER");
        var password = Environment.GetEnvironmentVariable("MYSQLPASSWORD");

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port) ||
            string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(user) || password is null)
            return null;

        var builder = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = uint.Parse(port),
            Database = database,
            UserID = user,
            Password = password
        };

        return builder.ConnectionString;
    }
}
