using System;
using CoreGraphics;
using UIKit;

namespace Microsoft.Maui.Platform;

internal readonly record struct SafeAreaPadding(double Left, double Right, double Top, double Bottom)
{
	public static SafeAreaPadding Empty { get; } = new(0, 0, 0, 0);

	public bool IsEmpty { get; } = Left == 0 && Right == 0 && Top == 0 && Bottom == 0;
	public double HorizontalThickness { get; } = Left + Right;
	public double VerticalThickness { get; } = Top + Bottom;

	public CGRect InsetRect(CGRect bounds)
	{
		if (IsEmpty)
		{
			return bounds;
		}

		return new CGRect(
			bounds.Left + Left,
			bounds.Top + Top,
			bounds.Width - HorizontalThickness,
			bounds.Height - VerticalThickness);
	}

	public CGRect ToCGRect() =>
		new((nfloat)Top, (nfloat)Left, (nfloat)Bottom, (nfloat)Right);

	/// <summary>
	/// Compares two SafeAreaPadding values at device-pixel resolution.
	/// Sub-pixel differences (e.g., 0.001pt from animation noise) that map to the same
	/// physical pixel are treated as equal, preventing unnecessary layout invalidation cycles.
	/// </summary>
	public bool EqualsAtPixelLevel(SafeAreaPadding other)
	{
		var scale = (double)UIScreen.MainScreen.Scale;
		return RoundToPixel(Left, scale) == RoundToPixel(other.Left, scale)
			&& RoundToPixel(Right, scale) == RoundToPixel(other.Right, scale)
			&& RoundToPixel(Top, scale) == RoundToPixel(other.Top, scale)
			&& RoundToPixel(Bottom, scale) == RoundToPixel(other.Bottom, scale);
	}

	static double RoundToPixel(double value, double scale)
		=> Math.Round(value * scale, MidpointRounding.AwayFromZero);
}

internal static class SafeAreaInsetsExtensions
{
	public static SafeAreaPadding ToSafeAreaInsets(this UIEdgeInsets insets)
	{
		// Filters out negligible floating-point values from UIKit that may cause layout issues (e.g., 3.5527136788005009e-15).
		const double tolerance = 1e-14;

		static double ApplyTolerance(double value) => Math.Abs(value) < tolerance ? 0 : value;

		return new(
			ApplyTolerance(insets.Left),
			ApplyTolerance(insets.Right),
			ApplyTolerance(insets.Top),
			ApplyTolerance(insets.Bottom)
		);
	}

	// Bit flags for the blocked-edges bitmask: bit 0=Left, 1=Top, 2=Right, 3=Bottom.
	const int LeftBlockedBit = 1 << 0;
	const int TopBlockedBit = 1 << 1;
	const int RightBlockedBit = 1 << 2;
	const int BottomBlockedBit = 1 << 3;
	const int AllEdgesBlocked = LeftBlockedBit | TopBlockedBit | RightBlockedBit | BottomBlockedBit;

	public static bool IsEdgeBlocked(this int blockedEdges, int edge) => (blockedEdges & (1 << edge)) != 0;

	/// <summary>
	/// Returns a bitmask (bit 0=Left, 1=Top, 2=Right, 3=Bottom) of which edges are already
	/// handled by a parent <see cref="MauiView"/> or <see cref="MauiScrollView"/> with a real,
	/// non-zero resolved inset, performing a single ancestor walk only when
	/// <paramref name="blockedEdgesCacheValid"/> is false. A parent that only handles Top must
	/// not also suppress a descendant's independent Bottom inset, and vice versa (#34563).
	///
	/// The result is written into the caller-owned <paramref name="blockedEdgesCache"/> field and
	/// reused across layout passes via <paramref name="blockedEdgesCacheValid"/> until the
	/// caller invalidates it (e.g. on SafeAreaInsetsDidChange/InvalidateSafeArea/MovedToWindow),
	/// so this walk only runs once per invalidation cycle instead of on every layout pass.
	///
	/// If any blocking ancestor resolves an edge via <see cref="SafeAreaRegions.SoftInput"/>, the
	/// result is intentionally NOT cached: SoftInput-driven insets (keyboard show/hide) can change
	/// at runtime without raising any invalidation event on this descendant, so the cache would go
	/// stale. In that case the walk re-runs on every call instead of risking a stale blocked state.
	/// </summary>
	/// <param name="startingView">The view whose ancestors should be walked.</param>
	/// <param name="blockedEdgesCache">Caller-owned bitmask field to populate.</param>
	/// <param name="blockedEdgesCacheValid">
	/// Whether <paramref name="blockedEdgesCache"/> already holds a valid result. Set to true
	/// before returning, unless a SoftInput-driven ancestor edge was encountered.
	/// </param>
	internal static int ResolveParentBlockedEdges(this UIView startingView, ref int blockedEdgesCache, ref bool blockedEdgesCacheValid)
	{
		if (blockedEdgesCacheValid)
			return blockedEdgesCache;

		int blockedEdges = 0;
		bool hasSoftInputRegion = false;

		startingView.FindParent(x =>
		{
			// A blocking ancestor is either a MauiView or a MauiScrollView (MauiScrollView
			// derives from UIScrollView, not MauiView, so it can't be matched via a single pattern).
			bool responds;
			if (x is MauiView mv1)
			{
				responds = mv1.RespondsToSafeArea();
			}
			else if (x is MauiScrollView msv1)
			{
				responds = msv1.RespondsToSafeArea();
			}
			else
			{
				return false;
			}

			if (!responds)
			{
				return false;
			}

			for (int edge = 0; edge < 4; edge++)
			{
				int bit = 1 << edge;
				if ((blockedEdges & bit) != 0)
				{
					continue;
				}

				var (region, component) = x is MauiView mv2
					? (mv2.GetSafeAreaRegionForEdge(edge), mv2.GetSafeAreaComponentForEdge(edge))
					: (((MauiScrollView)x).GetSafeAreaRegionForEdge(edge), ((MauiScrollView)x).GetSafeAreaComponentForEdge(edge));

				if (region == SafeAreaRegions.SoftInput)
				{
					hasSoftInputRegion = true;
				}

				if (region != SafeAreaRegions.None && component != 0)
				{
					blockedEdges |= bit;
				}
			}

			// Stop walking once all 4 edges are resolved
			return blockedEdges == AllEdgesBlocked;
		});

		blockedEdgesCache = blockedEdges;
		blockedEdgesCacheValid = !hasSoftInputRegion;
		return blockedEdges;
	}
}