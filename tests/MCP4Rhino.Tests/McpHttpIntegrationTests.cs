using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Protocol;
using Xunit;

namespace MCP4Rhino.Tests;

public class McpHttpIntegrationTests
{
    [Fact]
    public async Task ConcurrentProfilesUseIndependentMetadataOverRealHttp()
    {
        var port = AvailablePort();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var services = new McpServerServices(new RecordingTools());
        var server = Serve(listener, services, port, 3);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var requests = McpRequestContext.SupportedVersions.Select(async version =>
        {
            var source = McpTestRequests.Message(version == McpRequestContext.Modern ? "server/discover" : "initialize", version,
                new JsonObject { ["protocolVersion"] = version });
            var request = McpTestRequests.Http(source, version, port);
            using var http = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/mcp")
                { Content = new StringContent(request.Body, Encoding.UTF8, "application/json") };
            foreach (var header in request.Headers.Where(p => p.Key is not "Host" and not "Content-Type")) http.Headers.TryAddWithoutValidation(header.Key, header.Value);
            using var response = await client.SendAsync(http);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["result"]!;
            if (version == McpRequestContext.Modern) Assert.Equal("complete", result["resultType"]!.GetValue<string>());
            else { Assert.Equal(version, result["protocolVersion"]!.GetValue<string>()); Assert.Null(result["resultType"]); }
        });
        try { await Task.WhenAll(requests); await server.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { listener.Stop(); }
    }

    private static async Task Serve(HttpListener listener, McpServerServices services, int port, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var context = await listener.GetContextAsync();
            using var reader = new StreamReader(context.Request.InputStream);
            var headers = context.Request.Headers.AllKeys.ToDictionary(key => key!, key => context.Request.Headers[key!] ?? "");
            var request = new McpHttpRequest(context.Request.HttpMethod, context.Request.Url!.AbsolutePath, headers, await reader.ReadToEndAsync(), port);
            var response = McpHttpTransport.Process(request, services);
            var bytes = Encoding.UTF8.GetBytes(response.Body);
            context.Response.StatusCode = response.Status; context.Response.ContentType = response.ContentType;
            await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
        }
    }

    private static int AvailablePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
