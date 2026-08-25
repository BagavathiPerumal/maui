using System;
using Android.Animation;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Hardware.Lights;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using AndroidX.Core.Widget;
using ALayoutDirection = Android.Views.LayoutDirection;

namespace Microsoft.Maui.Platform
{
	public class MauiScrollView : NestedScrollView, IScrollBarView, NestedScrollView.IOnScrollChangeListener, ICrossPlatformLayoutBacking, IHandleWindowInsets
	{
		View? _content;
		readonly Context _context;
		MauiHorizontalScrollView? _hScrollView;
		bool _isBidirectional;
		ScrollOrientation _scrollOrientation = ScrollOrientation.Vertical;
		ScrollBarVisibility _defaultHorizontalScrollVisibility;
		ScrollBarVisibility _defaultVerticalScrollVisibility;
		ScrollBarVisibility _horizontalScrollVisibility;
		bool _didSafeAreaEdgeConfigurationChange = true;
		bool _isInsetListenerSet;
		Java.Lang.IRunnable? _setAppBarLiftTargetRunnable;
		ALayoutDirection _prevLayoutDirection = ALayoutDirection.Ltr;
		bool _checkedForRtlScroll;

		internal float LastX { get; set; }
		internal float LastY { get; set; }

		internal bool ShouldSkipOnTouch;
		internal int HorizontalScrollOffset => _hScrollView?.ScrollX ?? 0;

		// Stores the parent touch listener so horizontal ScrollView taps can be forwarded to it directly.
		internal IOnTouchListener? _touchListener;

		public override void SetOnTouchListener(IOnTouchListener? touchListener)
		{
			_touchListener = touchListener;
			base.SetOnTouchListener(touchListener);
		}

		public MauiScrollView(Context context) : base(context)
		{
			_context = context;
		}

		public MauiScrollView(Context context, IAttributeSet attrs) : base(context, attrs)
		{
			_context = context;
		}

