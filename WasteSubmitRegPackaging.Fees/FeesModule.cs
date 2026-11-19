using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WasteSubmitRegPackaging.Fees.Contracts;
using WasteSubmitRegPackaging.Fees.Endpoints;
using WasteSubmitRegPackaging.Fees.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Modules;

namespace WasteSubmitRegPackaging.Fees;

/// <summary>
/// The module's only public entry point. Other modules use <see cref="IFeesApi"/>, and may join to the
/// <c>fee_*</c> tables in SQL.
/// </summary>
public static class FeesModule
{
    public static IServiceCollection AddFeesModule(this IServiceCollection services)
    {
        services.AddReadOnlyModuleDbContext<FeesDbContext>();
        services.AddHandlersFromAssembly(typeof(FeesModule).Assembly);

        // The contract other modules call.
        services.AddScoped<IFeesApi, FeesApi>();

        services.AddValidation();

        return services;
    }

    public static IEndpointRouteBuilder MapFeesEndpoints(this IEndpointRouteBuilder app)
    {
        FeesEndpoints.Map(app);
        return app;
    }
}
