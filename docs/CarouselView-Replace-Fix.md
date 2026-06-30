# CarouselView Replace Fix — Issue #35643

## Overview

This document details the review concerns raised against the initial fix for `CarouselView` position reset when an item in a bound `ObservableCollection` is replaced (`collection[i] = newItem`), and the code changes applied to address each concern across Android, iOS, and Windows.

**Issue:** When a Replace operation is performed on a `CarouselView`-bound collection with `Loop=false`, the carousel incorrectly resets its visual position — snapping to index 0 — on Android, iOS/macOS, and Windows.

---

## Files Changed (Implementation Only)

| File | Platform |
|------|----------|
| `src/Controls/src/Core/Handlers/Items/Android/MauiCarouselRecyclerView.cs` | Android |
| `src/Controls/src/Core/Handlers/Items2/iOS/CarouselViewController2.cs` | iOS / macOS |
| `src/Controls/src/Core/Handlers/Items/CarouselViewHandler.Windows.cs` | Windows |
| `src/Controls/src/Core/Handlers/Items/ItemsViewHandler.Windows.cs` | Windows |

---

## Review Concerns and Code Changes

### Concern 1 — Android: Non-current item Replace still resets position

The original fix only handled Replace when `removingCurrentElement == true`. A Replace on a non-current item bypassed the fast-path and fell into the `KeepItemsInView` logic, resetting position to 0.

```csharp
// Before — only guarded when current item was not found
if (e.Action == Replace && removingCurrentElement) { ... }

// After — intercepts ALL Replace actions before Insert/Remove logic
if (e.Action == Replace)
{
    var replacedPosition = e.OldStartingIndex;
    var isCurrentItemReplaced = replacedPosition == carouselPosition;
    // handles both current and non-current item replace
    return;
}
```

---

### Concern 2 — Android: Replace fast-path not scoped to `Loop=false`

The fast-path dispatched a position-update callback even for `Loop=true`, where the loopable adapter uses virtual indices and needs a full rebuild.

```csharp
// Added inside the Replace branch
if (Carousel.Loop)
{
    if (isCurrentItemReplaced)
    {
        UpdateAdapter();
        ScrollToPosition(carouselPosition);
    }
    return;
}
```

---

### Concern 3 — Android: `Equals()`-based `removingCurrentElement` fails for value types

`GetPosition(CurrentItem)` uses `Equals()`. After a Replace the old item is gone from source, so it always returns `-1` — making `removingCurrentElement = true` for every Replace, even non-current ones. Broken for value types and objects overriding `Equals()`.

```csharp
// Before — Equals()-based, unreliable for Replace
bool removingCurrentElement = currentItemPosition == -1;

// After — index-based, reliable for all types
var isCurrentItemReplaced = replacedPosition == carouselPosition;
```

---

### Concern 4 — iOS: No explicit Replace branch in `CollectionViewUpdating` (implicit trap)

No Replace branch existed in `CollectionViewUpdating`. `_positionAfterUpdate` silently fell through to `carouselPosition` set at the top. If `carouselPosition` is stale (e.g. 0 after a Reset), the wrong position would be used.

```csharp
// Added in CollectionViewUpdating
// Replace does not change item count. Explicitly set _positionAfterUpdate using
// currentItemPosition (from CurrentItem, always authoritative).
// Loop=true is handled by GetTargetPosition() in CollectionViewUpdated.
if (e.Action == NotifyCollectionChangedAction.Replace && !ItemsView.Loop)
{
    _positionAfterUpdate = currentItemPosition >= 0 ? currentItemPosition : carouselPosition;
}
```

---

### Concern 5 — Windows: Deferred `ScrollIntoView` has no stale-work guard

The Replace fast-path enqueued a `ScrollIntoView` at `Low` priority. If another Replace fired before the queued work drained, the stale lambda would scroll to the wrong item.

