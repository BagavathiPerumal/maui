using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues
{
	public class Issue35643 : _IssuesUITest
	{
		public Issue35643(TestDevice device) : base(device) { }

		public override string Issue => "CurrentItem is updated incorrectly on Android when the CarouselView is bound to an ObservableCollection with Loop = false";

		[Test]
		[Category(UITestCategories.CarouselView)]
		public void CurrentItemShouldUpdateWhenCurrentItemIsReplaced()
		{
			// Wait for the page to load and verify the initial CurrentItem is "2"
			App.WaitForElement("CurrentItemLabel");
			var initialText = App.FindElement("CurrentItemLabel").GetText();
			Assert.That(initialText, Is.EqualTo("2"), "Initial CurrentItem should be '2'");

			var initialPosition = App.FindElement("PositionLabel").GetText();
			Assert.That(initialPosition, Is.EqualTo("2"), "Initial Position should be 2");

			// Tap the button to replace item at index 2 with "2b" and update CurrentItem to "2b"
			App.WaitForElement("UpdateButton");
			App.Tap("UpdateButton");

			// Verify CurrentItem is "2b" — on Android with Loop=false this incorrectly resets to "0"
			var updatedText = App.FindElement("CurrentItemLabel").GetText();
			Assert.That(updatedText, Is.EqualTo("2b"), "CurrentItem should be '2b' after replacing the last item, but got: " + updatedText);

			// Position must NOT reset to 0 — on Windows two independent paths reset the carousel
			// position to 0 even though CurrentItem was updated correctly.
			var updatedPosition = App.FindElement("PositionLabel").GetText();
			Assert.That(updatedPosition, Is.EqualTo("2"), "Position should stay at index 2 after replacing the item, but got: " + updatedPosition);
		}
	}
}
