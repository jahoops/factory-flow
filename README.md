# Factory Flow Lab

Factory Flow Lab is a recreational 2D factory/logistics game focused on the parts of factory games that are fun:

**resources → transport → processing → queues → routing → overflow → storage → expansion**

The goal is not to recreate Satisfactory. The goal is to build a factory-flow playground where logistics, bottlenecks, visible movement, and optimization are the game.

## Current Platform

* **Engine:** Godot 4.7.2 stable .NET
* **Language:** C#
* **Target:** Linux desktop
* **Godot renderer:** Compatibility / OpenGL 3.3
* **FactoryFlow.Godot:** `net8.0`
* **FactoryFlow.Core:** `net8.0`
* **FactoryFlow.Core.Tests:** `net10.0`

Known-good development environment has been verified:

* Core builds successfully.
* Core tests run successfully.
* Godot builds successfully.
* Godot references FactoryFlow.Core successfully.
* A value owned by Core (`StorageCount = 17`) has been read successfully by the Godot client.

## Architecture

### Core owns reality

`FactoryFlow.Core` is the authoritative source of game state and simulation behavior.

It owns concepts such as:

* resources
* items
* belts
* machines
* inventories
* queues
* routing
* storage
* simulation time

`FactoryFlow.Core` must not depend on Godot.

### Godot renders reality

Godot is responsible for:

* rendering
* camera
* visual presentation
* UI
* gathering player input

Godot does not own authoritative simulation state.

Conceptually:

```text
Player Input
     ↓
UI / Input State
     ↓
Commands / Intent
     ↓
FactoryFlow.Core
     ↓
Authoritative World State
     ↓
Godot
     ↓
Render This
```

Godot objects are representations of simulation objects, not the simulation objects themselves.

### Simulation time belongs to Core

The simulation must not depend on rendering frame rate or wall-clock time.

This should eventually allow the same Core simulation to run:

* normally during play
* faster than real time
* completely headless in tests
* for long-duration simulations without graphical overhead

These are consequences of the architecture, not features to implement now.

## Development Philosophy

> **Build visible fun before infrastructure.**

Do not build systems merely because they may be useful later.

At the same time, preserve clean boundaries that prevent today's simple implementation from becoming tomorrow's architectural trap.

Two guards:

**Madness guard:** Do not build a feature until current gameplay gives us a reason to need it.

**Boredom guard:** A playable slice is not finished until it contains at least one meaningful decision with a visible consequence.

## First Playable Slice

The first useful factory should quickly become:

```text
SOURCE
   ↓
moving items on belt
   ↓
MACHINE
   ↓
different item
   ↓
STORAGE
```

Items should visibly move.

Machines should visibly process.

Storage should visibly fill.

Queues and backpressure should eventually be visible.

The first meaningful gameplay target is the smallest factory in which the player can:

1. create or encounter a bottleneck,
2. recognize it visually,
3. change something,
4. watch the flow improve.

## Future Possibilities, Not Current Requirements

Potential later systems include:

* splitters and mergers
* buffers
* smart routing
* overflow
* priorities
* power
* research through physical prototypes
* discrete height levels
* trains
* drones
* large-scale factories

These are intentionally **not** requirements for the initial implementation.

Do not prepare generalized frameworks for them until the current game requires them.
