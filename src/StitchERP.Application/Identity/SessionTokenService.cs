using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StitchERP.Application.Identity;

public interface ISessionTokenService
{
    string Create(ManagedUser user);
    bool TryRead(string token, out SessionIdentity identity);
}

public sealed record SessionIdentity(long UserId, long OrganizationId, string Username, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions);

public sealed class SessionTokenService(string secret) : ISessionTokenService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);
    private static readonly IReadOnlyDictionary<string, string[]> RolePermissions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["SUPER_ADMIN"] = ["*"],
        ["ADMIN"] = ["USER_VIEW", "USER_CREATE", "USER_DISABLE", "PERMISSION_ASSIGN", "USER_RESET_PASSWORD", "PROGRAM_VIEW", "PROGRAM_CREATE", "PROGRAM_EDIT", "INVENTORY_VIEW", "INVENTORY_RECEIVE", "INVENTORY_RESERVE"],
        ["MANAGER"] = ["PROGRAM_VIEW", "PROGRAM_EDIT"],
        ["PRODUCTION_MANAGER"] = ["PROGRAM_VIEW", "PROGRAM_CREATE", "PROGRAM_EDIT", "INVENTORY_VIEW", "INVENTORY_RECEIVE"],
        ["PROCUREMENT_MANAGER"] = ["PO_VIEW", "PO_CREATE", "PO_SUBMIT", "PO_APPROVE", "PO_RECEIVE", "INVENTORY_VIEW"],
        ["PROCUREMENT_USER"] = ["PO_VIEW", "PO_CREATE", "PO_SUBMIT"],
        ["SALES_MANAGER"] = ["SALES_ORDER_VIEW", "SALES_ORDER_CREATE", "SALES_ORDER_EDIT", "SALES_ORDER_APPROVE"],
        ["SALES_USER"] = ["SALES_ORDER_VIEW", "SALES_ORDER_CREATE", "SALES_ORDER_EDIT"],
        ["FINANCE_MANAGER"] = ["INVOICE_CREATE", "PAYMENT_CREATE"],
        ["FINANCE_USER"] = ["INVOICE_CREATE", "PAYMENT_CREATE"],
        ["INVENTORY_MANAGER"] = ["INVENTORY_VIEW", "INVENTORY_RECEIVE", "INVENTORY_RESERVE"],
        ["INVENTORY_USER"] = ["INVENTORY_VIEW", "INVENTORY_RECEIVE"],
        ["VIEWER"] = ["PROGRAM_VIEW", "INVENTORY_VIEW"]
    };

    public string Create(ManagedUser user)
    {
        var roles = user.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = roles.SelectMany(role => RolePermissions.TryGetValue(role, out var values) ? values : []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new TokenPayload(user.Id, user.OrganizationId, user.Username, roles, permissions, DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds()));
        var encodedPayload = Base64Url(payload);
        var signature = Sign(encodedPayload);
        return $"{encodedPayload}.{signature}";
    }

    public bool TryRead(string token, out SessionIdentity identity)
    {
        identity = default!;
        var parts = token.Split('.', 2);
        if (parts.Length != 2 || !CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(Base64Padding(parts[1])), Convert.FromBase64String(Base64Padding(Sign(parts[0]))))) return false;
        try
        {
            var payload = JsonSerializer.Deserialize<TokenPayload>(FromBase64Url(parts[0]));
            if (payload is null || payload.ExpiresAt < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
            identity = new SessionIdentity(payload.UserId, payload.OrganizationId, payload.Username, payload.Roles, payload.Permissions);
            return true;
        }
        catch (FormatException) { return false; }
        catch (JsonException) { return false; }
    }

    private string Sign(string value)
    {
        var bytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(value));
        return Base64Url(bytes);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string value) => Convert.FromBase64String(Base64Padding(value).Replace('-', '+').Replace('_', '/'));
    private static string Base64Padding(string value) => value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4);
    private sealed record TokenPayload(long UserId, long OrganizationId, string Username, string[] Roles, string[] Permissions, long ExpiresAt);
}
