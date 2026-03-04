using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue32983 : _IssuesUITest
{
	public override string Issue => "CollectionView messes up Measure operation on Views";

	public Issue32983(TestDevice device) : base(device) { }

	[Test]
	[Category(UITestCategories.CollectionView)]
	public void BottomSheetDetentHeightIsCorrectWhenCollectionViewIsMeasuredBeforeMount()
	{
		if (Device != TestDevice.iOS)
			Assert.Ignore("This test is iOS-only. The CollectionView2 pre-mount Measure() fix applies only to iOS. Android/Windows use the unaffected Items/ handler; MacCatalyst ignores UISheetPresentationController custom detents by design.");

		App.WaitForElement("ShowBottomSheetButton");
		App.Tap("ShowBottomSheetButton");

		App.WaitForElement("BottomSheetCollectionView");

		VerifyScreenshot();
	}
}
