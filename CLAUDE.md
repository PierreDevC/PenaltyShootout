# CLAUDE.md — Penalty Shootout (.NET MAUI)

## Project Overview

Build a **Penalty Shootout** mobile/desktop game using **.NET MAUI**. The player swipes to aim and kick a ball at a goal defended by an AI goalkeeper. The game runs for 5 rounds, tracking goals scored vs. goals saved.

**Target frameworks:** .NET 8+ with MAUI workload  
**Platforms:** Android, iOS, Windows, macOS (via MAUI)  
**Architecture:** MVVM with CommunityToolkit.Mvvm  
**Rendering:** MAUI `GraphicsView` with custom `IDrawable` at ~60 FPS  

---

## Repository Structure

```
PenaltyShootout/
├── CLAUDE.md                          # This file
├── PenaltyShootout.sln
└── PenaltyShootout/
    ├── PenaltyShootout.csproj
    ├── App.xaml / App.xaml.cs
    ├── AppShell.xaml / AppShell.cs
    ├── MauiProgram.cs
    ├── Models/
    │   ├── Ball.cs
    │   ├── Goalkeeper.cs
    │   ├── GoalPost.cs
    │   └── GameState.cs
    ├── Enums/
    │   ├── Difficulty.cs
    │   ├── Direction.cs
    │   ├── GamePhase.cs
    │   └── ShotResult.cs
    ├── ViewModels/
    │   └── GameViewModel.cs
    ├── Views/
    │   └── GamePage.xaml / GamePage.xaml.cs
    ├── Services/
    │   ├── PhysicsEngine.cs
    │   └── AudioService.cs
    ├── Drawables/
    │   └── GameDrawable.cs
    └── Resources/
        ├── Raw/                       # Audio assets (kick.wav, save.wav, goal.wav, whistle.wav, crowd.wav)
        └── Images/                    # Any static image assets
```

---

## Build Order (follow this sequence)

### Phase 1 — Scaffold & Static Scene
1. Create .NET MAUI project via `dotnet new maui -n PenaltyShootout`.
2. Add NuGet packages: `CommunityToolkit.Mvvm`, `Plugin.Maui.Audio`.
3. Define all enums in `Enums/`.
4. Create `GameState.cs` model with properties: `PlayerScore`, `KeeperScore`, `CurrentRound`, `TotalRounds (5)`, `CurrentPhase`.
5. Create `Ball.cs` model with: `X`, `Y`, `VelocityX`, `VelocityY`, `Scale`, `IsVisible`, `DistanceTraveled`.
6. Create `Goalkeeper.cs` model with: `X`, `Y`, `DiveOffsetX`, `DiveOffsetY`, `CurrentDirection`.
7. Create `GoalPost.cs` with: `Left`, `Top`, `Width`, `Height` (normalized 0–1 coordinates).
8. Implement `GameDrawable : IDrawable` — draw green pitch, white goal frame, ball at bottom-center, and static goalkeeper.
9. Set up `GamePage.xaml` with a `GraphicsView` filling the screen and a score overlay using `Grid` layering.
10. Wire up a `DispatcherTimer` at 16 ms interval calling `GameCanvas.Invalidate()`.

**Checkpoint:** App launches showing a static football pitch with goal, ball, and keeper.

### Phase 2 — Swipe Input & Aim Indicator
1. Add `PanGestureRecognizer` to the `GraphicsView`.
2. In `GameViewModel`, expose `AimX` and `AimY` (normalized -1 to 1) updated by pan deltas.
3. Draw a dotted aim line from ball position toward `(AimX, AimY)` in `GameDrawable` during `Aiming` phase.
4. On gesture completed → transition to `Shooting` phase.

**Checkpoint:** Swiping draws a visible aim line; releasing triggers a shot.

### Phase 3 — Ball Animation & Perspective
1. In `PhysicsEngine.UpdateBall(float deltaTime)`:
   - Move `Ball.X` and `Ball.Y` toward the aim point.
   - Decrease `Ball.Scale` from 1.0 → 0.5 as it travels (fake depth).
   - Track `DistanceTraveled`.
