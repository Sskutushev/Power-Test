using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Weather.Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    [Fact]
    public void Domain_does_not_depend_on_application_infrastructure_or_web()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(typeof(Weather.Domain.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Weather.Application", "Weather.Infrastructure", "Weather.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Domain must stay independent from outer layers.");
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_web()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(typeof(Weather.Application.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Weather.Infrastructure", "Weather.Web", "System.Net.Http")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Application owns use cases and must not know infrastructure details.");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_web()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(typeof(Weather.Infrastructure.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Weather.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Infrastructure must be reusable outside the Blazor host.");
    }

    [Fact]
    public void Weatherapi_contracts_are_not_public()
    {
        bool allContractsAreInternal = typeof(Weather.Infrastructure.AssemblyMarker).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.Contains("WeatherApi.Contracts", StringComparison.Ordinal) == true)
            .All(type => !type.IsPublic);

        allContractsAreInternal.Should().BeTrue("Provider DTOs must not cross the Infrastructure boundary.");
    }

    [Fact]
    public void Domain_and_application_source_do_not_use_system_clock()
    {
        string root = FindRepositoryRoot();
        string[] files = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains("Weather.Domain", StringComparison.Ordinal) || path.Contains("Weather.Application", StringComparison.Ordinal))
            .ToArray();

        string[] forbidden =
        [
            "DateTime.Now",
            "DateTime.UtcNow",
            "DateTimeOffset.Now"
        ];

        List<string> offenders = [];
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            if (forbidden.Any(text.Contains))
            {
                offenders.Add(file);
            }
        }

        offenders.Should().BeEmpty("Domain/Application time must be deterministic and injected through TimeProvider/provider local time.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WeatherApp.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
