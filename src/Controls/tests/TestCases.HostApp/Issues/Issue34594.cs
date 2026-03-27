using System.Reflection;
#if ANDROID
using Android.OS;
using AndroidX.AppCompat.App;
using Microsoft.Maui.Platform;
#endif

namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 34594, "OnBackInvokedCallbacks block back-to-home animation", PlatformAffected.Android)]
public class Issue34594 : TestShell
{
	protected override void Init()
	{
		Routing.RegisterRoute(nameof(Issue34594DetailsPage), typeof(Issue34594DetailsPage));

		AddContentPage(new Issue34594RootPage(), "Predictive Back");
	}

	class Issue34594RootPage : ContentPage
	{
		readonly Label _navigationStateLabel;
		readonly Label _predictiveBackCallbackStateLabel;
		// Captures callback Enabled state just after GoToAsync pushes Page 2,
		// so the test can verify the callback becomes TRUE on a non-root page.
		readonly Label _callbackStateOnPage2Label;
		readonly Label _canConsumeBackNavigationStateLabel;
		readonly Label _backHandlerDelegatesLabel;
		readonly Label _predictiveBackEvaluationLabel;
		readonly Label _androidApiLevelLabel;

		public Issue34594RootPage()
		{
			Title = "Predictive Back";

			_navigationStateLabel = new Label
			{
				AutomationId = "RootNavigationStateLabel"
			};

			_predictiveBackCallbackStateLabel = new Label
			{
				AutomationId = "PredictiveBackCallbackStateLabel"
			};

			_callbackStateOnPage2Label = new Label
			{
				AutomationId = "CallbackStateOnPage2Label",
				Text = "CallbackOnPage2=NotNavigatedYet"
			};

			_canConsumeBackNavigationStateLabel = new Label
			{
				AutomationId = "CanConsumeBackNavigationStateLabel"
			};

			_backHandlerDelegatesLabel = new Label
			{
				AutomationId = "BackHandlerDelegatesLabel"
			};

			_predictiveBackEvaluationLabel = new Label
			{
				AutomationId = "PredictiveBackEvaluationLabel"
			};

			_androidApiLevelLabel = new Label
			{
				AutomationId = "AndroidApiLevelLabel"
			};

			Content = new ScrollView
			{
				Content = new VerticalStackLayout
				{
					Padding = 24,
					Spacing = 12,
					Children =
					{
						new Label
						{
							Text = "Issue #34594",
							AutomationId = "Issue34594TitleLabel",
							FontAttributes = FontAttributes.Bold,
							FontSize = 18
						},
						new Label
						{
							Text = "Use the Android back gesture from the second page. When the root page is shown again, MAUI should no longer keep its predictive back callback registered because that blocks the system back-to-home animation.",
							AutomationId = "Issue34594DescriptionLabel"
						},
						new Button
						{
							Text = "Go to Second Page",
							AutomationId = "GotoPage2",
							Command = new Command(async () => await Shell.Current.GoToAsync(nameof(Issue34594DetailsPage), false))
						},
						new Button
						{
							Text = "Refresh Status",
							AutomationId = "RefreshStatusButton",
							// Tapped by test AFTER returning to root so state is read after
							// UpdatePredictiveBackRegistration() has already settled.
							Command = new Command(UpdateStatus)
						},
						_navigationStateLabel,
						_predictiveBackCallbackStateLabel,
						_callbackStateOnPage2Label,
						_canConsumeBackNavigationStateLabel,
						_backHandlerDelegatesLabel,
						_predictiveBackEvaluationLabel,
						_androidApiLevelLabel
					}
				}
			};
		}

		protected override void OnAppearing()
		{
			base.OnAppearing();
			// Update only the navigation-stack label here (timing-safe at OnAppearing).
			// Callback-state labels are refreshed via the explicit RefreshStatusButton so the
			// test reads them AFTER UpdatePredictiveBackRegistration() has been called by the
			// platform (which happens after OnAppearing in Page.SendAppearing).
			_navigationStateLabel.Text = $"NavigationStackDepth={Shell.Current?.Navigation?.NavigationStack?.Count ?? 1}";
			_androidApiLevelLabel.Text = $"AndroidApiLevel={GetAndroidApiLevel()}";
		}

		void UpdateStatus()
		{
			_navigationStateLabel.Text = $"NavigationStackDepth={Shell.Current?.Navigation?.NavigationStack?.Count ?? 1}";
			_predictiveBackCallbackStateLabel.Text = $"PredictiveBackCallbackRegistered={IsPredictiveBackCallbackRegistered()}";
			_canConsumeBackNavigationStateLabel.Text = $"CanConsumeBackNavigation={CanConsumeBackNavigation()}";
			_backHandlerDelegatesLabel.Text = $"BackHandlerDelegates={GetBackHandlerDelegates()}";
			_predictiveBackEvaluationLabel.Text = GetPredictiveBackEvaluationState();
			_androidApiLevelLabel.Text = $"AndroidApiLevel={GetAndroidApiLevel()}";
		}

		static int GetAndroidApiLevel()
		{
#if ANDROID
			return (int)Build.VERSION.SdkInt;
#else
			return -1;
#endif
		}

		static bool IsPredictiveBackCallbackRegistered()
		{
#if ANDROID
			if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not AppCompatActivity activity)
				return false;

