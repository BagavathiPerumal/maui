using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebView.WebView2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Handlers;
using WebView2Control = Microsoft.UI.Xaml.Controls.WebView2;

namespace Microsoft.AspNetCore.Components.WebView.Maui
{
	/// <summary>
	/// A <see cref="ViewHandler"/> for <see cref="BlazorWebView"/>.
	/// </summary>
	public partial class BlazorWebViewHandler : ViewHandler<IBlazorWebView, WebView2Control>
	{
		private WebView2WebViewManager? _webviewManager;

		/// <inheritdoc />
		protected override WebView2Control CreatePlatformView()
		{
			return new WebView2Control();
		}

		/// <inheritdoc />
		protected override void DisconnectHandler(WebView2Control platformView)
		{
			if (_webviewManager != null)
			{
				// Start the disposal...
				var disposalTask = _webviewManager?
					.DisposeAsync()
					.AsTask()!;

				if (IsBlockingDisposalEnabled)
				{
					// If the app is configured to block on dispose via an AppContext switch,
					// we'll synchronously wait for the disposal to complete. This can cause a deadlock.
					disposalTask
						.GetAwaiter()
						.GetResult();
				}
				else
				{
					// Otherwise, by default, we'll fire-and-forget the disposal task.
					disposalTask.FireAndForget();
				}

				_webviewManager = null;
			}

			// WebView2 wraps a native CoreWebView2 (and its own browser process); disposing
			// WebView2WebViewManager above does not release those native resources on its own.
			// Previously PlatformView.Close() was only invoked from Window_Destroying (see
			// BlazorWebView.Windows.cs), i.e. only when the whole app Window shuts down. Any
			// other teardown path that calls DisconnectHandler mid-app-lifetime (e.g. replacing
			// Window.Page, or discarding a Shell tab hosting a BlazorWebView) never closed the
			// native control, leaking it -- and everything it transitively references -- even
			// after the managed WebViewManager was disposed.
			// See: https://github.com/microsoft/microsoft-ui-xaml/issues/6872
			CloseWebView2(platformView);
		}

		// Closes the native CoreWebView2 safely. This can be reached from two independent paths
		// that are not mutually exclusive -- DisconnectHandler (mid-app-lifetime teardown, e.g. a
		// Shell tab being discarded) and Window_Destroying (see BlazorWebView.Windows.cs, invoked
		// when the whole app Window shuts down). If the Window is destroyed shortly after a
		// mid-lifetime disconnect, both paths can call Close() on the same platform view, so this
		// must tolerate being called more than once and must tolerate CoreWebView2 never having
		// been initialized (e.g. handler connected then immediately disconnected before
		// StartWebViewCoreIfPossible ran).
		internal static void CloseWebView2(WebView2Control platformView)
		{
			try
			{
				// CoreWebView2 is null until initialization completes; calling Close() without it
				// having ever been created is unnecessary and, per the WebView2 API, can throw.
				if (platformView.CoreWebView2 is not null)
				{
					platformView.Close();
				}
			}
			catch (ObjectDisposedException)
			{
				// Already closed by the other teardown path (Window_Destroying vs
				// DisconnectHandler racing each other) -- nothing further to do.
			}
		}

		private bool RequiredStartupPropertiesSet =>
			//_webview != null &&
			HostPage != null &&
			Services != null;

		private void StartWebViewCoreIfPossible()
		{
			if (!RequiredStartupPropertiesSet ||
				_webviewManager != null)
			{
				return;
			}
			if (PlatformView == null)
			{
				throw new InvalidOperationException($"Can't start {nameof(BlazorWebView)} without native web view instance.");
			}

			var logger = Services!.GetService<ILogger<BlazorWebViewHandler>>() ?? NullLogger<BlazorWebViewHandler>.Instance;

			// We assume the host page is always in the root of the content directory, because it's
			// unclear there's any other use case. We can add more options later if so.
			var contentRootDir = Path.GetDirectoryName(HostPage!) ?? string.Empty;
			var hostPageRelativePath = Path.GetRelativePath(contentRootDir, HostPage!);

			logger.CreatingFileProvider(contentRootDir, hostPageRelativePath);
			var fileProvider = VirtualView.CreateFileProvider(contentRootDir);

			_webviewManager = new WinUIWebViewManager(
				PlatformView,
				Services!,
				new MauiDispatcher(Services!.GetRequiredService<IDispatcher>()),
				fileProvider,
				VirtualView.JSComponents,
				contentRootDir,
				hostPageRelativePath,
				this,
				logger);

			StaticContentHotReloadManager.AttachToWebViewManagerIfEnabled(_webviewManager);

			if (RootComponents != null)
			{
				foreach (var rootComponent in RootComponents)
				{
					if (rootComponent is null)
					{
						continue;
					}

					logger.AddingRootComponent(rootComponent.ComponentType?.FullName ?? string.Empty, rootComponent.Selector ?? string.Empty, rootComponent.Parameters?.Count ?? 0);

					// Since the page isn't loaded yet, this will always complete synchronously
					_ = rootComponent.AddToWebViewManagerAsync(_webviewManager);
				}
			}

			logger.StartingInitialNavigation(VirtualView.StartPath);
			_webviewManager.Navigate(VirtualView.StartPath);
		}

		internal static void MapFlowDirection(BlazorWebViewHandler handler, IView view)
		{
			// Explicitly do nothing here to override the base ViewHandler.MapFlowDirection behavior
			// This prevents the WebView2.FlowDirection from being set, avoiding content mirroring
		}

		internal IFileProvider CreateFileProvider(string contentRootDir)
		{
			// On WinUI we override HandleWebResourceRequest in WinUIWebViewManager so that loading static assets is done entirely there in an async manner.
			// This allows the code to be async because in WinUI all the file storage APIs are async-only, but IFileProvider is sync-only and we need to control
			// the precedence of which files are loaded from where.
			return new NullFileProvider();
		}

		/// <summary>
		/// Calls the specified <paramref name="workItem"/> asynchronously and passes in the scoped services available to Razor components.
		/// </summary>
		/// <param name="workItem">The action to call.</param>
		/// <returns>Returns a <see cref="Task"/> representing <c>true</c> if the <paramref name="workItem"/> was called, or <c>false</c> if it was not called because Blazor is not currently running.</returns>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="workItem"/> is <c>null</c>.</exception>
		public virtual async Task<bool> TryDispatchAsync(Action<IServiceProvider> workItem)
		{
			ArgumentNullException.ThrowIfNull(workItem);
			if (_webviewManager is null)
			{
				return false;
			}

			return await _webviewManager.TryDispatchAsync(workItem);
		}
	}
}
