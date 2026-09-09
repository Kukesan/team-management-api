namespace Application.Common.Exceptions;

/// <summary>
/// Thrown when a call to a dependent external service (currently: team-management-ai)
/// fails -- timeout, network error, or a non-success response. Mapped to HTTP 503 by
/// ExceptionHandlingMiddleware, distinct from a 500: the caller's request was valid and
/// this API is healthy, but a downstream dependency was not.
/// </summary>
public class ExternalServiceException : Exception
{
    public ExternalServiceException(string message) : base(message)
    {
    }

    public ExternalServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
