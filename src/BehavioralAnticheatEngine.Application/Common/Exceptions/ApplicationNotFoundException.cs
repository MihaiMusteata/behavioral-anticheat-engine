namespace BehavioralAnticheatEngine.Application.Common.Exceptions;

public sealed class ApplicationNotFoundException : Exception
{
    public ApplicationNotFoundException(string message)
        : base(message)
    {
    }
}
