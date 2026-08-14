using FluentAssertions;
using Xunit;

namespace Weather.ComponentTests;

public sealed class SmokeTests
{
    [Fact]
    public void Component_test_project_is_wired()
    {
        typeof(Weather.Web.Program).Assembly.GetName().Name.Should().Be("Weather.Web");
    }
}
