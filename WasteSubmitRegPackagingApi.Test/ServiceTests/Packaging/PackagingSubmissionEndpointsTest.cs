using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackagingApi.Test.ServiceTests.Packaging;

/// <summary>
/// The packaging workflow called over HTTP, as the frontend, the CDP uploader and the regulator frontend would, through
/// the real host, handlers and Postgres built from the changelog, on the periods it seeds. Nothing is substituted.
/// Each test uses its own organisation, so they don't share a submission. Needs Docker; skips without it.
/// </summary>
public sealed class PackagingSubmissionEndpointsTest(ChangelogDatabase database) : IClassFixture<ChangelogDatabase>
{
    private const string Period = "July to December 2025";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_period_with_no_upload_yet_has_no_submission()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var app = App();
        var producer = NewProducer();
        using var client = Client(app, producer);

        var response = await client.GetAsync(Tile(producer), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("packaging.not_started", await Code(response));
    }

    [Fact]
    public async Task A_first_submission_goes_from_upload_to_accepted()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var app = App();
        var producer = NewProducer();
        using var client = Client(app, producer);

        // The first upload creates the submission.
        var upload = await StartUploadAsync(client, producer);
        using (var waiting = await Json(await client.GetAsync($"/packaging/submissions/{upload.SubmissionId}", Ct)))
        {
            Assert.Equal("InProgress", waiting.RootElement.GetProperty("status").GetString());
            Assert.Equal("AwaitFileCheck", waiting.RootElement.GetProperty("nextStep").GetString());
        }

        // The uploader reports the file stored, which starts validation. The placeholder validator passes it at once.
        Assert.Equal(HttpStatusCode.NoContent, (await CallbackAsync(app, upload.CallbackPath, Stored())).StatusCode);

        using (var checkedFile = await Json(await client.GetAsync(Tile(producer), Ct)))
        {
            var root = checkedFile.RootElement;
            Assert.Equal(upload.SubmissionId, root.GetProperty("id").GetGuid());
            Assert.Equal(2025, root.GetProperty("packagingYear").GetInt32());
            Assert.Equal(2026, root.GetProperty("registrationYear").GetInt32());
            Assert.Equal("SubmitToRegulator", root.GetProperty("nextStep").GetString());
            var cycle = root.GetProperty("cycle");
            Assert.Equal(1, cycle.GetProperty("cycleNumber").GetInt32());
            Assert.False(cycle.GetProperty("isResubmission").GetBoolean());
            var file = cycle.GetProperty("latestFile");
            Assert.Equal("Valid", file.GetProperty("status").GetString());
            Assert.Equal("packaging.csv", file.GetProperty("fileName").GetString());
            Assert.Equal(0, file.GetProperty("errorCount").GetInt32());
            // Where the file is stored, and the callback secret, are not for the frontend.
            Assert.False(file.TryGetProperty("s3Key", out _));
            Assert.False(file.TryGetProperty("callbackToken", out _));
        }

        using (var submitted = await Json(await SubmitAsync(client, upload)))
        {
            Assert.Equal("SubmittedToRegulator", submitted.RootElement.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, submitted.RootElement.GetProperty("nextStep").ValueKind);
            Assert.Equal(upload.FileId, submitted.RootElement.GetProperty("cycle").GetProperty("submittedFileId").GetGuid());
        }

