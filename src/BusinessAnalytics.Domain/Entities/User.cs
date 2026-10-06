namespace BusinessAnalytics.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.Member;
    public bool IsActive { get; set; } = true;
}

public static class UserRoles
{
    public const string Admin = "admin";
    public const string Manager = "manager";
    public const string Member = "member";

    public static readonly string[] All = [Admin, Manager, Member];
}
