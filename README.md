# Exponential Grid Navigation System

Navigation on a 2D integer grid using only **power-of-2** vertical lines, horizontal lines, and slope ±1 diagonals with intercepts derived from powers of two. The implementation is in C# (`GridNav.cs`) and targets **.NET 8**.

For the full design write-up (line families, ID layout, and algorithm notes), see [Exponential Grid Navigation System.md](./Exponential%20Grid%20Navigation%20System.md).

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## Build

```bash
dotnet build
```

## Run (CLI)

```bash
dotnet run -- [options]
```

| Option | Description |
|--------|-------------|
| `--start <BigInteger>` | Starting **X**; **Y** is always `1` (default: `1024`) |
| `--mlr <BigInteger>` | Tolerance: each axis must lie within this distance of some `2^k` (default: `10`) |
| `--out <directory>` | Write `registry.json`, `history.json`, and `pos.json` |
| `--name <string>` | Label stored in `pos.json` (default: `run`) |
| `-h`, `--help` | Show help |

Example:

```bash
dotnet run -- --start 1000000 --mlr 10 --out ./output --name demo
```

With no arguments, the program runs a short default demo (`start=1024`, `mlr=10`).

## Programmatic API

Main entry point:

```csharp
NavigationOutcome outcome = ExponentialGridNavigator.Run(startX, mlr, name);
```

`NavigationOutcome` includes the final snapped coordinates, step count, path history, MLR, associated power-of-two exponents, and the populated `LineRegistry`.

To persist artifacts (same files as `--out`):

```csharp
ExponentialGridNavigator.WriteRunArtifacts(outcome, outputDirectory);
```

Path **verification** replays history backward using stored segment endpoints (`HistoryEntry` from/to coordinates), not ray–line intersection, so corner cases stay consistent.

## Output files (with `--out`)

| File | Contents |
|------|----------|
| `registry.json` | Serialized line registry (exponent-based line definitions) |
| `history.json` | Ordered jumps: direction, source/target line IDs, from/to coordinates |
| `pos.json` | Run summary: start/final positions as strings, MLR, exponents, step count |

## Repository layout

| Path | Role |
|------|------|
| `GridNav.cs` | Math, line types, registry, navigator, serialization |
| `Program.cs` | Command-line front end |
| `ExponentialGridNavigation.csproj` | SDK-style project file |
