---
name: Known Bugs — First QA Pass (2026-03-21)
description: All confirmed bugs and spec deviations found in the initial full-codebase review
type: project
---

## Critical Bugs

**BUG-01: Shoot() fires on GestureStatus.Canceled (wrong trigger)**
- File: GamePage.xaml.cs, OnPanUpdated, line 63
- Both `Completed` and `Canceled` call `_viewModel.Shoot(...)`. A canceled gesture (e.g., interrupted by a phone call or scroll conflict) will trigger a real shot. Only `Completed` should shoot.
- Why critical: produces unintended shots; on iOS PanGestureRecognizer routinely emits Canceled on scroll conflicts.

**BUG-02: Zero-length swipe produces NaN velocities**
- File: GameViewModel.cs, Shoot(), lines 294-300
- When totalX=0 and totalY=0, `_rawAimX = Ball.X` and `_rawAimY = Ball.Y`. Then `dx=0, dy=0, dist=0`. The `if (dist > 0)` guard prevents the divide, but Ball.VelocityX and VelocityY remain 0. The game transitions to Shooting with a stationary ball. The ball never moves, never reaches GoalLineY, and the Shooting phase never exits — game is permanently stuck.
- Why critical: complete game freeze; reproduces with a tap (no swipe movement).

**BUG-03: AimY normalization clamps away upward shots — Y forced to [goalBottom, goalBottom]**
- File: GameViewModel.cs, UpdateAim(), line 274 and 278
- `normalizedY` is clamped to `[0f, 1f]` where 1f represents maximum upward swipe. Then `_rawAimY = GoalPost.Bottom - normalizedY * GoalPost.Height`. When normalizedY=0 (no vertical movement), rawAimY=GoalPost.Bottom (0.20). When normalizedY=1, rawAimY=GoalPost.Top (0.10). A downward swipe (positive totalY in screen space) yields negative normalized value clamped to 0, so the aim point is stuck at the goal bottom. This means any shot with zero or minimal upward swipe component will target Y=0.20 exactly, which is the bottom of the goal frame and on the boundary of "off-target." This is a usability defect that narrows the vertical aim range unnecessarily.
- Additionally: the ball travels in a straight line from (0.5, 1.0) to the aimPoint. Because the Y axis goes 0=top to 1=bottom, a shot "toward the goal" requires VelocityY to be negative. The velocity computation `dy = _rawAimY - Ball.Y` = `rawAimY - 1.0` will always be negative when rawAimY is in [0.10, 0.20], so direction is correct. But the mapping means the player must swipe upward to aim at all — no downward swipe ever produces a shot toward the goal, which is correct behavior for a penalty kick. The clamp logic is not clearly wrong, but the range is too narrow — mapping 0–300px of swipe to only 0.10 units of normalized Y height. At 300px the aim barely moves within the goal frame.

**BUG-04: Velocity formula bypasses spec — normalized unit vector instead of (aimPoint - start) * power**
- File: GameViewModel.cs, Shoot(), lines 294-300
- Spec says: `VelocityX = (aimX - startX) * power`, `VelocityY = (aimY - startY) * power`. Code normalizes the direction vector first (`dx/dist, dy/dist`) and then multiplies by power. This means power only controls speed, never trajectory — a small swipe and a large swipe to the same aim point produce identical final positions. The spec intention is that the raw displacement vector IS the velocity, so a short swipe to the left edge of the goal vs. a long swipe to the same point have the same trajectory but the spec formula doesn't normalize either, resulting in power also encoding direction magnitude. The normalization is defensible design but deviates from spec.

**BUG-05: SpeedMultiplier is applied twice in UpdateBall**
- File: PhysicsEngine.cs, UpdateBall(), lines 52-56
- Ball position update: `ball.X += ball.VelocityX * SpeedMultiplier * dt`. DistanceTraveled update: `ball.DistanceTraveled += speed * SpeedMultiplier * dt` where `speed = sqrt(VelocityX^2 + VelocityY^2)`. `speed` is computed from raw VelocityX/Y (which already encode the unit-vector direction). Actual position changes by `VelocityX * SpeedMultiplier * dt`, but distance accumulated is based on the raw velocity magnitude, not the actual on-screen displacement. These will only agree if SpeedMultiplier=1. At SpeedMultiplier=2, the ball physically moves twice as fast as the distance counter tracks, meaning the scale formula under-decays — the ball stays large longer than intended.

**BUG-06: AudioService.PlayAsync() leaks IAudioPlayer — no Dispose called on transient players**
- File: AudioService.cs, PlayAsync(), lines 51-59
- A new IAudioPlayer is created per sound effect call. Play() is called but the player is never disposed. Plugin.Maui.Audio players hold native audio resources. After dozens of rounds the app will leak handles/memory.

**BUG-07: Timer leak on repeated OnAppearing calls**
- File: GamePage.xaml.cs, OnAppearing(), lines 29-32
- Each call to OnAppearing creates a new timer and assigns it to `_timer`. If OnAppearing is called while a previous timer is still running (e.g., navigation stack behavior), the old timer is orphaned — it keeps ticking and calling Update()/Invalidate() but the reference is lost so it can never be stopped.

