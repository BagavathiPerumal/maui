using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 16433, "Navigation bar background color bug when navigating in a specific manner", PlatformAffected.Android)]
public class Issue16433 : Shell
{
    public Issue16433()
    {
        Title = "Navigation Background Test";
        Shell.SetBackgroundColor(this, Colors.Blue);

        Routing.RegisterRoute(nameof(Issue16433Page1), typeof(Issue16433Page1));
        Routing.RegisterRoute(nameof(Issue16433Page2), typeof(Issue16433Page2));
        Routing.RegisterRoute(nameof(Issue16433ModalPage), typeof(Issue16433ModalPage));

        var flyoutItem = new FlyoutItem
        {
            Title = "Home and Settings tabs"
        };

        var homeContent = new ShellContent
        {
            Title = "Home",
            ContentTemplate = new DataTemplate(() => new Issue16433Page1()),
            Route = nameof(Issue16433Page1)
        };
        Shell.SetTabBarIsVisible(homeContent, true);

        var settingsContent = new ShellContent
        {
            Title = "Settings", 
            ContentTemplate = new DataTemplate(() => new Issue16433Page2()),
            Route = nameof(Issue16433Page2)
        };
        Shell.SetTabBarIsVisible(settingsContent, true);

        flyoutItem.Items.Add(homeContent);
        flyoutItem.Items.Add(settingsContent);
        Items.Add(flyoutItem);

        var modalContent = new ShellContent
        {
            Title = "Test Result",
            ContentTemplate = new DataTemplate(() => new Issue16433ModalPage()),
            Route = nameof(Issue16433ModalPage)
        };
        Shell.SetTabBarIsVisible(modalContent, false);
        Items.Add(modalContent);
    }
}

public class Issue16433MainPageViewModel : BaseViewModel
{
    public Command GoToCViaBCommand { get; set; }

    public Issue16433MainPageViewModel()
    {
        GoToCViaBCommand = new Command(GoToCViaB);
    }

    async void GoToCViaB()
    {
        await Shell.Current.GoToAsync($"//{nameof(Issue16433Page2)}/{nameof(Issue16433ModalPage)}");
    }
}

public class Issue16433SecondPageViewModel : BaseViewModel
{
    public Command GoToModalCommand { get; set; }

    public Issue16433SecondPageViewModel()
    {
        GoToModalCommand = new Command(GoToModal);
    }

    async void GoToModal()
    {
        await Shell.Current.GoToAsync(nameof(Issue16433ModalPage));
    }
}

public class Issue16433ModalPageViewModel : BaseViewModel
{
    public Command GoBackToBCommand { get; set; }

    public Issue16433ModalPageViewModel()
    {
        GoBackToBCommand = new Command(GoBackToB);
    }

    async void GoBackToB()
    {
        await Shell.Current.GoToAsync($"//{nameof(Issue16433Page2)}");
    }
}

public class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<TValue>(ref TValue backingField, TValue value, [CallerMemberName] string propertyName = null)
    {
        if (Comparer<TValue>.Default.Compare(backingField, value) == 0)
        {
            return false;
        }

        backingField = value;

        OnPropertyChanged(propertyName);

        return true;
    }
}

public class Issue16433Page1 : ContentPage
{
    public Issue16433Page1()
    {
        Title = "Main Page";
        BindingContext = new Issue16433MainPageViewModel();

        var layout = new VerticalStackLayout
        {
            Spacing = 25,
            Padding = new Thickness(30, 0),
            VerticalOptions = LayoutOptions.Center
        };

        var goToCViaBButton = new Button
        {
            Text = "Go to C via B",
            AutomationId = "GoToCViaBButton",
            Command = ((Issue16433MainPageViewModel)BindingContext).GoToCViaBCommand,
            HorizontalOptions = LayoutOptions.Center
        };

        layout.Children.Add(goToCViaBButton);

        Content = layout;
    }
}

public class Issue16433Page2 : ContentPage
{
    public Issue16433Page2()
    {
        Title = "Second Page";
        BindingContext = new Issue16433SecondPageViewModel();
        
        var layout = new VerticalStackLayout
        {
            Spacing = 25,
            Padding = new Thickness(30, 0),
            VerticalOptions = LayoutOptions.Center
        };

        var goToCDirectlyButton = new Button
        {
            Text = "Go to C directly",
            AutomationId = "GoToCDirectlyButton",
            Command = ((Issue16433SecondPageViewModel)BindingContext).GoToModalCommand,
            HorizontalOptions = LayoutOptions.Center
        };

        layout.Children.Add(goToCDirectlyButton);

        Content = layout;
    }
}

public class Issue16433ModalPage : ContentPage
{
    public Issue16433ModalPage()
    {
        Shell.SetPresentationMode(this, PresentationMode.Modal);
        Title = "Modal Page";
        BindingContext = new Issue16433ModalPageViewModel();
        
        var layout = new VerticalStackLayout
        {
            Spacing = 25,
            Padding = new Thickness(30, 0),
            VerticalOptions = LayoutOptions.Center
        };

        var goBackToBButton = new Button
        {
            Text = "Go back to B",
            AutomationId = "GoBackToBButton",
            Command = ((Issue16433ModalPageViewModel)BindingContext).GoBackToBCommand,
            HorizontalOptions = LayoutOptions.Center
        };

        layout.Children.Add(goBackToBButton);

        Content = layout;
    }
}
