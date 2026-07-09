namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 34563, "[iOS] Vertical SafeAreaEdge isn't respected when Top and Bottom constraints mismatch", PlatformAffected.iOS)]
public class Issue34563 : ContentPage
{
	public Issue34563()
	{
		// Page-level SafeAreaEdges mismatch: the Top edge is None (content is allowed to
		// bleed behind the status bar), while the Bottom edge is Container (content should
		// avoid the home indicator / bottom safe area). This mirrors the reported repro:
		// SafeAreaEdges="None,None,None,Container" set at the page level.
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

		var grid = new Grid { AutomationId = "RootGrid" };
		grid.Add(instructions);
		grid.Add(topSafeBox);
		grid.Add(bottomSafeBox);

		Content = grid;
	}
}
