namespace DepotFlow.Application.Auth;

public interface IAuthService
{
    /// <summary>Returns null when the email or password is wrong (callers must not say which).</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
