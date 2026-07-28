# Behaviour tests

259 assertions that run without RimWorld, Unity, Harmony, or a game install.

```
sudo apt-get install -y mono-mcs mono-runtime   # once, on WSL or Linux
Tests/run-tests.sh
```

Exit code is zero only if every suite builds and every assertion holds.

## Where these came from

These suites arrived with the code in 0.7. Sizing, the production and taxation half of the economy,
military reach and faction standing were built in **Regions and Territories** and moved here once it
was clear they were faction simulation rather than world-map compatibility. R&T kept the world
layer: what a world object is (`Integration/`), where it may stand (`Placement/`), and what is in
the ground under it (`Economy/Resource*`, persisted on `GeographicProvince`).

## Why this can exist at all

The rules layers are deliberately dependency-free. `Source/Sizing/`, `Source/Economy/`,
`Source/Military/` and `Source/Standing/` contain no `Find`, no Harmony attributes, no Unity types
and no `TechLevel`; they receive world state as plain numbers or `Func` delegates, and exactly one
façade file per subsystem touches the game. That separation is the reason the rules can be compiled
against the hand-written doubles in `RimWorldStubs.cs` and executed anywhere. It is also the
precondition for 0.8's Logic Externalization, which needs to move one rule table per subsystem
rather than hunt constants through patch files.

## Compiling against a sibling checkout

These suites compile Regions-and-Territories **source**, not its DLL: `Integration/` and
`Placement/` stayed there, and the pure rules read `WorldObjectKind` and `ProvinceControl` from
them. `run-tests.sh` resolves `../Regions-and-Territories/Source` and fails with a clear message if
that checkout is missing. This mirrors the build, where `RimSynapseFactions.csproj` already
references `..\..\Regions-and-Territories\Assemblies`.

## What is and is not covered

| Suite | Covers |
|---|---|
| `SizingTests` | settlement tiers, their thresholds and their production scale |
| `ProductionTests` | abundance, labour, security and locality factors, and that they compose to 0.6's number when nothing new is known |
| `TaxationTests` | how much of a levy reaches the capital, and why growing a city is not a trap |
| `MilitaryTests` | how far a faction can project force, and what holding the ground in between buys |
| `StandingTests` | the per-faction summary published for other mods, and how strong it makes a faction look |
| `ScalingTests` | the derived security rule and the Empire resource-name table |

`ProductionTests` is the one suite that deliberately spans both repos. Extraction lives with the
province's resource state in R&T; the settlement tier that drives it lives here. Neither repo can
assert "a major city draws harder than a village" alone, and that is exactly the kind of property a
split like this one can silently break — so it is asserted here, where both halves are visible.

The type-check at the end of the run is not a behaviour test. It compiles the impure files —
`SettlementSizeUtility`, `ProductionScalingUtility`, `TaxationUtility`, `MilitaryReachUtility`,
`FactionStandingUtility` — against stub signatures written from the real ones. It cannot tell you a
patch is correct. It can tell you a patch calls a method that no longer exists, which is otherwise
invisible until RimWorld loads the assembly and Harmony throws.

Not covered, and not pretended to be: anything that needs a live world. `Factions_EmpirePatch` binds
by reflection into an optional mod, and whether those targets still resolve is an in-game check —
a silently unbound patch looks identical to a working one.

## Layout note

`Tests/` is a sibling of `Source/`, and the csproj lives in `Source/`. SDK-style projects glob
`**/*.cs` relative to the project directory, so nothing here is picked up by the mod build. Do not
move this folder under `Source/`.

The stubs are not a mock framework. They are the smallest real types that make the callers compile,
written from the actual RimWorld signatures the code meets. Where a stub returns a fixed value
(`GetProvinceForTile` returning null, `GetPopulationAtTile` returning 0), that is the case being
tested — the fallback path — not a shortcut.
