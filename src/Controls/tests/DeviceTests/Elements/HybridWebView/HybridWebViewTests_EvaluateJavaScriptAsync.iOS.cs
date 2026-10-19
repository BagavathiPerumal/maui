#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using WebKit;
using Xunit;

namespace Microsoft.Maui.DeviceTests;

public partial class HybridWebViewTests_EvaluateJavaScriptAsync
{
	static readonly TimeSpan EvaluationTimeout = TimeSpan.FromSeconds(5);

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task FailedEvaluationCanBeCaughtByTheCaller(bool deferCompletion) =>
		RunFailedEvaluationTest(deferCompletion, async (hybridWebView, platformView) =>
		{
			var evaluation = hybridWebView.EvaluateJavaScriptAsync("1+1");
			if (deferCompletion)
			{
				Assert.False(evaluation.IsCompleted);
				platformView.CompletePendingEvaluation();
			}

			var exception = await Assert.ThrowsAsync<NSErrorException>(
				() => evaluation.WaitAsync(EvaluationTimeout));

			Assert.Equal("WKErrorDomain", exception.Error.Domain);
			Assert.Equal((nint)5, exception.Error.Code);
		});

	[Fact]
	public Task EvaluationWithoutPlatformViewIsCanceled() =>
		InvokeOnMainThreadAsync(async () =>
		{
			var request = new EvaluateJavaScriptAsyncRequest("1+1");
			HybridWebViewHandler.MapEvaluateJavaScriptAsync(
				new HybridWebViewHandler(), new HybridWebView(), request);

			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => request.Task.WaitAsync(EvaluationTimeout));
			Assert.True(request.Task.IsCanceled);
		});

	[Theory]
	[InlineData("null")]
	[InlineData("undefined")]
	[InlineData("throw new Error('test')")]
	public Task EvaluationReturnsNull(string script) =>
		RunTest(async hybridWebView =>
		{
			var result = await hybridWebView.EvaluateJavaScriptAsync(script).WaitAsync(EvaluationTimeout);
			Assert.Null(result);
		});

	[Theory]
	[InlineData("''", "")]
	[InlineData("'null'", "null")]
	[InlineData("1+1", "2")]
	public Task EvaluationPreservesResultValues(string script, string expected) =>
		RunTest(async hybridWebView =>
		{
			var result = await hybridWebView.EvaluateJavaScriptAsync(script).WaitAsync(EvaluationTimeout);
			Assert.Equal(expected, result);
		});

	async Task RunFailedEvaluationTest(
		bool deferCompletion,
		Func<HybridWebView, FailingMauiHybridWebView, Task> test)
	{
		using var error = new NSError(
			new NSString("WKErrorDomain"),
			5,
			new NSDictionary(
				NSError.LocalizedDescriptionKey,
				new NSString("JavaScript execution returned a result of an unsupported type")));

		EnsureHandlerCreated(builder =>
		{
			builder.ConfigureMauiHandlers(handlers =>
			{
				handlers.AddHandler<HybridWebView>(_ => new FailingHybridWebViewHandler(error));
			});

			builder.Services.AddHybridWebViewDeveloperTools();
			builder.Services.AddScoped<IHybridWebViewTaskManager, HybridWebViewTaskManager>();
		});

		var hybridWebView = new HybridWebView
		{
			WidthRequest = 100,
			HeightRequest = 100,
			HybridRoot = "HybridTestRoot",
			DefaultFile = "index.html",
		};

		await AttachAndRun(hybridWebView, async handler =>
		{
			var platformView = Assert.IsType<FailingMauiHybridWebView>(handler.PlatformView);
			platformView.DeferCompletion = deferCompletion;
			await test(hybridWebView, platformView);
		});
	}

	sealed class FailingHybridWebViewHandler : HybridWebViewHandler
	{
		readonly NSError _error;

		public FailingHybridWebViewHandler(NSError error)
		{
			_error = error;
		}

		protected override WKWebView CreatePlatformView() =>
			new FailingMauiHybridWebView(this, CGRect.Empty, new WKWebViewConfiguration(), _error);
	}

	sealed class FailingMauiHybridWebView : MauiHybridWebView
	{
		readonly NSError _error;
		WKJavascriptEvaluationResult? _pendingCompletion;

		public FailingMauiHybridWebView(
			HybridWebViewHandler handler,
			CGRect frame,
			WKWebViewConfiguration configuration,
			NSError error)
			: base(handler, frame, configuration)
		{
			_error = error;
		}

		public bool DeferCompletion { get; set; }

		public override void EvaluateJavaScript(
			NSString javaScriptString,
			[AllowNull] WKJavascriptEvaluationResult completionHandler)
		{
			if (DeferCompletion)
			{
				_pendingCompletion = completionHandler;
				return;
			}

			CompleteEvaluation(completionHandler);
		}

		public void CompletePendingEvaluation()
		{
			var completionHandler = _pendingCompletion;
			_pendingCompletion = null;
			CompleteEvaluation(completionHandler);
		}

		void CompleteEvaluation(WKJavascriptEvaluationResult? completionHandler)
		{
			completionHandler?.Invoke(null, _error);
		}
	}
}