**BUG-08: Sudden-death round counter does not reset — CurrentRound increments past TotalRounds unboundedly**
- File: GameViewModel.cs, AdvanceRound(), lines 225-252
- In sudden death, `GameState.CurrentRound` keeps incrementing every round. `UpdateRoundText()` shows "Sudden Death" so it's not visible, but if the game continues many rounds (equal scores each time), CurrentRound grows without bound. More importantly, `normalOver` check is `CurrentRound > TotalRounds && !IsSuddenDeath` — once IsSuddenDeath=true this is always false, which is correct. But the round counter is never meaningful after sudden death begins and the unbounded increment is a minor state pollution issue.

**BUG-09: Play Again re-shows difficulty selector but does NOT re-stop crowd audio**
- File: GameViewModel.cs, PlayAgain(), lines 341-356
- PlayAgain() resets all state and sets ShowDifficultySelector=true. However, `_audio.StopCrowdAmbience()` is never called. The crowd ambient audio continues looping while the difficulty screen is showing. When SetDifficulty is called again, StartCrowdAmbienceAsync() early-returns because `_crowdPlayer is not null`, so no double-play occurs, but the crowd noise plays through the difficulty selection screen.

**BUG-10: GoalPost.Bottom used as upper AimY bound instead of GoalPost.Top**
- File: GameViewModel.cs, UpdateAim(), line 278
- `_rawAimY = GoalPost.Bottom - normalizedY * GoalPost.Height`. When normalizedY=0: rawAimY=0.20 (goal bottom). When normalizedY=1: rawAimY=0.10 (goal top). This is actually mathematically correct — the formula maps the range correctly. Confirmed not a bug on re-examination.

## Deviations from Spec

**DEV-01: AudioService registered as Singleton, not matching spec pattern**
- Spec shows AudioService being used but does not explicitly specify its lifetime. The implementation uses Singleton. This is appropriate since it owns the crowd player instance.

**DEV-02: Sudden-death overtime is an undocumented extension**
- GameState.IsSuddenDeath, AdvanceRound() sudden-death branching — these are not in the CLAUDE.md spec. GameOver is supposed to trigger after round 5 completes. The sudden-death logic is well-implemented but represents unspecified behavior.

**DEV-03: KeeperScore incremented on both Save AND Miss**
- File: GameViewModel.cs, EvaluateResult(), lines 205-208
- Spec says: "Update PlayerScore or KeeperScore." On Save, KeeperScore++ is correct. On Miss (off-target), the spec says "ShotResult.Miss" but does not say KeeperScore should increment. A miss is the player's fault, not a keeper save. The current code rewards the keeper for misses, which is a scoring semantic error.

**DEV-04: DetermineShotZone off-target check uses ball.Y at GoalLineY (clamped)**
- File: GameViewModel.cs, UpdateShooting(), line 107 — `Ball.Y = PhysicsEngine.GoalLineY` is set before EvaluateShotZone(). This means Ball.Y is always 0.15 when DetermineShotZone is called. The Y check in DetermineShotZone (`y < 0.10f || y > 0.20f`) will always pass (0.15 is in range). The Y component of the off-target check is therefore never exercised. Off-target shots are only possible via the X boundary check. This is partially correct since shots that travel outside X [0.25,0.75] will be caught, but if the ball exits the goal frame at the top (y < 0.10) or bottom (y > 0.20), that detection path is dead code.

**DEV-05: Difficulty selector blocks shooting even when already visible during Aiming**
- File: GameViewModel.cs, Shoot(), line 289 — `if (ShowDifficultySelector) return;`
- This guard is necessary but the difficulty overlay also covers the GraphicsView, so pan gestures cannot reach the game canvas when ShowDifficultySelector=true. The guard is redundant but harmless.

**DEV-06: _wiggleTime in GameDrawable uses a hardcoded 0.016f delta instead of actual deltaTime**
- File: GameDrawable.cs, Draw(), line 36 — `_wiggleTime += 0.016f`
- This assumes exactly 60fps. At lower frame rates the wiggle is slower; at higher frame rates it's faster. Cosmetic only but violates the timing-via-deltaTime pattern.

**DEV-07: DrawField uses canvas.Alpha = 0.4f but never resets before DrawGoal**
- File: GameDrawable.cs, DrawField(), line 68 — alpha set to 0.4f. Line 89 resets to 1f. This reset is present; confirmed not a leak.

**DEV-08: spec says Result display = 1500ms; code uses 1500ms — matches.**

**DEV-09: Spec says KeeperReactionDelayMs=200, DiveDurationMs=300 — code matches exactly.**

**DEV-10: DetermineShotZone lower boundary — spec says Left: X ∈ [0.25, 0.42]. Code checks `x < 0.42f` starting from `x >= 0.25f` (off-target check already excludes x < 0.25). Boundary value x=0.25 falls in Left zone. x=0.42 falls in Center (switch `< 0.42f` → `< 0.58f`). Spec is ambiguous at exact boundaries (half-open vs closed intervals). The implementation uses half-open intervals consistently which is reasonable.**

Why: Recorded for future QA sessions so re-analysis of the same issues is avoided.

How to apply: Cross-reference this list when reviewing changes to confirm bugs have been fixed and no regressions introduced.
