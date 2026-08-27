using System.Security.Cryptography;
using System.Text;

namespace StitchERP.Application.Identity;

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(long UserId, string Username, string DisplayName, long OrganizationId, IReadOnlyCollection<string> Roles, string AccessToken);
public sealed record ForgotPasswordRequest(string UsernameOrEmail);
public sealed record ForgotPasswordResponse(string Message, string? DevelopmentResetToken);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record RoleNotification(long Id, string Type, string Title, string Message, bool IsRead, DateTime CreatedAtUtc, IReadOnlyCollection<string> TargetRoles);

public interface IAuthenticationService
{
    LoginResponse Login(LoginRequest request);
    ManagedUser GetUser(long userId);
    ForgotPasswordResponse RequestPasswordReset(ForgotPasswordRequest request);
    void ResetPassword(ResetPasswordRequest request);
    IReadOnlyCollection<RoleNotification> GetNotifications(long userId, IReadOnlyCollection<string> roles);
    void ChangePassword(ChangePasswordRequest request);
}

public sealed class AuthenticationService(IUserStore userStore) : IAuthenticationService
{
    private readonly List<RoleNotification> notifications =
    [
        new(1, "APPROVAL", "BOM approval required", "A submitted BOM is waiting for your review.", false, DateTime.UtcNow, ["MANAGER", "PRODUCTION_MANAGER"]),
        new(2, "P2P", "Goods receipt pending", "An approved purchase order is ready for receiving.", false, DateTime.UtcNow, ["MANAGER", "INVENTORY_MANAGER", "INVENTORY_USER"])
    ];

    private readonly Dictionary<string, PasswordReset> resetTokens = new(StringComparer.Ordinal);

    public LoginResponse Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Username and password are required.");
        var user = userStore.Find(request.Username);
        if (user is null || !user.IsActive || !user.EmailVerified || !CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(user.PasswordHash), Convert.FromBase64String(InMemoryUserStore.Hash(request.Password))))
            throw new UnauthorizedAccessException(user is not null && !user.EmailVerified ? "Email address must be verified before signing in." : "Invalid username or password.");
        return new LoginResponse(user.Id, user.Username, user.DisplayName, user.OrganizationId, user.Roles, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public ManagedUser GetUser(long userId) => userStore.GetUsers().First(x => x.Id == userId);

    public ForgotPasswordResponse RequestPasswordReset(ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail))
            throw new ArgumentException("Username or email is required.");

        var user = userStore.Find(request.UsernameOrEmail.Trim());
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        if (user is not null)
        {
            lock (resetTokens)
            {
                resetTokens[token] = new PasswordReset(user.Id, DateTime.UtcNow.AddMinutes(30));
            }
        }

        return new ForgotPasswordResponse("If the account exists, password reset instructions have been generated.", user is null ? null : token);
    }

    public void ResetPassword(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.NewPassword.Length < 8)
            throw new ArgumentException("A valid reset token and a password of at least 8 characters are required.");

        lock (resetTokens)
        {
            if (!resetTokens.Remove(request.Token, out var reset) || reset.ExpiresAtUtc <= DateTime.UtcNow)
                throw new UnauthorizedAccessException("The reset link is invalid or expired.");
            userStore.SetPassword(reset.UserId, request.NewPassword);
        }
    }

    public void ChangePassword(ChangePasswordRequest request) => userStore.ChangePassword(request);

    public IReadOnlyCollection<RoleNotification> GetNotifications(long userId, IReadOnlyCollection<string> roles)
    {
        if (userStore.GetUsers().All(x => x.Id != userId)) throw new UnauthorizedAccessException("User was not found.");
        if (roles.Any(role => role.Equals("SUPER_ADMIN", StringComparison.OrdinalIgnoreCase) || role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))) return notifications.ToArray();
        return notifications.Where(notification => notification.TargetRoles.Any(role => roles.Contains(role, StringComparer.OrdinalIgnoreCase))).ToArray();
    }

    private sealed record PasswordReset(long UserId, DateTime ExpiresAtUtc);
}
