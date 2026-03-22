---
name: Architecture Patterns and Recurring Observations
description: Recurring code patterns, architectural decisions, and areas of past bug concentration in this codebase
type: project
---

## Recurring Issues (as of 2026-03-21 first review)

**Magic numbers / missing consts:** AimSensitivity=300f is a const field (good). _wiggleTime increment 0.016f is a magic number (bad). TrailLength=6 is a const (good). Result display 1500ms is inline in UpdateResult() instead of a const (bad — spec requires timing constants as const fields in PhysicsEngine).

**Hardcoded canvas defaults:** _canvasWidth=400f, _canvasHeight=800f in GameViewModel. These get overridden via SetCanvasSize() on OnSizeAllocated, but there is a window at startup before OnSizeAllocated fires where normalization will be wrong if a shot could somehow occur.

**Dual-write pattern for CurrentPhase:** GameState.CurrentPhase and the [ObservableProperty] CurrentPhase on GameViewModel are kept manually in sync throughout. This is fragile — any place that updates one and forgets the other will desync UI from internal state. TransitionTo() keeps them in sync, but PlayAgain() and ResetRound() update both explicitly as belt-and-suspenders. This dual-state pattern is an architectural smell.

**async void fire-and-forget for audio:** Audio calls are `_ = _audio.PlayXxxAsync()` throughout. Exceptions from audio are swallowed in AudioService's try-catch, so fire-and-forget is relatively safe here. The pattern is intentional.

**GameDrawable rendered overlays (GameOver, ResultText):** These duplicate what could be done in XAML overlays. The GameDrawable contains no logic but does render UI state (winner text, score). This is within bounds of spec ("read from models and draw") but creates dual rendering paths — some UI is in XAML, some in the canvas.

## Areas With Highest Bug Concentration

1. GameViewModel.cs — velocity formula, aim normalization, phase transition side effects, scoring logic
2. GamePage.xaml.cs — timer lifecycle (leak on repeated appear), gesture Canceled handling
3. AudioService.cs — player disposal
4. PhysicsEngine.cs — SpeedMultiplier double-application to DistanceTraveled

## Clean Areas (low risk)

- All Enums: correct, spec-compliant, properly documented
- Models (Ball, Goalkeeper, GoalPost, GameState): correct Reset() implementations, proper defaults
- GoalPost: computed Right/Bottom properties are correct
- DI Registration in MauiProgram.cs: matches spec exactly
- AppShell.xaml: minimal, correct
- DrawResultText and DrawGoalkeeper: no business logic, reads-only pattern maintained

Why: Helps focus QA effort in future sessions on high-risk files first.

How to apply: When reviewing a PR or change, prioritize GameViewModel.cs and GamePage.xaml.cs for the most critical checks.
