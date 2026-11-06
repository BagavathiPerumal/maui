using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
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

	[Theory]
	[InlineData("", "")]
	[InlineData("xmlns=\"http://schemas.microsoft.com/dotnet/maui/global\"", "")]
	[InlineData("xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"", "generated:")]
	public void GeneratedStaticMembersPreserveConversions(string defaultNamespace, string prefix)
	{
		var compilation = SourceGeneratorDriver.CreateMauiCompilation($"Maui39763_{Guid.NewGuid():N}")
			.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
				"""
				#nullable enable
				using System.Collections.Generic;
				using Microsoft.Maui.Controls;
				[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "Test")]
				[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "Test.Generated")]

				namespace Test;

				public partial class TestPage : ContentPage
				{
					public TestPage() => InitializeComponent();
					public double NumericValue { get; set; }
					public double ConvertedValue { get; set; }
					public double ExplicitlyConvertedValue { get; set; }
					public object? ObjectValue { get; set; }
					public BindingBase? StaticBinding { get; set; }
					public IList<double> NumericValues { get; } = new List<double>();
					public IList<object> ObjectValues { get; } = new List<object>();
				}
				"""));

		Assert.Null(compilation.GetTypeByMetadataName("Test.Generated.Routes"));

		var xamlFile = new AdditionalXamlFile(
			"TestPage.xaml",
			$$"""
			<maui:ContentPage {{defaultNamespace}}
			               xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
			               xmlns:maui="http://schemas.microsoft.com/dotnet/2021/maui"
			               xmlns:local="clr-namespace:Test"
			               xmlns:generated="clr-namespace:Test.Generated"
			               x:Class="Test.TestPage"
			               Title="{x:Static {{prefix}}Routes.PageTitle}"
			               NumericValue="{x:Static {{prefix}}Routes.Amount}"
			               ConvertedValue="{x:Static {{prefix}}Routes.Convertible}"
			               ExplicitlyConvertedValue="{x:Static {{prefix}}Routes.ExplicitlyConvertible}"
			               ObjectValue="{x:Static {{prefix}}Routes.Amount}"
			               StaticBinding="{x:Static {{prefix}}Routes.TitleBinding}"
			               BindingContext="{x:Static {{prefix}}Routes.Amount}">
			    <local:TestPage.NumericValues>
			        <x:Static Member="{{prefix}}Routes.Amount" />
			        <x:Static Member="{{prefix}}Routes.Convertible" />
			    </local:TestPage.NumericValues>
			    <local:TestPage.ObjectValues>
			        <x:Static Member="{{prefix}}Routes.Amount" />
			    </local:TestPage.ObjectValues>
			</maui:ContentPage>
			""");
		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			generators: [new XamlGenerator().AsSourceGenerator(), new RoutesGenerator()],
			additionalTexts: [xamlFile.Text],
			optionsProvider: SourceGeneratorDriver.CreateAnalyzerConfigOptionsProvider(xamlFile));

		driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var diagnostics);

		Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
		Assert.Empty(updatedCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

		using var stream = new MemoryStream();
		var emitResult = updatedCompilation.Emit(stream);
		Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));
		stream.Position = 0;
		var loadContext = new AssemblyLoadContext(compilation.AssemblyName, isCollectible: true);
		try
		{
			var assembly = loadContext.LoadFromStream(stream);
			var pageType = assembly.GetType("Test.TestPage", throwOnError: true)!;
			var page = Assert.IsAssignableFrom<ContentPage>(Activator.CreateInstance(pageType));

			Assert.Equal("Generated route", page.Title);
			Assert.Equal(24d, pageType.GetProperty("NumericValue")!.GetValue(page));
			Assert.Equal(42d, pageType.GetProperty("ConvertedValue")!.GetValue(page));
			Assert.Equal(84d, pageType.GetProperty("ExplicitlyConvertedValue")!.GetValue(page));
			Assert.Equal(24, Assert.IsType<int>(pageType.GetProperty("ObjectValue")!.GetValue(page)));
			Assert.Equal(24, Assert.IsType<int>(page.BindingContext));
			Assert.Equal("Title", Assert.IsType<Binding>(pageType.GetProperty("StaticBinding")!.GetValue(page)).Path);
			Assert.Equal(new[] { 24d, 42d }, Assert.IsAssignableFrom<IList<double>>(pageType.GetProperty("NumericValues")!.GetValue(page)));
			Assert.Equal(24, Assert.IsType<int>(Assert.Single(Assert.IsAssignableFrom<IList<object>>(pageType.GetProperty("ObjectValues")!.GetValue(page)))));
		}
		finally
		{
			loadContext.Unload();
		}
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
					public const int Amount = 24;
					public static ConvertibleAmount Convertible => new();
					public static ExplicitlyConvertibleAmount ExplicitlyConvertible => new();
					public static Microsoft.Maui.Controls.Binding TitleBinding => new("Title");
				}

				public readonly struct ConvertibleAmount
				{
					public static implicit operator double(ConvertibleAmount value) => 42;
				}

				public readonly struct ExplicitlyConvertibleAmount
				{
					public static explicit operator double(ExplicitlyConvertibleAmount value) => 84;
				}
				""",
				Encoding.UTF8));
		}
	}
}
