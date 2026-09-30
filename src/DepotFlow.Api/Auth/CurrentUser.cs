using DepotFlow.Application;

namespace DepotFlow.Api.Auth;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string UserId =>
        httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("There is no signed-in user for this request.");
}
