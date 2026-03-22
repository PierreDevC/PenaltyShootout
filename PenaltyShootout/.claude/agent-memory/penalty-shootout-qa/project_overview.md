---
name: Penalty Shootout Project Overview
description: Core facts about the Penalty Shootout .NET MAUI game project — architecture, stack, and QA baseline
type: project
---

Penalty Shootout is a .NET MAUI game (net9.0 targets) where the player swipes to shoot at an AI-defended goal over 5 rounds.

**Stack:** CommunityToolkit.Mvvm 8.4.1, Plugin.Maui.Audio 4.0.0, MAUI Graphics (IDrawable/GraphicsView), no SkiaSharp.

**Architecture:** MVVM — GameViewModel owns all models (Ball, Goalkeeper, GoalPost, GameState) and the GameDrawable. PhysicsEngine is a stateless singleton service. AudioService wraps Plugin.Maui.Audio. GamePage drives the DispatcherTimer and PanGestureRecognizer.

**State machine phases:** Aiming → Shooting → KeeperDive → Result → (NextRound or GameOver). Sudden-death overtime is implemented (non-spec extension).

**First full QA pass:** 2026-03-21. All 7 build phases were implemented in a single pass.

**Why:** Baseline project memory for future QA review sessions so context does not need to be re-established from scratch.

**How to apply:** Use as orientation before diving into any file-level review. Check git log for changes since this date before relying on findings below.
