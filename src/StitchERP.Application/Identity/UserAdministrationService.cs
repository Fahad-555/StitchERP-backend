namespace StitchERP.Application.Identity;

public sealed record CreateUserRequest(string Username, string Email, string DisplayName, long OrganizationId, string TemporaryPassword, IReadOnlyCollection<string> Roles);
public sealed record SetUserStatusRequest(bool IsActive);
public sealed record SetUserRolesRequest(IReadOnlyCollection<string> Roles);
public sealed record SetUserPasswordRequest(string Password);

public interface IUserAdministrationService
{
    IReadOnlyCollection<ManagedUser> GetUsers();
    ManagedUser Create(CreateUserRequest request);
    ManagedUser SetStatus(long id, bool isActive);
    ManagedUser SetRoles(long id, IReadOnlyCollection<string> roles);
    ManagedUser SetPassword(long id, string password);
}

public sealed class UserAdministrationService(IUserStore userStore) : IUserAdministrationService
{
    public IReadOnlyCollection<ManagedUser> GetUsers() => userStore.GetUsers().Select(x => x with { PasswordHash = string.Empty }).ToArray();
    public ManagedUser Create(CreateUserRequest request) => userStore.Create(request.Username, request.Email, request.DisplayName, request.OrganizationId, request.TemporaryPassword, request.Roles) with { PasswordHash = string.Empty };
    public ManagedUser SetStatus(long id, bool isActive) => userStore.SetStatus(id, isActive) with { PasswordHash = string.Empty };
    public ManagedUser SetRoles(long id, IReadOnlyCollection<string> roles) => userStore.SetRoles(id, roles) with { PasswordHash = string.Empty };
    public ManagedUser SetPassword(long id, string password) => userStore.SetPassword(id, password) with { PasswordHash = string.Empty };
}