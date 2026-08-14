using System.Reflection;
using FluentAssertions;
using MediatR;
using NetArchTest.Rules;
using Xunit;

namespace Weather.Architecture.Tests;

/// <summary>
/// Executable architecture. Each rule states an invariant the layering depends on, so a violation fails
/// the build instead of surviving until someone notices it in review.
/// </summary>
public sealed class ArchitectureRulesTests
{
    private static readonly Assembly Domain = typeof(Weather.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly Application = typeof(Weather.Application.AssemblyMarker).Assembly;
    private static readonly Assembly Infrastructure = typeof(Weather.Infrastructure.AssemblyMarker).Assembly;
    private static readonly Assembly Web = typeof(Weather.Web.Program).Assembly;

    [Fact]
    public void Domain_depends_on_nothing_but_the_base_library()
    {
        Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Weather.Application", "Weather.Infrastructure", "Weather.Web", "MediatR", "Microsoft.Extensions")
            .GetResult()
            .ShouldSucceed("the Domain is the innermost layer and must stay framework free");
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_the_host()
    {
        Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("Weather.Infrastructure", "Weather.Web")
            .GetResult()
            .ShouldSucceed("use cases must not know how data is fetched or rendered");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_host()
    {
        Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn("Weather.Web")
            .GetResult()
            .ShouldSucceed("the adapter must be reusable outside the Blazor host");
    }

    [Fact]
    public void Domain_and_application_never_touch_http()
    {
        foreach (Assembly assembly in new[] { Domain, Application })
        {
            Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny("System.Net.Http", "System.Net.Sockets")
                .GetResult()
                .ShouldSucceed($"{assembly.GetName().Name} must not perform transport work");
        }
    }

    [Fact]
    public void Http_client_usage_stays_inside_infrastructure()
    {
        foreach (Assembly assembly in new[] { Domain, Application, Web })
        {
            Types.InAssembly(assembly)
                .That()
                .HaveDependencyOn("System.Net.Http.HttpClient")
                .GetTypes()
                .Should()
                .BeEmpty($"{assembly.GetName().Name} must reach the provider through IWeatherProvider");
        }
    }

    [Fact]
    public void Provider_dtos_never_leave_infrastructure()
    {
        IEnumerable<Type> leaked = Infrastructure
            .GetTypes()
            .Where(type => type.Namespace?.Contains("WeatherApi.Contracts", StringComparison.Ordinal) == true)
            .Where(type => type.IsPublic);

        leaked.Should().BeEmpty("WeatherAPI payload shapes are an implementation detail");
    }

    [Fact]
    public void Request_handlers_live_in_the_application_layer()
    {
        foreach (Assembly assembly in new[] { Infrastructure, Web })
        {
            IEnumerable<Type> handlers = assembly
                .GetTypes()
                .Where(type => type.GetInterfaces().Any(contract =>
                    contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));

            handlers.Should().BeEmpty($"business orchestration belongs to Weather.Application, not {assembly.GetName().Name}");
        }
    }

    [Fact]
    public void Domain_types_are_sealed()
    {
        Types.InAssembly(Domain)
            .That()
            .ArePublic()
            .And()
            .AreClasses()
            .Should()
            .BeSealed()
            .GetResult()
            .ShouldSucceed("Domain records model values, not extension points");
    }

    [Fact]
    public void Application_exceptions_carry_a_failure_kind()
    {
        IEnumerable<Type> exceptions = Application
            .GetTypes()
            .Where(type => typeof(Exception).IsAssignableFrom(type) && type.IsPublic);

        exceptions.Should().OnlyContain(
            type => typeof(Weather.Application.Common.WeatherProviderException).IsAssignableFrom(type),
            "every public failure must be classifiable by the UI and the HTTP API");
    }

    /// <summary>
    /// The strongest determinism guard in the suite: the moment someone reaches for the system clock in
    /// business logic, "remaining hours today" starts depending on the server's time zone.
    /// </summary>
    [Fact]
    public void Domain_and_application_sources_never_read_the_system_clock()
    {
        string[] forbidden = ["DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now", "DateTimeOffset.UtcNow"];

        var offenders = SourceFilesOf("Weather.Domain", "Weather.Application")
            .Where(file => forbidden.Any(File.ReadAllText(file).Contains))
            .ToList();

        offenders.Should().BeEmpty("time must arrive through TimeProvider or the provider's own local time");
    }

    [Fact]
    public void No_source_file_contains_unfinished_work_markers()
    {
        string[] markers = ["TODO", "FIXME", "HACK:", "NotImplementedException"];

        var offenders = SourceFilesOf("Weather.Domain", "Weather.Application", "Weather.Infrastructure", "Weather.Web")
            .Where(file => markers.Any(File.ReadAllText(file).Contains))
            .ToList();

        offenders.Should().BeEmpty("the shipped branch must not carry unfinished work");
    }

    /// <summary>Belt-and-braces guard against a credential ever being committed.</summary>
    [Fact]
    public void No_configuration_file_carries_a_provider_credential()
    {
        string root = RepositoryRoot();
        string[] files =
        [
            .. Directory.EnumerateFiles(Path.Combine(root, "src"), "appsettings*.json", SearchOption.AllDirectories),
            Path.Combine(root, "docker-compose.yml"),
            Path.Combine(root, ".env.example")
        ];

        foreach (string file in files.Where(File.Exists))
        {
            string content = File.ReadAllText(file);

            content.Should().NotContain(
                "Credential\": \"",
                $"{Path.GetFileName(file)} must not carry a credential value");
        }
    }

    private static IEnumerable<string> SourceFilesOf(params string[] projects)
    {
        string src = Path.Combine(RepositoryRoot(), "src");

        return Directory
            .EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => projects.Any(project => path.Contains(project, StringComparison.Ordinal)));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WeatherApp.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}

/// <summary>Turns a NetArchTest result into a failure that names the offending types.</summary>
internal static class ArchTestResultExtensions
{
    public static void ShouldSucceed(this NetArchTest.Rules.TestResult result, string because)
    {
        string offenders = result.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", result.FailingTypeNames);

        result.IsSuccessful.Should().BeTrue($"{because}. Offending types: {offenders}");
    }
}
