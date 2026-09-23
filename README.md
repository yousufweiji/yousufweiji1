Run `.\build.ps1` on Windows with .NET Framework to build the single offline
executable. No external CEP directory is required. Browser tests require Node
development dependencies and Microsoft Edge. Native checks can run separately:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1
.\tests\run-native-scene-tests.ps1
.\tests\run-native-tests.ps1
.\tests\run-native-formats-tests.ps1
```

VenueForge Pro 3.0.0 is centered on the Build, Edit, Trace, Audit,
History, and Production workspaces. Build and Edit manage venue layouts,
zones, seats, numbering, artwork, import, repair, and persistence. Trace is
an offline CPU workflow with palette, resolution, denoise, threshold, detail,
curve, overlay, cancellation, metrics, and SVG/PDF/EPS/DXF/PNG export flows.
Production remains the advanced SVG and delivery workspace.

For the complete product, user, architecture, file-format, import/export,
testing, and release guide, see [`docs/TOOLKIT.md`](docs/TOOLKIT.md).

The authoritative venue model remains `venue-toolkit` v1. The internal
`toolkit-native-scene` adapter and `NativeCanvasForm` are retained as
developer/compatibility components for migration and regression coverage; the
native surface is not exposed as a user-facing workspace in 3.0.0.

## Current guarantees

- Structured seating SVG import preserves `ZONE_*` and `STAGE` groups.
- Generic rectangle-seat SVG files import into an `IMPORTED` zone.
- PDFs and SOP documents are rejected as non-geometry inputs with guidance to
  convert or trace them first.
- Project save/reopen, recovery, snapshots, audit, repair, and undo remain
  available in the active canvas workflow.
- CEP install, update, repair, rollback, locking, and package traversal
  protections remain covered by native tests.

## Known limitations and roadmap

- Tracing is CPU-only and retains resolution and memory limits.
- Boolean geometry and SVG parsing are partial for complex concave,
  self-intersecting, curve, arc, transform, stroke, text, mask, filter,
  gradient, symbol, and multi-artboard content.
- The active canvas is venue-oriented rather than a complete Illustrator-style
  node editor or layer hierarchy.
- The tracing benchmark is an internal regression benchmark, not proof of
  superiority over another product.
- Direct2D/Direct3D, WinUI/C++20 migration, GPU tracing, ROI retracing,
  tiled processing, typography, gradients, symbols, and multiple artboards
  remain deferred.

The optional benchmark compares the current CPU tracer with the retained v2.0
reference on local fixtures and records time, path count, contour count, path
size, and rasterized error. It does not benchmark competitors.

JSZip is distributed under its MIT notice in `src/web/JSZip-LICENSE.txt`.
