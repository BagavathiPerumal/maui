using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;
public class Issue21828 : _IssuesUITest
{
	public Issue21828(TestDevice device) : base(device)
	{
	}

	public override string Issue => "Flyout Button Disappears";

	[Test]
	[Category(UITestCategories.FlyoutPage)]
	public void VerifyFlyoutIconAfterPageInsert()
	{
		App.WaitForElement("StartPageButton");
		App.Tap("StartPageButton");
		VerifyScreenshot();
	}
}