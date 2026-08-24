#if ANDROID // The bug is specific to Android's soft keyboard resize/pan behavior.
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue34020 : _IssuesUITest
{
	public Issue34020(TestDevice device) : base(device) { }

	public override string Issue => "ScrollView doesn't work with multiple Editor controls on a Page";

	[Test]
	[Category(UITestCategories.ScrollView)]
	public void ScrollViewRemainsScrollableWhenKeyboardIsShown()
	{
		// Step 1: Confirm the top marker is visible before any keyboard interaction.
		App.WaitForElement("TopMarker");

		// Step 2: Tap the LAST Editor (Editor5) to bring up the soft keyboard. This is the exact
		// regression scenario reported: focusing the last editor (not an earlier one) is what
		// exposes the shared MauiWindowInsetListener dispatch-order bug, because the set of views
		// that dispatch OnApplyWindowInsets while the IME animates - and therefore which one used
		// to "win" the old single-pending-view tracking - depends on which editor has focus.
		App.ScrollTo("Editor5");
		App.Tap("Editor5");

		// Give Android time to resize/pan the window and show the keyboard.
		Task.Delay(1500).Wait();

		// Step 3: With the keyboard showing, the ScrollView should still be able to scroll
		// so that the bottom marker (below the focused editor) can be revealed.
		App.ScrollTo("BottomMarker");
		var bottomMarkerRect = App.WaitForElement("BottomMarker").GetRect();
		Assert.That(bottomMarkerRect.Y, Is.GreaterThan(0),
			"BottomMarker should have scrolled into view while the keyboard was shown.");

		// Step 4: With the keyboard still showing (Editor5 still focused), the toolbar item must
		// still be reachable/visible — this is the exact regression reported: the ToolbarItem
		// stays hidden while an editor low in the content is focused and the keyboard is shown.
		//
		// Note: ToolbarItem.AutomationId is only surfaced as the native MenuItem's
		// ContentDescription on Android (see AutomationPropertiesProvider.SetTitleOrContentDescription),
		// NOT as a resource-id. App.WaitForElement(string) only matches resource-id / @text / @label /
		// @Name, so it can never find a toolbar item here — ByAccessibilityId (content-desc) must be
		// used instead.
		App.WaitForElement(AppiumQuery.ByAccessibilityId("ToolbarHideKeyboard"));
	}
}
#endif
