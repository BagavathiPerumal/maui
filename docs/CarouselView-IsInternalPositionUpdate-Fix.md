# Fix: `_isInternalPositionUpdate` Flag Leak in Android CarouselView

## Issue

[GitHub Issue #35643](https://github.com/dotnet/maui/issues/35643) — CarouselView resets to position 0 when an item is replaced in the bound `ObservableCollection`.

## Root Cause

In `MauiCarouselRecyclerView.cs`, the `CollectionItemsSourceChanged` method sets `_isInternalPositionUpdate = true` early in its body (before branching) to suppress spurious scroll events during collection changes. However, two early-return paths inside the `Replace` action block exited without resetting the flag to `false`, leaving it permanently `true`.

### Leaking Paths (before fix)

**Path 1 — Loop mode Replace:**
```csharp
if (Carousel.Loop)
{
    if (isCurrentItemReplaced)
    {
        UpdateAdapter();
        ScrollToPosition(carouselPosition);
    }
    return; // ← _isInternalPositionUpdate leaked as true
}
```

**Path 2 — Null dispatcher:**
```csharp
var dispatcher = Carousel.Handler?.MauiContext?.GetDispatcher();
if (dispatcher is null)
{
    return; // ← _isInternalPositionUpdate leaked as true
}
```

When `_isInternalPositionUpdate` stays `true` after the method returns, the `CarouselScrolled` handler (via `ScrollToCurrentItem`) checks this flag and disables animation — and more critically, subsequent user scroll events could be silently suppressed by any guard reading this flag.

## Fix

Added `_isInternalPositionUpdate = false;` before each bare `return` in the two leaking paths.

**File:** `src/Controls/src/Core/Handlers/Items/Android/MauiCarouselRecyclerView.cs`

```csharp
if (Carousel.Loop)
{
    if (isCurrentItemReplaced)
    {
        UpdateAdapter();
        ScrollToPosition(carouselPosition);
    }
    _isInternalPositionUpdate = false; // ← added
    return;
}

var dispatcher = Carousel.Handler?.MauiContext?.GetDispatcher();
if (dispatcher is null)
{
    _isInternalPositionUpdate = false; // ← added
    return;
}
```

## Paths Already Safe (no change needed)

| Path | How it resets |
|------|--------------|
| `removingAnyPrevious` branch | Explicit `_isInternalPositionUpdate = false;` before `return` (line ~337) |
| Dispatcher lambda (Replace) | `finally` block inside the dispatched callback |
| Dispatcher lambda (Insert/Remove) | `finally` block inside the dispatched callback |

## Impact

- Prevents user scroll interactions from being silently blocked after a `Replace` action on a looping CarouselView or when the dispatcher is unavailable.
- No behaviour change for normal (non-Loop, dispatcher-available) Replace operations.
- No behaviour change for Insert/Remove operations.

## Branch

`BagavathiPerumal:maui:temp-CarouselCurrentItem`
