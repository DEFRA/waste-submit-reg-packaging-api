using System.Reflection;
using NetArchTest.Rules;
using WasteSubmitRegPackaging.Packaging;
using WasteSubmitRegPackaging.Registration;
using WasteSubmitRegPackaging.SharedKernel.Domain;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackagingApi.Authentication;

namespace WasteSubmitRegPackagingApi.Test.Modules;

/// <summary>
/// The structure rules, as tests, so a pull request that breaks them fails the build instead of relying
/// on review.
/// </summary>
public class ArchitectureTest
{
    private static readonly Assembly s_registration = typeof(RegistrationModule).Assembly;
    private static readonly Assembly s_packaging = typeof(PackagingModule).Assembly;
    private static readonly Assembly s_sharedKernel = typeof(AggregateRoot<>).Assembly;

    public static TheoryData<string> Modules => [s_registration.GetName().Name!, s_packaging.GetName().Name!];

    [Fact]
    public void Packaging_does_not_reference_the_registration_module_only_its_contracts()
    {
        var references = s_packaging.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain("WasteSubmitRegPackaging.Registration", references);
        Assert.Contains("WasteSubmitRegPackaging.Registration.Contracts", references);
    }

    [Fact]
    public void Registration_does_not_reference_packaging()
    {
        var references = s_registration.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(references, name => name.StartsWith("WasteSubmitRegPackaging.Packaging", StringComparison.Ordinal));
    }

    [Fact]
    public void The_shared_kernel_has_no_infrastructure_dependencies()
    {
        var result = Types.InAssembly(s_sharedKernel)
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_code_does_not_depend_on_ef_core_aspnet_or_other_layers(string module)
    {
        var result = Types.InAssembly(Load(module))
            .That().ResideInNamespace($"{module}.Domain")
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql",
                $"{module}.Application",
                $"{module}.Infrastructure",
                $"{module}.Endpoints")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Only_the_entry_point_requests_and_the_enums_they_use_are_public(string module)
    {
        // Request types must be public, or .NET 10's validation generator silently skips them.
        var unexpected = Load(module).GetExportedTypes()
            .Where(type => !(type.Name.EndsWith("Module", StringComparison.Ordinal) && type.Namespace == module))
            .Where(type => !(type.Name.EndsWith("Request", StringComparison.Ordinal) && type.Namespace == $"{module}.Endpoints"))
            .Where(type => !(type.IsEnum && type.Namespace == $"{module}.Domain"))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(unexpected);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Every_request_type_is_public_so_it_is_validated(string module)
    {
        var internalRequests = Load(module).GetTypes()
            .Where(type => type.Namespace == $"{module}.Endpoints" && type.Name.EndsWith("Request", StringComparison.Ordinal))
            .Where(type => !type.IsPublic)
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(internalRequests);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Every_handler_lives_in_the_application_layer(string module)
    {
        var handlers = Load(module).GetTypes()
            .Where(type => type.GetInterfaces().Any(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) || i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))))
            .ToList();

        Assert.NotEmpty(handlers);
        Assert.All(handlers, handler => Assert.StartsWith($"{module}.Application.", handler.Namespace));
    }

    [Fact]
    public void Module_policy_names_match_the_policies_the_host_registers()
    {
        Assert.Equal(PolicyNames.Read, ModulePolicies.Read);
        Assert.Equal(PolicyNames.Write, ModulePolicies.Write);
    }

    private static Assembly Load(string name) => name == s_registration.GetName().Name ? s_registration : s_packaging;

    private static string Failing(NetArchTest.Rules.TestResult result) =>
        "Breaking types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
