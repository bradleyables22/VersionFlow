using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using VersionFlow.Extensions.Results;
using VersionFlow.Extensions.Results.AspNetCore;
using Xunit;

namespace VersionFlow.Tests;

public sealed class HostResultTests
{
    [Fact]
    public async Task Mvc_results_work_as_IActionResult_and_typed_ActionResult()
    {
        IActionResult command = TryResult.Succeeded().AsActionResult();
        ActionResult<string> scalar = TryResult<string>.Succeeded("value").AsActionResult();
        ActionResult<CursorTryResult<int>> page = CursorTryResult<int>.Succeeded([], "next").AsActionResult();
        Assert.IsType<NoContentResult>(command);
        Assert.Equal("value", Assert.IsType<OkObjectResult>(scalar.Result).Value);
        var response = await ExecuteMvc(page.Result!);
        Assert.Equal(200, response.Status);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal("next", json.RootElement.GetProperty("nextCursor").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("count").GetInt32());
        Assert.IsType<NoContentResult>(TryResult<string?>.Succeeded(null).AsActionResult());
        Assert.IsType<OkObjectResult>(TryResult<int[]>.Succeeded([]).AsActionResult());
    }

    [Fact]
    public async Task Mvc_failure_is_safe_problem_details_and_honors_customization()
    {
        var failure = TryResult<int>.Failed("internal", new Exception("secret"));
        var response = await ExecuteMvc(failure.AsActionResult(422, "Validation", "Safe detail"));
        Assert.Equal(422, response.Status);
        Assert.StartsWith("application/problem+json", response.ContentType);
        Assert.Contains("Safe detail", response.Body);
        Assert.DoesNotContain("secret", response.Body);
        Assert.DoesNotContain("internal", response.Body);
        var defaults = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(failure.AsActionResult()).Value);
        Assert.Equal(500, defaults.Status);
        Assert.Equal(VersionFlow.Extensions.Results.AspNetCore.ResultExtensions.DefaultFailureMessage, defaults.Detail);
        Assert.IsType<ObjectResult>(CursorTryResult<int>.Failed("failed").AsActionResult());
        Assert.IsType<ObjectResult>(TryResult.Failed("failed").AsActionResult());
        Assert.Throws<ArgumentOutOfRangeException>(() => failure.AsActionResult(200));
        Assert.Throws<ArgumentNullException>(() => ((TryResult)null!).AsActionResult());
    }

    private static async Task<(int Status, string Body, string? ContentType)> ExecuteMvc(IActionResult result)
    {
        var registrations = new ServiceCollection().AddLogging();
        registrations.AddMvcCore().AddJsonOptions(_ => { });
        using var services = registrations.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        using var stream = new MemoryStream();
        context.Response.Body = stream;
        await result.ExecuteResultAsync(new ActionContext(context, new RouteData(), new ActionDescriptor()));
        stream.Position = 0;
        return (context.Response.StatusCode, await new StreamReader(stream).ReadToEndAsync(), context.Response.ContentType);
    }

}

