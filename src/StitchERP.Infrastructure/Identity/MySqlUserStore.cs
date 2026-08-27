using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using StitchERP.Application.Identity;
using StitchERP.Infrastructure.Data;

namespace StitchERP.Infrastructure.Identity;

public sealed class MySqlUserStore : IUserStore
{
    private readonly StitchErpDbContext db;

    public MySqlUserStore(StitchErpDbContext db)
    {
        this.db = db;
        EnsureLifecycleSchema();
        EnsureBootstrapAdmin();
        EnsureOwnerAccount();
    }

    public IReadOnlyCollection<ManagedUser> GetUsers()
    {
        using var command = Command("SELECT u.user_id, u.username, u.email, CONCAT(u.first_name, ' ', u.last_name), u.organization_id, u.is_active, u.email_verified, u.password_hash, GROUP_CONCAT(r.role_code) FROM app_users u LEFT JOIN app_user_roles ur ON ur.user_id = u.user_id LEFT JOIN app_roles r ON r.role_id = ur.role_id WHERE u.deleted_at IS NULL GROUP BY u.user_id");
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
        using var connection = NewConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO app_users (organization_id, username, email, first_name, last_name, is_active, email_verified, password_hash) VALUES (@org, @username, @email, @first, @last, 1, 0, @hash); SELECT LAST_INSERT_ID();";
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
        using var connection = NewConnection(); connection.Open(); using var transaction = connection.BeginTransaction();
        ExecuteOn(connection, transaction, "DELETE FROM app_user_roles WHERE user_id = @id", ("@id", id));
        foreach (var role in roles) ExecuteOn(connection, transaction, "INSERT INTO app_user_roles (user_id, role_id) SELECT @user, role_id FROM app_roles WHERE organization_id = (SELECT organization_id FROM app_users WHERE user_id = @user) AND role_code = @role", ("@user", id), ("@role", role));
        transaction.Commit(); return FindById(id);
    }
    public ManagedUser SetPassword(long id, string password) { if (password.Length < 8) throw new ArgumentException("Password must be at least 8 characters."); Execute("UPDATE app_users SET password_hash = @hash, updated_at = CURRENT_TIMESTAMP WHERE user_id = @id", ("@hash", InMemoryUserStore.Hash(password)), ("@id", id)); return FindById(id); }
    public ManagedUser ChangePassword(ChangePasswordRequest request) { var user = FindById(request.UserId); if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(user.PasswordHash), Convert.FromBase64String(InMemoryUserStore.Hash(request.CurrentPassword)))) throw new UnauthorizedAccessException("Current password is incorrect."); return SetPassword(request.UserId, request.NewPassword); }
    public ManagedUser Delete(long id) { Execute("UPDATE app_users SET is_active = 0, deleted_at = CURRENT_TIMESTAMP WHERE user_id = @id", ("@id", id)); return FindById(id); }
    public string CreateVerificationToken(long id) { var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); using var command = Command("INSERT INTO email_verification_tokens (user_id, token_hash, expires_at) VALUES (@id, @hash, DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 24 HOUR))"); Add(command, "@id", id); Add(command, "@hash", HashToken(token)); command.ExecuteNonQuery(); return token; }
    public ManagedUser VerifyEmail(string token) { using var command = Command("UPDATE app_users u JOIN email_verification_tokens t ON t.user_id = u.user_id SET u.email_verified = 1, t.used_at = CURRENT_TIMESTAMP WHERE t.token_hash = @hash AND t.used_at IS NULL AND t.expires_at > CURRENT_TIMESTAMP"); Add(command, "@hash", HashToken(token)); if(command.ExecuteNonQuery() != 1) throw new UnauthorizedAccessException("Email verification token is invalid or expired."); using var lookup = Command("SELECT u.user_id FROM app_users u JOIN email_verification_tokens t ON t.user_id=u.user_id WHERE t.token_hash=@hash"); Add(lookup, "@hash", HashToken(token)); return FindById(Convert.ToInt64(lookup.ExecuteScalar())); }

    private void EnsureBootstrapAdmin()
    {
        var password = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(password)) return;
        using var connection = NewConnection(); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO app_roles (organization_id, role_name, role_code, is_system) SELECT 1, 'Admin', 'ADMIN', 1 WHERE NOT EXISTS (SELECT 1 FROM app_roles WHERE organization_id = 1 AND role_code = 'ADMIN'); INSERT INTO app_users (organization_id, username, email, first_name, last_name, is_active, password_hash) SELECT 1, 'admin', 'admin@stitcherp.local', 'System', 'Administrator', 1, @hash WHERE NOT EXISTS (SELECT 1 FROM app_users WHERE username = 'admin'); INSERT INTO app_user_roles (user_id, role_id) SELECT u.user_id, r.role_id FROM app_users u JOIN app_roles r ON r.organization_id = u.organization_id AND r.role_code = 'ADMIN' WHERE u.username = 'admin' AND NOT EXISTS (SELECT 1 FROM app_user_roles ur WHERE ur.user_id = u.user_id AND ur.role_id = r.role_id);";
        Add(command, "@hash", InMemoryUserStore.Hash(password));
        command.ExecuteNonQuery();
    }

    private void EnsureOwnerAccount()
    {
        var password = Environment.GetEnvironmentVariable("OWNER_PASSWORD");
        if (string.IsNullOrWhiteSpace(password)) return;
        using var connection = NewConnection(); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO app_roles (organization_id, role_name, role_code, is_system) SELECT 1, 'Super Admin', 'SUPER_ADMIN', 1 WHERE NOT EXISTS (SELECT 1 FROM app_roles WHERE organization_id = 1 AND role_code = 'SUPER_ADMIN'); UPDATE app_users SET username = 'fahadbhutta', password_hash = @hash, email_verified = 1, is_active = 1, deleted_at = NULL WHERE email = 'fahad.bhutta@stitcherp.local'; DELETE ur FROM app_user_roles ur JOIN app_users u ON u.user_id = ur.user_id WHERE u.username = 'fahadbhutta'; INSERT INTO app_user_roles (user_id, role_id) SELECT u.user_id, r.role_id FROM app_users u JOIN app_roles r ON r.organization_id = 1 AND r.role_code = 'SUPER_ADMIN' WHERE u.username = 'fahadbhutta';";
        Add(command, "@hash", InMemoryUserStore.Hash(password));
        command.ExecuteNonQuery();
    }

    private void EnsureLifecycleSchema()
    {
        using var connection = NewConnection(); connection.Open();
        ExecuteSchema(connection, "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'app_users' AND column_name = 'email_verified'", "ALTER TABLE app_users ADD COLUMN email_verified TINYINT DEFAULT 0 NOT NULL");
        ExecuteSchema(connection, "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'app_users' AND column_name = 'deleted_at'", "ALTER TABLE app_users ADD COLUMN deleted_at TIMESTAMP NULL");
        ExecuteSchema(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'email_verification_tokens'", "CREATE TABLE email_verification_tokens (email_verification_token_id BIGINT AUTO_INCREMENT PRIMARY KEY, user_id BIGINT NOT NULL, token_hash VARCHAR(128) NOT NULL UNIQUE, expires_at TIMESTAMP NOT NULL, used_at TIMESTAMP NULL, created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL, CONSTRAINT fk_email_verification_user FOREIGN KEY (user_id) REFERENCES app_users(user_id))");
    }

    private static void ExecuteSchema(MySqlConnection connection, string checkSql, string createSql)
    {
        using var check = connection.CreateCommand(); check.CommandText = checkSql;
        if (Convert.ToInt32(check.ExecuteScalar()) == 0) { using var create = connection.CreateCommand(); create.CommandText = createSql; create.ExecuteNonQuery(); }
    }

    private ManagedUser FindById(long id) => GetUsers().First(x => x.Id == id);
    private MySqlConnection NewConnection() => new(db.Database.GetConnectionString() ?? throw new InvalidOperationException("Database connection string is required."));
    private IDbCommand Command(string sql) { var connection = NewConnection(); connection.Open(); var command = connection.CreateCommand(); command.CommandText = sql; return command; }
    private void Execute(string sql, params (string Name, object Value)[] parameters) { using var command = Command(sql); foreach (var parameter in parameters) Add(command, parameter.Name, parameter.Value); command.ExecuteNonQuery(); }
    private static void ExecuteOn(IDbConnection connection, IDbTransaction transaction, string sql, params (string Name, object Value)[] parameters) { using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; foreach (var parameter in parameters) Add(command, parameter.Name, parameter.Value); command.ExecuteNonQuery(); }
    private static void Add(IDbCommand command, string name, object value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    private static ManagedUser Read(IDataRecord reader)
    {
        var roles = reader.IsDBNull(8) ? ["VIEWER"] : reader.GetString(8).Split(',', StringSplitOptions.RemoveEmptyEntries);
        var passwordHash = reader.IsDBNull(7) ? InMemoryUserStore.Hash("StitchERP-Demo-2026") : reader.GetString(7);
        return new ManagedUser(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), roles, reader.GetBoolean(5), reader.GetBoolean(6), passwordHash);
    }
}
