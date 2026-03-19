#if ANDROID
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue33508 : _IssuesUITest
{
	public Issue33508(TestDevice device) : base(device)
	{
	}

	public override string Issue => "Android: BackButton on Android 16 not working after command from FlyOutPage";

	[Test]
	[Category(UITestCategories.FlyoutPage)]
	public void SystemBackShouldStillInvokeOnBackButtonPressedAfterFlyoutNavigation()
	{
		App.WaitForElement("Issue33508StartPageLabel");
		App.Tap("Issue33508NavigateToDetailButton");
		App.WaitForElement("Issue33508DetailPageLabel");

		Assert.That(App.FindElement("Issue33508BackHandledCountLabel").GetText(), Is.EqualTo("Back handled count: 0"));

		App.Back();
		Assert.That(App.FindElement("Issue33508BackHandledCountLabel").GetText(), Is.EqualTo("Back handled count: 1"),
			"The first Android back press should be handled by DetailPage1.");

		App.Tap("Issue33508DetailNavigateToStartButton");
		App.WaitForElement("Issue33508StartPageLabel");
		App.Tap("Issue33508NavigateToDetailButton");
		App.WaitForElement("Issue33508DetailPageLabel");

		App.Back();
		Assert.That(App.FindElement("Issue33508BackHandledCountLabel").GetText(), Is.EqualTo("Back handled count: 2"),
			"The in-page StartPage navigation path should keep OnBackButtonPressed wired up.");

		App.Tap("Issue33508OpenFlyoutButton");
		App.WaitForElement("Issue33508FlyoutNavigateToStartButton");
		App.Tap("Issue33508FlyoutNavigateToStartButton");
		App.WaitForElement("Issue33508StartPageLabel");
		Assert.That(App.FindElement("Issue33508LastNavigationSourceLabel").GetText(), Is.EqualTo("Last navigation source: Flyout"));

		App.Tap("Issue33508NavigateToDetailButton");
		App.WaitForElement("Issue33508DetailPageLabel");
		App.Back();

		Assert.That(App.FindElement("Issue33508BackHandledCountLabel").GetText(), Is.EqualTo("Back handled count: 3"),
			"After replacing Window.Page from an open FlyoutPage, Android back should still call the FlyoutPage OnBackButtonPressed override.");
	}
}
#endif