		public MauiScrollView(Context context, IAttributeSet attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
		{
			_context = context;
		}

		protected MauiScrollView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{
			var context = Context;
			ArgumentNullException.ThrowIfNull(context);
			_context = context;
		}
		public ICrossPlatformLayout? CrossPlatformLayout
		{
			get; set;
		}

		public override void OnAttachedToWindow()
		{
			base.OnAttachedToWindow();
			_isInsetListenerSet = MauiWindowInsetListenerExtensions.TrySetMauiWindowInsetListener(this, _context);

			if (RuntimeFeature.IsMaterial3Enabled)
			{
				// Pin the MAUI navigation AppBarLayout's lift-on-scroll target to this NestedScrollView.
				// Otherwise AppBarLayout auto-detects the outer FragmentContainerView as the scrolling target,
				// and its ViewTreeObserver-driven shouldLift() check evaluates canScrollVertically() on the
				// container (which is always 0), causing the lifted state to flip on every layout pass
				// triggered by sibling views (e.g. CheckBox/Switch state animations) and producing a
				// visible scrolledContainerColor flicker.
				// Use Post() to defer until layout is complete — when this ScrollView is inside
				// a CarouselView, adjacent off-screen pages also attach and we need to verify
				// the view is actually on-screen before claiming the lift target.
				PostTrySetAppBarLiftTargetIfOnScreen();
			}
		}

		protected override void OnDetachedFromWindow()
		{
			// Clean up AppBar listener while the ViewTreeObserver is still valid.
			if (RuntimeFeature.IsMaterial3Enabled)
			{
				ClearAppBarLiftTargetAndPendingPost();
			}

			base.OnDetachedFromWindow();
			if (_isInsetListenerSet)
				MauiWindowInsetListenerExtensions.RemoveMauiWindowInsetListener(this, _context);

			_isInsetListenerSet = false;
			_didSafeAreaEdgeConfigurationChange = true;
		}

		protected override void OnVisibilityChanged(View changedView, ViewStates visibility)
		{
			base.OnVisibilityChanged(changedView, visibility);

			if (changedView != this)
			{
				return;
			}

			if (!RuntimeFeature.IsMaterial3Enabled)
			{
				return;
			}

			if (visibility == ViewStates.Visible)
			{
				PostTrySetAppBarLiftTargetIfOnScreen();
			}
			else
			{
				ClearAppBarLiftTargetAndPendingPost();
			}
		}

		void PostTrySetAppBarLiftTargetIfOnScreen()
		{
			var runnable = GetOrCreateSetAppBarLiftTargetRunnable();
			RemoveCallbacks(runnable);
			Post(runnable);
		}

		void ClearAppBarLiftTargetAndPendingPost()
		{
			if (_setAppBarLiftTargetRunnable is not null)
			{
				RemoveCallbacks(_setAppBarLiftTargetRunnable);
			}

			this.ClearAppBarLiftTarget();
		}

		Java.Lang.IRunnable GetOrCreateSetAppBarLiftTargetRunnable()
		{
			return _setAppBarLiftTargetRunnable ??= new Java.Lang.Runnable(() => this.TrySetAppBarLiftTargetIfOnScreen());
		}

		#region IHandleWindowInsets Implementation

		(int left, int top, int right, int bottom) _originalPadding;
		bool _hasStoredOriginalPadding;

		// Tracks the paddingShim's original padding so it can be restored when the IME hides.
		(int left, int top, int right, int bottom) _originalContentPadding;
		bool _hasStoredOriginalContentPadding;
		bool _contentPaddingAppliedForIme;

		WindowInsetsCompat? IHandleWindowInsets.HandleWindowInsets(View view, WindowInsetsCompat insets)
		{
			// If we don't have a cross platform layout or insets are null just return
			if (CrossPlatformLayout is null || insets is null)
			{
				return insets;
			}

			if (!_hasStoredOriginalPadding)
			{
				_originalPadding = (PaddingLeft, PaddingTop, PaddingRight, PaddingBottom);
				_hasStoredOriginalPadding = true;
			}

			var adjusted = SafeAreaExtensions.ApplyAdjustedSafeAreaInsetsPx(insets, CrossPlatformLayout, _context, view);

			// In edge-to-edge mode, resizing the window doesn't shrink the ScrollView's viewport,
			// so if the content just fits, the scroll range can't grow to reveal a focused Editor
			// hidden behind the keyboard. Grow it by padding the inner content shim instead.
			// Only do this when SafeArea hasn't already compensated for the keyboard via this
			// view's own PaddingBottom (e.g. SafeAreaEdges=All/Container/SoftInput) — otherwise
			// the two mechanisms stack and double-pad the content.
			var imeInsets = insets.GetInsets(WindowInsetsCompat.Type.Ime());
			var imeBottom = imeInsets?.Bottom ?? 0;
			if (PaddingBottom < imeBottom)
			{
				ApplyImeContentGrow(insets);
			}
			else
			{
				ResetContentImePadding();
			}

			return adjusted;
		}

		void IHandleWindowInsets.ResetWindowInsets(View view)
		{
			if (_hasStoredOriginalPadding)
			{
				SetPadding(_originalPadding.left, _originalPadding.top, _originalPadding.right, _originalPadding.bottom);
			}
			ResetContentImePadding();
		}

		// Pads the inner content shim to grow the scroll range by the IME-occluded amount.
		void ApplyImeContentGrow(WindowInsetsCompat insets)
		{
			// Direct scrollable child (the ContentViewGroup wrapper from ScrollViewHandler).
			var shim = _content;
			if (shim is null)
			{
				return;
			}

			// Mirror SafeAreaExtensions.ApplyAdjustedSafeAreaInsetsPx: in AdjustPan mode the whole
			// window pans instead of resizing, so there's no scroll-range shortfall for us to grow —
			// the OS already moves the focused view into place above the keyboard.
			if (_context.GetActivity()?.Window?.Attributes is WindowManagerLayoutParams attr &&
				(attr.SoftInputMode & SoftInput.MaskAdjust) == SoftInput.AdjustPan)
			{
				ResetContentImePadding();
				return;
			}

			var imeInsets = insets.GetInsets(WindowInsetsCompat.Type.Ime());
			var imeBottom = imeInsets?.Bottom ?? 0;

			// Skip if our own bottom padding already covers the IME overlap.
			var growAmount = Math.Max(0, imeBottom - PaddingBottom);

			if (!_hasStoredOriginalContentPadding)
			{
				_originalContentPadding = (shim.PaddingLeft, shim.PaddingTop, shim.PaddingRight, shim.PaddingBottom);
				_hasStoredOriginalContentPadding = true;
			}

			if (growAmount <= 0)
			{
				ResetContentImePadding();
				return;
			}

			var targetBottom = _originalContentPadding.bottom + growAmount;
			if (!_contentPaddingAppliedForIme || shim.PaddingBottom != targetBottom)
			{
				shim.SetPadding(
					_originalContentPadding.left,
					_originalContentPadding.top,
					_originalContentPadding.right,
					targetBottom);
				_contentPaddingAppliedForIme = true;
				PlatformInterop.RequestLayoutIfNeeded(shim);

				// NestedScrollView only auto-scrolls to bring a focused child into view at the
				// moment focus is requested — which happens BEFORE the keyboard slides up, so it
				// computes "already visible" and does nothing. Growing the padding here creates
				// scroll room, but doesn't itself move the scroll position, so without this the
				// focused Editor (and its cursor) stays hidden behind the keyboard. Post() defers
				// until after the layout triggered above has actually taken effect.
				var targetImeBottom = imeBottom;
				Post(() => ScrollFocusedViewAboveKeyboard(targetImeBottom));
			}
		}

		// Scrolls so the currently-focused descendant is fully above the keyboard, if it isn't already.
		void ScrollFocusedViewAboveKeyboard(int imeBottom)
		{
			if (FindFocus() is not View focused || !focused.IsAttachedToWindow)
			{
				return;
			}

			var rect = new Rect();
			focused.GetDrawingRect(rect);
			OffsetDescendantRectToMyCoords(focused, rect);

			// Bottom edge of the area still visible above the keyboard, in this ScrollView's
			// own scrolled coordinate space.
			var visibleBottom = ScrollY + Height - imeBottom - PaddingBottom;
			if (rect.Bottom > visibleBottom)
			{
				SmoothScrollBy(0, rect.Bottom - visibleBottom);
			}
		}

		void ResetContentImePadding()
		{
			if (_contentPaddingAppliedForIme && _content is not null && _hasStoredOriginalContentPadding)
			{
				// Capture how much extra padding is being removed BEFORE resetting it —
				// this is exactly how much the scroll range is about to shrink by.
				var growAmount = Math.Max(0, _content.PaddingBottom - _originalContentPadding.bottom);

				_content.SetPadding(
					_originalContentPadding.left,
					_originalContentPadding.top,
					_originalContentPadding.right,
					_originalContentPadding.bottom);
				_contentPaddingAppliedForIme = false;
				PlatformInterop.RequestLayoutIfNeeded(_content);

				// If the current scroll offset would now be unreachable (dead/blank space at the
				// bottom), clamp it back into the new valid range instead of unconditionally
				// jumping to the top — this preserves the user's scroll position whenever it's
				// still valid after the shrink. Post() defers until the pending layout (triggered
				// by RequestLayoutIfNeeded above) has actually taken effect.
				if (growAmount > 0 && ScrollY > 0)
				{
					var clampedScrollY = Math.Max(0, ScrollY - growAmount);
					Post(() => ScrollTo(ScrollX, clampedScrollY));
				}
			}
		}

		#endregion

		public void SetHorizontalScrollBarVisibility(ScrollBarVisibility scrollBarVisibility)
		{
			_horizontalScrollVisibility = scrollBarVisibility;
			if (_hScrollView == null)
			{
				return;
			}

			if (_defaultHorizontalScrollVisibility == 0)
			{
				_defaultHorizontalScrollVisibility = _hScrollView.HorizontalScrollBarEnabled ? ScrollBarVisibility.Always : ScrollBarVisibility.Never;
			}

			if (scrollBarVisibility == ScrollBarVisibility.Default)
			{
				scrollBarVisibility = _defaultHorizontalScrollVisibility;
			}

			_hScrollView.HorizontalScrollBarEnabled = scrollBarVisibility == ScrollBarVisibility.Always;
			_hScrollView.ScrollbarFadingEnabled = _horizontalScrollVisibility != ScrollBarVisibility.Always;
			PlatformInterop.RequestLayoutIfNeeded(_hScrollView);
		}

		public void SetVerticalScrollBarVisibility(ScrollBarVisibility scrollBarVisibility)
		{
			ScrollBarVisibility verticalScrollVisibility = scrollBarVisibility;

			if (_defaultVerticalScrollVisibility == 0)
				_defaultVerticalScrollVisibility = VerticalScrollBarEnabled ? ScrollBarVisibility.Always : ScrollBarVisibility.Never;

			if (scrollBarVisibility == ScrollBarVisibility.Default)
				scrollBarVisibility = _defaultVerticalScrollVisibility;

			VerticalScrollBarEnabled = scrollBarVisibility == ScrollBarVisibility.Always;
			ScrollbarFadingEnabled = verticalScrollVisibility != ScrollBarVisibility.Always;
			PlatformInterop.RequestLayoutIfNeeded(this);
		}

		public void SetContent(View content)
		{
			// Content shim changed — re-capture the padding baseline on next inset pass.
			if (!ReferenceEquals(_content, content))
			{
				_hasStoredOriginalContentPadding = false;
				_contentPaddingAppliedForIme = false;
			}
			_content = content;
			SetOrientation(_scrollOrientation);
		}

		public void SetOrientation(ScrollOrientation orientation)
		{
			bool orientationChanged = _scrollOrientation != orientation;
			_scrollOrientation = orientation;

			// Reset RTL tracking when orientation changes
			if (orientationChanged)
			{
				_checkedForRtlScroll = false;
			}

			if (orientation == ScrollOrientation.Horizontal || orientation == ScrollOrientation.Both)
			{
				if (_hScrollView == null)
				{
					_hScrollView = new MauiHorizontalScrollView(Context, this)
					{
						FillViewport = true
					};

					_hScrollView.HorizontalFadingEdgeEnabled = HorizontalFadingEdgeEnabled;
					_hScrollView.SetFadingEdgeLength(HorizontalFadingEdgeLength);
					SetHorizontalScrollBarVisibility(_horizontalScrollVisibility);
				}

				_hScrollView.IsBidirectional = _isBidirectional = orientation == ScrollOrientation.Both;

				if (_hScrollView.Parent != this)
				{
					if (_content != null)
					{
						_content.RemoveFromParent();
						_hScrollView.AddView(_content);
					}

					AddView(_hScrollView);
				}
				// If the user has changed between horiztonal and both we want to request a new layout
				// so the Horizontal Layout can be adjusted to satisfy the new orientation.
				else if (orientationChanged)
				{
					PlatformInterop.RequestLayoutIfNeeded(this);
				}
			}
			else
			{
				if (_content != null && _content.Parent != this)
				{
					_content.RemoveFromParent();
					_hScrollView?.RemoveFromParent();
					AddView(_content);
				}
			}
		}

		internal void UpdateFlowDirection(IView view)
		{
			var layoutDirection = ViewExtensions.GetLayoutDirection(view);

			// Handle FlowDirection specifically for horizontal scroll view
			if (_hScrollView != null && _scrollOrientation == ScrollOrientation.Horizontal)
			{
				if (_prevLayoutDirection != layoutDirection)
				{
					_prevLayoutDirection = layoutDirection;
					_hScrollView.LayoutDirection = layoutDirection;
					_checkedForRtlScroll = false; // Reset to allow re-evaluation
				}
			}
			else
			{
				// Fallback to default mechanism for other cases (vertical scroll or no horizontal scroll)
				// Use the common ViewExtensions logic for standard FlowDirection handling
				this.LayoutDirection = layoutDirection;
			}
		}

		public override bool OnInterceptTouchEvent(MotionEvent? ev)
		{
			// See also MauiHorizontalScrollView notes in OnInterceptTouchEvent

			if (ev == null)
				return false;

			// set the start point for the bidirectional scroll; 
			// Down is swallowed by other controls, so we'll just sneak this in here without actually preventing
			// other controls from getting the event.			
			if (_isBidirectional && ev.Action == MotionEventActions.Down)
			{
				LastY = ev.RawY;
				LastX = ev.RawX;
			}

			return base.OnInterceptTouchEvent(ev);
		}

		public override bool OnTouchEvent(MotionEvent? ev)
		{
			if (ev == null || !Enabled || _scrollOrientation == ScrollOrientation.Neither)
				return false;

			if (ShouldSkipOnTouch)
			{
				ShouldSkipOnTouch = false;
				return false;
			}


			// The nested ScrollViews will allow us to scroll EITHER vertically OR horizontally in a single gesture.
			// This will allow us to also scroll diagonally.
			// We'll fall through to the base event so we still get the fling from the ScrollViews.
			// We have to do this in both ScrollViews, since a single gesture will be owned by one or the other, depending
			// on the initial direction of movement (i.e., horizontal/vertical).
			if (_isBidirectional) // // See also MauiHorizontalScrollView notes in OnInterceptTouchEvent
			{
				float dX = LastX - ev.RawX;

				LastY = ev.RawY;
				LastX = ev.RawX;
				if (ev.Action == MotionEventActions.Move)
				{
					foreach (MauiHorizontalScrollView child in this.GetChildrenOfType<MauiHorizontalScrollView>())
					{
						child.ScrollBy((int)dX, 0);
						break;
					}
					// Fall through to base.OnTouchEvent, it'll take care of the Y scrolling				
				}
			}

			return base.OnTouchEvent(ev);
		}

		void IScrollBarView.AwakenScrollBars()
		{
			base.AwakenScrollBars();
		}

		bool IScrollBarView.ScrollBarsInitialized { get; set; }

		protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
		{
			base.OnMeasure(widthMeasureSpec, heightMeasureSpec);

			// If we have bidirectional scrolling then we can just let everything flow through naturally.
			// The HorizontalScrollView will automatically size its height to the content and thus enable
			// vertical scolling
			// If we're only enabling horizontal scrolling then we want to force the horizontal scrollView
			// to be the same size as the NestedScrollView this way it can't be scrolled vertically
			if (_hScrollView?.Parent == this && _content is not null && !_isBidirectional)
			{
				var hScrollViewHeight = this.MeasuredHeight;
				var hScrollViewWidth = this.MeasuredWidth;

				_hScrollView.Measure(MeasureSpec.MakeMeasureSpec(hScrollViewWidth, MeasureSpecMode.Exactly),
					MeasureSpec.MakeMeasureSpec(hScrollViewHeight, MeasureSpecMode.Exactly));
			}
		}

		protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
		{
			base.OnLayout(changed, left, top, right, bottom);

			if (_hScrollView?.Parent == this && _content is not null)
			{
				var scrollViewContentHeight = _content.Height;
				var hScrollViewHeight = bottom - top;
				var hScrollViewWidth = right - left;

				//if we are scrolling both ways we need to lay out our MauiHorizontalScrollView with more than the available height
				//so its parent the NestedScrollView can scroll vertically
				hScrollViewHeight = _isBidirectional ? Math.Max(hScrollViewHeight, scrollViewContentHeight) : hScrollViewHeight;
				_hScrollView.Layout(0, 0, hScrollViewWidth, hScrollViewHeight);
			}

			// Handle RTL initial positioning
			if (!_checkedForRtlScroll && _hScrollView != null && _scrollOrientation == ScrollOrientation.Horizontal)
			{
				if (_hScrollView.LayoutDirection == ALayoutDirection.Rtl)
				{
					Post(() =>
					{
						// Scroll to the right end for RTL
						_hScrollView?.ScrollTo(_hScrollView?.GetChildAt(0)?.Width ?? 0, 0);
					});
				}
			}

			_checkedForRtlScroll = true;

			if (_didSafeAreaEdgeConfigurationChange && _isInsetListenerSet)
			{
				ViewCompat.RequestApplyInsets(this);
				_didSafeAreaEdgeConfigurationChange = false;
			}
		}

		protected override void OnConfigurationChanged(Configuration? newConfig)
		{
			base.OnConfigurationChanged(newConfig);

			MauiWindowInsetListener.FindListenerForView(this)?.ResetView(this);
			_didSafeAreaEdgeConfigurationChange = true;
		}

		/// <summary>
		/// Marks that the SafeAreaEdges configuration changed so we re-request window insets next layout.
		/// </summary>
		internal void MarkSafeAreaEdgeConfigurationChanged()
		{
			_isInsetListenerSet = MauiWindowInsetListenerExtensions.RefreshMauiWindowInsetListener(this, _context);
			_didSafeAreaEdgeConfigurationChange = true;
			RequestLayout();
		}

		public void ScrollTo(int x, int y, bool instant, Action finished)
		{
			if (instant)
			{
				JumpTo(x, y, finished);
			}
			else
			{
				SmoothScrollTo(x, y, finished);
			}
		}

		void JumpTo(int x, int y, Action finished)
		{
			switch (_scrollOrientation)
			{
				case ScrollOrientation.Vertical:
					ScrollTo(x, y);
					break;
				case ScrollOrientation.Horizontal:
					_hScrollView?.ScrollTo(x, y);
					break;
				case ScrollOrientation.Both:
					_hScrollView?.ScrollTo(x, y);
					ScrollTo(x, y);
					break;
				case ScrollOrientation.Neither:
					break;
			}

			finished();
		}

		static int GetDistance(double start, double position, double v)
		{
			return (int)(start + (position - start) * v);
		}

		void SmoothScrollTo(int x, int y, Action finished)
		{
			int currentX = _scrollOrientation == ScrollOrientation.Horizontal || _scrollOrientation == ScrollOrientation.Both ? _hScrollView!.ScrollX : ScrollX;
			int currentY = _scrollOrientation == ScrollOrientation.Vertical || _scrollOrientation == ScrollOrientation.Both ? ScrollY : _hScrollView!.ScrollY;

			ValueAnimator? animator = ValueAnimator.OfFloat(0f, 1f);
			animator!.SetDuration(1000);

			animator.Update += (o, animatorUpdateEventArgs) =>
			{
				var v = (double)(animatorUpdateEventArgs.Animation!.AnimatedValue!);
				int distX = GetDistance(currentX, x, v);
				int distY = GetDistance(currentY, y, v);

				switch (_scrollOrientation)
				{
					case ScrollOrientation.Horizontal:
						_hScrollView?.ScrollTo(distX, distY);
						break;
					case ScrollOrientation.Vertical:
						ScrollTo(distX, distY);
						break;
					default:
						_hScrollView?.ScrollTo(distX, distY);
						ScrollTo(distX, distY);
						break;
				}
			};

			animator.AnimationEnd += delegate
			{
				finished();
			};

			animator.Start();
		}

#pragma warning disable CA1822 // DO NOT REMOVE! Needed because dotnet format will else try to make this static and break things
		void IOnScrollChangeListener.OnScrollChange(NestedScrollView? v, int scrollX, int scrollY, int oldScrollX, int oldScrollY)
#pragma warning restore CA1822
		{
			_checkedForRtlScroll = true;
			OnScrollChanged(scrollX, scrollY, oldScrollX, oldScrollY);
		}
	}

