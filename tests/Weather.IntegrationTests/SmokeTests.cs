using FluentAssertions;
using Xunit;

namespace Weather.IntegrationTests;

public sealed class SmokeTests
{
    [Fact]
    public void Integration_test_project_is_wired()
    {
        typeof(Weather.Web.Program).Assembly.GetName().Name.Should().Be("Weather.Web");
    }
}
