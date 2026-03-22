# QA Review Prompt — Penalty Shootout (.NET MAUI)

You are a senior QA engineer reviewing a completed .NET MAUI game called **Penalty Shootout**. Your job is to perform a thorough code review, logic audit, and consistency check across the entire codebase. Report every bug, inconsistency, missing feature, and potential runtime failure you find.

---

## Project Context

This is a penalty kick game where the player swipes to aim and shoot at a goal defended by an AI goalkeeper. It runs for 5 rounds. The app uses:

- **.NET 8+ MAUI** targeting Android, iOS, Windows, macOS
- **MVVM pattern** with `CommunityToolkit.Mvvm`
- **GraphicsView** with a custom `IDrawable` for rendering at ~60 FPS
- **PanGestureRecognizer** for swipe-to-shoot input
- **Plugin.Maui.Audio** for sound effects
- **DispatcherTimer** at 16ms interval for the game loop

---

## Expected Project Structure

```
PenaltyShootout/
├── Models/
│   ├── Ball.cs            → X, Y, VelocityX, VelocityY, Scale, IsVisible, DistanceTraveled
│   ├── Goalkeeper.cs      → X, Y, DiveOffsetX, DiveOffsetY, CurrentDirection
│   ├── GoalPost.cs        → Left, Top, Width, Height (normalized 0–1)
│   └── GameState.cs       → PlayerScore, KeeperScore, CurrentRound, TotalRounds (5), CurrentPhase
├── Enums/
│   ├── Difficulty.cs      → Easy, Medium, Hard
│   ├── Direction.cs       → Left, Center, Right
│   ├── GamePhase.cs       → Aiming, Shooting, KeeperDive, Result, NextRound, GameOver
│   └── ShotResult.cs      → Goal, Save, Miss
├── ViewModels/
│   └── GameViewModel.cs   → MVVM game logic, observable properties, commands
├── Views/
│   └── GamePage.xaml/.cs  → GraphicsView, gesture recognizer, timer
├── Services/
│   ├── PhysicsEngine.cs   → Ball trajectory, goal-line detection, collision
│   └── AudioService.cs    → Audio playback wrapper
├── Drawables/
│   └── GameDrawable.cs    → IDrawable implementation
├── MauiProgram.cs         → DI registration
├── App.xaml/.cs
└── AppShell.xaml/.cs
```

---

## Review Checklist — Verify Each Item

### 1. GAME STATE MACHINE

The phase transitions must follow this exact flow with no deviations:

```
Aiming ──[swipe complete]──▶ Shooting ──[ball reaches goal line]──▶ KeeperDive
   ▲                                                                    │
   │                                                                    ▼
NextRound ◀──[round < 5]── Result ◀──[dive animation 300ms done]── KeeperDive
                              │
                        [round >= 5]
                              ▼
                          GameOver ──[play again]──▶ Aiming
```

**Check for these bugs:**
- [ ] Can the player swipe during Shooting, KeeperDive, or Result phases? (they should NOT be able to)
- [ ] Does `CurrentPhase` ever get set to an invalid value or skip a phase?
- [ ] Is there a race condition where the timer tick advances the phase while a gesture callback also tries to advance it?
- [ ] Does the GameOver phase trigger at exactly round > 5 (after 5 rounds complete), not at round 5?
- [ ] Does Play Again fully reset: PlayerScore=0, KeeperScore=0, CurrentRound=1, phase=Aiming, ball position, keeper position?
- [ ] Can the player trigger Play Again during any phase other than GameOver?
- [ ] Is there sudden-death logic for tied scores after 5 rounds, or does the spec simply end at 5? Flag if the behavior is ambiguous.

### 2. INPUT HANDLING

- [ ] Is a `PanGestureRecognizer` attached to the `GraphicsView`?
- [ ] Does the pan handler check `CurrentPhase == Aiming` before updating aim? If not, swiping during other phases could corrupt state.
- [ ] Are `TotalX` and `TotalY` correctly normalized to the -1.0 to 1.0 range?
- [ ] Is there a sensitivity divisor and is it reasonable for different screen densities?
- [ ] Does `GestureStatus.Completed` trigger `Shoot()` and transition to Shooting phase?
- [ ] What happens if the user taps without swiping (TotalX=0, TotalY=0)? Does the ball shoot straight at center or is this handled?
- [ ] Is `InputTransparent` set to `False` on the GraphicsView in XAML?

