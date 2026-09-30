namespace DepotFlow.Application.Auth;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string Role);
