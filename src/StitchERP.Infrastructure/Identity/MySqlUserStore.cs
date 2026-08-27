using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StitchERP.Application.Identity;
using StitchERP.Infrastructure.Data;

namespace StitchERP.Infrastructure.Identity;

public sealed class MySqlUserStore : IUserStore
{
    private readonly StitchErpDbContext db;

    public MySqlUserStore(StitchErpDbContext db)
    {
        this.db = db;
        EnsureBootstrapAdmin();
    }

    public IReadOnlyCollection<ManagedUser> GetUsers()
    {
        using var command = Command("SELECT u.user_id, u.username, u.email, CONCAT(u.first_name, ' ', u.last_name), u.organization_id, u.is_active, u.password_hash, GROUP_CONCAT(r.role_code) FROM app_users u LEFT JOIN app_user_roles ur ON ur.user_id = u.user_id LEFT JOIN app_roles r ON r.role_id = ur.role_id GROUP BY u.user_id");
        using var reader = command.ExecuteReader();
        var users = new List<ManagedUser>();
        while (reader.Read()) users.Add(Read(reader));
        return users;
    }

    public ManagedUser? Find(string usernameOrEmail) => GetUsers().FirstOrDefault(x => x.Username.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase) || x.Email.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase));

    public ManagedUser Create(string username, string email, string displayName, long organizationId, string password, IReadOnlyCollection<string> roles)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName) || password.Length < 8) throw new ArgumentException("Username, email, display name, and a password of at least 8 characters are required.");
        var names = displayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        using var connection = db.Database.GetDbConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO app_users (organization_id, username, email, first_name, last_name, is_active, password_hash) VALUES (@org, @username, @email, @first, @last, 1, @hash); SELECT LAST_INSERT_ID();";
        Add(command, "@org", organizationId); Add(command, "@username", username.Trim()); Add(command, "@email", email.Trim()); Add(command, "@first", names[0]); Add(command, "@last", names.Length > 1 ? names[1] : names[0]); Add(command, "@hash", InMemoryUserStore.Hash(password));
        var id = Convert.ToInt64(command.ExecuteScalar());
        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var roleCommand = connection.CreateCommand(); roleCommand.Transaction = transaction;
            roleCommand.CommandText = "INSERT INTO app_user_roles (user_id, role_id) SELECT @user, role_id FROM app_roles WHERE organization_id = @org AND role_code = @role";
            Add(roleCommand, "@user", id); Add(roleCommand, "@org", organizationId); Add(roleCommand, "@role", role);
            roleCommand.ExecuteNonQuery();
        }
        transaction.Commit();
        return Find(username)!;
    }

    public ManagedUser SetStatus(long id, bool isActive) { Execute("UPDATE app_users SET is_active = @active, updated_at = CURRENT_TIMESTAMP WHERE user_id = @id", ("@active", isActive), ("@id", id)); return FindById(id); }
    public ManagedUser SetRoles(long id, IReadOnlyCollection<string> roles)
    {
        using var connection = db.Database.GetDbConnection(); connection.Open(); using var transaction = connection.BeginTransaction();
        ExecuteOn(connection, transaction, "DELETE FROM app_user_roles WHERE user_id = @id", ("@id", id));
        foreach (var role in roles) ExecuteOn(connection, transaction, "INSERT INTO app_user_roles (user_id, role_id) SELECT @user, role_id FROM app_roles WHERE organization_id = (SELECT organization_id FROM app_users WHERE user_id = @user) AND role_code = @role", ("@user", id), ("@role", role));
        transaction.Commit(); return FindById(id);
    }
    public ManagedUser SetPassword(long id, string password) { if (password.Length < 8) throw new ArgumentException("Password must be at least 8 characters."); Execute("UPDATE app_users SET password_hash = @hash, updated_at = CURRENT_TIMESTAMP WHERE user_id = @id", ("@hash", InMemoryUserStore.Hash(password)), ("@id", id)); return FindById(id); }
    public ManagedUser ChangePassword(ChangePasswordRequest request) { var user = FindById(request.UserId); if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(user.PasswordHash), Convert.FromBase64String(InMemoryUserStore.Hash(request.CurrentPassword)))) throw new UnauthorizedAccessException("Current password is incorrect."); return SetPassword(request.UserId, request.NewPassword); }

    private void EnsureBootstrapAdmin()
    {
        var password = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(password)) return;
        using var connection = db.Database.GetDbConnection(); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO app_users (organization_id, username, email, first_name, last_name, is_active, password_hash) SELECT 1, 'admin', 'admin@stitcherp.local', 'System', 'Administrator', 1, @hash WHERE NOT EXISTS (SELECT 1 FROM app_users WHERE username = 'admin'); INSERT INTO app_user_roles (user_id, role_id) SELECT u.user_id, r.role_id FROM app_users u JOIN app_roles r ON r.organization_id = u.organization_id AND r.role_code = 'ADMIN' WHERE u.username = 'admin' AND NOT EXISTS (SELECT 1 FROM app_user_roles ur WHERE ur.user_id = u.user_id AND ur.role_id = r.role_id);";
        Add(command, "@hash", InMemoryUserStore.Hash(password));
        command.ExecuteNonQuery();
    }

    private ManagedUser FindById(long id) => GetUsers().First(x => x.Id == id);
    private IDbCommand Command(string sql) { var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; if (command.Connection!.State != ConnectionState.Open) command.Connection.Open(); return command; }
    private void Execute(string sql, params (string Name, object Value)[] parameters) { using var command = Command(sql); foreach (var parameter in parameters) Add(command, parameter.Name, parameter.Value); command.ExecuteNonQuery(); }
    private static void ExecuteOn(IDbConnection connection, IDbTransaction transaction, string sql, params (string Name, object Value)[] parameters) { using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; foreach (var parameter in parameters) Add(command, parameter.Name, parameter.Value); command.ExecuteNonQuery(); }
    private static void Add(IDbCommand command, string name, object value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
    private static ManagedUser Read(IDataRecord reader)
    {
        var roles = reader.IsDBNull(7) ? ["VIEWER"] : reader.GetString(7).Split(',', StringSplitOptions.RemoveEmptyEntries);
        var passwordHash = reader.IsDBNull(6) ? InMemoryUserStore.Hash("StitchERP-Demo-2026") : reader.GetString(6);
        return new ManagedUser(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), roles, reader.GetBoolean(5), passwordHash);
    }
}