### 3. BALL PHYSICS

- [ ] Does the ball start at normalized position (0.5, 1.0) — bottom-center?
- [ ] Is velocity calculated as `(aimPoint - startPoint) * power * speedMultiplier`?
- [ ] Is power clamped between 0.3 and 1.0?
- [ ] Does `Ball.Scale` decrease from 1.0 toward 0.5 using `Math.Max(0.5f, 1.0f - DistanceTraveled * 0.003f)`?
- [ ] Is goal-line detection checking `Ball.Y <= 0.15` (or similar threshold) each frame?
- [ ] Can the ball overshoot the goal line if deltaTime is large (e.g., after a frame hitch)? Is there clamping?
- [ ] Is `DistanceTraveled` accumulated correctly each frame?
- [ ] Is the ball hidden or reset between rounds so it doesn't flash in its old position?
- [ ] Does the ball position get reset to (0.5, 1.0) at the start of each round?

### 4. GOALKEEPER AI

- [ ] Are difficulty thresholds correct: Easy=0.25, Medium=0.45, Hard=0.65?
- [ ] When the random roll is below the threshold, does the keeper dive toward the ACTUAL shot direction (not a random one)?
- [ ] When the random roll is above the threshold, does the keeper pick a genuinely random direction from Left/Center/Right?
- [ ] Does the dive animation start 200ms AFTER the shot is taken (reaction delay)?
- [ ] Does the dive animation last 300ms?
- [ ] Is the keeper position reset to center at the start of each round?
- [ ] Does the keeper dive only once per round? (no re-diving)
- [ ] Is `DiveOffsetX` applied relative to the keeper's base center position?

### 5. COLLISION / SHOT DETECTION

- [ ] Are the shot zones correctly defined:
  - Left: X ∈ [0.25, 0.42]
  - Center: X ∈ [0.42, 0.58]
  - Right: X ∈ [0.58, 0.75]
  - Off-target: X < 0.25 or X > 0.75 or Y outside goal range
- [ ] Is the comparison between shot zone and keeper dive direction correct (zone maps to Direction enum)?
- [ ] Does a zone match produce `ShotResult.Save`?
- [ ] Does a zone mismatch produce `ShotResult.Goal` and increment `PlayerScore`?
- [ ] Does off-target always produce `ShotResult.Miss` regardless of keeper dive?
- [ ] Is `KeeperScore` incremented on save (or is it just tracked as "saves")?
- [ ] Are boundary values handled? What happens if ball X is exactly 0.25, 0.42, 0.58, or 0.75?

### 6. RENDERING (GameDrawable)

- [ ] Does `GameDrawable` implement `IDrawable` with a `Draw(ICanvas canvas, RectF dirtyRect)` method?
- [ ] Is the draw order correct: Field → Goal → Goalkeeper → Ball → AimIndicator → ResultText?
- [ ] Are all normalized coordinates multiplied by `dirtyRect.Width` / `dirtyRect.Height` to produce pixel values?
- [ ] Are there any hardcoded pixel values in the drawable? (there should NOT be)
- [ ] Does the aim indicator only render during the `Aiming` phase?
- [ ] Does the result text only render during the `Result` phase?
- [ ] Is the ball drawn at the correct scale using `Ball.Scale`?
- [ ] Is the keeper drawn at the correct offset position during/after a dive?
- [ ] Does the goal net have visible structure (grid lines or pattern)?
- [ ] Is there any null reference risk if `GameState` or `Ball` is null when `Draw()` is called?

### 7. TIMER & GAME LOOP

- [ ] Is the `DispatcherTimer` created with a 16ms interval?
- [ ] Does the timer tick call `GameCanvas.Invalidate()` on the UI thread?
- [ ] Is the timer started on page appearing and stopped on page disappearing?
- [ ] Is `deltaTime` calculated correctly between frames, or is it assumed to be a constant 16ms? (Variable deltaTime is more robust)
- [ ] Is there a risk of timer ticks stacking if a frame takes longer than 16ms?
- [ ] Does the physics engine receive proper deltaTime values?