```csharp
// Added generation counter
var capturedGeneration = ++_replaceScrollGeneration;

ListViewBase?.DispatcherQueue?.TryEnqueue(
    DispatcherQueuePriority.Low, () =>
    {
        if (_replaceScrollGeneration != capturedGeneration)
            return; // stale — a newer Replace has taken over
        var item = GetItem(capturedPosition);
        if (item is not null)
            ListViewBase.ScrollIntoView(item, ScrollIntoViewAlignment.Leading);
    });
```

---

### Concern 6 — iOS/Android: `Loop=true` not guarded

**Android:** handled in Concern 2 — `if (Carousel.Loop)` block added.

**iOS:** The `CollectionViewUpdated` Replace branch used `_positionAfterUpdate` unconditionally. For `Loop=true`, `_positionAfterUpdate` holds a logical index while the loopable adapter needs a virtual index resolved by `GetTargetPosition()`.

```csharp
// Before
if (e.Action == NotifyCollectionChangedAction.Replace)
    targetPosition = _positionAfterUpdate;

// After — Loop=true falls through to GetTargetPosition() which resolves virtual index
if (e.Action == NotifyCollectionChangedAction.Replace && !ItemsView.Loop)
    targetPosition = _positionAfterUpdate;
else
    targetPosition = GetTargetPosition();
```

---

### Concern 7 — Test coverage too narrow — only last-item replace tested

Added a `PositionLabel` bound directly to `CarouselView.Position` in the HostApp. The existing `CurrentItemLabel` is bound to `ViewModel.CurrentItem` which is explicitly set to `"2b"` by the button handler — it always looks correct. `PositionLabel` exposes the actual scroll position which resets to `0` on Windows without the fix.

```csharp
// HostApp — PositionLabel bound to CarouselView.Position (not ViewModel)
positionLabel.SetBinding(Label.TextProperty, new Binding("Position", source: carousel));

// Test — position assertions added
var initialPosition = App.FindElement("PositionLabel").GetText();
Assert.That(initialPosition, Is.EqualTo("2"));

App.Tap("UpdateButton");

var updatedPosition = App.FindElement("PositionLabel").GetText();
Assert.That(updatedPosition, Is.EqualTo("2")); // fails on Windows without fix
```

---

### Concern 8 — Windows: `_isHandlingReplace` not `volatile`

`_isHandlingReplace` was a plain `bool`. It is written in `OnCollectionItemsSourceChanged` and read asynchronously in `MapCurrentItem` via the property mapper. Without `volatile`, the JIT can cache the value in a register and miss the write.

```csharp
// Before
bool _isHandlingReplace;

// After
volatile bool _isHandlingReplace;
```

---

### Concern 9 — Android: `isLastItem` uses `NewStartingIndex`

`NewStartingIndex` means "where the new item landed" — not "which index was replaced". For a same-index Replace both are equal by coincidence, but `OldStartingIndex` is semantically correct.

```csharp
// Before
var isLastItem = e.NewStartingIndex == count - 1;

// After
var replacedPosition = e.OldStartingIndex;
var isLastItem = replacedPosition == count - 1;
```

---

### Concern 10 — Variable name `removingCurrentElement` misleading for Replace

For Remove/Reset, `currentItemPosition == -1` means the item was removed. For Replace it is always `-1` (old item gone) — making the name actively misleading in the Replace context.

```csharp
// Before
bool removingCurrentElement = currentItemPosition == -1;
// ...
if (removingCurrentElement) { ... }

// After
// True for Remove/Reset when the current item no longer exists in the collection.
// Not meaningful for Replace — the Replace branch uses index comparison instead.
bool currentItemNotFound = currentItemPosition == -1;
// ...
if (currentItemNotFound) { ... }
```

---

## Summary Table

