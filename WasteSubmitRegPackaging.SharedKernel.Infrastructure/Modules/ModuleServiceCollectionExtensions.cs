using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using WasteSubmitRegPackaging.SharedKernel.Users;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Outbox;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;
using WasteSubmitRegPackaging.SharedKernel.Messaging;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Modules;

public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// The services every module needs. The host calls this once, before adding the modules.
    /// </summary>
    public static IServiceCollection AddSharedKernel(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, HeaderCurrentUser>();
        services.TryAddSingleton<DomainEventsToOutboxInterceptor>();
        services.TryAddScoped<IOutboxMessagePublisher, LoggingOutboxMessagePublisher>();
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection("Outbox"));

        return services;
    }

    /// <summary>
    /// Registers a module's DbContext on the host's <see cref="NpgsqlDataSource"/>, which brings CDP's IAM
    /// token refresh and TLS. Column names follow the snake_case convention, so C# <c>ApplicationReferenceNumber</c>
    /// maps to <c>application_reference_number</c> in the Liquibase changelog. Also starts the module's
    /// outbox dispatcher.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services)
        where TContext : ModuleDbContext
    {
        services.AddDbContext<TContext>((serviceProvider, options) => options
            .UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetRequiredService<DomainEventsToOutboxInterceptor>()));

        services.AddHostedService<OutboxDispatcher<TContext>>();

        return services;
    }

    /// <summary>
    /// Registers a read-only module's DbContext (see <see cref="ReadOnlyDbContext"/>) on the host's
    /// <see cref="NpgsqlDataSource"/>, with the same naming convention. No outbox dispatcher.
    /// </summary>
    public static IServiceCollection AddReadOnlyModuleDbContext<TContext>(this IServiceCollection services)
        where TContext : ReadOnlyDbContext
    {
        services.AddDbContext<TContext>((serviceProvider, options) => options
            .UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>())
            .UseSnakeCaseNamingConvention());

        return services;
    }

    /// <summary>
    /// Registers every command and query handler in a module's assembly, internal ones included, as
    /// scoped. Endpoints ask for <c>ICommandHandler&lt;TCommand, TResponse&gt;</c>, so there is no
    /// mediator and no reflection when a request is handled.
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var handlerInterfaces = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>) };

        var registrations = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition()))
                .Select(i => (Service: i, Implementation: type)));

        foreach (var (service, implementation) in registrations)
            services.AddScoped(service, implementation);

        return services;
    }
}
