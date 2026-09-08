namespace BehavioralAnticheatEngine.Application.Common.Exceptions;

public sealed class ApplicationConflictException : Exception
{
    public ApplicationConflictException(string message)
        : base(message)
    {
    }
}
