using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Xunit;

namespace Microsoft.Maui.MauiBlazorWebView.DeviceTests.Elements;

public partial class BlazorWebViewTests
{
	// https://github.com/dotnet/maui/issues/36584
	// ShellItem/ShellSection don't implement IView, so DisconnectHandlers() stopped
	// traversing the visual tree once it hit the Shell and never reached the pages
	// hosted inside it. For a Shell tab hosting a BlazorWebView this meant
	// BlazorWebViewHandler.DisconnectHandler() -- the only place that starts
	// WebViewManager.DisposeAsync() -- was never invoked, leaking the handler, the
	// platform web view, and the whole rendered Blazor component tree indefinitely.
	[Fact]
	public async Task DisconnectHandlersReachesBlazorWebViewInsideShell()
	{
		EnsureHandlerCreated(additionalCreationActions: appBuilder =>
		{
			appBuilder.Services.AddMauiBlazorWebView();
		});

		var bwv = new BlazorWebViewWithCustomFiles
		{
			HostPage = "wwwroot/index.html",
			CustomFiles = new Dictionary<string, string>
			{
				{ "index.html", TestStaticFilesContents.DefaultMauiIndexHtmlContent },
			},
		};
		bwv.RootComponents.Add(new RootComponent { ComponentType = typeof(MauiBlazorWebView.DeviceTests.Components.NoOpComponent), Selector = "#app", });

		// Mirrors the reported Shell -> ShellItem -> ShellSection -> ShellContent -> Page
		// hierarchy that hosted the BlazorWebView on a Shell tab.
		var page = new ContentPage { Content = bwv };
		var shellContent = new ShellContent { Content = page };
		var shellSection = new ShellSection();
		shellSection.Items.Add(shellContent);
		var shellItem = new ShellItem();
		shellItem.Items.Add(shellSection);
		var shell = new Shell();
		shell.Items.Add(shellItem);

		await InvokeOnMainThreadAsync(async () =>
		{
			var bwvHandler = CreateHandler<BlazorWebViewHandler>(bwv);
			var platformWebView = bwvHandler.PlatformView;
			await WebViewHelpers.WaitForWebViewReady(platformWebView);

			Assert.NotNull(bwv.Handler);

			// This is the exact call the issue reports as broken: replacing Window.Page
			// triggers oldShell.DisconnectHandlers() internally, and the traversal must
			// reach all the way down to the BlazorWebView to dispose its WebViewManager.
			shell.DisconnectHandlers();

			Assert.Null(bwv.Handler);
		});
	}
}
