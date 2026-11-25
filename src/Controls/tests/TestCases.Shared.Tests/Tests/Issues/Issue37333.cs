// This test is Android-only because safe-area insets can arrive before the page is measured, triggering a second allocation.
#if ANDROID
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue37333 : _IssuesUITest
{
	public Issue37333(TestDevice device) : base(device)
	{
	}

	public override string Issue => "OnSizeAllocated is called twice when navigating to a Shell page";

	[Test]
	[ShardedTestCategory(UITestCategories.Shell, shard: 1)]
	public void MainAndDestinationPagesAllocateOnceOnLoadAndNavigation()
	{
		Assert.That(App.WaitForElement("MainPageAllocationCount").GetText(), Is.EqualTo("1"),
			"The main page should have one size allocation after loading.");

		App.Tap("ShowAllButton");

		Assert.That(App.WaitForElement("TextPageAllocationCount").GetText(), Is.EqualTo("1"),
			"The destination page should have one size allocation after Shell navigation.");
	}
}
#endif
