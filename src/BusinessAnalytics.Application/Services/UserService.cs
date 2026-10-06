using BusinessAnalytics.Application.Common.Exceptions;
using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Interfaces.Persistence;
using BusinessAnalytics.Domain.Entities;
using FluentValidation;

namespace BusinessAnalytics.Application.Services;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query, CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public class UserService(
    IUnitOfWork uow,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    IValidator<PagedQuery> queryValidator) : IUserService
{
    public async Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query, CancellationToken ct = default)
    {
        await queryValidator.ValidateAndThrowAsync(query, ct);
        var page = await uow.Users.GetPagedAsync(query, ct);
        return page.Map(UserDto.From);
    }

    public async Task<UserDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var user = await uow.Users.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(User), id);
        return UserDto.From(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        return await uow.ExecuteInTransactionAsync(async token =>
        {
            if (await uow.Users.IsUsernameOrEmailTakenAsync(request.Username, request.Email, null, token))
                throw new ConflictException("Username or email is already in use.");

            var user = new User
            {
                Username = request.Username.Trim(),
                Email = request.Email.Trim().ToLowerInvariant(),
                FullName = request.FullName.Trim(),
                Department = request.Department.Trim(),
                Role = request.Role,
                IsActive = true
            };
            user.Id = await uow.Users.AddAsync(user, token);

            await uow.SystemLogs.AddAsync(SystemLog.Create("create", "user", user.Id,
                $"User '{user.Username}' created", AuditMetadata.Of(new { user.Username, user.Department, user.Role })), token);

            return UserDto.From((await uow.Users.GetByIdAsync(user.Id, token))!);
        }, ct);
    }

    public async Task<UserDto> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);

        return await uow.ExecuteInTransactionAsync(async token =>
        {
            var user = await uow.Users.GetByIdAsync(id, token) ?? throw new NotFoundException(nameof(User), id);

            if (await uow.Users.IsUsernameOrEmailTakenAsync(request.Username, request.Email, id, token))
                throw new ConflictException("Username or email is already in use.");

            user.Username = request.Username.Trim();
            user.Email = request.Email.Trim().ToLowerInvariant();
            user.FullName = request.FullName.Trim();
            user.Department = request.Department.Trim();
            user.Role = request.Role;
            user.IsActive = request.IsActive;
            await uow.Users.UpdateAsync(user, token);

            await uow.SystemLogs.AddAsync(SystemLog.Create("update", "user", id,
                $"User '{user.Username}' updated", AuditMetadata.Of(request)), token);

            return UserDto.From((await uow.Users.GetByIdAsync(id, token))!);
        }, ct);
    }

    public Task DeleteAsync(long id, CancellationToken ct = default) =>
        uow.ExecuteInTransactionAsync(async token =>
        {
            var user = await uow.Users.GetByIdAsync(id, token) ?? throw new NotFoundException(nameof(User), id);
            await uow.Users.DeleteAsync(id, token);
            await uow.SystemLogs.AddAsync(SystemLog.Create("delete", "user", id,
                $"User '{user.Username}' deleted", level: LogLevels.Warning), token);
        }, ct);
}
