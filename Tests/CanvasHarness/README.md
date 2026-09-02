# Canvas harness

Requires Windows, .NET Framework 4.7.2, MSBuild, the Desktop C++ workload and the
Windows SDK (including FXC). VS 2022 uses v143; VS 2026 uses v145. VSIX users do
not need the C++ workload: the native runtime is statically linked and packaged.

```powershell
msbuild Tests/CanvasHarness/CanvasHarness.csproj /restore /p:Configuration=Release
Tests/CanvasHarness/bin/Release/net472/CanvasHarness.exe verify
Tests/CanvasHarness/bin/Release/net472/CanvasHarness.exe 10000 mixed
Tests/CanvasHarness/bin/Release/net472/CanvasHarness.exe 100000 points
Tests/CanvasHarness/bin/Release/net472/CanvasHarness.exe 100000 lines
Tests/CanvasHarness/bin/Release/net472/CanvasHarness.exe 100000 arcs
Tests/CanvasHarness/bin/Release/net472/CanvasHarness.exe 1000000 mixed
```

`verify` checks 24 pixel-contract cases against the production WPF converter:
selection, previous state, direction caps and 100/125/200% DPI. Coverage tolerance
is one DIP, allowing up to ten isolated fringe pixels above alpha 40; this covers
WPF's flattened arc dash placement and different antialiasing. Separate assertions
check ghost holes, thickness, opacity across blocks and numerical rejection.
This is not proof of bit-identical rasterization or complete UI parity.

The numeric runs use reproducible geometry, five warm-up frames and 60 samples.
They report device initialization, scene preparation, uploads and completion of
GPU work. They assert zero uploads on camera-only changes, one changed block for
a mutation, reuse on rewind and release/blank pixels on clearing. Lines/arcs also
compare cached Direct2D paths, not geometry realizations. PNGs go to the executable
directory or to an optional third argument. Do not run other builds during timing.

GPU completion includes submission and synchronization, **not WPF/DWM
presentation**. The reference `RenderTargetBitmap` uses software rasterization.
Neither measurement is an interactive FPS claim. The row's existing load clock
covers debugger loading and scene preparation, not final presentation. The
extension emits `NeoWatch.Canvas` trace records with the render-stage timings.

## Composition probes

```powershell
Tests/CanvasHarness/bin/Release/net472/CanvasHarness.exe window
Tests/CanvasHarness/bin/Release/net472/CanvasHarness.exe window wpf
Tests/CanvasHarness/bin/Release/net472/CanvasHarness.exe window software
Tests/CanvasHarness/bin/Release/net472/CanvasHarness.exe window gpu-software
```

These open a small window to isolate WPF composition from Visual Studio. Close
the window to end the probe. `wpf` has no GPU renderer; `software` draws only WPF
with software rendering. `gpu-software` is a diagnostic-only shared-surface
readback. Production never enables that readback and uses the original WPF
renderer when WPF is in software mode.

## Manual VS check

1. Build/deploy the VSIX to the experimental instance.
2. Enable `Tools > Options > Neo Watch > General > Enable GPU canvas (experimental)`.
3. Debug `Demos/CppNeoDemo` at the `return` in `main`, after all initializers.
4. Add `stressPoints`, `stressSegments`, `stressArcs` or `f10Points` to Neo Watch.
5. Compare pan, zoom, selection, sense, autofit and rewind with the option off.
6. Stop/restart debugging and verify that the previous session is not displayed.

The memory-blueprint loader is a separate option: keep it unchanged when comparing
canvas renderers. A GPU failure falls back for the current window and logs a reason
in VS ActivityLog; the saved preference remains on. Toggle off/on to retry. No
default rollout until the remaining VS checks in `docs/canvas-rendering-plan.md`
have passed.
