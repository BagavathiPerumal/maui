namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 34563, "[iOS] Vertical SafeAreaEdge isn't respected when Top and Bottom constraints mismatch", PlatformAffected.iOS)]
public class Issue34563 : ContentPage
{
	public Issue34563()
	{
		// Page-level SafeAreaEdges mismatch: the Top edge is None (content is allowed to
		// bleed behind the status bar), while the Bottom edge is Container (content should
		// avoid the home indicator / bottom safe area). This mirrors the originally reported
		// customer repro: SafeAreaEdges="None,None,None,Container" set at the page level.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.Container);

		// Red represents the "unsafe" area (status bar / home indicator). If the bug is
		// present, the green boxes below will overlap this red background.
		BackgroundColor = Colors.Red;

		// SafeAreaEdges is only supported on Layout/ContentView/ContentPage/ScrollView/Border,
		// so a ContentView is used as the "safe box" instead of a plain BoxView.
		var topSafeBox = new ContentView
		{
			AutomationId = "TopSafeBox",
			BackgroundColor = Colors.LimeGreen,
			HeightRequest = 60,
			VerticalOptions = LayoutOptions.Start,
			// This child opts into respecting ALL edges via Container, even though the
			// Page's own Top edge is None. It should never render under the status bar.
			SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container),
		};

		var bottomSafeBox = new ContentView
		{
			AutomationId = "BottomSafeBox",
			BackgroundColor = Colors.LimeGreen,
			HeightRequest = 60,
			VerticalOptions = LayoutOptions.End,
			SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container),
		};

		var instructions = new Label
		{
			AutomationId = "InstructionsLabel",
			Text = "TopSafeBox and BottomSafeBox (green) must never overlap the red background, which represents the OS safe areas (status bar / home indicator).",
			Margin = new Thickness(20),
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalTextAlignment = TextAlignment.Center,
			VerticalOptions = LayoutOptions.Center,
		};

		// Entry point to a second, code-review-driven scenario (see NestedAncestorMismatchPage
		// below): unlike the page-level repro above, this one puts the Top/Bottom mismatch on an
		// intermediate ancestor (not the Page itself), to explicitly cover "parent blocks Top
		// only, child still applies Bottom" (see ResolveParentBlockedEdges in MauiView.cs). It has
		// to live on its own page because this page's own Bottom=Container would otherwise block
		// the Bottom edge for every descendant, including any nested scenario placed here.
		var openNestedAncestorScenarioButton = new Button
		{
			AutomationId = "OpenNestedAncestorScenarioButton",
			Text = "Open Nested Ancestor Mismatch Scenario",
			VerticalOptions = LayoutOptions.End,
			Margin = new Thickness(0, 0, 0, 80),
			// PushModalAsync is used (rather than PushAsync) because this page is not hosted
			// inside a NavigationPage, and PushAsync requires one.
			Command = new Command(async () => await Navigation.PushModalAsync(new NestedAncestorMismatchPage())),
		};

		var grid = new Grid { AutomationId = "RootGrid" };
		grid.Add(instructions);
		grid.Add(topSafeBox);
		grid.Add(bottomSafeBox);
		grid.Add(openNestedAncestorScenarioButton);

		Content = grid;
	}

	// Additional scenario added during code review of the #34563 fix: exercises an intermediate
	// ancestor (not the Page) that only blocks ONE edge (Top), verifying a descendant still
	// independently applies its own inset for a DIFFERENT edge (Bottom) that no ancestor blocks.
	// Kept on a separate page because the Page in the scenario above sets Bottom=Container, which
	// would otherwise block the Bottom edge for every descendant of that page, including this one.
	public class NestedAncestorMismatchPage : ContentPage
	{
		public NestedAncestorMismatchPage()
		{
			// Page itself is edge-to-edge (no safe area handling at this level).
			SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
			BackgroundColor = Colors.Red;

			// MidContainer only handles the Top edge (Top=Container, Bottom=None) — a real,
			// non-zero inset — and is the direct parent of both TopSafeBox2 and BottomSafeBox2.
			var midContainer = new ContentView
			{
				AutomationId = "MidContainer",
				SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None),
			};

			var topSafeBox2 = new ContentView
			{
				AutomationId = "TopSafeBox2",
				BackgroundColor = Colors.LimeGreen,
				HeightRequest = 60,
				VerticalOptions = LayoutOptions.Start,
				// MidContainer already applies a real Top inset, so this Top edge should be
				// blocked (no double-padding) while still never rendering under the status bar.
				SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container),
			};

			var bottomSafeBox2 = new ContentView
			{
				AutomationId = "BottomSafeBox2",
				BackgroundColor = Colors.LimeGreen,
				HeightRequest = 60,
				VerticalOptions = LayoutOptions.End,
				// MidContainer (the direct parent) never blocks the Bottom edge — it only
				// handles Top — so this inset must be applied independently by this view itself.
				SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container),
			};

			var instructions = new Label
			{
				AutomationId = "NestedInstructionsLabel",
				Text = "TopSafeBox2 and BottomSafeBox2 (green) must never overlap the red background, even though MidContainer (their direct parent) only blocks the Top edge.",
				Margin = new Thickness(20),
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
				VerticalOptions = LayoutOptions.Center,
			};

			var innerGrid = new Grid { AutomationId = "InnerGrid" };
			innerGrid.Add(instructions);
			innerGrid.Add(topSafeBox2);
			innerGrid.Add(bottomSafeBox2);
			midContainer.Content = innerGrid;

			// Lets the UI test return to the original Issue34563 page (this page was opened via
			// PushModalAsync, so tests must explicitly pop it) so subsequent tests in the same
			// fixture instance find their expected elements on the original page.
			var closeButton = new Button
			{
				AutomationId = "CloseNestedAncestorScenarioButton",
				Text = "Close",
				VerticalOptions = LayoutOptions.End,
				Command = new Command(async () => await Navigation.PopModalAsync()),
			};

			var grid = new Grid { AutomationId = "NestedRootGrid" };
			grid.Add(midContainer);
			grid.Add(closeButton);

			Content = grid;
		}
	}
}
