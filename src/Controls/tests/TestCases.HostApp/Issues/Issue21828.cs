namespace Maui.Controls.Sample.Issues;

[XamlCompilation(XamlCompilationOptions.Compile)]
[Issue(IssueTracker.Github, 21828, "Flyout Button Disappears", PlatformAffected.iOS)]
public class Issue21828 : FlyoutPage
{
    readonly _21828StartPage _runBugPage;
    readonly _21828EndPage _endPage;
    readonly NavigationPage _navigationPage;

    public Issue21828()
    {
        _endPage = new _21828EndPage();
        _runBugPage = new _21828StartPage(BreakFlyoutButton);
        _navigationPage = new NavigationPage(_runBugPage);

        var flyoutContentPage = new ContentPage
        {
            Title = "Flyout Page",
            IconImageSource = "menu_icon.png",
            Content = new Label
            {
                Text = "This is the flyout page",
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            }
        };

        Flyout = flyoutContentPage;
        Detail = _navigationPage;

        Title = "FlyoutBug";
        FlyoutLayoutBehavior = FlyoutLayoutBehavior.Default;
    }

    async Task BreakFlyoutButton()
    {
        _navigationPage.Navigation.InsertPageBefore(_endPage, _runBugPage);
        await _navigationPage.Navigation.PopAsync();
    }
}

public class _21828StartPage : ContentPage
{
    readonly Func<Task> _function;

    public _21828StartPage(Func<Task> function)
    {
        _function = function;

        Title = "StartPage";

        Content = new StackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "Click the button below to demonstrate the bug-StartPage"
                },
                new Button
                {
					AutomationId = "StartPageButton",
                    Text = "Demo Bug",
                    Command = new Command(async () => await _function())
                }
            }
        };
    }
}

public class _21828EndPage : ContentPage
{
    public _21828EndPage()
    {
        Title = "EndPage";

        Content = new StackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "EndPage"
                }
            }
        };
    }
}
