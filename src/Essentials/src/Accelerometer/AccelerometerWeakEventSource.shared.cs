#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Maui.Devices.Sensors
{
	// Weakly references each subscriber's target (not the delegate itself) so a subscriber
	// that is never removed with "-=" can still be collected, while a still-alive subscriber
	// keeps receiving events. Mirrors the pattern used by Microsoft.Maui.WeakEventManager.
	sealed class WeakEventSource<TEventArgs>
		where TEventArgs : EventArgs
	{
		readonly object gate = new object();
		readonly List<Subscription> subscriptions = new();

		public void Subscribe(EventHandler<TEventArgs>? handler)
		{
			if (handler is null)
			{
				return;
			}

			lock (gate)
			{
				subscriptions.Add(new Subscription(handler.Target, handler.Method));
			}
		}

		public void Unsubscribe(EventHandler<TEventArgs>? handler)
		{
			if (handler is null)
			{
				return;
			}

			lock (gate)
			{
				for (int i = subscriptions.Count - 1; i >= 0; i--)
				{
					var subscription = subscriptions[i];

					if (subscription.Target != null && !subscription.Target.TryGetTarget(out _))
					{
						subscriptions.RemoveAt(i);
						continue;
					}

					if (subscription.Matches(handler.Target, handler.Method))
					{
						subscriptions.RemoveAt(i);
						break;
					}
				}
			}
		}

		public void Raise(object? sender, TEventArgs args)
		{
			List<(object? target, MethodInfo method)> toInvoke;

			lock (gate)
			{
				toInvoke = new List<(object?, MethodInfo)>(subscriptions.Count);

				for (int i = subscriptions.Count - 1; i >= 0; i--)
				{
					var subscription = subscriptions[i];

					if (subscription.Target == null)
					{
						// Static method target.
						toInvoke.Add((null, subscription.Method));
						continue;
					}

					if (subscription.Target.TryGetTarget(out var target))
					{
						toInvoke.Add((target, subscription.Method));
					}
					else
					{
						subscriptions.RemoveAt(i);
					}
				}
			}

			for (int i = toInvoke.Count - 1; i >= 0; i--)
			{
				toInvoke[i].method.Invoke(toInvoke[i].target, new object?[] { sender, args });
			}
		}

		readonly struct Subscription
		{
			public Subscription(object? target, MethodInfo method)
			{
				Target = target is null ? null : new WeakReference<object>(target);
				Method = method;
			}

			public readonly WeakReference<object>? Target;
			public readonly MethodInfo Method;

			public bool Matches(object? target, MethodInfo method)
			{
				if (Method != method)
				{
					return false;
				}

				if (target is null)
				{
					return Target is null;
				}

				return Target != null && Target.TryGetTarget(out var current) && ReferenceEquals(current, target);
			}
		}
	}

	sealed class WeakEventSource
	{
		readonly object gate = new object();
		readonly List<Subscription> subscriptions = new();

		public void Subscribe(EventHandler? handler)
		{
			if (handler is null)
			{
				return;
			}

			lock (gate)
			{
				subscriptions.Add(new Subscription(handler.Target, handler.Method));
			}
		}

		public void Unsubscribe(EventHandler? handler)
		{
			if (handler is null)
			{
				return;
			}

			lock (gate)
			{
				for (int i = subscriptions.Count - 1; i >= 0; i--)
				{
					var subscription = subscriptions[i];

					if (subscription.Target != null && !subscription.Target.TryGetTarget(out _))
					{
						subscriptions.RemoveAt(i);
						continue;
					}

					if (subscription.Matches(handler.Target, handler.Method))
					{
						subscriptions.RemoveAt(i);
						break;
					}
				}
			}
		}

		public bool HasHandlers
		{
			get
			{
				lock (gate)
				{
					for (int i = subscriptions.Count - 1; i >= 0; i--)
					{
						var subscription = subscriptions[i];

						if (subscription.Target == null || subscription.Target.TryGetTarget(out _))
						{
							return true;
						}

						subscriptions.RemoveAt(i);
					}

					return false;
				}
			}
		}

		public void Raise(object? sender, EventArgs args)
		{
			List<(object? target, MethodInfo method)> toInvoke;

			lock (gate)
			{
				toInvoke = new List<(object?, MethodInfo)>(subscriptions.Count);

				for (int i = subscriptions.Count - 1; i >= 0; i--)
				{
					var subscription = subscriptions[i];

					if (subscription.Target == null)
					{
						toInvoke.Add((null, subscription.Method));
						continue;
					}

					if (subscription.Target.TryGetTarget(out var target))
					{
						toInvoke.Add((target, subscription.Method));
					}
					else
					{
						subscriptions.RemoveAt(i);
					}
				}
			}

			for (int i = toInvoke.Count - 1; i >= 0; i--)
			{
				toInvoke[i].method.Invoke(toInvoke[i].target, new object?[] { sender, args });
			}
		}

		readonly struct Subscription
		{
			public Subscription(object? target, MethodInfo method)
			{
				Target = target is null ? null : new WeakReference<object>(target);
				Method = method;
			}

			public readonly WeakReference<object>? Target;
			public readonly MethodInfo Method;

			public bool Matches(object? target, MethodInfo method)
			{
				if (Method != method)
				{
					return false;
				}

				if (target is null)
				{
					return Target is null;
				}

				return Target != null && Target.TryGetTarget(out var current) && ReferenceEquals(current, target);
			}
		}
	}
}
