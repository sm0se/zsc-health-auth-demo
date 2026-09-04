using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Zsc.CommonRoutes;

// Proxies the current inbound request to the next hop in the chain.
//
// The named HttpClient it resolves has TokenForwardingHandler attached, so the
// caller's context travels with the call. The downstream status code and body
// are returned to the caller untouched - a 401 raised four hops down surfaces at
// the edge as a 401.
public sealed class ZscForwarder(IHttpClientFactory httpClientFactory, ILogger<ZscForwarder> logger)
{
    public async Task<IResult> ForwardAsync(HttpContext context, string clientName, string targetUrl, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(clientName);
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

            return Results.Text(body, contentType, null, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Forwarding {Method} {Path} to {Target} failed", context.Request.Method, context.Request.Path, targetUrl);
            return Results.Problem($"Upstream call to {targetUrl} failed.", statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