2. When ball reaches the goal line Y-coordinate → transition to `KeeperDive` phase.

**Checkpoint:** Ball animates from bottom-center toward the aim point, shrinking as it goes.

### Phase 4 — Goalkeeper AI & Dive
1. In `Goalkeeper.DecideDive(Difficulty, Direction actualShot)`:
   - Roll random float. If below threshold (Easy 0.25, Medium 0.45, Hard 0.65) → dive toward actual shot direction.
   - Otherwise → pick random direction (Left, Center, Right).
2. Animate keeper dive: offset `DiveOffsetX` over ~300 ms in the chosen direction.
3. After dive completes → transition to `Result` phase.

**Checkpoint:** Keeper visibly dives left, right, or stays center after each shot.

### Phase 5 — Collision & Shot Result
1. Determine shot zone: Left third, Center third, Right third of goal, or Off-target (outside goal frame).
2. Compare shot zone to keeper dive direction:
   - Match → `ShotResult.Save`
   - No match → `ShotResult.Goal`
   - Off-target → `ShotResult.Miss`
3. Display a brief result label ("GOAL!", "SAVED!", "MISS!") for 1.5 seconds.
4. Update `PlayerScore` or `KeeperScore`.

**Checkpoint:** Goals, saves, and misses are correctly detected and scored.

### Phase 6 — Round Management & Game Over
1. After result display → increment `CurrentRound`.
2. If `CurrentRound > TotalRounds` → transition to `GameOver` phase.
3. Show final score screen with "Play Again" button that resets `GameState`.
4. Otherwise → reset ball position, keeper position, and transition to `Aiming`.

**Checkpoint:** Full 5-round game loop works end to end.

### Phase 7 — Polish
1. Add audio via `Plugin.Maui.Audio`: kick sound on shoot, crowd cheer on goal, buzzer on save.
2. Add a power meter (optional hold-and-release mechanic before shot).
3. Add difficulty selection screen (Easy / Medium / Hard).
4. Add ball trail / particle effect during flight.
5. Add crowd noise ambient loop during gameplay.
6. Smooth animations: keeper anticipation wiggle, net ripple on goal.

---

## Key Technical Specifications

### Coordinate System
- All game coordinates are **normalized 0.0–1.0** relative to the `GraphicsView` bounds.
- `(0.5, 1.0)` = bottom-center (ball start).
- `(0.5, 0.15)` = top-center (goal center).
- Goal spans X: `0.25–0.75`, Y: `0.10–0.20`.

### Rendering Pipeline
```
DispatcherTimer (16ms) → GameCanvas.Invalidate()
  → GameDrawable.Draw(ICanvas, RectF)
    → DrawField()       // green gradient background
    → DrawGoal()        // white rectangular frame + net pattern
    → DrawGoalkeeper()  // rectangle or sprite at goal line
    → DrawBall()        // circle scaled by Ball.Scale
    → DrawAimIndicator() // dotted line during Aiming phase
    → DrawResultText()  // "GOAL!" / "SAVED!" / "MISS!" overlay
```

### Game Phase State Machine
```
Aiming ──[swipe complete]──▶ Shooting ──[ball reaches goal]──▶ KeeperDive
   ▲                                                              │
   │                                                              ▼
NextRound ◀──[round < 5]── Result ◀──[dive animation done]── KeeperDive
                              │
                        [round >= 5]
                              ▼
                          GameOver ──[play again]──▶ Aiming
```

### Ball Physics (Simplified)
- No gravity or spin. Straight-line interpolation from start to aim point.
- `Ball.VelocityX = (aimX - startX) * power`
- `Ball.VelocityY = (aimY - startY) * power`
- `Ball.Scale = max(0.5, 1.0 - distanceTraveled * 0.003)` for perspective.
- Flight duration: ~0.6–1.0 seconds depending on power.

