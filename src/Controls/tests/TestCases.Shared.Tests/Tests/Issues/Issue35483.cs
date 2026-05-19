using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue35483 : _IssuesUITest
{
	public Issue35483(TestDevice device) : base(device) { }

	public override string Issue => "WebView leaks when using a shared WebViewSource";

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
