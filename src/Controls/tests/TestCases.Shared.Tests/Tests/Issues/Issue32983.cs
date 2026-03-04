// This test is iOS-only: CollectionView2 (Items2/) runs on iOS; Android/Windows use Items/ (unaffected); MacCatalyst ignores UISheetPresentationController custom detents by design.
#if IOS
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
		App.WaitForElement("ShowBottomSheetButton");
		App.Tap("ShowBottomSheetButton");

		App.WaitForElement("BottomSheetCollectionView");
		VerifyScreenshot();
	}
}
#endif
