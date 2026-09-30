namespace DepotFlow.Application;

/// <summary>The signed-in user making the current request. Implemented in the Api layer from the JWT "sub" claim.</summary>
public interface ICurrentUser
{
    /// <exception cref="InvalidOperationException">There is no signed-in user.</exception>
    string UserId { get; }
}
