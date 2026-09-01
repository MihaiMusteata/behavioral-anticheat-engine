namespace BehavioralAnticheatEngine.Domain.Identity;

public static class UserRoles
{
    public const string Student = "Student";
    public const string Proctor = "Proctor";
    public const string Admin = "Admin";

    public static readonly string[] All = [Student, Proctor, Admin];
}
