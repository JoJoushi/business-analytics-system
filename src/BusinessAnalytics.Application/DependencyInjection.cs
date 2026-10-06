using BusinessAnalytics.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessAnalytics.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISystemLogService, SystemLogService>();
        return services;
    }
}
