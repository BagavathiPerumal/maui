#if WINDOWS // This scenario is specific to the Windows TopNav overflow "More" menu.
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues
{
	public class Issue37985 : _IssuesUITest
	{
		public override string Issue => "Shell crashes when a hierarchical Tab is the last item in the TabBar overflow 'More' menu";

		public Issue37985(TestDevice testDevice) : base(testDevice)
		{
		}

		[Test]
		[Category(UITestCategories.Shell)]
		public void HierarchicalTabInOverflowMenuShouldNotCrash()
		{
			App.WaitForElement("Page 1");
			App.Tap("More");
			App.WaitForElement("Hierarchical Tab");

			// Tapping the body of the hierarchical tab while it's in the overflow menu previously
			// crashed the app. It should now only expand the submenu without crashing.
			App.Tap("Hierarchical Tab");
			App.WaitForElement("Sub Page 1");

			// Selecting a child from the expanded submenu should still navigate normally.
			App.Tap("Sub Page 1");
			App.WaitForElement("SubPage1Label");
		}
	}
}
#endif
