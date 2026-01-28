#if MACCATALYST
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue33463 : _IssuesUITest
{
	public Issue33463(TestDevice testDevice) : base(testDevice)
	{
	}

	public override string Issue => "[macOS]Picker items are not visible";

	[Test]
	[Category(UITestCategories.Picker)]
	public void PickerShouldStayOpenAfterTabKeyPress()
	{
		App.WaitForElement("TestPicker");

		App.Tap("TestPicker");
		App.WaitForElement("Done");
		App.Tap("Done");

		App.SendTabKey();
		Task.Delay(800).Wait();

		var doneButton = App.FindElement("Done");
		Assert.That(doneButton, Is.Not.Null, 
			"Picker dialog should stay open after TAB key press");
	}
}
#endif
