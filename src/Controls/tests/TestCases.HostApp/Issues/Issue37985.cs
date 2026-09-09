namespace Maui.Controls.Sample.Issues
{
	[Issue(IssueTracker.Github, 37985, "Shell crashes when a hierarchical Tab is the last item in the TabBar overflow 'More' menu", PlatformAffected.UWP)]
	public class Issue37985 : Shell
	{
		public Issue37985()
		{
			var tabBar = new TabBar();

			// Add enough flat tabs so that the TabBar overflows into the Windows TopNav "More" menu.
			for (int i = 1; i <= 12; i++)
			{
				tabBar.Items.Add(CreateShellContent($"Page {i}", $"Page{i}"));
			}

			// The last tab is hierarchical (contains multiple ShellContent children).
			// This tab ends up in the overflow "More" menu, which previously crashed the app when tapped.
			var hierarchicalTab = new Tab
			{
				Title = "Hierarchical Tab",
				AutomationId = "Hierarchical Tab"
			};

			hierarchicalTab.Items.Add(CreateShellContent("Sub Page 1", "SubPage1"));
			hierarchicalTab.Items.Add(CreateShellContent("Sub Page 2", "SubPage2"));

			tabBar.Items.Add(hierarchicalTab);

			Items.Add(tabBar);
		}

		ShellContent CreateShellContent(string title, string route) => new()
		{
			Title = title,
			AutomationId = title,
			Route = route,
			ContentTemplate = new DataTemplate(() => new ContentPage
			{
				Content = new Label { Text = title, AutomationId = $"{route}Label" }
			})
		};
	}
}
