using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue32983 : _IssuesUITest
{
	public override string Issue => "CollectionView messes up Measure operation on Views";

	public Issue32983(TestDevice device) : base(device) { }

	// Reproduces the exact customer scenario from SampleMeasureIssue.zip:
	//
	// 1. Tap "ShowBottomSheet" button
	// 2. HostApp creates BottomSheetContentView (ContentView wrapping CollectionView, 10 items)
	// 3. Calls Measure(screenWidth, screenHeight) BEFORE adding the view to any visual tree
	// 4. Uses the measured height as a custom UISheetPresentationControllerDetent height
	// 5. Presents as a native iOS bottom sheet
	//
	// Bug (.NET 10, CollectionView2 handler):
	//   Measure() returns wrong height → items are incorrectly sized or invisible:
	//     - Too large: sheet covers full screen (detent ≈ screen height)
	//     - Too small: sheet shows only a fraction of items (detent ≈ 0 or single item height)
	//
	// Expected (fixed): All 10 items ("Item #1"…"Item #10") are visible without scrolling.
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
