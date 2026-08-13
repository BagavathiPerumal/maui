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

	/// <summary>
	/// Returns whether <paramref name="edge"/> (0=Left, 1=Top, 2=Right, 3=Bottom) is set in the
	/// packed bitmask produced by <see cref="ResolveParentBlockedEdges"/>.
	/// </summary>
	public static bool IsEdgeBlocked(this int blockedEdges, int edge) => (blockedEdges & (1 << edge)) != 0;

	/// <summary>
	/// Returns which edges (0=Left, 1=Top, 2=Right, 3=Bottom) are already handled by a parent
	/// <see cref="MauiView"/> or <see cref="MauiScrollView"/> with a real, non-zero resolved
	/// inset, performing a single ancestor walk only when <paramref name="blockedEdgesCacheValid"/>
	/// is false. Shared by <see cref="MauiView"/> and <see cref="MauiScrollView"/> so both get
	/// identical per-edge (rather than all-or-nothing) parent-blocking behavior — a parent that
	/// only handles Top must not also suppress a descendant's independent Bottom inset, and vice
	/// versa (#34563). A <see cref="MauiScrollView"/> ancestor is recognized too, so a ScrollView
	/// that already applies its own safe area inset for an edge blocks a descendant from
	/// re-applying that same edge.
	///
	/// The result is a packed bitmask (bit N set = edge N is blocked) written into the
	/// caller-owned <paramref name="blockedEdgesCache"/> field, avoiding the per-instance
	/// <c>bool[4]</c> heap allocation this used to require (notably for recycled views such as
	/// <c>CollectionView</c> cells). It's reused across layout passes via
	/// <paramref name="blockedEdgesCacheValid"/> until the caller invalidates it (e.g. on
	/// SafeAreaInsetsDidChange/InvalidateSafeArea/MovedToWindow), so this walk only runs once per
	/// invalidation cycle instead of on every layout pass — EXCEPT when a blocking ancestor's edge
	/// uses <see cref="SafeAreaRegions.SoftInput"/>: that region's resolved inset can change at
	/// runtime (keyboard show/hide) without firing any of those invalidation events, so in that
	/// case the cache is deliberately left invalid and this walk re-runs on every call.
	/// </summary>
	/// <param name="startingView">The view whose ancestors should be walked.</param>
	/// <param name="blockedEdgesCache">A caller-owned bitmask field to populate.</param>
	/// <param name="blockedEdgesCacheValid">
	/// Whether <paramref name="blockedEdgesCache"/> already holds a valid result. Set to true
	/// before returning, unless a SoftInput-blocking ancestor was found.
	/// </param>
	internal static int ResolveParentBlockedEdges(this UIView startingView, ref int blockedEdgesCache, ref bool blockedEdgesCacheValid)
	{
		if (blockedEdgesCacheValid)
		{
			return blockedEdgesCache;
		}

		int blockedEdges = 0;
		int resolvedCount = 0;
		bool hasSoftInputRegion = false;

		startingView.FindParent(x =>
		{
			bool responds;

			if (x is MauiView outerMv)
			{
				responds = outerMv.RespondsToSafeArea();
			}
			else if (x is MauiScrollView outerMsv)
			{
				responds = outerMsv.RespondsToSafeArea();
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

				SafeAreaRegions region;
				double component;

				if (x is MauiView mv)
				{
					region = mv.GetSafeAreaRegionForEdge(edge);
					component = mv.GetSafeAreaComponentForEdge(edge);
				}
				else
				{
					var msv = (MauiScrollView)x;
					region = msv.GetSafeAreaRegionForEdge(edge);
					component = msv.GetSafeAreaComponentForEdge(edge);
				}

				if (region != SafeAreaRegions.None && component != 0)
				{
					blockedEdges |= bit;
					resolvedCount++;

					if (SafeAreaEdges.IsSoftInput(region))
					{
						hasSoftInputRegion = true;
					}
				}
			}

			// Stop walking once all 4 edges are resolved
			return resolvedCount == 4;
		});

		blockedEdgesCache = blockedEdges;

		// SoftInput can change (keyboard show/hide) without firing any of the invalidation
		// events that normally invalidate this cache, so never mark it valid in that case —
		// forcing a fresh ancestor walk on every subsequent call for these descendants.
		blockedEdgesCacheValid = !hasSoftInputRegion;

		return blockedEdges;
	}
}