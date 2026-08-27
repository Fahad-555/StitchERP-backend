using System.Security.Cryptography;
using System.Text;

namespace StitchERP.Application.Identity;

public sealed record ManagedUser(long Id, string Username, string Email, string DisplayName, long OrganizationId, IReadOnlyCollection<string> Roles, bool IsActive, bool EmailVerified, string PasswordHash);
public sealed record ChangePasswordRequest(long UserId, string CurrentPassword, string NewPassword);

public interface IUserStore
{
    IReadOnlyCollection<ManagedUser> GetUsers();
    ManagedUser? Find(string usernameOrEmail);
    ManagedUser Create(string username, string email, string displayName, long organizationId, string password, IReadOnlyCollection<string> roles);
    ManagedUser SetStatus(long id, bool isActive);
    ManagedUser SetRoles(long id, IReadOnlyCollection<string> roles);
    ManagedUser SetPassword(long id, string password);
    ManagedUser ChangePassword(ChangePasswordRequest request);
    ManagedUser Delete(long id);
    string CreateVerificationToken(long id);
    ManagedUser VerifyEmail(string token);
}

public sealed class InMemoryUserStore : IUserStore
{
    private readonly object sync = new();
    private readonly List<ManagedUser> users =
    [
        new(1, "fahadbhutta", "fahad.bhutta@stitcherp.local", "Fahad Bhutta", 1, ["SUPER_ADMIN"], true, true, Hash("Pakistan123@"))
    ];
    private long nextId = 1;

    public IReadOnlyCollection<ManagedUser> GetUsers() { lock (sync) return users.ToArray(); }

    public ManagedUser? Find(string usernameOrEmail)
    {
        lock (sync) return users.FirstOrDefault(x => x.Username.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase) || x.Email.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase));
    }

    public ManagedUser Create(string username, string email, string displayName, long organizationId, string password, IReadOnlyCollection<string> roles)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName) || password.Length < 8)
            throw new ArgumentException("Username, email, display name, and a password of at least 8 characters are required.");
        lock (sync)
        {
            if (users.Any(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase) || x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Username or email already exists.");
            var user = new ManagedUser(++nextId, username.Trim(), email.Trim(), displayName.Trim(), organizationId, roles, true, false, Hash(password));
            users.Add(user);
            return user;
        }
    }

    public ManagedUser SetStatus(long id, bool isActive) => Update(id, user => user with { IsActive = isActive });
    public ManagedUser SetRoles(long id, IReadOnlyCollection<string> roles) => Update(id, user => user with { Roles = roles });
    public ManagedUser SetPassword(long id, string password)
    {
        if (password.Length < 8) throw new ArgumentException("Password must be at least 8 characters.");
        return Update(id, user => user with { PasswordHash = Hash(password) });
    }

    public ManagedUser Delete(long id) => Update(id, user => user with { IsActive = false });
    public string CreateVerificationToken(long id) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public ManagedUser VerifyEmail(string token) => throw new UnauthorizedAccessException("Email verification token is invalid or expired.");

    public ManagedUser ChangePassword(ChangePasswordRequest request)
    {
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(users.First(x => x.Id == request.UserId).PasswordHash), Convert.FromBase64String(Hash(request.CurrentPassword))))
            throw new UnauthorizedAccessException("Current password is incorrect.");
        return SetPassword(request.UserId, request.NewPassword);
    }

    private ManagedUser Update(long id, Func<ManagedUser, ManagedUser> update)
    {
        lock (sync)
        {
            var index = users.FindIndex(x => x.Id == id);
            if (index < 0) throw new KeyNotFoundException("User was not found.");
            users[index] = update(users[index]);
            return users[index];
        }
    }

    public static string Hash(string value) => Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetBytes("stitcherp-demo-salt"), 120_000, HashAlgorithmName.SHA256, 32));
}
