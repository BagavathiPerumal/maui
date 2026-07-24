// iOS only: MauiScrollView.cs (backing UIScrollView) maintains its own, separate copy of the
// parent/ancestor safe-area-blocking logic — it does not inherit from MauiView — so it required
// an independent fix and independent regression coverage from Issue34563.cs's plain-MauiView
// scenarios (ContentView/Layout/Page). This file exercises MauiScrollView specifically.
#if IOS
using System.Threading;
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues
{
	public class Issue34563_ScrollView : _IssuesUITest
	{
		public override string Issue => "[iOS] Vertical SafeAreaEdge isn't respected when Top and Bottom constraints mismatch (ScrollView coverage)";

		public Issue34563_ScrollView(TestDevice device) : base(device) { }

		// Scenario A: an ancestor (the Page) has a real inset ONLY on Bottom. The ScrollView,
		// as the Page's direct child, opts into Container on Top ONLY. Since no ancestor has a
		// real inset on Top, the ScrollView must apply its own independent Top inset.
		[Test]
		[Category(UITestCategories.SafeAreaEdges)]
		public void ScrollViewAppliesTopInsetWhenParentOnlyBlocksBottom()
		{
			App.WaitForElement("ScrollViewScenarioMenu");
			App.Tap("OpenScrollViewTopMismatchButton");
			App.WaitForElement("TopMismatchScrollView");

			// Force a rotation cycle so SafeAreaInsetsDidChange fires on every view in the
			// hierarchy, clearing MauiScrollView's cached _parentHandlesSafeArea and forcing a
			// fresh IsParentHandlingSafeArea() re-check *after* the ancestor Page's own
			// AppliesSafeAreaAdjustments has already stabilized. Without this, the very first
			// layout pass can race — the ScrollView may cache "parent doesn't block" before the
			// Page has computed its own flag, silently masking the whole-view blocking bug.
			App.SetOrientationLandscape();
			Thread.Sleep(1000);
			App.SetOrientationPortrait();
			Thread.Sleep(1000);
			App.WaitForElement("TopMismatchScrollView");

			// The assertion is wrapped so that ANY failure here (a real one, not just this test's
			// own) still lets us tap Close and return to the menu. Without this, a genuine
			// assertion failure leaves the app stranded on this scenario's page, and every
			// subsequent test's initial App.WaitForElement("ScrollViewScenarioMenu") times out —
			// a misleading cascade of unrelated-looking TimeoutExceptions that are really just
			// downstream symptoms of this one real failure.
			try
			{
				var contentRect = App.WaitForElement("TopMismatchScrollViewContent").GetRect();
				Assert.That(contentRect.Y, Is.GreaterThan(0),
					"ScrollView opted into SafeAreaEdges.Container on Top and should be inset from the top of the screen, " +
					"even though the ancestor Page's real inset is only on Bottom.");
			}
			finally
			{
				App.Tap("CloseTopMismatchScenarioButton");
				App.WaitForElement("ScrollViewScenarioMenu");
			}
		}

		// Scenario G: horizontal-edge counterpart to Scenario A. The whole-view
		// IsParentHandlingSafeArea() bug applies identically to all 4 edges (Left/Top/Right/
		// Bottom), but the original coverage only ever exercised Top/Bottom. An ancestor (the
		// Page) has a real inset ONLY on Right. The ScrollView, as the Page's direct child,
		// opts into Container on Left ONLY. Since no ancestor has a real inset on Left, the
		// ScrollView must apply its own independent Left inset. Uses the same direct-measurement
		// methodology as Scenario A (no scrolling required).
		//
		// IMPORTANT (unlike Scenario A): on notched iPhones, the native
		// UIView.SafeAreaInsets.Left/Right are only ever non-zero in LANDSCAPE orientation (the
		// notch/Dynamic Island sits on a side edge only when rotated) — in portrait they are
		// always 0. So, unlike Scenario A/B's Top/Bottom checks (which are valid in portrait,
		// since the status bar/home indicator always produce a real inset there), this
		// assertion MUST be taken while still rotated to landscape. Rotating back to portrait
		// first would make contentRect.X read 0 regardless of whether the fix is correct, since
		// there is no real native Left inset to apply in portrait in the first place.
		[Test]
		[Category(UITestCategories.SafeAreaEdges)]
		public void ScrollViewAppliesLeftInsetWhenParentOnlyBlocksRight()
		{
			App.WaitForElement("ScrollViewScenarioMenu");
			App.Tap("OpenScrollViewLeftRightMismatchButton");
			App.WaitForElement("LeftRightMismatchScrollView");

			// Rotate to landscape and stay there: this is what makes the native Left/Right
			// safe area insets non-zero on a notched device, and also clears MauiScrollView's
			// cached parent-blocking state (see comment in
			// ScrollViewAppliesTopInsetWhenParentOnlyBlocksBottom) so it re-evaluates after the
			// ancestor Page's own flag has stabilized in this orientation.
			App.SetOrientationLandscape();
			Thread.Sleep(1000);
			App.WaitForElement("LeftRightMismatchScrollView");

			// See comment in ScrollViewAppliesTopInsetWhenParentOnlyBlocksBottom: wrap the
			// assertion so a real failure still lets us tap Close and return to the menu,
			// instead of stranding the app and cascading TimeoutExceptions into later tests.
			try
			{
				var contentRect = App.WaitForElement("LeftRightMismatchScrollViewContent").GetRect();
				Assert.That(contentRect.X, Is.GreaterThan(0),
					"ScrollView opted into SafeAreaEdges.Container on Left and should be inset from the left of the screen, " +
					"even though the ancestor Page's real inset is only on Right.");
			}
			finally
			{
				App.Tap("CloseLeftRightMismatchScenarioButton");
				App.WaitForElement("ScrollViewScenarioMenu");
				// Restore portrait so subsequent scenarios (and any orientation-sensitive
				// assumptions in _IssuesUITest teardown) start from a consistent baseline.
				App.SetOrientationPortrait();
				Thread.Sleep(1000);
			}
		}
	}
}
#endif
