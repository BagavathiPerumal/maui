#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Maui.DeviceTests;

// Regression tests for https://github.com/dotnet/maui/issues/35696
// On Windows, calling EvaluateJavaScriptAsync or InvokeJavaScriptAsync before
// CoreWebView2 completes its async initialization results in an
// InvalidOperationException or a hang. These tests verify that such calls are
// safely deferred until the WebView2 is ready and complete with the correct result.
[Category(TestCategory.HybridWebView)]
#if WINDOWS
[Collection(WebViewsCollection)]
#endif
public class HybridWebViewTests_EarlyJsInvocation : HybridWebViewTestsBase
{
	// Verifies that EvaluateJavaScriptAsync called before CoreWebView2 is fully
	// initialized does not throw or hang, and returns the correct result once ready.
	[Fact]
	public Task EvaluateJavaScriptAsync_BeforeWebViewInitialized_Succeeds() =>
		RunTest(async hybridWebView =>
		{
			// Invoke EvaluateJavaScriptAsync immediately before the WebView2 CoreWebView2
			// has fully initialized. The call should be queued and execute once ready.
			var earlyTask = hybridWebView.EvaluateJavaScriptAsync("1 + 1");

			// Allow the page to fully load so the deferred call can complete.
			await WebViewHelpers.WaitForHybridWebViewLoaded(hybridWebView);

			using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(15));
			var result = await earlyTask.WaitAsync(cts.Token);

			Assert.Equal("2", result);
		});

	// Verifies that multiple EvaluateJavaScriptAsync calls made before CoreWebView2
	// initialization each complete successfully with the correct result.
	[Fact]
	public Task EvaluateJavaScriptAsync_MultipleCallsBeforeWebViewInitialized_AllSucceed() =>
		RunTest(async hybridWebView =>
		{
			// Queue several JS evaluations before the WebView is fully initialized.
			var task1 = hybridWebView.EvaluateJavaScriptAsync("2 + 2");
			var task2 = hybridWebView.EvaluateJavaScriptAsync("3 * 3");
			var task3 = hybridWebView.EvaluateJavaScriptAsync("10 - 4");

			await WebViewHelpers.WaitForHybridWebViewLoaded(hybridWebView);

			using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(15));
			var result1 = await task1.WaitAsync(cts.Token);
			var result2 = await task2.WaitAsync(cts.Token);
			var result3 = await task3.WaitAsync(cts.Token);

			Assert.Equal("4", result1);
			Assert.Equal("9", result2);
			Assert.Equal("6", result3);
		});
}
