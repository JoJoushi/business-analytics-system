using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Domain.Entities;

namespace BusinessAnalytics.Application.DTOs;

public record UserDto(long Id, string Username, string Email, string FullName, string Department, string Role, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static UserDto From(User u) => new(u.Id, u.Username, u.Email, u.FullName, u.Department, u.Role, u.IsActive, u.CreatedAt, u.UpdatedAt);
}

public record CreateUserRequest(string Username, string Email, string FullName, string Department, string Role = UserRoles.Member);

public record UpdateUserRequest(string Username, string Email, string FullName, string Department, string Role, bool IsActive);

public record UserQuery : PagedQuery
{
    public string? Department { get; init; }
    public bool? IsActive { get; init; }
}
