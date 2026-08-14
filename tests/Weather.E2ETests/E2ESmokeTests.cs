using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Weather.E2ETests;

public sealed class E2ESmokeTests
{
    [Fact]
    public async Task Home_page_serves_blazor_shell()
    {
        await using WebApplicationFactory<Weather.Web.Program> factory = new WebApplicationFactory<Weather.Web.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["WeatherApi:Credential"] = "test-credential"
                    });
                });
            });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/", Xunit.TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("blazor.web.js");
        html.Should().Contain("app.css");
    }
}
