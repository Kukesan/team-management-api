namespace Application.Common.Exceptions;

/// <summary>
/// Thrown by the service layer for ownership/role checks that a controller-level
/// [Authorize] attribute cannot express (e.g. "this report belongs to someone else").
/// Mapped to HTTP 403 by the global exception middleware.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