			// Check _mauiOnBackPressedCallback.Enabled — the AndroidX OnBackPressedCallback approach.
			// When Enabled=false the system shows the back-to-home animation; when true MAUI handles it.
			var callbackField = typeof(Microsoft.Maui.MauiAppCompatActivity)
				.GetField("_mauiOnBackPressedCallback", BindingFlags.Instance | BindingFlags.NonPublic);

			var callback = callbackField?.GetValue(activity);
			if (callback is null)
				return false;

			var enabledProperty = callback.GetType().GetProperty("Enabled",
				BindingFlags.Instance | BindingFlags.Public);

			return enabledProperty?.GetValue(callback) is true;
#else
			return false;
#endif
		}

		static bool CanConsumeBackNavigation()
		{
#if ANDROID
			if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not AppCompatActivity activity)
				return false;

			var mauiWindow = activity.GetWindow();
			var backNavigationStateType = Type.GetType("Microsoft.Maui.IBackNavigationState, Microsoft.Maui");
			var canConsumeBackNavigationProperty = backNavigationStateType?.GetProperty("CanConsumeBackNavigation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

			return backNavigationStateType?.IsInstanceOfType(mauiWindow) == true &&
				canConsumeBackNavigationProperty?.GetValue(mauiWindow) is true;
#else
			return false;
#endif
		}

		static string GetBackHandlerDelegates()
		{
#if ANDROID
			var lifecycleService = IPlatformApplication.Current?.Services?.GetService(typeof(Microsoft.Maui.LifecycleEvents.ILifecycleEventService)) as Microsoft.Maui.LifecycleEvents.ILifecycleEventService;
			if (lifecycleService is null)
				return "None";

			var handlers = lifecycleService
				.GetEventDelegates<Microsoft.Maui.LifecycleEvents.AndroidLifecycle.OnBackPressed>(nameof(Microsoft.Maui.LifecycleEvents.AndroidLifecycle.OnBackPressed))
				.Select(del => $"{del.Method.DeclaringType?.FullName}.{del.Method.Name}");

			return string.Join("|", handlers);
#else
			return "None";
#endif
		}

		static string GetPredictiveBackEvaluationState()
		{
#if ANDROID
			if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not AppCompatActivity activity)
				return "PredictiveBackEvaluation=Unavailable";

			var activityType = typeof(Microsoft.Maui.MauiAppCompatActivity);
			var updateCountField = activityType.GetField("_predictiveBackRegistrationUpdateCount", BindingFlags.Instance | BindingFlags.NonPublic);
			var lastShouldRegisterField = activityType.GetField("_lastPredictiveBackShouldRegister", BindingFlags.Instance | BindingFlags.NonPublic);
			var handlerAnalysisField = activityType.GetField("_lastPredictiveBackHandlerAnalysis", BindingFlags.Instance | BindingFlags.NonPublic);

			var updateCount = updateCountField?.GetValue(activity);
			var lastShouldRegister = lastShouldRegisterField?.GetValue(activity);
			var handlerAnalysis = handlerAnalysisField?.GetValue(activity);

			return $"PredictiveBackEvaluation=Count:{updateCount ?? "null"};ShouldRegister:{lastShouldRegister ?? "null"};Analysis:{handlerAnalysis ?? "null"}";
#else
			return "PredictiveBackEvaluation=Unavailable";
#endif
		}
	}

	class Issue34594DetailsPage : ContentPage
	{
		readonly Label _callbackStateLabel;

		public Issue34594DetailsPage()
		{
			Title = "Second Page";

			_callbackStateLabel = new Label
			{
				AutomationId = "Page2CallbackStateLabel",
				Text = "Page2CallbackState=NotLoaded"
			};

			Content = new VerticalStackLayout
			{
				Padding = 24,
				Spacing = 12,
				Children =
				{
					new Label
					{
						Text = "Second Page",
						AutomationId = "Page2Label",
						FontAttributes = FontAttributes.Bold,
						FontSize = 18
					},
					new Label
					{
						Text = "Navigate back with the Android back gesture to return to the root page.",
						AutomationId = "Page2InstructionsLabel"
					},
					// Shows OnBackPressedCallback.Enabled while on Page 2 — must be TRUE
					_callbackStateLabel
				}
			};
		}

		protected override void OnAppearing()
		{
			base.OnAppearing();
			// OnAppearing fires BEFORE Shell.SendNavigated (and before _navStack is updated).
			// Subscribe to Shell.Navigated so we capture callback state AFTER navigation settles
			// and UpdatePredictiveBackRegistration() has been called with the correct stack depth.
			void OnNavigated(object s, ShellNavigatedEventArgs e)
			{
				Shell.Current.Navigated -= OnNavigated;
				_callbackStateLabel.Text = $"Page2CallbackState={IsPredictiveBackCallbackRegisteredAndEnabled()}";
			}
			Shell.Current.Navigated += OnNavigated;
		}

		static bool IsPredictiveBackCallbackRegisteredAndEnabled()
		{
#if ANDROID
			if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not AndroidX.AppCompat.App.AppCompatActivity activity)
				return false;

			var callbackField = typeof(Microsoft.Maui.MauiAppCompatActivity)
				.GetField("_mauiOnBackPressedCallback", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

			var callback = callbackField?.GetValue(activity);
			if (callback is null)
				return false;

			var enabledProperty = callback.GetType().GetProperty("Enabled",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

			return enabledProperty?.GetValue(callback) is true;
#else
			return false;
#endif
		}
	}
}
