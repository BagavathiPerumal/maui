using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue30251 : _IssuesUITest
{

	public Issue30251(TestDevice testDevice) : base(testDevice) { }

	public override string Issue => "[iOS] RotateTo visually rotates through the layers that are on top";

	[Test]
	[Category(UITestCategories.Image)]
	public void RotateToShouldNotVisuallyRotateThroughTopLayers()
	{
		App.WaitForElement("RotateButton");
		App.Tap("RotateButton");
		App.WaitForElement("BottomImage");
		VerifyScreenshot();
	}
}