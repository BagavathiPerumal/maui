using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues
{
	public class IssueDoubleTapSingleTap : _IssuesUITest
	{
		public override string Issue => "Single tap event handler triggered for double mouse click on Windows";

		public IssueDoubleTapSingleTap(TestDevice device) : base(device)
		{
		}

		[Test]
		[Category(UITestCategories.Gesture)]
		public void DoubleTapShouldNotTriggerSingleTap()
		{
			App.WaitForElement("TestGraphicsView");
			
			// Perform a single tap first to verify the single tap behavior works
			App.Tap("TestGraphicsView");
			
			// Verify single tap was detected
			var statusLabel = App.FindElement("StatusLabel");
			Assert.That(statusLabel.Text, Is.EqualTo("Single tap detected"));
			
			// Now perform a double tap 
			App.DoubleTap("TestGraphicsView");
			
			// Verify that only double tap was detected, not single tap
			// The status should show "Double tap detected" and not "Single tap detected"
			statusLabel = App.FindElement("StatusLabel");
			Assert.That(statusLabel.Text, Is.EqualTo("Double tap detected"));
		}
	}
}