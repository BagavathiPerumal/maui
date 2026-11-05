using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Maui.Controls.Xaml.UnitTests.SourceGen;
using Xunit;

namespace Microsoft.Maui.Controls.SourceGen.UnitTests;

public class Maui39763Tests : SourceGenXamlInitializeComponentTestBase
{
	[Fact]
	public void StaticMemberFromAnotherGeneratorResolvesInImplicitGlobalNamespace()
	{
		const string globalXmlns = "http://schemas.microsoft.com/dotnet/maui/global";
		var compilation = SourceGeneratorDriver.CreateMauiCompilation()
			.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
				$$"""
				using Microsoft.Maui.Controls;
				[assembly: XmlnsDefinition("{{globalXmlns}}", "Test")]
				[assembly: XmlnsDefinition("{{globalXmlns}}", "Test.Generated")]

				namespace Test;

				public partial class TestPage : ContentPage
				{
					public TestPage() => InitializeComponent();
				}
				"""));

		Assert.Null(compilation.GetTypeByMetadataName("Test.Generated.Routes"));

		var xamlFile = new AdditionalXamlFile(
			"TestPage.xaml",
			"""
			<ContentPage xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
			             x:Class="Test.TestPage"
			             Title="{x:Static Routes.PageTitle}" />
			""");
		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			generators: [new XamlGenerator().AsSourceGenerator(), new RoutesGenerator()],
			additionalTexts: [xamlFile.Text],
			optionsProvider: SourceGeneratorDriver.CreateAnalyzerConfigOptionsProvider(xamlFile));

		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var diagnostics);

		Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "MAUIG1001");
		Assert.Empty(updatedCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
		Assert.NotNull(updatedCompilation.GetTypeByMetadataName("Test.Generated.Routes"));
	}

	sealed class RoutesGenerator : ISourceGenerator
	{
		public void Initialize(GeneratorInitializationContext context)
		{
		}

		public void Execute(GeneratorExecutionContext context)
		{
			context.AddSource("Routes.g.cs", SourceText.From(
				"""
				namespace Test.Generated;

				public static class Routes
				{
					public const string PageTitle = "Generated route";
				}
				""",
				Encoding.UTF8));
		}
	}
}
