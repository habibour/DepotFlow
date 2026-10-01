using DepotFlow.Application;

namespace DepotFlow.Api.Auth;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string UserId =>
        UserIdOrNull ?? throw new InvalidOperationException("There is no signed-in user for this request.");

    public string? UserIdOrNull => httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

    public string? Email => httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value;
}
