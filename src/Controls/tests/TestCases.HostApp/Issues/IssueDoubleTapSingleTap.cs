using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Maui.Controls.Sample.Issues
{
	[Issue(IssueTracker.Github, 12345, "Single tap event handler triggered for double mouse click on Windows")]
	public class IssueDoubleTapSingleTap : TestContentPage
	{
		private readonly Label _statusLabel;
		private readonly GraphicsView _graphicsView;
		private readonly TestDrawable _drawable;

		public IssueDoubleTapSingleTap()
		{
			_statusLabel = new Label
			{
				AutomationId = "StatusLabel",
				Text = "Tap or double-tap the graphics view",
				FontSize = 16,
				HorizontalOptions = LayoutOptions.Center,
				Margin = new Thickness(10)
			};

			_drawable = new TestDrawable();
			_graphicsView = new GraphicsView
			{
				AutomationId = "TestGraphicsView",
				Drawable = _drawable,
				BackgroundColor = Colors.LightBlue,
				HeightRequest = 300,
				WidthRequest = 300,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center
			};

			// Add single tap gesture
			var singleTapGesture = new TapGestureRecognizer
			{
				NumberOfTapsRequired = 1
			};
			singleTapGesture.Tapped += OnSingleTap;
			_graphicsView.GestureRecognizers.Add(singleTapGesture);

			// Add double tap gesture
			var doubleTapGesture = new TapGestureRecognizer
			{
				NumberOfTapsRequired = 2
			};
			doubleTapGesture.Tapped += OnDoubleTap;
			_graphicsView.GestureRecognizers.Add(doubleTapGesture);

			Content = new StackLayout
			{
				Children = { _statusLabel, _graphicsView },
				Spacing = 20,
				Padding = new Thickness(20)
			};
		}

		protected override void Init()
		{
			// Implementation required by TestContentPage
		}

		private void OnSingleTap(object sender, TappedEventArgs e)
		{
			_statusLabel.Text = "Single tap detected";
			_drawable.AddCircle(e.GetPosition(_graphicsView) ?? Point.Zero, Colors.Red);
			_graphicsView.Invalidate();
		}

		private void OnDoubleTap(object sender, TappedEventArgs e)
		{
			_statusLabel.Text = "Double tap detected";
			_drawable.AddCircle(e.GetPosition(_graphicsView) ?? Point.Zero, Colors.Blue);
			_graphicsView.Invalidate();
		}

		private class TestDrawable : IDrawable
		{
			private readonly List<(Point Position, Color Color)> _circles = new();

			public void AddCircle(Point position, Color color)
			{
				_circles.Add((position, color));
			}

			public void Draw(ICanvas canvas, RectF dirtyRect)
			{
				canvas.FillColor = Colors.White;
				canvas.FillRectangle(dirtyRect);

				foreach (var (position, color) in _circles)
				{
					canvas.FillColor = color;
					canvas.FillCircle((float)position.X, (float)position.Y, 10);
				}
			}
		}
	}
}