using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Modules;
using WasteSubmitRegPackaging.SubmissionPeriods.Contracts;
using WasteSubmitRegPackaging.SubmissionPeriods.Endpoints;
using WasteSubmitRegPackaging.SubmissionPeriods.Infrastructure;

namespace WasteSubmitRegPackaging.SubmissionPeriods;

/// <summary>
/// The module's only public entry point. Other modules use <see cref="ISubmissionPeriodsApi"/>, and may join to
/// the <c>period_*</c> tables in SQL.
/// </summary>
public static class SubmissionPeriodsModule
{
    public static IServiceCollection AddSubmissionPeriodsModule(this IServiceCollection services)
    {
        services.AddReadOnlyModuleDbContext<SubmissionPeriodsDbContext>();
        services.AddHandlersFromAssembly(typeof(SubmissionPeriodsModule).Assembly);

        // The contract other modules call.
        services.AddScoped<ISubmissionPeriodsApi, SubmissionPeriodsApi>();

        // Registers this module's request types with .NET's validation generator. Each module calls it, because
        // the generator only sees the request types in the project that makes the call.
        services.AddValidation();

        return services;
    }

    public static IEndpointRouteBuilder MapSubmissionPeriodsEndpoints(this IEndpointRouteBuilder app)
    {
        SubmissionPeriodsEndpoints.Map(app);
        return app;
    }
}
