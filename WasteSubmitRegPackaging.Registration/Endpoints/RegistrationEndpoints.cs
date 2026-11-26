using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WasteSubmitRegPackaging.Registration.Application.Commands.RecordRegulatorDecision;
using WasteSubmitRegPackaging.Registration.Application.Commands.StartRegistration;
using WasteSubmitRegPackaging.Registration.Application.Commands.SubmitRegistrationApplication;
using WasteSubmitRegPackaging.Registration.Application.Queries.GetRegistrationSubmission;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Users;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Messaging;

namespace WasteSubmitRegPackaging.Registration.Endpoints;

/// <summary>
/// <b>SAMPLE endpoints.</b> They show the structure (producer and regulator routes, commands, queries, the user
/// headers and the Result mapping) and will be replaced as the registration work is developed. They are marked
/// "(sample)" and left out of the v1 OpenAPI document that frontends generate clients from.
///
/// Thin endpoints: bind the request, turn it into a command or query, call the handler, and map the
/// <c>Result</c>. No business logic here.
///
/// Producer routes are under <c>/registrations</c> and regulator routes under <c>/regulator/registrations</c>,
/// so each frontend's surface is easy to see and secure separately.
/// </summary>
internal static class RegistrationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var producer = app.MapGroup("/registrations").AsSample("Registration", "registration");

        producer.MapPost("/", StartRegistration)
            .WithName("StartRegistration")
            .WithSummary("Start a registration for an organisation and year (sample)")
            .RequireAuthorization(ModulePolicies.Write)
            .RequireProducer();

        producer.MapGet("/{registrationSubmissionId:guid}", GetRegistrationSubmission)
            .WithName("GetRegistrationSubmission")
            .WithSummary("A registration, with its status, history and the actions available (sample)")
            .RequireAuthorization(ModulePolicies.Read)
            .RequireProducer();

        producer.MapPost("/{registrationSubmissionId:guid}/application-submission", SubmitRegistrationApplication)
            .WithName("SubmitRegistrationApplication")
            .WithSummary("Submit the application to the regulator (sample)")
            .RequireAuthorization(ModulePolicies.Write)
            .RequireProducer();

        var regulator = app.MapGroup("/regulator/registrations").AsSample("Regulator", "registration");

        regulator.MapGet("/{registrationSubmissionId:guid}", GetRegulatorRegistrationSubmission)
            .WithName("GetRegulatorRegistrationSubmission")
            .WithSummary("A registration, as the regulator sees it (sample)")
            .RequireAuthorization(ModulePolicies.Read)
            .RequireRegulator();

        regulator.MapPost("/{registrationSubmissionId:guid}/decisions", RecordRegulatorDecision)
            .WithName("RecordRegulatorDecision")
            .WithSummary("Grant, refuse, query or cancel a registration (sample)")
            .RequireAuthorization(ModulePolicies.Write)
            .RequireRegulator();
    }

    private static async Task<Results<Created<StartRegistrationResponse>, ProblemHttpResult>> StartRegistration(
        StartRegistrationRequest request,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICommandHandler<StartRegistrationCommand, RegistrationSubmissionId> handler,
        CancellationToken cancellationToken)
    {
        // The Required checks have already run, so the ! operators are safe.
        var command = new StartRegistrationCommand(
            currentUser.Producer!,
            request.OrganisationId!.Value,
            request.ComplianceSchemeId,
            request.RegistrationYear!.Value,
            request.Journey!.Value,
            request.Nation!.Value,
            request.ApplicationReferenceNumber!);

        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? TypedResults.Created($"/registrations/{result.Value}", new StartRegistrationResponse(result.Value.Value))
            : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<RegistrationSubmissionView>, ProblemHttpResult>> GetRegistrationSubmission(
        Guid registrationSubmissionId,
        [FromServices] ICurrentUser currentUser,
        [FromServices] IQueryHandler<GetRegistrationSubmissionQuery, RegistrationSubmissionView> handler,
        CancellationToken cancellationToken)
    {
        // The producer is passed in, so the handler checks the registration is their organisation's.
        var result = await handler.HandleAsync(
            new GetRegistrationSubmissionQuery(new RegistrationSubmissionId(registrationSubmissionId), currentUser.Producer!),
            cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<RegistrationSubmissionView>, ProblemHttpResult>> GetRegulatorRegistrationSubmission(
        Guid registrationSubmissionId,
        [FromServices] IQueryHandler<GetRegistrationSubmissionQuery, RegistrationSubmissionView> handler,
        CancellationToken cancellationToken)
    {
        // A regulator can see every organisation's registration.
        var result = await handler.HandleAsync(
            new GetRegistrationSubmissionQuery(new RegistrationSubmissionId(registrationSubmissionId)), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> SubmitRegistrationApplication(
        Guid registrationSubmissionId,
        SubmitRegistrationApplicationRequest request,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICommandHandler<SubmitRegistrationApplicationCommand, Unit> handler,
        CancellationToken cancellationToken)
    {
        var command = new SubmitRegistrationApplicationCommand(
            currentUser.Producer!, new RegistrationSubmissionId(registrationSubmissionId), request.Comments);

        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<RecordRegulatorDecisionResult>, ProblemHttpResult>> RecordRegulatorDecision(
        Guid registrationSubmissionId,
        RecordRegulatorDecisionRequest request,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICommandHandler<RecordRegulatorDecisionCommand, RecordRegulatorDecisionResult> handler,
        CancellationToken cancellationToken)
    {
        var command = new RecordRegulatorDecisionCommand(
            currentUser.Regulator!,
            new RegistrationSubmissionId(registrationSubmissionId),
            request.Decision!.Value,
            request.Comments,
            request.CancellationEffectiveDate);

        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