| # | Concern | File | Change |
|---|---------|------|--------|
| 1 | Android non-current Replace resets position | `MauiCarouselRecyclerView.cs` | Replace branch covers all Replace actions, not just `removingCurrentElement` |
| 2 | Android Replace fast-path not scoped to `Loop=false` | `MauiCarouselRecyclerView.cs` | `if (Carousel.Loop)` guard: `UpdateAdapter()` + `ScrollToPosition()` |
| 3 | `Equals()`-based detection fails for value types | `MauiCarouselRecyclerView.cs` | Index comparison: `e.OldStartingIndex == carouselPosition` |
| 4 | iOS no explicit Replace branch in `CollectionViewUpdating` | `CarouselViewController2.cs` | Explicit `if (Replace && !Loop)` branch in `CollectionViewUpdating` |
| 5 | Windows deferred scroll has no stale-work guard | `CarouselViewHandler.Windows.cs` | `_replaceScrollGeneration` counter; stale lambda discarded |
| 6 | iOS/Android `Loop=true` not guarded | Both | Android: `if (Carousel.Loop)` guard; iOS: `!ItemsView.Loop` on both branches |
| 7 | Test coverage too narrow | HostApp + test | `PositionLabel` added; position asserted in test |
| 8 | `_isHandlingReplace` not `volatile` | `CarouselViewHandler.Windows.cs` | `volatile bool _isHandlingReplace` |
| 9 | `isLastItem` used `NewStartingIndex` | `MauiCarouselRecyclerView.cs` | `replacedPosition == count - 1` using `OldStartingIndex` |
| 10 | `removingCurrentElement` misleading for Replace | `MauiCarouselRecyclerView.cs` | Renamed to `currentItemNotFound` with explanatory comment |

## Review Concerns and Code Changes

### Concern 1 — Android: Non-current item Replace still resets position

**Problem:** The initial fix only intercepted Replace when `removingCurrentElement == true` (i.e., current item not found by `Equals`). A Replace on any non-current item bypassed the fast-path and fell into the `KeepItemsInView`/`KeepLastItemInView` logic, which reset `carouselPosition` to 0.

**Fix:** Intercept **all** Replace actions at the top of `OnCollectionItemsSourceChanged`, before any Insert/Remove logic, regardless of whether the replaced item is the current item or not.

```csharp
// File: MauiCarouselRecyclerView.cs

if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
{
    var replacedPosition = e.OldStartingIndex;
    var isCurrentItemReplaced = replacedPosition == carouselPosition;
    var isLastItem = replacedPosition == count - 1;
    // ... fast-path handles all Replace regardless of which index was replaced
    return;
}
```

---

### Concern 2 — Android: Replace fast-path not scoped to `Loop=false`

**Problem:** The Replace fast-path dispatched a position-update callback for all cases, including `Loop=true`. The loopable adapter uses a virtual infinite position scheme — a simple `SetCurrentItem`/`UpdatePosition` call is insufficient; the adapter must be rebuilt.

**Fix:** Added an explicit `if (Carousel.Loop)` guard inside the Replace branch. When `Loop=true` and the current item was replaced, the adapter is rebuilt and the carousel re-scrolled to the same logical position.

```csharp
if (Carousel.Loop)
{
    // Loop mode uses a virtual infinite adapter — rebuild and re-scroll to
    // preserve the infinite scroll state when the current item is replaced.
    if (isCurrentItemReplaced)
    {
        UpdateAdapter();
        ScrollToPosition(carouselPosition);
    }
    return;
}
```

---

### Concern 3 — Android: `Equals()`-based `removingCurrentElement` fails for value types

**Problem:** `GetPosition(Carousel.CurrentItem)` uses `Equals()` to find the current item in the source. For a Replace, the old item is removed from the source before the event fires — `GetPosition()` returns `-1`, making `removingCurrentElement = true` for every Replace, even a non-current item Replace. This is also broken for value types (e.g., `int`, `string`) and objects that override `Equals()` in unexpected ways.

**Fix:** The Replace branch uses index comparison (`e.OldStartingIndex == carouselPosition`) to determine whether the current item was replaced. This is type-safe and correct for all item types.

```csharp
var replacedPosition = e.OldStartingIndex;
var isCurrentItemReplaced = replacedPosition == carouselPosition; // index-based, not Equals-based
```

The original `removingCurrentElement` variable is still used for the Remove/Reset paths below (where `Equals`-based detection is appropriate), but is not consulted in the Replace branch at all.

