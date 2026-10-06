using BusinessAnalytics.Application.Interfaces.Persistence;
using BusinessAnalytics.Infrastructure.Persistence;
using BusinessAnalytics.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BusinessAnalytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        DapperConfig.Configure();

        // One pooled data source per app; connections are rented per request by DbSession.
        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());

        services.AddScoped<DbSession>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ISystemLogRepository, SystemLogRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
