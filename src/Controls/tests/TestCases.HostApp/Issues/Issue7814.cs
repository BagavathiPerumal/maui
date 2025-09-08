using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using Microsoft.Maui.Layouts;

namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 7814, "Vertical scrolling not working for CarouselView and CustomLayouts", PlatformAffected.Android)]
public class Issue7814 : ContentPage
{
    public Issue7814()
    {
        BindingContext = new Issue7814ViewModel();
        CreateUI();
    }

    void CreateUI()
    {
        Content = new ScrollView
        {
            Padding = new Thickness(15, 15, 15, 50),
            Orientation = ScrollOrientation.Vertical,
            AutomationId = "VerticalScrollView",
            Content = new StackLayout
            {
                Padding = new Thickness(15, 0),
                Spacing = 20,
                Children =
                {
                    new StackLayout
                    {
                        Children =
                        {
                            new CarouselView
                            {
                                HeightRequest = 700,
                                BackgroundColor = Colors.LimeGreen,
                                AutomationId = "TestCarouselView",
                                ItemsSource = ((Issue7814ViewModel)BindingContext).CarouselItems,
                                ItemTemplate = new DataTemplate(() =>
                                {
                                    var border = new Border();
                                    border.SetBinding(Border.BackgroundColorProperty, "BackColor");

                                    var label = new Label
                                    {
                                        HeightRequest = 50,
                                        HorizontalTextAlignment = TextAlignment.Center,
                                        VerticalOptions = LayoutOptions.CenterAndExpand,
                                        VerticalTextAlignment = TextAlignment.Center,
                                        TextColor = Colors.White,
                                        FontAttributes = FontAttributes.Bold
                                    };
                                    label.SetBinding(Label.TextProperty, "Text");

                                    border.Content = label;
                                    return border;
                                })
                            }
                        }
                    },
                    
                    new ScrollView
                    {
                        Orientation = ScrollOrientation.Horizontal,
                        AutomationId = "HorizontalScrollView",
                        Content = new StackLayout
                        {
                            Children =
                            {
                                new Label
                                {
                                    Text = "Horizontal Scroll Section - Test Here",
                                    FontSize = 14,
                                    Margin = new Thickness(0, 0, 0, 10)
                                },
                                new FlexLayout
                                {
                                    Direction = FlexDirection.Row,
                                    Children =
                                    {
                                        CreateHorizontalItem("Item 1", Colors.Red),
                                        CreateHorizontalItem("Item 2", Colors.Green),
                                        CreateHorizontalItem("Item 3", Colors.Blue),
                                        CreateHorizontalItem("Item 4", Colors.Orange)
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    Border CreateHorizontalItem(string text, Color backgroundColor)
    {
        return new Border
        {
            Margin = new Thickness(10),
            WidthRequest = 160,
            HeightRequest = 180,
            BackgroundColor = backgroundColor,
            Content = new Label
            {
                Text = text,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold
            }
        };
    }
}

public class Issue7814ViewModel
{
    public IList<CarouselItemData> CarouselItems { get; set; }

    public Issue7814ViewModel()
    {
        CarouselItems = new List<CarouselItemData>
        {
            new CarouselItemData { Text = "Carousel Item 1", BackColor = Colors.DarkRed },
            new CarouselItemData { Text = "Carousel Item 2", BackColor = Colors.DarkGreen },
            new CarouselItemData { Text = "Carousel Item 3", BackColor = Colors.DarkBlue },
            new CarouselItemData { Text = "Carousel Item 4", BackColor = Colors.DarkOrange },
        };
    }
}

public class CarouselItemData
{
    public string Text { get; set; }
    public Color BackColor { get; set; }
}