        using var decided = await Json(await DecideAsync(app, upload.SubmissionId, "Accepted"));
        Assert.Equal("AcceptedByRegulator", decided.RootElement.GetProperty("status").GetString());
        Assert.Equal("Accepted", decided.RootElement.GetProperty("cycle").GetProperty("decision").GetString());
    }

    [Fact]
    public async Task A_second_upload_for_the_same_period_uses_the_same_submission()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var app = App();
        var producer = NewProducer();
        using var client = Client(app, producer);

        var first = await StartUploadAsync(client, producer);
        var second = await StartUploadAsync(client, producer);

        Assert.Equal(first.SubmissionId, second.SubmissionId);
        Assert.NotEqual(first.FileId, second.FileId);
    }

    [Fact]
    public async Task A_resubmission_after_acceptance_needs_a_payment_reference_before_it_can_be_submitted()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var app = App();
        var producer = NewProducer();
        var complianceSchemeId = Guid.NewGuid();
        using var client = Client(app, producer);

        var first = await StartUploadAsync(client, producer, complianceSchemeId);
        await CallbackAsync(app, first.CallbackPath, Stored());
        await SubmitAsync(client, first);
        await DecideAsync(app, first.SubmissionId, "Accepted");

        // The next upload opens cycle 2, a resubmission.
        var second = await StartUploadAsync(client, producer, complianceSchemeId);
        await CallbackAsync(app, second.CallbackPath, Stored());

        var early = await SubmitAsync(client, second);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("packaging.step_not_available", await Code(early));

        var paid = await client.PostAsJsonAsync(
            $"/packaging/submissions/{second.SubmissionId}/resubmission-fee-payment", new { paymentReference = "PAY-123" }, Ct);

        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        using (var body = await Json(paid))
        {
            var cycle = body.RootElement.GetProperty("cycle");
            Assert.Equal(2, cycle.GetProperty("cycleNumber").GetInt32());
            Assert.True(cycle.GetProperty("isResubmission").GetBoolean());
            Assert.Equal("PAY-123", cycle.GetProperty("paymentReference").GetString());
            Assert.Equal("SubmitToRegulator", body.RootElement.GetProperty("nextStep").GetString());
        }

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, second)).StatusCode);
    }

    [Fact]
    public async Task A_first_submission_has_no_fee_to_pay()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var app = App();
        var producer = NewProducer();
        using var client = Client(app, producer);
        var upload = await StartUploadAsync(client, producer);
        await CallbackAsync(app, upload.CallbackPath, Stored());

        var response = await client.PostAsJsonAsync(
            $"/packaging/submissions/{upload.SubmissionId}/resubmission-fee-payment",
            new { paymentReference = "PAY-123" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("packaging.step_not_available", await Code(response));
    }

    [Fact]
    public async Task A_file_the_uploader_rejects_is_recorded_and_the_producer_uploads_again()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var app = App();
        var producer = NewProducer();
        using var client = Client(app, producer);
        var upload = await StartUploadAsync(client, producer);

        var callback = await CallbackAsync(app, upload.CallbackPath, new
        {
            uploadStatus = "ready",
            form = new { file = new { fileStatus = "rejected", filename = "packaging.csv", hasError = true, errorMessage = "The selected file contains a virus" } },
            numberOfRejectedFiles = 1
        });

        Assert.Equal(HttpStatusCode.NoContent, callback.StatusCode);
        using var body = await Json(await client.GetAsync($"/packaging/submissions/{upload.SubmissionId}", Ct));
        Assert.Equal("UploadFile", body.RootElement.GetProperty("nextStep").GetString());
        Assert.Equal("Rejected", body.RootElement.GetProperty("cycle").GetProperty("latestFile").GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_callback_with_the_wrong_token_is_refused_and_changes_nothing()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var app = App();
        var producer = NewProducer();
        using var client = Client(app, producer);
        var upload = await StartUploadAsync(client, producer);

        var callback = await CallbackAsync(app, $"/packaging/file-uploads/{upload.FileId}/scan-result/not-the-token", Stored());

        Assert.Equal(HttpStatusCode.NotFound, callback.StatusCode);
        Assert.Equal("packaging.file_not_found", await Code(callback));
        using var body = await Json(await client.GetAsync($"/packaging/submissions/{upload.SubmissionId}", Ct));
        Assert.Equal("AwaitFileCheck", body.RootElement.GetProperty("nextStep").GetString());
    }

    [Fact]
    public async Task A_repeated_callback_changes_nothing()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var app = App();
        var producer = NewProducer();
        using var client = Client(app, producer);
        var upload = await StartUploadAsync(client, producer);
        await CallbackAsync(app, upload.CallbackPath, Stored());

        var repeat = await CallbackAsync(app, upload.CallbackPath, Stored("another-name.csv"));

        Assert.Equal(HttpStatusCode.NoContent, repeat.StatusCode);
        using var body = await Json(await client.GetAsync($"/packaging/submissions/{upload.SubmissionId}", Ct));
        Assert.Equal("packaging.csv", body.RootElement.GetProperty("cycle").GetProperty("latestFile").GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task A_producer_cannot_start_or_read_a_submission_for_another_organisation()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var app = App();
        var owner = NewProducer();
        using var ownerClient = Client(app, owner);
        var upload = await StartUploadAsync(ownerClient, owner);
        using var other = Client(app, NewProducer());

        var start = await other.PostAsJsonAsync("/packaging/submissions/file-uploads",
            new { organisationId = owner.OrganisationId, submissionPeriod = Period }, Ct);
        var read = await other.GetAsync($"/packaging/submissions/{upload.SubmissionId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal("packaging.not_your_organisation", await Code(read));
    }

    [Fact]
    public async Task An_upload_for_a_period_that_does_not_exist_is_a_400()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var app = App();
        var producer = NewProducer();
        using var client = Client(app, producer);

        var response = await client.PostAsJsonAsync("/packaging/submissions/file-uploads",
            new { organisationId = producer.OrganisationId, submissionPeriod = "January to June 1999" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("packaging.unknown_submission_period", await Code(response));
    }

    private sealed record Upload(Guid SubmissionId, Guid FileId, string CallbackPath);

    private static async Task<Upload> StartUploadAsync(HttpClient client, ProducerUser producer, Guid? complianceSchemeId = null)
    {
        var response = await client.PostAsJsonAsync("/packaging/submissions/file-uploads",
            new { organisationId = producer.OrganisationId, complianceSchemeId, submissionPeriod = Period }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await Json(response);
        return new Upload(
            body.RootElement.GetProperty("packagingSubmissionId").GetGuid(),
            body.RootElement.GetProperty("fileId").GetGuid(),
            body.RootElement.GetProperty("callbackPath").GetString()!);
    }

    /// <summary>As the CDP uploader calls: no API key and no user headers.</summary>
    private static async Task<HttpResponseMessage> CallbackAsync(WebApplicationFactory<Program> app, string callbackPath, object payload)
    {
        using var uploader = app.CreateClient();
        return await uploader.PostAsJsonAsync(callbackPath, payload, Ct);
    }

    /// <summary>The uploader's payload for a clean file (cdp-documentation, how-to/file-upload.md).</summary>
    private static object Stored(string fileName = "packaging.csv") => new
    {
        uploadStatus = "ready",
        metadata = new { reference = "anything" },
        form = new
        {
            file = new
            {
                fileId = Guid.NewGuid(),
                filename = fileName,
                contentType = "text/csv",
                fileStatus = "complete",
                contentLength = 5489,
                s3Key = $"{Guid.NewGuid()}/{Guid.NewGuid()}",
                s3Bucket = "waste-submit-packaging-files"
            }
        },
        numberOfRejectedFiles = 0
    };

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Upload upload) =>
        client.PostAsJsonAsync($"/packaging/submissions/{upload.SubmissionId}/submit",
            new { fileId = upload.FileId, submittedBy = "A Producer" }, Ct);

    private static async Task<HttpResponseMessage> DecideAsync(WebApplicationFactory<Program> app, Guid submissionId, string decision)
    {
        using var regulator = Client(app, new Dictionary<string, string>
        {
            [UserHeaders.UserId] = TestUsers.Regulator.UserId.ToString(),
            [UserHeaders.Type] = "Regulator"
        });

        var response = await regulator.PostAsJsonAsync($"/regulator/packaging/submissions/{submissionId}/decision", new { decision }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }

    private static string Tile(ProducerUser producer) =>
        $"/packaging/submissions?organisationId={producer.OrganisationId}&submissionPeriod={Uri.EscapeDataString(Period)}";

    private static ProducerUser NewProducer() => new(Guid.NewGuid(), Guid.NewGuid());

    private WebApplicationFactory<Program> App()
    {
        var settings = new Dictionary<string, string?>(PostgresSettings.For(database.ConnectionDetails))
        {
            [$"Acl:Clients:{AclTestFactory.ClientId}:Scopes:1"] = "write"
        };

        return new AclTestFactory(settings);
    }

    private static HttpClient Client(WebApplicationFactory<Program> app, ProducerUser producer) => Client(app, new Dictionary<string, string>
    {
        [UserHeaders.UserId] = producer.UserId.ToString(),
        [UserHeaders.Type] = "Producer",
        [UserHeaders.OrganisationId] = producer.OrganisationId.ToString()
    });

    private static HttpClient Client(WebApplicationFactory<Program> app, Dictionary<string, string> userHeaders)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret);

        foreach (var (name, value) in userHeaders)
            client.DefaultRequestHeaders.Add(name, value);

        return client;
    }

    private static async Task<JsonDocument> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var problem = await Json(response);
        return problem.RootElement.GetProperty("code").GetString();
    }
}
