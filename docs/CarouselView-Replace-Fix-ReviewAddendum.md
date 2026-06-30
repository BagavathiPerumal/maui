# CarouselView Replace Fix — All Review Concerns (Issue #35643)

This document is the complete record of all review concerns raised against the fix for [GitHub Issue #35643](https://github.com/dotnet/maui/issues/35643) — CarouselView position reset on ObservableCollection Replace — across two rounds of review. Each concern includes the problem statement, root cause analysis, and the exact code change applied.

---

## Issue Summary

**Bug:** When a Replace operation (`collection[i] = newItem`) is performed on a `CarouselView`-bound `ObservableCollection`, the carousel incorrectly resets its visual position — snapping to index 0 — on Android, iOS/macOS, and Windows.

**Root cause:** No platform had an explicit `Replace` path. All three platforms let `Replace` fall through into their `Add`/`Remove`/`Reset` logic, which applies scroll-offset policies (`KeepItemsInView`, `KeepLastItemInView`) that are designed for count-changing operations. Replace does not change count; these policies reset the position anyway.

---

## Files Changed

| File | Platform |
|------|----------|
| `src/Controls/src/Core/Handlers/Items/Android/MauiCarouselRecyclerView.cs` | Android |
| `src/Controls/src/Core/Handlers/Items2/iOS/CarouselViewController2.cs` | iOS / macOS |
| `src/Controls/src/Core/Handlers/Items/CarouselViewHandler.Windows.cs` | Windows |
| `src/Controls/src/Core/Handlers/Items/ItemsViewHandler.Windows.cs` | Windows |
| `src/Controls/tests/TestCases.HostApp/Issues/Issue35643.cs` | All (host app) |
| `src/Controls/tests/TestCases.Shared.Tests/Tests/Issues/Issue35643.cs` | All (test cases) |

---

## Concern 1 — Android: Non-current item Replace still resets position

### Problem

The initial fix only handled Replace when `removingCurrentElement == true`. A Replace on a **non-current** item (e.g., replacing index 0 while viewing index 2) bypassed the fast-path entirely and fell into the `KeepItemsInView` block:

```csharp
// KeepItemsInView designed for Add/Remove — wrong for Replace
else if (Carousel.ItemsUpdatingScrollMode == ItemsUpdatingScrollMode.KeepItemsInView)
{
    carouselPosition = 0;  // ← resets to 0 for every Replace of a non-current item
}
```

### Fix

Intercept **all** Replace actions at the top of `CollectionItemsSourceChanged`, before any Insert/Remove logic, regardless of which index was replaced:

```csharp
// File: MauiCarouselRecyclerView.cs

if (e.Action == NotifyCollectionChangedAction.Replace)
{
    HandleReplaceAction(e, carouselPosition, count, savedScrollToCounter);
    return;  // ← never reaches KeepItemsInView/KeepLastItemInView
}
```

---

## Concern 2 — Android: Replace fast-path not scoped to `Loop=false`

### Problem

The initial Replace fast-path dispatched a `SetCurrentItem`/`UpdatePosition` callback for all cases, including `Loop=true`. The loopable adapter (`CarouselViewLoopManager`) uses a virtual infinite position scheme with `LoopScale` (16,384) virtual entries. A simple position-update call is insufficient for loop mode — the adapter must be fully rebuilt.

The initial code only called `UpdateAdapter()` when the **current** item was replaced:

```csharp
if (Carousel.Loop)
{
    if (isCurrentItemReplaced)   // ← only rebuilds for current item
    {
        UpdateAdapter();
        ScrollToPosition(carouselPosition);
    }
    return;
}
```

This meant replacing a non-current item in loop mode left stale data at the replaced index throughout the virtual 16,384-entry range. The stale data would become visible when the user scrolled past that index.

### Fix

`UpdateAdapter()` is called for **every Replace in loop mode**, not only current-item replacements. `ScrollToPosition` is still called only for current-item replacements:

```csharp
if (Carousel.Loop)
{
    // Any Replace requires a full rebuild — the loop manager replicates all items
    // across the virtual scroll range; stale data at any index will appear when scrolled to.
    UpdateAdapter();
    if (isCurrentItemReplaced)
    {
        ScrollToPosition(carouselPosition);
    }
    _isInternalPositionUpdate = false;
    return;
}
```

---

## Concern 3 — Android: `Equals()`-based `removingCurrentElement` fails for value types

### Problem

`GetPosition(Carousel.CurrentItem)` uses `Equals()` to locate the current item in the source. For a Replace, the old item is removed from the source **before** the event fires — `GetPosition()` always returns `-1`, making `removingCurrentElement = true` for every Replace, including non-current item replacements.

Additionally, value types (e.g., `int`, `string`) and objects with custom `Equals()` overrides can produce false positives, making the detection fundamentally unreliable for Replace.

### Fix

The Replace branch uses **index comparison** (`e.OldStartingIndex == carouselPosition`) instead of `Equals`-based lookup. This is type-safe and correct for all item types:

```csharp
var replacedPosition = e.OldStartingIndex;
var isCurrentItemReplaced = replacedPosition == carouselPosition;  // index-based, not Equals-based
```

The `currentItemNotFound` variable (renamed from `removingCurrentElement` — see Concern 10) is still used for the Remove/Reset paths where `Equals`-based detection is appropriate, but is never consulted in the Replace branch.

---

## Concern 4 — iOS: No explicit Replace branch in `CollectionViewUpdating`

### Problem

`CollectionViewUpdating` had explicit branches for Add, Remove, and Reset — but nothing for Replace. `_positionAfterUpdate` silently defaulted to `carouselPosition` from the top of the method. This was an implicit trap: any future change that conditionally overwrites `_positionAfterUpdate` for unhandled actions could silently break Replace with no compile-time warning.

### Fix

Added an explicit Replace branch in `CollectionViewUpdating` that sets `_positionAfterUpdate` from `currentItemPosition` (authoritative, always reflects `CurrentItem`) rather than relying on the implicit fallthrough. Guarded with `!ItemsView.Loop` since Loop mode uses `GetTargetPosition()` in `CollectionViewUpdated`:

```csharp
// File: CarouselViewController2.cs — CollectionViewUpdating

// Replace does not change item count. Explicitly set _positionAfterUpdate using
// currentItemPosition (from CurrentItem, always authoritative) so CollectionViewUpdated
// uses the correct index even if carouselPosition is transiently stale.
if (e.Action == NotifyCollectionChangedAction.Replace && !ItemsView.Loop)
{
    _positionAfterUpdate = currentItemPosition >= 0 ? currentItemPosition : carouselPosition;
}
```

---

## Concern 5 — Windows: Deferred `ScrollIntoView` has no stale-work guard

### Problem

The Windows Replace fast-path enqueued a `ScrollIntoView` at `DispatcherQueue.Low` priority to run after WinUI's Normal-priority container rebind. If another Replace (or Remove/Add) fired before the Low-priority work drained, the stale lambda would scroll to the position from the earlier Replace, causing a visual jump.

### Fix

Added a generation counter `_replaceScrollGeneration` incremented on each Replace. The captured generation is checked inside the deferred lambda — if the counter has advanced, the scroll is discarded:

```csharp
// File: CarouselViewHandler.Windows.cs

var capturedPosition = position;
var capturedGeneration = ++_replaceScrollGeneration;
ListViewBase?.DispatcherQueue?.TryEnqueue(
    Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
    {
        if (ListViewBase is null || !IsValidPosition(capturedPosition))
            return;
        if (_replaceScrollGeneration != capturedGeneration)  // stale-work guard
            return;
        var item = GetItem(capturedPosition);
        if (item is not null)
            ListViewBase.ScrollIntoView(item, ScrollIntoViewAlignment.Leading);
    });
```

---

## Concern 6 — iOS/Android: `Loop=true` not guarded in `CollectionViewUpdated`

**Android** — addressed in Concern 2.

### Problem (iOS)

`CollectionViewUpdated` had a Replace branch that assigned `targetPosition = _positionAfterUpdate` unconditionally. For `Loop=true`, the loopable adapter uses virtual/offset indices. `_positionAfterUpdate` holds a logical index (e.g., `1`), but the adapter expects a virtual index resolved by `GetTargetPosition()`. Using the logical index directly causes the carousel to snap to the wrong virtual position.

### Fix

Guarded the `CollectionViewUpdated` Replace branch with `!ItemsView.Loop`. Loop mode falls through to `GetTargetPosition()` which correctly resolves the virtual index:

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

## Concern 7 — Test coverage too narrow (Replace current item only)

### Problem

The original test only covered one scenario: replace the **last item** which is also the **current item**, with `Loop=false`. Three important scenarios had no coverage:

| Scenario | Failure mode without coverage |
|----------|-------------------------------|
| Replace a non-current item (Loop=false) | Position resets to 0 via `KeepItemsInView` |
| Replace the current item (Loop=true) | Adapter not rebuilt → stale data when scrolling |
| `PositionLabel` not bound to `CarouselView.Position` | Windows position reset undetectable |

The original `CurrentItemLabel` was bound to `ViewModel.CurrentItem`, which the button handler explicitly sets to `"2b"` — it always looks correct. The actual visual scroll position on Windows was silently resetting to `0`.

### Fix

Added a `PositionLabel` bound **directly** to `CarouselView.Position` (not the ViewModel). Added two new test methods and a second Loop=true carousel in the HostApp:

```csharp
// HostApp — PositionLabel bound to CarouselView.Position (not ViewModel)
positionLabel.SetBinding(Label.TextProperty, new Binding("Position", source: carousel));

// Test — position asserted independently of CurrentItem
var updatedPosition = App.FindElement("PositionLabel").GetText();
Assert.That(updatedPosition, Is.EqualTo("2"));  // fails on Windows without fix
```

**New test: Replace non-current item (Loop=false)**
```csharp
public void PositionShouldNotChangeWhenNonCurrentItemIsReplaced()
// Items=["0","1","2"], CurrentItem="2", Position=2
// Tap ReplaceNonCurrentButton → Items[0]="0b"
// Assert: CurrentItemLabel="2", PositionLabel="2"
```

**New test: Replace current item (Loop=true)**
```csharp
public void PositionShouldNotChangeWhenCurrentItemIsReplacedWithLoopEnabled()
// LoopItems=["A","B","C"], LoopCurrentItem="B", Position=1
// App.ScrollTo("LoopReplaceButton")  ← required: element is below viewport
// Tap LoopReplaceButton → LoopItems[1]="B2", LoopCurrentItem="B2"
// Assert: LoopCurrentItemLabel="B2", LoopPositionLabel="1"
```

The `App.ScrollTo` call is necessary because Appium's UIAutomator2 driver only locates elements present in the accessibility hierarchy — off-screen elements are not accessible until scrolled into view.

**Test results on Android:**

| Test | Outcome |
|------|---------|
| `CurrentItemShouldUpdateWhenCurrentItemIsReplaced` | ✅ PASSED |
| `PositionShouldNotChangeWhenNonCurrentItemIsReplaced` | ✅ PASSED |
| `PositionShouldNotChangeWhenCurrentItemIsReplacedWithLoopEnabled` | ✅ PASSED |

---

## Concern 8 — Windows: `_isHandlingReplace` not `volatile`

### Problem

`_isHandlingReplace` was declared as a plain `bool`. It is written in `OnCollectionItemsSourceChanged` and read asynchronously in `MapCurrentItem` via the property mapper dispatch chain. Without `volatile`, the C# compiler and JIT are permitted to cache the field value in a register and not see the write made in the previous call frame, causing `MapCurrentItem` to proceed with a stale `false` value and subscribe to `ScrollViewer.ViewChanged` — triggering the incorrect position snap it was designed to suppress.

### Fix

```csharp
// File: CarouselViewHandler.Windows.cs

// Before
bool _isHandlingReplace;

// After
volatile bool _isHandlingReplace;
int _replaceScrollGeneration;
```

---

## Concern 9 — Android: `isLastItem` used `NewStartingIndex`

### Problem

`isLastItem` was computed as `e.NewStartingIndex == count - 1`. For a same-index Replace, `NewStartingIndex` equals `OldStartingIndex` by coincidence, so the result was correct. However, `NewStartingIndex` semantics are *"where the new item landed"* — not *"which index was replaced"*. Using `OldStartingIndex` is the semantically correct and consistent choice.

### Fix

```csharp
// Before
var isLastItem = e.NewStartingIndex == count - 1;

// After
var replacedPosition = e.OldStartingIndex;          // the index that was replaced
var isLastItem = replacedPosition == count - 1;     // semantically correct
```

---

## Concern 10 — Android: Variable name `removingCurrentElement` misleading for Replace

### Problem

`removingCurrentElement` was defined as `currentItemPosition == -1`. For Remove/Reset, this is meaningful: position `-1` means the current item is no longer in the collection. For Replace, `currentItemPosition` is **always** `-1` (old item is gone from source before the event fires) — making the name actively misleading in the Replace context.

### Fix

Renamed to `currentItemNotFound` with an explanatory comment. The Replace branch is explicitly excluded from using it:

```csharp
// File: MauiCarouselRecyclerView.cs

// True for Remove/Reset when the current item no longer exists in the collection.
// Not meaningful for Replace — the Replace branch uses index comparison instead.
bool currentItemNotFound = currentItemPosition == -1;

// ...

if (currentItemNotFound)  // previously: removingCurrentElement
{
    // Remove/Reset handling — unchanged
}
```

---

## Concern 11 — Android: Dispatcher null handling inconsistent across paths (post-review)

### Problem

After the initial fix, the Replace path obtained the dispatcher with null-safe operators and **silently skipped** the visual-state update with only a `Debug.WriteLine` if null:

```csharp
// Replace path — silent skip
var dispatcher = Carousel.Handler?.MauiContext?.GetDispatcher();
if (dispatcher is null)
{
    System.Diagnostics.Debug.WriteLine("[CarouselView] dispatcher is null — skipping");
    _isInternalPositionUpdate = false;
    return;
}
dispatcher.Dispatch(() => { ... });
```

The Add/Remove/Reset path just below it used **no null-safety**:

```csharp
// Add/Remove/Reset path — crashes on null
Carousel.Handler.MauiContext.GetDispatcher().Dispatch(() => { ... });
```

This inconsistency meant:
- For **Add/Remove/Reset**: null Handler → `NullReferenceException` → bug is visible and reported.
- For **Replace**: null Handler → silent skip → `CurrentItem` and `Position` visual states become inconsistent with no trace.

The `Debug.WriteLine` treated the null as an expected/acceptable condition, when it is actually a sign of a **teardown race** (collection-change event fires on a background thread after `TearDownOldElement` has started). Papering over it for Replace while crashing for Add/Remove/Reset is the wrong tradeoff.

### Fix

The null check was **moved to the entry point** of `CollectionItemsSourceChanged`, protecting all paths uniformly. The inline check in `HandleReplaceAction` was removed:

```csharp
// File: MauiCarouselRecyclerView.cs — CollectionItemsSourceChanged

void CollectionItemsSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
{
    if (!(ItemsViewAdapter.ItemsSource is IItemsViewSource observableItemsSource))
        return;

    _isInternalPositionUpdate = true;

    // Guard: Handler or MauiContext may be null during a teardown race (a background-thread
    // collection change firing after TearDownOldElement begins). Reset the flag before
    // returning so future scroll interactions are not permanently blocked.
    // All paths below assume Handler and MauiContext are non-null.
    if (Carousel.Handler?.MauiContext is null)
    {
        _isInternalPositionUpdate = false;
        return;
    }

    // ... rest of method — Handler/MauiContext guaranteed non-null from here
```

The Replace path in `HandleReplaceAction` now uses the same direct call as Add/Remove/Reset:

```csharp
// HandleReplaceAction — after fix (no inline null check needed)
// Handler and MauiContext are guaranteed non-null — entry-point guard handles it.
Carousel.Handler.MauiContext.GetDispatcher().Dispatch(() =>
{
    // ...
});
```

### Why This Is Better Than `Debug.WriteLine`

| Approach | Behaviour when Handler is null |
|----------|-------------------------------|
| `Debug.WriteLine` + silent return (initial fix) | Replace: visual state silently inconsistent |
| No guard (original Add/Remove/Reset path) | NullReferenceException — crashes, bug surfaced |
| **Single entry-point null guard (final fix)** | All paths: clean early return, flag always reset, consistent behaviour |

---

## Concern 12 — Android: `HandleReplaceAction` extraction improves readability

### Problem

The Replace logic was inlined inside `CollectionItemsSourceChanged`, which was already a long method handling Add, Remove, Reset, and visual state updates. The Replace block added ~60 lines inline, making the method difficult to review.

### Fix

The Replace block was extracted into `HandleReplaceAction(...)`:

```csharp
// CollectionItemsSourceChanged — after extraction
if (e.Action == NotifyCollectionChangedAction.Replace)
{
    HandleReplaceAction(e, carouselPosition, count, savedScrollToCounter);
    return;
}
// Add/Remove/Reset handling only below this point
```

Benefits:
- `CollectionItemsSourceChanged` now reads as a dispatch table — each action is a one-liner.
- `HandleReplaceAction` can be reviewed, tested, and modified independently.
- Future maintainers editing Add/Remove/Reset logic cannot accidentally affect Replace.

---

## Concern 13 — Confirm dispatcher-null is an expected state

### Analysis

The reviewer asked: *"Can `CollectionItemsSourceChanged` fire before handler attachment? Is dispatcher-null a valid runtime state?"*

**Before handler attachment:** The subscription is set up in `SubscribeCollectionItemsSourceChanged`, which is called from `UpdateAdapter()`, which is called from `SetUpNewElement`. `SetUpNewElement` is only called after the handler is attached. So pre-attachment firing is not possible through the normal lifecycle.

**After handler detachment (teardown race):** `TearDownOldElement` calls `UnsubscribeCollectionItemsSourceChanged` before nulling the handler. However, there is a brief window between the unsubscribe and `Handler` being set to null where a background-thread event could have already been captured by the dispatcher and is pending delivery. This is the genuine race window.

**Conclusion:** Dispatcher-null is **not** an expected state in normal lifecycle operation. It is a sign of a teardown race. The single entry-point null guard (Concern 11) handles this correctly and consistently. Logging it as an error-level diagnostic (or asserting in debug builds) would be appropriate if the race becomes frequent; the current guard is a safe minimum for now.

---

## Complete Summary Table

| # | Concern | Platform | File | Change |
|---|---------|----------|------|--------|
| 1 | Non-current Replace resets position | Android | `MauiCarouselRecyclerView.cs` | Intercept all Replace before Add/Remove logic |
| 2 | Loop=true adapter not rebuilt for non-current Replace | Android | `MauiCarouselRecyclerView.cs` | `UpdateAdapter()` for every Replace in loop mode |
| 3 | `Equals()`-based detection unreliable for Replace | Android | `MauiCarouselRecyclerView.cs` | Index comparison: `e.OldStartingIndex == carouselPosition` |
| 4 | No explicit Replace branch in `CollectionViewUpdating` | iOS/macOS | `CarouselViewController2.cs` | Explicit `if (Replace && !Loop)` sets `_positionAfterUpdate` |
| 5 | Deferred `ScrollIntoView` has no stale-work guard | Windows | `CarouselViewHandler.Windows.cs` | `_replaceScrollGeneration` counter discards stale lambdas |
| 6 | `Loop=true` not guarded in `CollectionViewUpdated` | iOS/macOS | `CarouselViewController2.cs` | `!ItemsView.Loop` guard; Loop falls through to `GetTargetPosition()` |
| 7 | Test coverage too narrow | All | HostApp + test project | `PositionLabel`, `ReplaceNonCurrentButton`, `LoopReplaceButton`; 2 new tests |
| 8 | `_isHandlingReplace` not `volatile` | Windows | `CarouselViewHandler.Windows.cs` | `volatile bool _isHandlingReplace` |
| 9 | `isLastItem` used `NewStartingIndex` | Android | `MauiCarouselRecyclerView.cs` | `OldStartingIndex == count - 1` |
| 10 | `removingCurrentElement` misleading for Replace | Android | `MauiCarouselRecyclerView.cs` | Renamed to `currentItemNotFound` with comment |
| 11 | Dispatcher null handling inconsistent across paths | Android | `MauiCarouselRecyclerView.cs` | Single entry-point null guard in `CollectionItemsSourceChanged` |
| 12 | Replace logic inlined in long method | Android | `MauiCarouselRecyclerView.cs` | Extracted to `HandleReplaceAction(...)` |
| 13 | Is dispatcher-null a valid runtime state? | Android | `MauiCarouselRecyclerView.cs` | Analysis: teardown race; entry-point guard is correct mitigation |

### Problem

The `HandleReplaceAction` method obtained the dispatcher with null-safe operators and then **silently skipped** the entire visual-state update if it was null:

```csharp
var dispatcher = Carousel.Handler?.MauiContext?.GetDispatcher();
if (dispatcher is null)
{
    // Only a Debug.WriteLine — visual state silently left inconsistent
    _isInternalPositionUpdate = false;
    return;
}
dispatcher.Dispatch(() => { ... });
```

The fundamental issue was **inconsistency within the same method**. The Add/Remove/Reset path just below it used no null-safety at all:

```csharp
Carousel.
    Handler.           // ← NullReferenceException if Handler is null
    MauiContext.
    GetDispatcher()
        .Dispatch(() => { ... });
```

This meant:
- For **Add/Remove/Reset**: a null Handler crashes immediately — the bug is visible.
- For **Replace**: a null Handler silently skips — `CurrentItem` and `Position` visual states become inconsistent with no trace, no crash, and no indication of what happened.

Even with a `Debug.WriteLine`, **the root cause** (why is Handler null while an active collection change fires?) is never surfaced or handled. It just papers over the symptom for the Replace path while leaving Add/Remove/Reset vulnerable.

### Why Dispatcher Can Be Null

`CollectionItemsSourceChanged` is subscribed when the adapter is set up and unsubscribed in `TearDownOldElement`. There is a genuine **teardown race window**:

1. User initiates navigation away from the page (begins teardown).
2. A background thread fires a collection change (e.g., from an async data refresh).
3. `TearDownOldElement` has already nulled out `Handler`/`MauiContext`.
4. `CollectionItemsSourceChanged` fires on the UI thread with a stale adapter reference.

This is a pre-existing architectural issue — not introduced by this fix. The correct response is to **guard once at the entry point** of the method so all code paths are consistently protected.

### Fix — Single Guard at Method Entry

The null check was moved to the **top of `CollectionItemsSourceChanged`**, before any branching:

```csharp
void CollectionItemsSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
{
    if (!(ItemsViewAdapter.ItemsSource is IItemsViewSource observableItemsSource))
        return;

    // Set flag to disable animation during collection changes
    _isInternalPositionUpdate = true;

    // Guard: handler or MauiContext may be null during a teardown race (a background-thread
    // collection change firing after TearDownOldElement begins). Reset the flag before
    // bailing out so future scroll interactions are not permanently blocked. All code paths
    // below assume Handler and MauiContext are non-null; a single guard here is preferable
    // to inconsistent null-checks scattered across individual paths.
    if (Carousel.Handler?.MauiContext is null)
    {
        _isInternalPositionUpdate = false;
        return;
    }

    // ... rest of method — Handler and MauiContext guaranteed non-null below here
```

The inline null-check inside `HandleReplaceAction` was removed. The Replace path now matches the Add/Remove/Reset path — both rely on the single entry-point guard:

```csharp
// HandleReplaceAction — after fix
// Handler and MauiContext are guaranteed non-null here — CollectionItemsSourceChanged
// guards for null at its entry point and returns early.
Carousel.Handler.MauiContext.GetDispatcher().Dispatch(() =>
{
    // ...
});
```

### Why This Is Better Than Debug.WriteLine

| Approach | What Happens When Handler Is Null |
|----------|-----------------------------------|
| `Debug.WriteLine` + silent return | Visual state silently inconsistent; race condition masked |
| Single entry-point null guard | All paths consistently protected; `_isInternalPositionUpdate` always reset |
| No guard (original Add/Remove path) | NullReferenceException — bug visible but app crashes |

The single entry-point guard is the right pattern: it is **consistent**, **visible** (returns early cleanly), and **safe** (resets the `_isInternalPositionUpdate` flag so future interactions are not blocked).

---

## Concern 2 — Loop Mode Adapter Rebuild Incomplete (Android)

### Problem

The loop mode guard inside `HandleReplaceAction` only called `UpdateAdapter()` when the **current item** was replaced:

```csharp
if (Carousel.Loop)
{
    if (isCurrentItemReplaced)   // ← only rebuilds for current item
    {
        UpdateAdapter();
        ScrollToPosition(carouselPosition);
    }
    _isInternalPositionUpdate = false;
    return;
}
```

The comment stated: *"Loop mode uses a virtual infinite adapter."*

The loopable adapter (`CarouselViewLoopManager`) replicates **all** items across a virtual scroll range of `LoopScale` (16,384 by default) entries. When item at index 0 is replaced and the user is currently at index 2, the adapter still has a stale copy of index 0 replicated throughout its virtual range. When the user subsequently scrolls back to index 0, they see the old data.

### Fix

`UpdateAdapter()` is now called for **every Replace in loop mode**, not only current-item replacements:

```csharp
if (Carousel.Loop)
{
    // Loop mode uses a virtual infinite adapter. Any Replace — not only
    // current-item replacements — requires a full adapter rebuild because
    // the loop manager replicates all items across the virtual scroll range;
    // stale data at any index will appear when the user scrolls past it.
    UpdateAdapter();
    if (isCurrentItemReplaced)
    {
        ScrollToPosition(carouselPosition);
    }
    _isInternalPositionUpdate = false;
    return;
}
```

`ScrollToPosition` is only called when the **current item** was replaced, to re-anchor the infinite scroll to the correct virtual position. For non-current item replacements the visual position does not change, so no re-scroll is needed.

---

## Concern 3 — Test Coverage Gaps

### Problem

The original test only covered one scenario: **replace the current item (Loop=false, last index)**. Three other important scenarios had no test coverage:

| Scenario | Risk Without Coverage |
|----------|----------------------|
| Replace a **non-current** item (Loop=false) | Position could reset to 0 via `KeepItemsInView` |
| Replace the current item (Loop=true) | Adapter not rebuilt → stale data on scroll |
| Loop carousel scroll brings element into view | Appium can't find off-screen elements |

### Fix — Three Tests Added

**Test 1 (existing, already passing):** Replace current item, Loop=false

```csharp
public void CurrentItemShouldUpdateWhenCurrentItemIsReplaced()
// Items=["0","1","2"], CurrentItem="2", Position=2
// Tap UpdateButton → Items[2]="2b", CurrentItem="2b"
// Assert: CurrentItemLabel="2b", PositionLabel="2"
```

**Test 2 (new):** Replace non-current item, Loop=false

```csharp
public void PositionShouldNotChangeWhenNonCurrentItemIsReplaced()
// Items=["0","1","2"], CurrentItem="2", Position=2
// Tap ReplaceNonCurrentButton → Items[0]="0b"
// Assert: CurrentItemLabel="2" (unchanged), PositionLabel="2" (not reset to 0)
```

**Test 3 (new):** Replace current item, Loop=true

```csharp
public void PositionShouldNotChangeWhenCurrentItemIsReplacedWithLoopEnabled()
// LoopItems=["A","B","C"], LoopCurrentItem="B", Position=1
// Tap LoopReplaceButton → LoopItems[1]="B2", LoopCurrentItem="B2"
// Assert: LoopCurrentItemLabel="B2", LoopPositionLabel="1"
```

### Host App — Two Separate Carousels

The host app now renders two distinct CarouselView sections on the same page inside a `ScrollView`:

| Section | `Loop` | `AutomationId` | Purpose |
|---------|--------|----------------|---------|
| Top | `false` | `CarouselView` | Non-loop replace tests |
| Bottom | `true` | `LoopCarouselView` | Loop replace test |

The loop carousel is initially **below the visible viewport**. Appium's UIAutomator2 driver only locates elements that exist in the accessibility hierarchy — off-screen elements may not be present. The loop test uses `App.ScrollTo("LoopReplaceButton")` to bring the section into view before asserting:

```csharp
App.ScrollTo("LoopReplaceButton");   // bring loop section into viewport
App.WaitForElement("LoopCurrentItemLabel");
```

### Test Results (Android)

| Test | Outcome |
|------|---------|
| `CurrentItemShouldUpdateWhenCurrentItemIsReplaced` | ✅ PASSED |
| `PositionShouldNotChangeWhenNonCurrentItemIsReplaced` | ✅ PASSED |
| `PositionShouldNotChangeWhenCurrentItemIsReplacedWithLoopEnabled` | ✅ PASSED |

---

## Files Changed (This Addendum)

| File | Change |
|------|--------|
| `src/Controls/src/Core/Handlers/Items/Android/MauiCarouselRecyclerView.cs` | Single null guard at entry point; `UpdateAdapter()` for all Loop replaces |
| `src/Controls/tests/TestCases.HostApp/Issues/Issue35643.cs` | Second carousel (Loop=true); `ReplaceNonCurrentButton`; `LoopReplaceButton` |
| `src/Controls/tests/TestCases.Shared.Tests/Tests/Issues/Issue35643.cs` | Two new test methods + `App.ScrollTo` for loop section |

---

## Architectural Note — Pre-existing Teardown Race

The dispatcher null condition is a symptom of a broader pattern in `MauiCarouselRecyclerView`: collection-change events are subscribed on the adapter, and the adapter lives slightly longer than the handler during teardown. The correct long-term fix would be to unsubscribe `CollectionItemsSourceChanged` *before* the handler is detached, or to use a cancellation token tied to the handler lifecycle. The null guard added here is a safe and consistent short-term mitigation that is consistent with how the framework handles similar teardown races elsewhere.

---

## Reviewer's Suggested Changes (Round 3)

These changes were applied to `MauiCarouselRecyclerView.cs` based on reviewer feedback after the second round of review.

---

### Change 1 — Replace check moved before boolean flag declarations

**Problem:** The Replace guard was placed *after* the Remove/Insert boolean flags (`removingLastElement`, `removingFirstElement`, `removingAnyPrevious`) and the `_noNeedForScroll`/`_gotoPosition` resets. These flags are only meaningful for Remove/Insert — computing them before a `Replace` early-exit was wasteful and misleading to future readers.

**Fix:** The Replace check is now the **first branch** after the variable declarations, before any flags are computed:

```csharp
// File: MauiCarouselRecyclerView.cs — CollectionItemsSourceChanged

var carouselPosition = Carousel.Position;
var currentItemPosition = observableItemsSource.GetPosition(Carousel.CurrentItem);
var count = observableItemsSource.Count;
var savedScrollToCounter = _scrollToCounter;

// Replace exits here — before Remove/Insert flags are computed
if (e.Action == NotifyCollectionChangedAction.Replace)
{
    HandleReplaceAction(e, carouselPosition, count, savedScrollToCounter);
    return;
}

bool removingCurrentElement = currentItemPosition == -1;
bool removingLastElement = ...
// _noNeedForScroll and _gotoPosition set below — only reached for Remove/Insert/Reset
```

---

### Change 2 — Variable rename: `currentItemNotFound` → `removingCurrentElement`

**Problem:** After the Replace branch was moved above the flag declarations, the variable name `currentItemNotFound` no longer needed to be renamed away from `removingCurrentElement` — the Replace path never reaches it. The name `removingCurrentElement` is accurate for the Remove/Reset context where the variable is used: `currentItemPosition == -1` means the item was removed from the collection.

**Fix:** Reverted the rename back to `removingCurrentElement`:

```csharp
// Accurate for Remove/Reset — the current item no longer exists because it was removed
bool removingCurrentElement = currentItemPosition == -1;
```

---

### Change 3 — Loop mode: `UpdateAdapter()` replaced by `RebindVisibleLoopItem()` (new method)

**Problem:** The loop mode branch in `HandleReplaceAction` called `UpdateAdapter()` — a full adapter rebuild. This caused:
- A visible **flash to position 0** before restoring the carousel to the correct position
- Spurious `PositionChanged` and `CurrentItemChanged` events firing with incorrect values (0 / items[0])
- `ScrollToPosition()` required to recover — causing an unnecessary scroll jump

**Root cause:** `UpdateAdapter()` internally calls `UpdateInitialPosition()` which resets `Position = 0` and `CurrentItem = null` before re-initialising from scratch.

**Fix:** Added a new `RebindVisibleLoopItem(int changedIndex, int itemCount)` method that surgically rebinds only the visible virtual cells that map to the replaced real index:

```csharp
// File: MauiCarouselRecyclerView.cs

void RebindVisibleLoopItem(int changedIndex, int itemCount)
{
    // ... guard checks ...

    var firstVisibleItemPosition = layoutManager.FindFirstVisibleItemPosition();
    var lastVisibleItemPosition  = layoutManager.FindLastVisibleItemPosition();

    for (int virtualPosition = firstVisibleItemPosition;
             virtualPosition <= lastVisibleItemPosition;
             virtualPosition++)
    {
        if (virtualPosition % itemCount == changedIndex)
            adapter.NotifyItemChanged(virtualPosition);
    }
}
```

In loop mode, visible cells live at virtual positions where `virtualPosition % itemCount == realIndex`. A plain `NotifyItemChanged(realIndex)` never reaches them. This method walks only visible cells — typically 1 to 3 — and notifies the correct virtual positions. Off-screen cells pick up the new value naturally when scrolled into view.

**`HandleReplaceAction` loop path — after fix:**

```csharp
if (Carousel.Loop)
{
    RebindVisibleLoopItem(e.OldStartingIndex, count);  // targeted rebind, no full rebuild
    Carousel.Handler.MauiContext.GetDispatcher().Dispatch(() =>
    {
        if (_scrollToCounter == savedScrollToCounter)
        {
            SetCurrentItem(carouselPosition);
            UpdateVisualStates();
            // ScrollToPosition intentionally omitted — would jump to start of virtual range
        }
        _isInternalPositionUpdate = false;
    });
    return;
}
```

---

### Change 4 — Removed unnecessary `UpdateItemDecoration()` for replaced last item

**Problem:** `HandleReplaceAction` called `UpdateItemDecoration()` when the replaced index was the last item (`isLastItem == true`). `UpdateItemDecoration` recalculates spacing insets used to centre items in the carousel. These insets only need updating when the **item count changes** (Add/Remove). Replace never changes the count — the insets are always unchanged.

**Fix:** `UpdateItemDecoration()` and the `isLastItem` variable were removed from `HandleReplaceAction`:

```csharp
// Removed — not needed for Replace (count unchanged, insets unchanged)
// var isLastItem = replacedPosition == count - 1;
// if (isLastItem) { UpdateItemDecoration(); }
```

---

### Change 5 — Variable naming in `RebindVisibleLoopItem`: `first`/`last` → `firstVisibleItemPosition`/`lastVisibleItemPosition`

**Problem:** `first` and `last` were too generic — a reader had to look at the assignment to understand they represent visible virtual positions.

**Fix:** Renamed to mirror the method names they are assigned from:

```csharp
var firstVisibleItemPosition = layoutManager.FindFirstVisibleItemPosition();
var lastVisibleItemPosition  = layoutManager.FindLastVisibleItemPosition();
```

---

### Summary of Round 3 Changes

| # | Change | File | Detail |
|---|--------|------|--------|
| 1 | Replace check moved before boolean flags | `MauiCarouselRecyclerView.cs` | Early exit before Remove/Insert flags are computed |
| 2 | `currentItemNotFound` → `removingCurrentElement` | `MauiCarouselRecyclerView.cs` | Accurate name for Remove/Reset context |
| 3 | `UpdateAdapter()` → `RebindVisibleLoopItem()` | `MauiCarouselRecyclerView.cs` | New method; no full rebuild, no flash, no spurious events |
| 4 | Removed `UpdateItemDecoration()` for last item | `MauiCarouselRecyclerView.cs` | Unnecessary — Replace never changes item count |
| 5 | `first`/`last` → `firstVisibleItemPosition`/`lastVisibleItemPosition` | `MauiCarouselRecyclerView.cs` | Mirrors `FindFirstVisibleItemPosition()` method names |
