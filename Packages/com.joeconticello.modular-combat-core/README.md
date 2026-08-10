# Modular Combat Core

Reusable, network-agnostic damage contracts and stat infrastructure for Unity 6 projects.

## Features

- One `IDamageable.ApplyDamage(DamageRequest)` entry point
- Structured `DamageRequest` and `DamageResult` values
- Serializable, case-insensitive `StatContainer`
- ScriptableObject stat definitions and calculation rules
- Explicit or Resources-based database initialization
- Runtime and Editor assembly separation

The package contains no networking, UI, damage floaters, persistence, or legacy stat types.

## Install

In Unity Package Manager, choose **Add package from git URL** and enter:

```text
https://github.com/JC2197/unity-modular-combat-core.git#v0.1.0
```

For local development, add this dependency to the consuming project's `Packages/manifest.json`:

```json
"com.joeconticello.modular-combat-core": "file:../../unity-modular-combat-core"
```

## Stats

Create a database through **Assets > Create > Modular Combat Core > Stat Type Database**. The database starts empty, and you add both categories and stats in the inspector:

```csharp
using JoeConticello.ModularCombatCore;

var stats = new StatContainer();
stats.Initialize(database);
stats.SetStat("MaxHealth", 100f);
stats.ModifyStat("MaxHealth", 25f);
```

Calling `InitializeFromResources()` loads `Resources/StatTypeDatabase` as a convenience, but explicit database references are preferred for reusable projects.

Categories are now editor-defined entries inside the database asset, not a fixed enum.

## Damage

```csharp
DamageResult result = target.ApplyDamage(new DamageRequest(
	amount: 20f,
	damageType: "Fire",
	criticalMultiplier: 1.5f,
	sourcePosition: transform.position,
	source: gameObject));
```

Presentation systems can inspect `DamageResult` and independently display floaters, flashes, audio, or other feedback.

## Versioning

Install tagged releases rather than `main`. Breaking API changes increment the major version.