---

### Concern 4 — iOS: No explicit Replace branch in `CollectionViewUpdating` (implicit trap)

**Problem:** `CollectionViewUpdating` had explicit branches for Add, Remove, and Reset — but nothing for Replace. `_positionAfterUpdate` defaulted to `carouselPosition` set at the top of the method. This worked by coincidence in the normal case but was an implicit trap: any future change that conditionally overwrites `_positionAfterUpdate` for unhandled actions could silently break Replace.

**Fix:** Added an explicit Replace branch in `CollectionViewUpdating` that sets `_positionAfterUpdate` from `currentItemPosition` (authoritative) rather than relying on the implicit fallthrough. Guarded with `!ItemsView.Loop` since Loop uses `GetTargetPosition()` in `CollectionViewUpdated`.

```csharp
// File: CarouselViewController2.cs — CollectionViewUpdating

// Replace does not change item count. Explicitly set _positionAfterUpdate using
// currentItemPosition (from CurrentItem, always authoritative) so CollectionViewUpdated
// uses the correct index even if carouselPosition is transiently stale.
// Loop=true is handled by GetTargetPosition() in CollectionViewUpdated.
if (e.Action == NotifyCollectionChangedAction.Replace && !ItemsView.Loop)
{
    _positionAfterUpdate = currentItemPosition >= 0 ? currentItemPosition : carouselPosition;
}
```

---

### Concern 5 — Windows: Deferred `ScrollIntoView` has no stale-work guard

**Problem:** The Replace fast-path dispatched a `ScrollIntoView` at `DispatcherQueue.Low` priority to run after WinUI's Normal-priority container rebind. If another Replace (or a Remove/Add) fired before the Low-priority work drained from the queue, the stale scroll would execute against the wrong item, causing a visual jump.

**Fix:** Added a generation counter `_replaceScrollGeneration` incremented on each Replace. The captured generation is compared inside the deferred lambda — if the counter has advanced, the scroll is discarded.

```csharp
// File: CarouselViewHandler.Windows.cs

var capturedPosition = position;
var capturedGeneration = ++_replaceScrollGeneration;
ListViewBase?.DispatcherQueue?.TryEnqueue(
    Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
    {
        if (ListViewBase is null || !IsValidPosition(capturedPosition))
            return;
        if (_replaceScrollGeneration != capturedGeneration) // stale-work guard
            return;
        var item = GetItem(capturedPosition);
        if (item is not null)
            ListViewBase.ScrollIntoView(item, ScrollIntoViewAlignment.Leading);
    });
```

---

### Concern 6 — iOS/Android: `Loop=true` not guarded

**Android** — addressed in Concern 2 above.

**iOS** — `CollectionViewUpdated` had a `Replace` branch that used `_positionAfterUpdate` unconditionally. For `Loop=true`, the loopable adapter uses virtual/offset indices; `_positionAfterUpdate` (from `carouselPosition`) is a logical index, not a virtual one. `GetTargetPosition()` resolves the correct virtual index for Loop mode.

**Fix:** Guarded both the `CollectionViewUpdating` and `CollectionViewUpdated` Replace branches with `!ItemsView.Loop`.

```csharp
// File: CarouselViewController2.cs — CollectionViewUpdated

// For Loop=true the loopable adapter uses virtual indices, so fall through to
// GetTargetPosition() which correctly resolves the looped position.
if (e.Action == NotifyCollectionChangedAction.Replace && !ItemsView.Loop)
{
    targetPosition = _positionAfterUpdate;
}
else
{
    targetPosition = GetTargetPosition();
}
```

---

### Concern 7 — Test coverage too narrow (implementation impact only)

I have updated the test based on the suggestion. Added a `PositionLabel` (bound directly to `CarouselView.Position`) to the HostApp so the test can assert the actual visual scroll position — not just the ViewModel label which is always overwritten to `"2b"` by the button handler. The test now asserts both the initial position (`"2"`) and the post-replace position (`"2"`), which catches the Windows regression where the carousel position silently resets to `0` even though `CurrentItem` appears correct.

