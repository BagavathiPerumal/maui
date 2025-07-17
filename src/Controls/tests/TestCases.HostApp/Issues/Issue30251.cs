namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 30251, "[iOS] RotateTo visually rotates through the layers that are on top", PlatformAffected.iOS)]
public class Issue30251 : ContentPage
{
	Image _bottomImage;
	Image _topImage;

	public Issue30251()
	{
		Title = "Rotate Image Demo";

		_bottomImage = new Image
		{
			Source = "groceries.png",
			WidthRequest = 400,
			AutomationId = "BottomImage"
		};

		_topImage = new Image
		{
			Source = "royals.png",
			WidthRequest = 400,
			AutomationId = "TopImage"
		};

		var absoluteLayout = new AbsoluteLayout
		{
			BackgroundColor = Colors.Blue,
			WidthRequest = 400,
			HeightRequest = 400
		};

		AbsoluteLayout.SetLayoutBounds(_bottomImage, new Rect(0, 0, 400, 400));
		AbsoluteLayout.SetLayoutBounds(_topImage, new Rect(0, 0, 400, 400));

		absoluteLayout.Children.Add(_bottomImage);
		absoluteLayout.Children.Add(_topImage);

		var rotateButton = new Button
		{
			Text = "Rotate",
			AutomationId = "RotateButton"
		};
		rotateButton.Clicked += RotateButton_Clicked;

		var stack = new VerticalStackLayout
		{
			Children = {
				absoluteLayout,
				rotateButton
			}
		};

		Content = new ScrollView
		{
			Content = stack
		};
	}

	async void RotateButton_Clicked(object sender, EventArgs e)
	{
		await _bottomImage.RotateYTo(-70, 250);
	}
}