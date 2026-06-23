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
		[Order(1)]
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

		[Test]
		[Order(2)]
		[Category(UITestCategories.CarouselView)]
		public void PositionShouldNotChangeWhenNonCurrentItemIsReplaced()
		{
			// Section 2 has its own independent carousel (NonCurrentItems=["0","1","2"], CurrentItem="2", Position=2).
			// Scroll to the button (interactive element — Appium ScrollTo requires an interactive target);
			// the label is just above it and will be in the viewport after scrolling.
			App.ScrollTo("ReplaceNonCurrentButton");
			App.WaitForElement("NonCurrentCurrentItemLabel");
			Assert.That(App.FindElement("NonCurrentCurrentItemLabel").GetText(), Is.EqualTo("2"), "Initial NonCurrentCurrentItem should be '2'");
			Assert.That(App.FindElement("NonCurrentPositionLabel").GetText(), Is.EqualTo("2"), "Initial Position should be 2");

			// Replace NonCurrentItems[0] ("0" → "0b") — this is a non-current item.
			// The carousel must stay on items[2]; Position must not reset to 0.
			App.Tap("ReplaceNonCurrentButton");

			// CurrentItem must remain "2" — replacing a background item must not affect CurrentItem
			Assert.That(App.FindElement("NonCurrentCurrentItemLabel").GetText(), Is.EqualTo("2"),
				"NonCurrentCurrentItem should stay '2' after replacing a non-current item");

			// Position must remain 2 — KeepItemsInView must not scroll to 0 on Replace
			Assert.That(App.FindElement("NonCurrentPositionLabel").GetText(), Is.EqualTo("2"),
				"Position should stay at 2 after replacing a non-current item");
		}

		[Test]
		[Order(3)]
		[Category(UITestCategories.CarouselView)]
		public void PositionShouldNotChangeWhenCurrentItemIsReplacedWithLoopEnabled()
		{
			// Scroll the loop section into view — it lives below the non-loop carousel in a ScrollView.
			App.ScrollTo("LoopReplaceButton");

			// Initial state: LoopItems=["A","B","C"], LoopCurrentItem="C", Position=2
			App.WaitForElement("LoopCurrentItemLabel");
			Assert.That(App.FindElement("LoopCurrentItemLabel").GetText(), Is.EqualTo("C"),
				"Initial LoopCurrentItem should be 'C'");
			Assert.That(App.FindElement("LoopPositionLabel").GetText(), Is.EqualTo("2"),
				"Initial loop carousel Position should be 2");

			// Replace items[2] ("C" → "C2") — the current item in the loop carousel.
			// The virtual infinite adapter must be rebuilt via UpdateAdapter(); position must stay at 2.
			App.WaitForElement("LoopReplaceButton");
			App.Tap("LoopReplaceButton");

			// CurrentItem must update to "C2"
			Assert.That(App.FindElement("LoopCurrentItemLabel").GetText(), Is.EqualTo("C2"),
				"LoopCurrentItem should be 'C2' after replacing the current item in loop mode");

			// Position must NOT reset — on Android with Loop=true the adapter rebuild should not scroll away
			Assert.That(App.FindElement("LoopPositionLabel").GetText(), Is.EqualTo("2"),
				"Loop carousel Position should stay at 2 after replacing the current item");
		}

		[Test]
		[Order(4)]
		[Category(UITestCategories.CarouselView)]
		public void PositionShouldNotChangeWithKeepLastItemInViewOnReplace()
		{
			// Scroll to the KeepLast button (interactive element — Appium ScrollTo requires an interactive target).
			// The label is directly above it and will be in the viewport after scrolling.
			App.ScrollTo("KeepLastReplaceButton");

			// Initial state: KeepLastItems=["P","Q","R"], KeepLastCurrentItem="P", Position=0
			App.WaitForElement("KeepLastCurrentItemLabel");
			Assert.That(App.FindElement("KeepLastCurrentItemLabel").GetText(), Is.EqualTo("P"),
				"Initial KeepLastCurrentItem should be 'P'");
			Assert.That(App.FindElement("KeepLastPositionLabel").GetText(), Is.EqualTo("0"),
				"Initial KeepLast carousel Position should be 0");

			// Replace items[2] ("R" → "R2") — the last, non-current item.
			// With ItemsUpdatingScrollMode = KeepLastItemInView, Insert/Remove would scroll to index 2.
			// Replace must bypass that path — the position must stay at 0.
			App.WaitForElement("KeepLastReplaceButton");
			App.Tap("KeepLastReplaceButton");

			// CurrentItem must remain "P" — replacing a different item must not affect CurrentItem
			Assert.That(App.FindElement("KeepLastCurrentItemLabel").GetText(), Is.EqualTo("P"),
				"KeepLastCurrentItem should remain 'P' after replacing the last item");

			// Position must NOT jump to 2 (the last item index) as KeepLastItemInView would for Insert/Remove
			Assert.That(App.FindElement("KeepLastPositionLabel").GetText(), Is.EqualTo("0"),
				"Position should stay at 0 — KeepLastItemInView must not fire for Replace");
		}

		[Test]
		[Order(5)]
		[Category(UITestCategories.CarouselView)]
		public void PositionShouldNotChangeWhenDuplicateValueItemIsReplaced()
		{
			// Scroll to the duplicate-value section.
			App.ScrollTo("DupReplaceButton");

			// Initial state: DupItems=["A","B","A"], DupCurrentItem="A" (items[0]), Position=0
			// items[0] and items[2] share the same string value "A".
			App.WaitForElement("DupCurrentItemLabel");
			Assert.That(App.FindElement("DupCurrentItemLabel").GetText(), Is.EqualTo("A"),
				"Initial DupCurrentItem should be 'A'");
			Assert.That(App.FindElement("DupPositionLabel").GetText(), Is.EqualTo("0"),
				"Initial Dup carousel Position should be 0");

			// Replace items[2] (the second "A" → "X").
			// Equals-based lookup (GetPosition) would find index 0 for "A" and incorrectly treat
			// this as a current-item Replace, potentially moving the carousel. Index-based comparison
			// (e.OldStartingIndex == carouselPosition → 2 != 0) must correctly identify this as a
			// non-current item Replace and leave the carousel at Position=0.
			App.WaitForElement("DupReplaceButton");
			App.Tap("DupReplaceButton");

			// CurrentItem must remain "A" — only items[2] was replaced, not items[0]
			Assert.That(App.FindElement("DupCurrentItemLabel").GetText(), Is.EqualTo("A"),
				"DupCurrentItem should remain 'A' after replacing the duplicate item at index 2");

			// Position must remain 0 — the carousel must not confuse items[2] with CurrentItem
			Assert.That(App.FindElement("DupPositionLabel").GetText(), Is.EqualTo("0"),
				"Position should stay at 0 — replacing duplicate-value item must not affect current position");
		}
	}
}