### Keeper Dive Timing
- Dive starts **200 ms after shot is taken** (reaction time).
- Dive animation duration: **300 ms**.
- Keeper reaches max offset at dive end, then result is evaluated.

### Shot Detection Zones
```
Goal frame (normalized):
  Left zone:   X ∈ [0.25, 0.42]
  Center zone: X ∈ [0.42, 0.58]
  Right zone:  X ∈ [0.58, 0.75]
  Off-target:  X < 0.25 or X > 0.75 or Y < 0.10 or Y > 0.20
```

---

## MVVM Wiring

### GameViewModel (inherits ObservableObject)
**Observable properties:** `PlayerScore`, `KeeperScore`, `CurrentRound`, `RoundText`, `CurrentPhase`, `AimX`, `AimY`, `Drawable` (GameDrawable ref).  
**Commands:** `ShootCommand`, `PlayAgainCommand`, `SetDifficultyCommand`.  
**Methods:** `UpdateAim(double totalX, double totalY)`, `Shoot()`, `ResetRound()`, `ResetGame()`.

### GamePage.xaml.cs
- Binds `GraphicsView.Drawable` to `GameViewModel.Drawable`.
- Attaches `PanGestureRecognizer` wired to `UpdateAim` / `Shoot`.
- Starts `DispatcherTimer` on page appearing, stops on disappearing.

### Dependency Injection (MauiProgram.cs)
```csharp
builder.Services.AddSingleton<PhysicsEngine>();
builder.Services.AddSingleton<IAudioManager>(AudioManager.Current);
builder.Services.AddTransient<GameViewModel>();
builder.Services.AddTransient<GamePage>();
```

---

## NuGet Packages

| Package | Purpose |
|---------|---------|
| `CommunityToolkit.Mvvm` | `ObservableObject`, `RelayCommand`, `ObservableProperty` attribute |
| `Plugin.Maui.Audio` | Cross-platform audio playback for SFX |
| `SkiaSharp.Views.Maui.Controls` | Optional: use instead of MAUI Graphics for richer rendering |

---

## Testing Notes

- **Unit test `PhysicsEngine`** — verify ball reaches goal line within expected frame count.
- **Unit test `Goalkeeper.DecideDive`** — run 10,000 iterations, verify correct-guess rate matches difficulty threshold ±5%.
- **Unit test `GameState` transitions** — verify phase machine doesn't skip or repeat phases.
- **Manual test on Android + iOS** — verify swipe gestures work with varying DPI and screen sizes.
- **Edge cases:** rapid double-swipe during animation, app backgrounding mid-round, screen rotation.

---

## Coding Conventions

- Use `file-scoped namespaces`.
- Use `primary constructors` where appropriate (.NET 8+).
- Use `[ObservableProperty]` and `[RelayCommand]` source generators from CommunityToolkit.Mvvm.
- Keep `GameDrawable` free of business logic — it reads from models and draws.
- All timing constants (dive delay, animation duration, etc.) should be `const` fields in `PhysicsEngine` for easy tuning.
- XML doc comments on all public members.

---

## Common Pitfalls

1. **GraphicsView not updating:** Ensure `Invalidate()` is called from the UI thread via `DispatcherTimer`, not a background thread.
2. **Pan gestures not firing:** `GraphicsView` must have `InputTransparent="False"` and be the topmost element receiving touch.
3. **Ball overshooting goal:** Use `deltaTime` clamping and check goal-line crossing per frame, not exact position match.
4. **Keeper dives after result:** Gate the dive animation behind the `KeeperDive` phase — don't let it run during `Result` or `Aiming`.
5. **Audio not playing on Android:** Register `AudioManager` in DI and ensure `.wav` files are in `Resources/Raw` with build action `MauiAsset`.
6. **Normalized coordinates vs. pixels:** Always multiply by `dirtyRect.Width` / `dirtyRect.Height` inside `Draw()` — never store pixel values in models.
