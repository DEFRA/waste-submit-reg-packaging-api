using Microsoft.AspNetCore.Http;
using Npgsql;
using Polly.Timeout;
using WasteSubmitRegPackagingApi.Utils.ErrorHandling;

namespace WasteSubmitRegPackagingApi.Test.Utils.ErrorHandling;

public class ApplicationBuilderExtensionsTest
{
    [Fact]
    public void An_http_call_that_times_out_answers_504() =>
        Assert.Equal(StatusCodes.Status504GatewayTimeout, ApplicationBuilderExtensions.Describe(new TimeoutRejectedException()).StatusCode);

    [Fact]
    public void A_postgres_call_that_times_out_answers_504() =>
        // Npgsql reports both connect and command timeouts as an NpgsqlException wrapping a TimeoutException.
        Assert.Equal(
            StatusCodes.Status504GatewayTimeout,
            ApplicationBuilderExtensions.Describe(new NpgsqlException("Exception while reading from stream", new TimeoutException())).StatusCode);

    [Fact]
    public void Other_postgres_errors_answer_500() =>
        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            ApplicationBuilderExtensions.Describe(new NpgsqlException("password authentication failed")).StatusCode);

    [Fact]
    public void A_request_the_caller_abandoned_is_not_a_timeout() =>
        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            ApplicationBuilderExtensions.Describe(new TaskCanceledException("The operation was canceled.")).StatusCode);
}
