#if TEST_FAILS_ON_WINDOWS
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue16433 : _IssuesUITest
{
	public Issue16433(TestDevice device) : base(device) { }

	public override string Issue => "Navigation bar background color bug when navigating in a specific manner";
	[Test]
	[Category(UITestCategories.Navigation)]
	public void VerifyShellBackgroundAfterNavigationFromModalPage()
	{
		App.WaitForElement("GoToCViaBButton");
		App.Click("GoToCViaBButton");
		App.WaitForElement("GoBackToBButton");
		App.Click("GoBackToBButton");
		VerifyScreenshot();
	}
}
#endif