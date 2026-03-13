#if ANDROID
using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenQA.Selenium.Appium.Interactions;
using OpenQA.Selenium.Interactions;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

[Category(UITestCategories.RefreshView)]
public class Issue33510 : _IssuesUITest
{
	const string StatusLabel = "StatusLabel";
	const string ScrollWebViewButton = "ScrollWebViewButton";
	const string ReadScrollTopButton = "ReadScrollTopButton";
	const string ScrollTopLabel = "ScrollTopLabel";
	const string TestWebViewContainer = "TestWebViewContainer";

	public Issue33510(TestDevice device) : base(device)
	{
	}

	public override string Issue => "[Android] RefreshView triggers pull-to-refresh immediately when scrolling up inside a WebView";

	protected override bool ResetAfterEachTest => true;

	[Test]
	public void PullToRefreshWaitsUntilInternalWebViewContainerReachesTop()
	{
		if (App is not AppiumAndroidApp androidApp)
		{
			Assert.Ignore("Issue #33510 is Android-specific.");
			return;
		}

		Assert.That(
			App.WaitForTextToBePresentInElement(StatusLabel, "WebView ready", timeout: TimeSpan.FromSeconds(30)),
			Is.True,
			"WebView never finished loading.");

		App.WaitForElement(ScrollWebViewButton);
		App.Tap(ScrollWebViewButton);

		var initialScrollTop = GetScrollTop();
		Assert.That(initialScrollTop, Is.GreaterThan(200), "The inner HTML container must start away from the top.");

		Assert.That(GetStatus(), Does.Not.Contain("Refresh triggered"));

		var webViewRect = App.WaitForElement(TestWebViewContainer).GetRect();
		var x = webViewRect.CenterX();
		var fromY = webViewRect.Y + (webViewRect.Height * 35 / 100);
		var toY = webViewRect.Y + (webViewRect.Height * 70 / 100);

		DragInsideWebView(androidApp, x, fromY, x, toY);

		var scrollTopAfterGesture = GetScrollTop();
		Assert.That(scrollTopAfterGesture, Is.LessThan(initialScrollTop), "Dragging down inside the WebView should scroll the inner container upward.");
		Assert.That(scrollTopAfterGesture, Is.GreaterThan(0), "The inner container should still be away from the top after a partial upward scroll.");
		Assert.That(GetStatus(), Does.Not.Contain("Refresh triggered"));
	}

	double GetScrollTop()
	{
		App.Tap(ReadScrollTopButton);
		Assert.That(
			App.WaitForTextToBePresentInElement(ScrollTopLabel, "ScrollTop:", timeout: TimeSpan.FromSeconds(5)),
			Is.True,
			"Scroll position was not reported.");

		var status = App.FindElement(ScrollTopLabel).GetText() ?? string.Empty;
		var match = Regex.Match(status, @"(\d+(\.\d+)?)");

		Assert.That(match.Success, Is.True, $"Could not parse scroll position from '{status}'.");

		return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
	}

	string GetStatus() =>
		App.FindElement(StatusLabel).GetText() ?? string.Empty;

	static void DragInsideWebView(AppiumAndroidApp androidApp, int fromX, int fromY, int toX, int toY)
	{
		var touchDevice = new OpenQA.Selenium.Appium.Interactions.PointerInputDevice(PointerKind.Touch);
		var dragSequence = new ActionSequence(touchDevice, 0);

		dragSequence.AddAction(touchDevice.CreatePointerMove(CoordinateOrigin.Viewport, fromX, fromY, TimeSpan.Zero));
		dragSequence.AddAction(touchDevice.CreatePointerDown(PointerButton.TouchContact));
		dragSequence.AddAction(touchDevice.CreatePointerMove(CoordinateOrigin.Viewport, toX, toY, TimeSpan.FromMilliseconds(450)));
		dragSequence.AddAction(touchDevice.CreatePointerUp(PointerButton.TouchContact));

		androidApp.Driver.PerformActions([dragSequence]);
	}
}
#endif
