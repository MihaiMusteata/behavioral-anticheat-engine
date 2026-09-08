namespace BehavioralAnticheatEngine.Application.Common.Exceptions;

public sealed class ApplicationUnauthorizedException : Exception
{
    public ApplicationUnauthorizedException(string message)
        : base(message)
    {
    }
}
