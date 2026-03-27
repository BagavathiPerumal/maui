#if ANDROID
namespace Microsoft.Maui
{
	interface IBackNavigationState
	{
		bool CanConsumeBackNavigation { get; }
	}
}
#endif