---

### Concern 8 — Windows: `_isHandlingReplace` not `volatile`

**Problem:** `_isHandlingReplace` was a plain `bool` field. It is written on the UI thread (inside `OnCollectionItemsSourceChanged`) and read on the UI thread (inside `MapCurrentItem`). However, `MapCurrentItem` is called asynchronously via `SetCarouselViewCurrentItem` → property mapper dispatch. Without `volatile`, the compiler or JIT is permitted to cache the field value in a register and not observe the write.

**Fix:** Declared as `volatile bool`.

```csharp
// File: CarouselViewHandler.Windows.cs

volatile bool _isHandlingReplace;
int _replaceScrollGeneration;
```

---

### Concern 9 — Android: `isLastItem` used `NewStartingIndex`

**Problem:** The initial fix computed `isLastItem` as `e.NewStartingIndex == count - 1`. For a same-index Replace, `NewStartingIndex` equals `OldStartingIndex`, so it worked. However, `NewStartingIndex` semantics are "where the new item landed", not "which index was replaced". Using `OldStartingIndex` is the correct and consistent approach.

**Fix:**

```csharp
var replacedPosition = e.OldStartingIndex;          // the index that was replaced
var isLastItem = replacedPosition == count - 1;     // use OldStartingIndex, not NewStartingIndex
```

---

### Concern 10 — Variable name `removingCurrentElement` misleading for Replace

**Problem:** `removingCurrentElement` was defined as `currentItemPosition == -1`. For Remove/Reset this is meaningful: position `-1` means the current item was removed. For Replace, the same condition is always true (old item no longer in source after Replace) — making the variable name actively misleading when read in the Replace context.

**Fix:** Renamed to `currentItemNotFound` with an explanatory comment, and updated all usage sites.

```csharp
// File: MauiCarouselRecyclerView.cs

// True for Remove/Reset when the current item no longer exists in the collection.
// Not meaningful for Replace — the Replace branch above uses index comparison instead.
bool currentItemNotFound = currentItemPosition == -1;

// ...

if (currentItemNotFound) // previously: removingCurrentElement
{
    // Remove/Reset handling
}
```

---

## Summary Table

| # | Concern | File | Change |
|---|---------|------|--------|
| 1 | Android non-current Replace resets position | `MauiCarouselRecyclerView.cs` | Replace branch covers all Replace actions, not just `removingCurrentElement` |
| 2 | Android Replace fast-path not scoped to `Loop=false` | `MauiCarouselRecyclerView.cs` | `if (Carousel.Loop)` guard: `UpdateAdapter()` + `ScrollToPosition()` |
| 3 | `Equals()`-based detection fails for value types | `MauiCarouselRecyclerView.cs` | Index comparison: `e.OldStartingIndex == carouselPosition` |
| 4 | iOS no explicit Replace branch in `CollectionViewUpdating` | `CarouselViewController2.cs` | Explicit `if (Replace && !Loop)` branch in `CollectionViewUpdating` |
| 5 | Windows deferred scroll has no stale-work guard | `CarouselViewHandler.Windows.cs` | `_replaceScrollGeneration` counter; stale lambda discarded |
| 6 | iOS/Android `Loop=true` not guarded | Both | Android: `if (Carousel.Loop)` guard; iOS: `!ItemsView.Loop` on both branches |
| 7 | Test coverage too narrow | HostApp + test | `PositionLabel` added; position asserted in test (see test doc) |
| 8 | `_isHandlingReplace` not `volatile` | `CarouselViewHandler.Windows.cs` | `volatile bool _isHandlingReplace` |
| 9 | `isLastItem` used `NewStartingIndex` | `MauiCarouselRecyclerView.cs` | `replacedPosition == count - 1` using `OldStartingIndex` |
| 10 | `removingCurrentElement` misleading for Replace | `MauiCarouselRecyclerView.cs` | Renamed to `currentItemNotFound` with explanatory comment |

