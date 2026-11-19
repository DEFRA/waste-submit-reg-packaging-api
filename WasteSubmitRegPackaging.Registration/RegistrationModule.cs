using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WasteSubmitRegPackaging.Registration.Contracts;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.Registration.Endpoints;
using WasteSubmitRegPackaging.Registration.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Modules;

namespace WasteSubmitRegPackaging.Registration;

/// <summary>
/// The module's only public entry point. The host calls <see cref="AddRegistrationModule"/> and
/// <see cref="MapRegistrationEndpoints"/>, and knows nothing else about Registration.
/// </summary>
public static class RegistrationModule
{
    public static IServiceCollection AddRegistrationModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<RegistrationDbContext>();
        services.AddHandlersFromAssembly(typeof(RegistrationModule).Assembly);

        services.AddScoped<IRegistrationSubmissionRepository, RegistrationSubmissionRepository>();

        // The contract other modules call.
        services.AddScoped<IRegistrationModuleApi, RegistrationModuleApi>();

        // Built-in validation is source-generated per assembly. Calling it here lets the generator find
        // this module's request types.
        services.AddValidation();

        return services;
    }

    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        RegistrationEndpoints.Map(app);
        return app;
    }
}
