using System.Net;
using System.Text;

namespace SeeSharp.App.Tests;

public sealed class UpdateCheckTests
{
    private const string Versions = """{"versions":["0.1.1","0.1.2","0.1.3","0.2.0-beta.1"]}""";

    [Theory]
    [InlineData("0.1.2", "0.1.3")]
    [InlineData("0.1.3", null)]
    [InlineData("0.2.0", null)]
    [InlineData("0.0.0-dev", null)] // dev build: no request, no message
    [InlineData(null, null)]
    public async Task Names_the_newest_stable_version_only_when_it_is_newer(string? current, string? expected)
    {
        var handler = new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Versions, Encoding.UTF8, "application/json") });
        Assert.Equal(expected, await UpdateCheck.NewerAsync(new HttpClient(handler), current));
        Assert.Equal(Version.TryParse(current, out _) ? [UpdateCheck.Index] : [], handler.Uris);
    }

    [Fact]
    public async Task Offline_or_odd_replies_give_no_message()
    {
        Assert.Null(await UpdateCheck.NewerAsync(new HttpClient(new Stub(_ => throw new HttpRequestException())), "0.1.0"));
        Assert.Null(await UpdateCheck.NewerAsync(new HttpClient(new Stub(_ => new HttpResponseMessage(HttpStatusCode.NotFound))), "0.1.0"));
        Assert.Null(await UpdateCheck.NewerAsync(new HttpClient(new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") })), "0.1.0"));
    }

    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Uris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uris.Add(request.RequestUri!.ToString());
            return Task.FromResult(respond(request));
        }
    }
}