	public class MauiHorizontalScrollView : HorizontalScrollView, IScrollBarView
	{
		readonly MauiScrollView? _parentScrollView;

		protected MauiHorizontalScrollView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{
		}

		public MauiHorizontalScrollView(Context? context, MauiScrollView parentScrollView) : base(context)
		{
			_parentScrollView = parentScrollView;
			Tag = "Microsoft.Maui.Android.HorizontalScrollView";
		}

		public MauiHorizontalScrollView(Context? context, IAttributeSet? attrs) : base(context, attrs)
		{
		}

		public MauiHorizontalScrollView(Context? context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
		{
		}

		public MauiHorizontalScrollView(Context? context, IAttributeSet? attrs, int defStyleAttr, int defStyleRes) : base(context, attrs, defStyleAttr, defStyleRes)
		{
		}

		internal bool IsBidirectional { get; set; }

		public override void Draw(Canvas? canvas)
		{
			try
			{
				canvas?.ClipRect(canvas?.ClipBounds!);

				base.Draw(canvas!);
			}
			catch (Java.Lang.NullPointerException)
			{
				// This will most likely never run since UpdateScrollBars is called 
				// when the scrollbars visibilities are updated but I left it here
				// just in case there's an edge case that causes an exception
				this.HandleScrollBarVisibilityChange();
			}
		}

		public override bool OnInterceptTouchEvent(MotionEvent? ev)
		{
			if (ev == null || _parentScrollView == null)
				return false;

			// TODO ezhart 2021-07-12 The previous version of this checked _renderer.Element.InputTransparent; we don't have acces to that here,
			// and I'm not sure it even applies. We need to determine whether touch events will get here at all if we've marked the ScrollView InputTransparent
			// We _should_ be able to deal with it at the handler level by force-setting an OnTouchListener for the PlatformView that always returns false; then we
			// can just stop worrying about it here because the touches _can't_ reach this.

			// set the start point for the bidirectional scroll; 
			// Down is swallowed by other controls, so we'll just sneak this in here without actually preventing
			// other controls from getting the event.
			if (IsBidirectional && ev.Action == MotionEventActions.Down)
			{
				_parentScrollView.LastY = ev.RawY;
				_parentScrollView.LastX = ev.RawX;
			}

			return base.OnInterceptTouchEvent(ev);
		}

		public override bool OnTouchEvent(MotionEvent? ev)
		{
			if (ev == null || _parentScrollView == null)
				return false;

			if (!_parentScrollView.Enabled)
				return false;

			// OnTouchEvent is only called when no child has claimed the touch event, which mirrors
			// exactly when a vertical ScrollView's touch listener fires. We invoke the parent's
			// stored touch listener here so TapGestureRecognizers on a horizontal/both ScrollView
			// fire correctly.
			_parentScrollView._touchListener?.OnTouch(_parentScrollView, ev);

			// If the touch is caught by the horizontal scrollview, forward it to the parent 
			_parentScrollView.ShouldSkipOnTouch = true;
			_parentScrollView.OnTouchEvent(ev);

			// The nested ScrollViews will allow us to scroll EITHER vertically OR horizontally in a single gesture.
			// This will allow us to also scroll diagonally.
			// We'll fall through to the base event so we still get the fling from the ScrollViews.
			// We have to do this in both ScrollViews, since a single gesture will be owned by one or the other, depending
			// on the initial direction of movement (i.e., horizontal/vertical).
			if (IsBidirectional)
			{
				float dY = _parentScrollView.LastY - ev.RawY;

				_parentScrollView.LastY = ev.RawY;
				_parentScrollView.LastX = ev.RawX;
				if (ev.Action == MotionEventActions.Move)
				{
					_parentScrollView.ScrollBy(0, (int)dY);
					// Fall through to base.OnTouchEvent, it'll take care of the X scrolling 					
				}
			}

			return base.OnTouchEvent(ev);
		}

		public override bool HorizontalScrollBarEnabled
		{
			get { return base.HorizontalScrollBarEnabled; }
			set
			{
				base.HorizontalScrollBarEnabled = value;
			}
		}

		void IScrollBarView.AwakenScrollBars()
		{
			base.AwakenScrollBars();
		}

		bool IScrollBarView.ScrollBarsInitialized { get; set; }

		protected override void OnScrollChanged(int l, int t, int oldl, int oldt)
		{
			base.OnScrollChanged(l, t, oldl, oldt);

			if (_parentScrollView is NestedScrollView.IOnScrollChangeListener scrollChangeListener)
			{
				scrollChangeListener.OnScrollChange(_parentScrollView, l, t, oldl, oldt);
			}
		}
	}

	internal interface IScrollBarView
	{
		bool ScrollBarsInitialized { get; set; }
		bool ScrollbarFadingEnabled { get; set; }
		void AwakenScrollBars();
	}
}
