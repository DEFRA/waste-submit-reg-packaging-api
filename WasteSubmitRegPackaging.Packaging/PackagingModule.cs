using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.Packaging.Endpoints;
using WasteSubmitRegPackaging.Packaging.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Modules;

namespace WasteSubmitRegPackaging.Packaging;

/// <summary>The module's only public entry point.</summary>
public static class PackagingModule
{
    public static IServiceCollection AddPackagingModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<PackagingDbContext>();
        services.AddHandlersFromAssembly(typeof(PackagingModule).Assembly);
        services.AddScoped<IPackagingSubmissionRepository, PackagingSubmissionRepository>();
        services.AddValidation();

        return services;
    }

    public static IEndpointRouteBuilder MapPackagingEndpoints(this IEndpointRouteBuilder app)
    {
        PackagingSubmissionEndpoints.Map(app);
        return app;
    }
}
