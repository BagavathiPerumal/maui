namespace Maui.Controls.Sample.Issues;

// Dedicated ScrollView (MauiScrollView) coverage for #34563. MauiScrollView.cs does NOT inherit
// from MauiView and maintains its own, separate copy of the parent/ancestor safe-area-blocking
// logic, so a fix applied only to MauiView.cs does not automatically apply to ScrollView. This
// page exercises every scenario needed to confirm MauiScrollView's own per-edge arbitration
// behaves correctly, independent of the plain-MauiView scenarios already covered by Issue34563.cs.
[Issue(IssueTracker.Github, 34563, "[iOS] Vertical SafeAreaEdge isn't respected when Top and Bottom constraints mismatch (ScrollView coverage)", PlatformAffected.iOS, issueTestNumber: 2)]
public class Issue34563_ScrollView : ContentPage
{
	public Issue34563_ScrollView()
	{
		Title = "Issue34563 - ScrollView SafeArea";

		var openTopMismatchButton = new Button
		{
			AutomationId = "OpenScrollViewTopMismatchButton",
			Text = "A: Page blocks Bottom only, ScrollView applies Top",
			Command = new Command(async () => await Navigation.PushModalAsync(new ScrollViewTopMismatchPage())),
		};

		var openLeftRightMismatchButton = new Button
		{
			AutomationId = "OpenScrollViewLeftRightMismatchButton",
			Text = "G: Page blocks Right only, ScrollView applies Left",
			Command = new Command(async () => await Navigation.PushModalAsync(new ScrollViewLeftRightMismatchPage())),
		};

		var stack = new VerticalStackLayout
		{
			AutomationId = "ScrollViewScenarioMenu",
			Spacing = 12,
			Padding = new Thickness(20),
			Children =
			{
				openTopMismatchButton,
				openLeftRightMismatchButton,
			}
		};

		Content = stack;
	}

	// Scenario A: Page has a Top/Bottom mismatch (Top=None, Bottom=Container — a real, non-zero
	// inset ONLY on Bottom). A ScrollView is the Page's *direct* Content (no intermediate
	// Layout), isolating MauiScrollView's own ancestor walk from any MauiView-side masking.
	// The ScrollView opts into Container on Top ONLY (mixed edges), which forces
	// MauiScrollView's manual, per-edge inset path (GetInset()) rather than iOS's automatic
	// ContentInsetAdjustmentBehavior, so the assertion actually exercises the per-edge fix.
	// Since the Page never has a real inset on Top, the ScrollView's Top edge must NOT be
	// blocked and must apply its own inset.
	public class ScrollViewTopMismatchPage : ContentPage
	{
		public ScrollViewTopMismatchPage()
		{
			SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.Container);
			BackgroundColor = Colors.Red;

			var topContent = new ContentView
			{
				AutomationId = "TopMismatchScrollViewContent",
				BackgroundColor = Colors.LimeGreen,
				HeightRequest = 60,
				VerticalOptions = LayoutOptions.Start,
			};

			var closeButton = new Button
			{
				AutomationId = "CloseTopMismatchScenarioButton",
				Text = "Close",
				VerticalOptions = LayoutOptions.End,
				Command = new Command(async () => await Navigation.PopModalAsync()),
			};

			var innerStack = new VerticalStackLayout();
			innerStack.Add(topContent);
			innerStack.Add(closeButton);

			Content = new ScrollView
			{
				AutomationId = "TopMismatchScrollView",
				SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None),
				Content = innerStack,
			};
		}
	}

	// Scenario G: horizontal-edge counterpart to Scenario A. The whole-view
	// IsParentHandlingSafeArea() bug applies identically to all 4 edges (Left/Top/Right/Bottom),
	// but the original coverage only ever exercised Top/Bottom. Page has a Left/Right mismatch
	// (Right=Container — a real, non-zero inset ONLY on Right; Left=None). The ScrollView is the
	// Page's direct Content and opts into Container on Left ONLY (mixed edges forces the manual
	// GetInset() path). Since the Page never has a real inset on Left, the ScrollView's Left edge
	// must NOT be blocked and must apply its own inset. Uses Scenario A's direct-measurement
	// methodology (no scrolling required).
	public class ScrollViewLeftRightMismatchPage : ContentPage
	{
		public ScrollViewLeftRightMismatchPage()
		{
			SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None);
			BackgroundColor = Colors.Red;

			var leftContent = new ContentView
			{
				AutomationId = "LeftRightMismatchScrollViewContent",
				BackgroundColor = Colors.LimeGreen,
				WidthRequest = 60,
				HeightRequest = 60,
				HorizontalOptions = LayoutOptions.Start,
			};

			var closeButton = new Button
			{
				AutomationId = "CloseLeftRightMismatchScenarioButton",
				Text = "Close",
				VerticalOptions = LayoutOptions.End,
				Command = new Command(async () => await Navigation.PopModalAsync()),
			};

			var innerStack = new VerticalStackLayout();
			innerStack.Add(leftContent);
			innerStack.Add(closeButton);

			Content = new ScrollView
			{
				AutomationId = "LeftRightMismatchScrollView",
				SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None),
				Content = innerStack,
			};
		}
	}
}
