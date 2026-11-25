using System.Reflection;
using NetArchTest.Rules;
using WasteSubmitRegPackaging.Packaging;
using WasteSubmitRegPackaging.Fees;
using WasteSubmitRegPackaging.SubmissionPeriods;
using WasteSubmitRegPackaging.Registration;
using WasteSubmitRegPackaging.SharedKernel.Domain;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackagingApi.Authentication;

namespace WasteSubmitRegPackagingApi.Test.Architecture;

/// <summary>
/// The structure rules, as tests, so a pull request that breaks them fails the build instead of relying
/// on review.
/// </summary>
public class ArchitectureTest
{
    private static readonly Assembly s_registration = typeof(RegistrationModule).Assembly;
    private static readonly Assembly s_packaging = typeof(PackagingModule).Assembly;
    private static readonly Assembly s_fees = typeof(FeesModule).Assembly;
    private static readonly Assembly s_submissionPeriods = typeof(SubmissionPeriodsModule).Assembly;
    private static readonly Assembly[] s_sharedDataModules = [s_fees, s_submissionPeriods];
    private static readonly Assembly s_sharedKernel = typeof(AggregateRoot<>).Assembly;

    public static TheoryData<string> Modules =>
        [.. new[] { s_registration, s_packaging, s_fees, s_submissionPeriods }.Select(assembly => assembly.GetName().Name!)];

    [Fact]
    public void Packaging_does_not_reference_the_registration_module()
    {
        // If Packaging ever needs Registration in a command, it uses Registration.Contracts, never the module.
        var references = s_packaging.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain("WasteSubmitRegPackaging.Registration", references);
    }

    [Fact]
    public void Registration_does_not_reference_packaging()
    {
        var references = s_registration.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(references, name => name.StartsWith("WasteSubmitRegPackaging.Packaging", StringComparison.Ordinal));
    }

    [Fact]
    public void The_shared_data_modules_depend_on_no_other_module()
    {
        // Every workflow uses fees and submission periods, so they can't use a workflow: that would be a cycle.
        // They don't need each other either.
        foreach (var assembly in s_sharedDataModules)
        {
            var references = assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
            var others = new[] { "Registration", "Packaging", "Fees", "SubmissionPeriods" }
                .Where(name => assembly.GetName().Name != $"WasteSubmitRegPackaging.{name}");

            foreach (var other in others)
                Assert.DoesNotContain(references, name => name.StartsWith($"WasteSubmitRegPackaging.{other}", StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Modules_use_the_shared_data_modules_only_through_their_contracts(string module)
    {
        var references = Load(module).GetReferencedAssemblies().Select(a => a.Name).ToList();

        foreach (var shared in s_sharedDataModules.Where(assembly => assembly.GetName().Name != module))
            Assert.DoesNotContain(shared.GetName().Name, references);
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

    private static Assembly Load(string name) =>
        new[] { s_registration, s_packaging, s_fees, s_submissionPeriods }.Single(assembly => assembly.GetName().Name == name);

    private static string Failing(NetArchTest.Rules.TestResult result) =>
        "Breaking types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
