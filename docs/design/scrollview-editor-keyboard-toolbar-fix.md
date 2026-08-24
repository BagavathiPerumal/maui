# ScrollView/Editor keyboard overlap and toolbar visibility (Android)

This document describes the root cause and fix for two related Android issues
that occur when a `ScrollView` contains multiple `Editor` controls and the
on-screen keyboard is shown.

## Issue 1: ScrollView doesn't work with multiple Editor controls

**Root Cause:** The issue occurs because of Android's edge-to-edge display
mode not physically shrinking the window when the on-screen keyboard appears,
combined with `ScrollView`'s default safe-area configuration not accounting
for the keyboard overlap. When a fixed column of Editors just fits the
viewport, the internal `NestedScrollView`'s scroll range becomes zero the
moment the keyboard shows, leaving the focused Editor and any content below
it permanently hidden behind the keyboard with no way to scroll to reveal
them.

**Solution Description:** The fix involves growing the ScrollView's internal
scroll range by adding bottom padding directly to the ScrollView's inner
content shim (the `ContentViewGroup` wrapper), sized precisely from the
keyboard's inset height (`WindowInsetsCompat.Type.Ime().Bottom`). Since
padding on this inner view contributes to its measured height, and measured
height is what `NestedScrollView` uses to compute scroll range, this reliably
creates enough extra scrollable space to bring the focused Editor into view,
and the padding is automatically removed (with scroll position reset) once
the keyboard is dismissed.

## Issue 2: Toolbar becomes invisible/unreachable when focusing the last Editor

**Root Cause:** The issue occurs because of Android's default
`WindowSoftInputModeAdjust.Pan` mode panning the entire application window
upward when a low-positioned Editor is focused, so that it stays visible
above the keyboard. Since the pinned toolbar (`AppBarLayout`/
`MaterialToolbar`) lives in the same window as the scrollable content, it
gets carried along with this window-level pan and pushed completely off the
top of the screen — making it unreachable while the keyboard remains open,
regardless of the ScrollView's own scroll position.

**Solution Description:** The fix involves opting the affected page into
`WindowSoftInputModeAdjust.Resize` instead of the default `Pan` mode, using
the existing platform-specific configuration API. Under Resize mode, Android
physically shrinks the window rather than translating it, so the toolbar
stays fixed at the top of the now-smaller window, while the ScrollView's own
keyboard-aware content-padding logic (from Issue 1's fix) keeps the focused
Editor scrolled into view within that resized space. A framework-level
compensation approach (translating/resizing the toolbar's container to
counteract the pan) was explored first but was abandoned after it introduced
a content-overlap visual bug and, after correction, a re-entrant layout
flicker — making the page-level Resize opt-in the more reliable choice.

## Files changed

- `src/Core/src/Platform/Android/MauiScrollView.cs` — scroll-range grow fix
  for Issue 1.
- `src/Controls/tests/TestCases.HostApp/Issues/Issue34020.cs` — repro page,
  opted into `WindowSoftInputModeAdjust.Resize` for Issue 2.
- `src/Controls/tests/TestCases.Shared.Tests/Tests/Issues/Issue34020.cs` — UI
  test covering both issues.

## Commit message

```
Made code changes to fix ScrollView content hidden behind the keyboard by growing its inner scroll range via IME-inset-driven padding, and to keep the toolbar visible by opting the affected page into WindowSoftInputModeAdjust.Resize.
```
