using FluentAssertions;
using Xunit;

namespace Weather.E2ETests;

public sealed class SmokeTests
{
    [Fact]
    public void E2e_test_project_is_wired()
    {
        typeof(Weather.Web.Program).Assembly.GetName().Name.Should().Be("Weather.Web");
    }
}
