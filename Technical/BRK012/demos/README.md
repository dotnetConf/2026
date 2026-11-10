# .NET 11 Numerics live demos

One console project, six short scenarios. Each scenario has its own source file
under `Numerics` so it is easy to show or edit the relevant code during the talk.

## Requirements

Use the installed **.NET 11 GA SDK and runtime**. `global.json` selects SDK
`11.0.100` or a later stable patch in the same feature band. The application
targets `net11.0` and restores the released `System.Numerics.Tensors` **11.0.0**
package from NuGet.org. No custom runtime, source build, local package feed, or
environment setup script is required.

If a terminal still has the previous demo's runtime overrides, use a fresh
terminal before building or presenting.

## Prepare once

From the repository root:

```powershell
Set-Location .\demos
dotnet build .\Numerics.slnx -c Release
dotnet test .\Numerics.slnx -c Release --no-build
```

Build and restore before presenting. The `--no-build` commands below reuse those
outputs without a package restore or compilation on stage. The runtime version is
printed so the selected build is visible; the tensor scenario also prints its
package build identity.

## Run on stage

From `demos`:

```powershell
dotnet run --project .\Numerics -c Release --no-build -- decimal
```

| Argument | Slides | What to show |
| --- | --- | --- |
| `decimal` | 3-5 | Decimal addition, quantum, ties-to-even quantization, and the residual near an `Acos` endpoint. |
| `bfloat16` | 6 | `Half` versus `BFloat16` range/precision and direct versus double-rounded conversions. |
| `complex` | 7-8 | One `Square<T>` helper across scalar decimal and binary/decimal complex numbers. |
| `tensors` | 9-11 | A gapped view, resize zero-fill, supported/rejected overlapping copies, and NaN/tie indexes. |
| `random` | 13 | Bounded `BigInteger`, `Half` fractions, and equal/zero bounds. |
| `text` | 14 | A validated numeric prefix/unit, UTF-8 round-trips, and a hexadecimal float. |
| `all` | Rehearsal | Run all six scenarios in presentation order. This is also the default when no argument is supplied. |

Use `--help` for the selector list. To rehearse everything:

```powershell
dotnet run --project .\Numerics -c Release --no-build -- all
```

If you edit a source file, rebuild before using `--no-build`.

The rehearsal bundle also includes the built Windows x64 application. With the
.NET 11 GA runtime installed, it can run without an SDK:

```powershell
.\Numerics\bin\Release\net11.0\Numerics.exe all
```

Editing or rebuilding the C# examples still requires the .NET 11 SDK.

## Boundaries

The tensor scenario demonstrates behavior and layout, not a timing benchmark.
The performance slides use the recorded comparisons in
[dotnet/runtime#135281](https://github.com/dotnet/runtime/pull/135281). They are not
claimed as measurements from this demo application.

The live demos use .NET 11 GA. The .NET 12 slide remains a separate lookahead;
its implementation is not loaded by the demos, and its measurements are not
live-demo timings.

Random samples use a fixed seed for repeatable runs on the selected build. Seeded
sequences are not promised to remain identical across .NET versions; the tests
verify types and bounds rather than snapshotting a particular sequence.

The decimal endpoint example illustrates retaining decimal information through
a cancellation-sensitive calculation. It does not establish universal correct
rounding for transcendental functions.
