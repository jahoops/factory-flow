# Factory Flow Engineering Rules

## Environment

This repository uses:

* **Godot 4.7.2 stable .NET**
* **C# only**
* Linux desktop
* `FactoryFlow.Godot`: `net8.0`
* `FactoryFlow.Core`: `net8.0`
* `FactoryFlow.Core.Tests`: `net10.0`

Do not use:

* GDScript
* Godot 3 APIs
* APIs from unreleased/later Godot versions without explicit approval
* examples copied from other Godot versions without verifying that they apply to Godot 4.7.2

When Godot-version-specific behavior matters, verify it rather than relying on memory.

## Architectural Invariants

### 1. Core is authoritative

`FactoryFlow.Core` owns simulation state and simulation rules.

There must be no Godot dependency in `FactoryFlow.Core`.

### 2. Godot is a client of Core

Godot:

* renders Core state,
* maintains presentation/UI state,
* gathers player input,
* translates input into explicit commands or intent.

Godot must not become the authoritative source of simulation state.

### 3. Simulation entities are not Godot nodes

Do not make a Godot `Node`, `Node2D`, sprite, control, or scene the authoritative representation of a belt, item, machine, inventory, or other simulation entity.

Visual objects may represent Core entities.

They do not define their truth.

### 4. Input expresses intent

UI controls should not directly mutate simulation internals.

Input should result in a meaningful command or intent that Core can accept, reject, or apply.

Do not build a generalized command framework unless current requirements justify one.

### 5. Simulation owns time

Simulation progression belongs to `FactoryFlow.Core`.

Do not make game-state correctness depend on frame rate, animation position, or Godot's visual state.

### 5. Minimal or no physics
Do not use Godot physics to simulate factory logistics. Belts, queues, routing, machine flow, and backpressure are simulation logic in Core. Use physics only when a specific gameplay requirement genuinely requires it.

## Engineering Method

Before making a change:

1. Inspect the relevant existing code and tests.
2. Understand the requested behavior.
3. Make the smallest conventional change that satisfies it.
4. Avoid unrelated refactoring, cleanup, abstraction, or generalization.
5. Run the relevant verification.
6. Report honestly what was and was not verified.

## Scope Discipline

> **Build visible fun before infrastructure.**

Do not introduce infrastructure for hypothetical future systems.

In particular, do not currently create generalized systems for:

* evolutionary computing
* AI-controlled factories
* replay
* networking
* plugins
* event sourcing
* trains
* drones
* height levels
* generalized research
* large-scale optimization

The architecture should avoid preventing such things, but no implementation should be added merely to prepare for them.

Prefer a small seam over a large framework.

## First Gameplay Goal

The first playable path is:

```text
SOURCE → moving item → MACHINE → transformed item → STORAGE
```

The simulation should own the state.

Godot should make that state visible.

The first slice should develop only far enough to produce a meaningful visible factory-flow decision, such as recognizing and correcting a bottleneck.

## Current Verified State

The project currently proves the Core/Godot dependency boundary:

* `FactoryFlow.Core` builds.
* `FactoryFlow.Core.Tests` passes.
* `FactoryFlow.Godot` builds while referencing Core.
* Godot successfully read `StorageCount = 17` from a Core object.

Preserve this boundary as gameplay is introduced.
