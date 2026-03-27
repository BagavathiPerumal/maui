#if ANDROID
using System.Collections.Generic;
using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue34594 : _IssuesUITest
{
	public Issue34594(TestDevice device) : base(device)
	{
	}

	public override string Issue => "OnBackInvokedCallbacks block back-to-home animation";

	[Test]
	[Category(UITestCategories.Shell)]
	public void PredictiveBackGestureShouldNotRemainRegisteredAtRoot()
	{
		if (App is not AppiumAndroidApp androidApp)
			throw new InvalidOperationException($"Invalid App Type For this Test: {App} Expected AppiumAndroidApp.");

		var deviceApiLevel = GetDeviceApiLevel();
		if (deviceApiLevel != 36)
			Assert.Ignore("Issue #34594 reproduces only on Android API 36 / Android 16.");

		if (!IsGestureNavigationEnabled(androidApp))
			Assert.Ignore("Issue #34594 reproduces only with Android gesture navigation enabled.");

		App.WaitForElement("GotoPage2");
		App.Tap("GotoPage2");
		App.WaitForElement("Page2Label");
		App.WaitForElement("Page2CallbackStateLabel");

		// Read callback state while ON Page 2 — must be TRUE so MAUI intercepts back gesture
		var callbackStateOnPage2 = App.FindElement("Page2CallbackStateLabel").GetText();

		// Navigate back to root
		App.SwipeBackNavigation();

		App.WaitForElement("RootNavigationStateLabel");
		App.WaitForNoElement("Page2Label");

		// Tap "Refresh Status" so labels are read AFTER UpdatePredictiveBackRegistration() settles.
		// OnAppearing fires before platform callback update; tapping the button ensures correct state.
		App.WaitForElement("RefreshStatusButton");
		App.Tap("RefreshStatusButton");

		var navigationState = App.FindElement("RootNavigationStateLabel").GetText();
		var predictiveBackCallbackState = App.FindElement("PredictiveBackCallbackStateLabel").GetText();
		var canConsumeBackNavigationState = App.FindElement("CanConsumeBackNavigationStateLabel").GetText();
		var backHandlerDelegates = App.FindElement("BackHandlerDelegatesLabel").GetText();
		var predictiveBackEvaluation = App.FindElement("PredictiveBackEvaluationLabel").GetText();

		Assert.Multiple(() =>
		{
			Assert.That(navigationState, Is.EqualTo("NavigationStackDepth=1"));

			// Callback MUST have been enabled on Page 2 — proves MAUI intercepted back gesture correctly
			Assert.That(callbackStateOnPage2, Is.EqualTo("Page2CallbackState=True"),
				$"MAUI should have its OnBackPressedCallback ENABLED on a non-root page so it can handle in-app back navigation. {canConsumeBackNavigationState}; {backHandlerDelegates}; {predictiveBackEvaluation}");

			// Callback MUST be disabled at root — proves predictive back-to-home animation is unblocked
			Assert.That(predictiveBackCallbackState, Is.EqualTo("PredictiveBackCallbackRegistered=False"),
				$"MAUI should not keep the predictive back callback registered on the root page because that blocks Android's back-to-home gesture animation. {canConsumeBackNavigationState}; {backHandlerDelegates}; {predictiveBackEvaluation}");
		});
	}

	long GetDeviceApiLevel()
	{
		return (long?)((AppiumApp)App).Driver.Capabilities.GetCapability("deviceApiLevel")
			?? throw new InvalidOperationException("deviceApiLevel capability is missing or null.");
	}

	static bool IsGestureNavigationEnabled(AppiumAndroidApp androidApp)
	{
		var response = androidApp.CommandExecutor.Execute("checkIfGestureNavigationIsEnabled", new Dictionary<string, object>());
		return response?.Value is bool enabled && enabled;
	}
}
#endif
