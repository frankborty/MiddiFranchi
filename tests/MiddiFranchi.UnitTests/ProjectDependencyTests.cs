using System.Xml.Linq;
using Shouldly;

namespace MiddiFranchi.UnitTests;

public class ProjectDependencyTests
{
    [Fact]
    public void Domain_ShouldNotReferenceOtherSolutionProjects()
    {
        GetProjectReferences("src/MiddiFranchi.Domain/MiddiFranchi.Domain.csproj").ShouldBeEmpty();
    }

    [Fact]
    public void Application_ShouldReferenceDomainOnly()
    {
        GetProjectReferences("src/MiddiFranchi.Application/MiddiFranchi.Application.csproj")
            .ShouldBe(["MiddiFranchi.Domain"], ignoreOrder: true);
    }

    [Fact]
    public void Infrastructure_ShouldReferenceApplicationAndDomain()
    {
        GetProjectReferences("src/MiddiFranchi.Infrastructure/MiddiFranchi.Infrastructure.csproj")
            .ShouldBe(["MiddiFranchi.Application", "MiddiFranchi.Domain"], ignoreOrder: true);
    }

    [Fact]
    public void Web_ShouldReferenceApplicationAndInfrastructure()
    {
        GetProjectReferences("src/MiddiFranchi.Web/MiddiFranchi.Web.csproj")
            .ShouldBe(["MiddiFranchi.Application", "MiddiFranchi.Infrastructure"], ignoreOrder: true);
    }

    [Fact]
    public void UnitTests_ShouldReferenceDomainAndApplication()
    {
        GetProjectReferences("tests/MiddiFranchi.UnitTests/MiddiFranchi.UnitTests.csproj")
            .ShouldBe(["MiddiFranchi.Domain", "MiddiFranchi.Application"], ignoreOrder: true);
    }

    [Fact]
    public void IntegrationTests_ShouldReferenceDomainApplicationInfrastructureAndWeb()
    {
        // Web è incluso per testare i coordinatori della UI (senza framework di rendering).
        GetProjectReferences("tests/MiddiFranchi.IntegrationTests/MiddiFranchi.IntegrationTests.csproj")
            .ShouldBe(
                ["MiddiFranchi.Domain", "MiddiFranchi.Application", "MiddiFranchi.Infrastructure", "MiddiFranchi.Web"],
                ignoreOrder: true);
    }

    private static string[] GetProjectReferences(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindSolutionRoot(), relativeProjectPath);
        var project = XDocument.Load(projectPath);

        return project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")?.Value))
            .OfType<string>()
            .ToArray();
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "MiddiFranchi.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "The solution root could not be located from the test output directory.");
        }

        return directory.FullName;
    }
}
