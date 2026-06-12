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
			// Verify initial state: CurrentItem = "2", Position = 2
			App.WaitForElement("CurrentItemLabel");
			var initialText = App.FindElement("CurrentItemLabel").GetText();
			Assert.That(initialText, Is.EqualTo("2"), "Initial CurrentItem should be '2'");

			var initialPosition = App.FindElement("PositionLabel").GetText();
			Assert.That(initialPosition, Is.EqualTo("2"), "Initial Position should be 2");

			// Replace items[2] with "2b" (current item, not the last item)
			App.WaitForElement("UpdateButton");
			App.Tap("UpdateButton");

			// CurrentItem must update to "2b"
			var updatedText = App.FindElement("CurrentItemLabel").GetText();
			Assert.That(updatedText, Is.EqualTo("2b"), "CurrentItem should be '2b' after replacing the current item, but got: " + updatedText);

			// Position must NOT reset to 0 — this is the Windows-specific regression:
			// without the fix, two paths (KeepItemsInView block + WinUI VectorChanged) each
			// independently scroll the carousel back to index 0, even though CurrentItem is "2b".
			var updatedPosition = App.FindElement("PositionLabel").GetText();
			Assert.That(updatedPosition, Is.EqualTo("2"), "Position should stay at index 2 after replacing the current item, but got: " + updatedPosition);
		}
	}
}
