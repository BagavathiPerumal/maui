using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue34122 : _IssuesUITest
{
	public override string Issue => "I5_EmptyView_Swap - Continuously turning the Toggle EmptyViews on and off would cause an item from the list to show up";

	public Issue34122(TestDevice device) : base(device) { }

	[Test]
	[Category(UITestCategories.CollectionView)]
	public void EmptyViewSwapShouldNotRevealFilteredOutItems()
	{
		Exception? exception = null;

		// Verify items are visible initially
		App.WaitForElement("Baboon");

		// Clear all items one-by-one (matches the original ManualTests FilterCommand),
		// which fires multiple CollectionChanged events and puts RecyclerView into
		// the state that triggers the EmptyView-swap bug.
		App.WaitForElement("FilterButton");
		App.Tap("FilterButton");
		VerifyScreenshotOrSetException(ref exception, "Issue34122_AdvancedEmptyView", retryTimeout: TimeSpan.FromSeconds(1));

		App.WaitForElement("ToggleEmptyViewButton");

		// Reuse the same screenshot names so later toggles compare against the first
		// correct BasicEmptyView/AdvancedEmptyView states and catch visual leaks.
		for (int i = 1; i <= 8; i++)
		{
			App.Tap("ToggleEmptyViewButton");

			if (i % 2 == 1) // odd → BasicEmptyView
			{
				VerifyScreenshotOrSetException(ref exception, "Issue34122_BasicEmptyView", retryTimeout: TimeSpan.FromSeconds(1));
			}
			else // even → AdvancedEmptyView
			{
				VerifyScreenshotOrSetException(ref exception, "Issue34122_AdvancedEmptyView", retryTimeout: TimeSpan.FromSeconds(1));
			}
		}

		if (exception != null)
		{
			throw exception;
		}
	}
}
