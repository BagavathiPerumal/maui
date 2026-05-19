using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue35483 : _IssuesUITest
{
	public Issue35483(TestDevice device) : base(device) { }

	public override string Issue => "WebView leaks when using a shared WebViewSource";

#if MACCATALYST
	// On Mac, TryToResetTestState is a no-op in the base class (relies on startup env var).
	// If startup nav failed (app shows gallery), fall back to search-bar navigation.
	protected override void TryToResetTestState()
	{
		try
		{
			App.WaitForElement("RunButton", timeout: TimeSpan.FromSeconds(5));
		}
		catch
		{
			// Startup navigation didn't land on the test page; navigate via gallery.
			App.WaitForElement("GoToTestButton");
			App.EnterText("SearchBar", Issue);
			App.WaitForElement("GoToTestButton");
			App.Tap("GoToTestButton");
		}
	}
#endif

	[Test]
	[Category(UITestCategories.WebView)]
	public void WebViewDoesNotLeakWithSharedSource()
	{
		// Wait for the main page to appear and run the leak test.
		App.WaitForElement("RunButton");
		App.Tap("RunButton");

		// The test pushes 5 pages then pops them. During that time ResultLabel is temporarily
		// hidden by the navigation stack on iOS (XCUITest only queries the topmost page).
		// WaitForTextToBePresentInElement uses FindElements (plural) which returns empty list
		// instead of throwing, so it safely polls through the navigation phase.
		bool collected = App.WaitForTextToBePresentInElement("ResultLabel", "Collected: 0/5",
			timeout: TimeSpan.FromSeconds(60));

		if (!collected)
		{
			// GC did not confirm all collected — check if a "Leaked" result came through
			App.WaitForTextToBePresentInElement("ResultLabel", "Leaked", timeout: TimeSpan.FromSeconds(10));
		}

		var result = App.FindElement("ResultLabel").GetText();
		Assert.That(result, Is.EqualTo("Collected: 0/5"),
			"WebViews should be garbage collected after navigation pop. " +
			"With a shared WebViewSource, the strong SourceChanged event subscription " +
			"prevents collection (issue #35483).");
	}
}
