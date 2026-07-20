// iOS only: this is a regression specific to iOS SafeAreaEdges handling when a Page's
// Top and Bottom edges are configured differently (e.g. Top=None, Bottom=Container).
// Android already handles this correctly per the reported issue.
#if IOS
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues
{
	public class Issue34563 : _IssuesUITest
	{
		public override string Issue => "[iOS] Vertical SafeAreaEdge isn't respected when Top and Bottom constraints mismatch";

		public Issue34563(TestDevice device) : base(device) { }

		[Test]
		[Category(UITestCategories.SafeAreaEdges)]
		public void SafeAreaEdgesRespectedWhenTopAndBottomMismatch()
		{
			App.WaitForElement("RootGrid");

			// Even though the Page's Top edge is None, TopSafeBox opts into
			// SafeAreaEdges.Container and must not render flush against the very top
			// of the screen (i.e. under the status bar / notch).
			var topSafeBoxRect = App.WaitForElement("TopSafeBox").GetRect();
			Assert.That(topSafeBoxRect.Y, Is.GreaterThan(0),
				"TopSafeBox opted into SafeAreaEdges.Container and should be inset from the top of the screen, " +
				"even though the Page's Top edge is None.");

			// Even though the Page's Bottom edge is Container (which already the Page
			// itself accounts for), BottomSafeBox additionally opts into Container and
			// must not render flush against the very bottom of the screen (i.e. under
			// the home indicator).
			var bottomSafeBoxRect = App.WaitForElement("BottomSafeBox").GetRect();
			var rootGridRect = App.WaitForElement("RootGrid").GetRect();
			var screenBottom = rootGridRect.Y + rootGridRect.Height;
			var bottomSafeBoxBottom = bottomSafeBoxRect.Y + bottomSafeBoxRect.Height;

			Assert.That(bottomSafeBoxBottom, Is.LessThanOrEqualTo(screenBottom),
				"BottomSafeBox should not extend past the bottom of its container.");
		}

		// Added during code review of the #34563 fix: the scenario above always has the
		// mismatch on the Page itself, so it can't isolate "a parent blocks Top only, and an
		// unrelated Bottom edge on a DESCENDANT (not the Page) is still applied independently" —
		// once the Page blocks Bottom, that blocks Bottom for every descendant on that page.
		// This test instead uses an intermediate ancestor (MidContainer, Top=Container/Bottom=None)
		// on a separate, edge-to-edge page so the Bottom edge is never blocked by any ancestor.
		[Test]
		[Category(UITestCategories.SafeAreaEdges)]
		public void SafeAreaEdgesRespectedWhenNestedAncestorBlocksOnlyOneEdge()
		{
			App.WaitForElement("RootGrid");
			App.Tap("OpenNestedAncestorScenarioButton");
			App.WaitForElement("NestedRootGrid");

			// MidContainer only handles the Top edge (Top=Container, Bottom=None) — a real,
			// non-zero inset — and is the direct parent of both TopSafeBox2 and BottomSafeBox2.
			// TopSafeBox2 opts into SafeAreaEdges.Container and must not render flush against
			// the very top of the screen (i.e. under the status bar / notch), even though the
			// blocking ancestor here is MidContainer, not the Page.
			var topSafeBox2Rect = App.WaitForElement("TopSafeBox2").GetRect();
			Assert.That(topSafeBox2Rect.Y, Is.GreaterThan(0),
				"TopSafeBox2 opted into SafeAreaEdges.Container and should be inset from the top of the screen, " +
				"even though MidContainer's own Bottom edge is None.");

			// MidContainer never handles the Bottom edge, so BottomSafeBox2 — its child — must
			// independently apply its own Bottom inset. This is the "parent blocks Top only,
			// child still applies Bottom" scenario: a strict "<" (not "<=") verifies a real,
			// non-zero inset was actually applied rather than merely fitting within bounds.
			var bottomSafeBox2Rect = App.WaitForElement("BottomSafeBox2").GetRect();
			var nestedRootGridRect = App.WaitForElement("NestedRootGrid").GetRect();
			var screenBottom = nestedRootGridRect.Y + nestedRootGridRect.Height;
			var bottomSafeBox2Bottom = bottomSafeBox2Rect.Y + bottomSafeBox2Rect.Height;

			Assert.That(bottomSafeBox2Bottom, Is.LessThan(screenBottom),
				"BottomSafeBox2 should independently apply its own Bottom safe area inset (not rely on any " +
				"ancestor, since MidContainer only blocks the Top edge) and must not render flush against " +
				"the very bottom of the screen (i.e. under the home indicator).");

			// Return to the original page (this page was reached via PushModalAsync) so that
			// subsequent tests sharing this fixture instance find "RootGrid" as expected.
			App.Tap("CloseNestedAncestorScenarioButton");
			App.WaitForElement("RootGrid");
		}
	}
}
#endif
