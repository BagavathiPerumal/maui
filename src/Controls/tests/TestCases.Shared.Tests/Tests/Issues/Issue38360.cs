#if ANDROID // This regression is specific to Android's Loaded event and native drawing lifecycle.
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue38360 : _IssuesUITest
{
	public Issue38360(TestDevice device) : base(device) { }

	public override string Issue => "Android: Behavior Change in the Loaded Event Observed in Version 10.0.60+";

	[Test]
	[Category(UITestCategories.LifeCycle)]
	public void LoadedEventFiresBeforeFirstDraw()
	{
		App.WaitForElement("ResultLabel");

		var resultText = App.FindElement("ResultLabel").GetText();

		Assert.That(resultText, Is.EqualTo("LoadedBeforeDraw"),
			"Loaded must fire before the native Android view's first draw.");
	}
}
#endif
