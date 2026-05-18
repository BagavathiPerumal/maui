namespace Maui.Controls.Sample.Issues;

// Outer navigation container. TestNavigationPage is required so the Issue attribute is
// on a NavigationPage subclass, which MauiProgram can set directly as the window root on Mac.
[Issue(IssueTracker.Github, 35483, "WebView leaks when using a shared WebViewSource", PlatformAffected.All)]
public class Issue35483 : TestNavigationPage
{
	protected override void Init()
	{
		PushAsync(new Issue35483MainPage());
	}
}

public class Issue35483MainPage : ContentPage
{
	// Long-lived shared source — simulates a singleton/resource/ViewModel scenario.
	static readonly HtmlWebViewSource s_sharedSource = new HtmlWebViewSource
	{
		Html = "<html><body><h1>Shared WebViewSource</h1></body></html>"
	};

	const int PageCount = 5;

	readonly Label _resultLabel;
	readonly List<WeakReference> _webViewRefs = new List<WeakReference>();

	public Issue35483MainPage()
	{
		_resultLabel = new Label
		{
			Text = "Tap 'Run Leak Test' to begin",
			AutomationId = "ResultLabel"
		};

		var runButton = new Button
		{
			Text = "Run Leak Test",
			AutomationId = "RunButton"
		};

		runButton.Clicked += OnRunButtonClicked;

		Content = new VerticalStackLayout
		{
			Padding = new Thickness(20),
			Spacing = 15,
			Children = { _resultLabel, runButton }
		};
	}

	async void OnRunButtonClicked(object sender, EventArgs e)
	{
		_webViewRefs.Clear();
		_resultLabel.Text = "Pushing pages...";

		// Push PageCount pages, each holding a WebView assigned to the shared source.
		// This matches the repro: many pages share one HtmlWebViewSource.
		for (int i = 0; i < PageCount; i++)
		{
			await Navigation.PushAsync(CreateWebViewPage(), animated: false);
			await Task.Delay(50);
		}

		// Pop all pages to release them from the navigation stack.
		for (int i = 0; i < PageCount; i++)
		{
			await Navigation.PopAsync(animated: false);
			await Task.Delay(25);
		}

		_resultLabel.Text = "Running GC...";

		// Multiple GC cycles to give the runtime every opportunity to collect.
		for (int i = 0; i < 3; i++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			await Task.Delay(100);
		}

		int aliveWebViews = 0;
		foreach (var wr in _webViewRefs)
		{
			if (wr.IsAlive)
				aliveWebViews++;
		}

		// "Collected: 0/5" means no leak; "Leaked: N/5" means the bug is present.
		_resultLabel.Text = aliveWebViews == 0
			? string.Format("Collected: 0/{0}", PageCount)
			: string.Format("Leaked: {0}/{1}", aliveWebViews, PageCount);
	}

	// Separate method ensures local WebView variable is out of scope before GC runs.
	ContentPage CreateWebViewPage()
	{
		var webView = new WebView { Source = s_sharedSource };
		_webViewRefs.Add(new WeakReference(webView));

		return new ContentPage
		{
			Content = new VerticalStackLayout
			{
				Children = { webView }
			}
		};
	}
}