### 8. MVVM & DATA BINDING

- [ ] Does `GameViewModel` inherit from `ObservableObject`?
- [ ] Are score properties (`PlayerScore`, `KeeperScore`) using `[ObservableProperty]` or manually raising `PropertyChanged`?
- [ ] Is `RoundText` (e.g., "Round 3 of 5") a computed property that updates when `CurrentRound` changes?
- [ ] Is the `GraphicsView.Drawable` property bound to the ViewModel's `GameDrawable` instance?
- [ ] Do commands (`ShootCommand`, `PlayAgainCommand`) use `[RelayCommand]` or manual `ICommand` implementations?
- [ ] Is the ViewModel injected via DI into the page, not manually instantiated?

### 9. DEPENDENCY INJECTION (MauiProgram.cs)

- [ ] Is `PhysicsEngine` registered as a singleton?
- [ ] Is `IAudioManager` registered as a singleton via `AudioManager.Current`?
- [ ] Is `GameViewModel` registered as transient?
- [ ] Is `GamePage` registered as transient?
- [ ] Are there any missing registrations that would cause runtime DI failures?

### 10. AUDIO

- [ ] Are audio files present in `Resources/Raw/` with build action `MauiAsset`?
- [ ] Is the kick sound played when the shot is taken?
- [ ] Is the goal/save/miss sound played when the result is determined?
- [ ] Is the whistle played at game start and game over?
- [ ] Is ambient crowd noise looped during gameplay?
- [ ] Are audio calls wrapped in try-catch to prevent crashes if files are missing?
- [ ] Is audio stopped/disposed properly when the page disappears or the app backgrounds?

### 11. XAML LAYOUT (GamePage.xaml)

- [ ] Is the root layout a `Grid` with the `GraphicsView` as the first child (bottom layer)?
- [ ] Are overlay elements (score, round text, buttons) layered on top of the canvas?
- [ ] Does the `GraphicsView` fill the entire screen?
- [ ] Is `InputTransparent="False"` set on the `GraphicsView`?
- [ ] Do overlay labels have `InputTransparent="True"` so they don't steal touch events?
- [ ] Is the Play Again button only visible during GameOver phase?
- [ ] Are fonts and sizes reasonable for both phone and tablet screen sizes?

### 12. EDGE CASES & CRASH SCENARIOS

- [ ] What happens if the app is backgrounded mid-shot? Does it resume correctly?
- [ ] What happens on screen rotation? Do coordinates recalculate?
- [ ] Is there a divide-by-zero risk anywhere (e.g., normalizing a zero-length swipe)?
- [ ] Can rapid repeated tapping cause multiple shots per round?
- [ ] Are all enum switches exhaustive (handle all cases or have a default)?
- [ ] Are there any `async void` methods that could swallow exceptions?
- [ ] Is thread safety maintained if timer ticks overlap with gesture callbacks?

### 13. CODE QUALITY

- [ ] Are file-scoped namespaces used throughout?
- [ ] Are timing constants (dive delay, animation duration, etc.) defined as `const` fields rather than magic numbers?
- [ ] Does `GameDrawable` contain any business logic? (it should NOT — only read models and draw)
- [ ] Are all public members documented with XML doc comments?
- [ ] Are there any TODO/HACK/FIXME comments indicating unfinished work?
- [ ] Is there any dead code or unused methods?
- [ ] Are nullable reference types handled properly (no unguarded null dereferences)?

---

## Output Format

Produce your findings as a structured report with the following sections:

### Critical Bugs
Issues that will cause crashes, incorrect game behavior, or broken gameplay. Each must include the file name, line number (or method), what is wrong, and a recommended fix.

### Logic Inconsistencies
Cases where the implementation deviates from the spec (phase transitions, AI thresholds, zone boundaries, timing values, coordinate ranges). Include the expected vs. actual behavior.

### Missing Features
Any spec requirement from the checklist above that is not implemented at all.

### Warnings
Non-breaking issues that could cause problems under specific conditions (edge cases, thread safety, memory leaks, large deltaTime spikes, missing error handling).

### Code Quality Issues
Style violations, magic numbers, missing documentation, dead code, or architectural concerns.

### Summary
A short overall assessment: is the app shippable? What are the top 3 issues to fix first?
