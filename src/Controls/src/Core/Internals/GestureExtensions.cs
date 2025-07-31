using System;
using System.ComponentModel;

namespace Microsoft.Maui.Controls.Internals
{
	/// <summary>
	/// Extension methods for gesture-related operations
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal static class GestureExtensions
	{
		/// <summary>
		/// Normalizes a rotation angle to be within the range [0, 360)
		/// </summary>
		/// <param name="rotation">The rotation angle in degrees</param>
		/// <returns>The normalized rotation angle in the range [0, 360)</returns>
		internal static double NormalizeRotation(this double rotation)
		{
			return ((rotation % 360) + 360) % 360;
		}

		/// <summary>
		/// Validates that floating-point coordinates are finite (not NaN or Infinity)
		/// </summary>
		/// <param name="coordinates">Tuple containing x and y coordinates</param>
		/// <returns>True if both coordinates are finite, false otherwise</returns>
		internal static bool AreCoordinatesValid(this (float x, float y) coordinates)
		{
			return IsFiniteFloat(coordinates.x) && IsFiniteFloat(coordinates.y);
		}

		/// <summary>
		/// Validates that floating-point coordinates are finite (not NaN or Infinity)
		/// </summary>
		/// <param name="x">X coordinate</param>
		/// <param name="y">Y coordinate</param>
		/// <returns>True if both coordinates are finite, false otherwise</returns>
		internal static bool AreCoordinatesValid(float x, float y)
		{
			return IsFiniteFloat(x) && IsFiniteFloat(y);
		}

		/// <summary>
		/// Validates that a rotation value is finite (not NaN or Infinity)
		/// </summary>
		/// <param name="rotation">The rotation angle</param>
		/// <returns>True if the rotation is finite, false otherwise</returns>
		internal static bool IsRotationValid(this double rotation)
		{
			return IsFiniteDouble(rotation);
		}

		/// <summary>
		/// Helper method to check if a float is finite (compatible with .NET Standard 2.0/2.1)
		/// </summary>
		static bool IsFiniteFloat(float value)
		{
			return !float.IsNaN(value) && !float.IsInfinity(value);
		}

		/// <summary>
		/// Helper method to check if a double is finite (compatible with .NET Standard 2.0/2.1)
		/// </summary>
		static bool IsFiniteDouble(double value)
		{
			return !double.IsNaN(value) && !double.IsInfinity(value);
		}
	}
}
