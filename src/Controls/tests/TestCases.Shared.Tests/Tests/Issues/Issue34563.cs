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
	}
}
#endif
