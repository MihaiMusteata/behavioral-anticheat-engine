namespace BehavioralAnticheatEngine.Domain.Identity;

public sealed class User
{
    private User()
    {
    }

    private User(string email, string normalizedEmail, string? passwordHash, string role, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        Email = email;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
        Role = role;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    // Null for guest accounts (see CreateGuest) - they authenticate via a
    // one-time PIN-issued JWT, never a password, so there is nothing to
    // hash or verify for them.
    public string? PasswordHash { get; private set; }

    public string Role { get; private set; } = UserRoles.Student;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    public static User Create(
        string email,
        string normalizedEmail,
        string passwordHash,
        DateTimeOffset createdAtUtc,
        string role = UserRoles.Student)
    {
        if (!UserRoles.All.Contains(role, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Unsupported user role.");
        }

        return new User(email, normalizedEmail, passwordHash, role, createdAtUtc);
    }

    // For guest/PIN-joined accounts only - never for anything a real person
    // logs into with a password (admin, proctor, self-registered students).
    public static User CreateGuest(
        string email,
        string normalizedEmail,
        DateTimeOffset createdAtUtc,
        string role = UserRoles.Student)
    {
        if (!UserRoles.All.Contains(role, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Unsupported user role.");
        }

        return new User(email, normalizedEmail, passwordHash: null, role, createdAtUtc);
    }

    public void RecordLogin(DateTimeOffset loggedInAtUtc)
    {
        LastLoginAtUtc = loggedInAtUtc;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
