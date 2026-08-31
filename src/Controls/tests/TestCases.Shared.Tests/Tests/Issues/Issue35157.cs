// This regression is Android-only because it exercises WebView.HitTestResult URL resolution for image anchors.
#if ANDROID
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue35157(TestDevice device) : _IssuesUITest(device)
{
	public override string Issue => "Target blank link with data URI image crashes BlazorWebView";

	[Test]
	[Category(UITestCategories.WebView)]
	public void TargetBlankLinkWithDataImageDoesNotCrash()
	{
		App.WaitForElement("Open Google");
		App.Tap("Open Google");
		Thread.Sleep(2000);

		App.ForegroundApp();
		App.WaitForElement("Issue35157SurvivalLabel");
	}
}
#endif
