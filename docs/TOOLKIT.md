# Yousufweiji Toolkit

Complete product and engineering guide for Yousufweiji Toolkit 3.0.0, formerly
distributed under the names Yousufweiji Toolkit and Toolkit Apex.

## 1. Product overview

Yousufweiji Toolkit is an offline Windows desktop application for building,
repairing, tracing, editing, auditing, and delivering venue seating maps. It
keeps the practical mental model used by venue teams:

**zones -> seats -> stage -> artwork -> production output**

The application is designed for work that must remain available without a
cloud connection. A single packaged executable hosts the desktop shell and
the local web workspaces. Projects remain local and can be saved, reopened,
repaired, exported, and backed up without a server account.

The current release is deliberately focused on a reliable venue workflow
rather than pretending to be a complete Illustrator replacement. It includes
strong seating-map operations and a capable SVG/production pipeline while
leaving advanced typography, gradients, symbols, GPU tracing, and a modern
GPU renderer for later releases.

## 2. Product identity

| Item | Value |
|---|---|
| Product | Yousufweiji Toolkit |
| Release | 3.0.0 |
| Executable | `build\Toolkit.exe` |
| Primary repository | `yousufweiji/yousufweiji1` |
| Release page | <https://github.com/yousufweiji/yousufweiji1/releases/tag/v3.0.0> |
| Project model | `venue-toolkit` version 1 |
| Desktop host | .NET Framework Windows executable |
| Web runtime | Local embedded web assets |
| Network requirement | None for normal editing and tracing |

Older names such as Toolkit Apex and Yousufweiji Toolkit refer to the same
product lineage. New builds should use Yousufweiji Toolkit in user-facing text,
package metadata, documentation, and release notes.

## 3. Main workspaces

## 3A. Complete tab and screen guide

### Build tab — Venue Builder

Build creates a new venue layout or imports an existing one.

- **Layout** chooses Theatre, Fan, Amphitheatre, Oval, Arena, Thrust, Club,
  or Tiered geometry.
- **Zones** controls the number of generated sections.
- **Seats / zone** controls the generated seat count.
- **Rows / zone** controls row distribution.
- **Label size** controls zone and seat-label sizing.
- **Stage shape, width, and height** define the generated stage.
- **Preview layout** creates a non-destructive preview.
- **Apply preview** commits the preview as the working map.
- **Discard preview** cancels the preview without changing the current map.
- **Import seating SVG** accepts normal Illustrator, CAD, and seating SVG
  files, including files without Toolkit-specific layer names.

Build is the recommended starting point for a new seating project.

### Edit tab — Map Contents

Edit manages the objects already in the map.

- The zone list selects a zone and its seats.
- **Select stage** selects the stage object.
- **Order** chooses row, snake, or column numbering.
- **Start at** chooses the first number.
- **Renumber selected zone** applies numbering while preserving positions.
- The canvas selection tool moves selected zones or seats.
- Rectangle, ellipse, and pen tools add artwork geometry.
- The inspector displays selection and project properties.
- Seat numbers can be shown or hidden from the canvas toolbar.

### Trace tab — Image to Vector

Trace converts local raster artwork into editable vector paths.

- **Choose an image** loads PNG, JPEG, WebP, GIF, BMP, TIFF, or supported
  first-frame/first-page variants.
- **Trace style** chooses automatic colour, limited colour, logo, photo,
  or monochrome behavior.
- **Colours** sets the palette size.
- **Resolution** controls the tracing raster.
- **Locked colours** preserves selected HEX colors.
- **Palette only** restricts output to locked colors.
- **Remove isolated colour pixels** enables denoising.
- **Path detail** controls point reduction.
- **Fit smooth curves** enables curve fitting.
- **Silhouette threshold** controls monochrome separation.
- **Minimum shape area** removes tiny shapes.
- **Remove near-white background** removes common white backgrounds.
- **Trace image** starts processing.
- **Cancel tracing** stops a running operation.
- **Original overlay** compares the source and generated result.
- **Output format** selects SVG, PDF, EPS, DXF, PNG, or an Illustrator script.
- **Output width** controls vector output size in millimetres.
- **PNG preview width** controls raster export size.
- **Trace a folder** creates a ZIP batch result.
- **Add artwork** places the traced result into the current map.

Trace never uploads the source image. It runs in the local application.

### Audit tab — Quality Check

Audit checks the active map before delivery.

- **Run map audit** reports geometry, numbering, references, and project
  consistency issues.
- **Repair seat sizes & duplicate numbers** performs supported automatic
  repairs and creates an undoable change.
- **Save audit report** saves a human-readable report.
- **Export seat manifest CSV** creates a seat list for production or ticketing
  workflows.
- **Export project package** creates a portable project package.

Audit is a review tool, not a substitute for comparing critical venue data
against an authoritative seating source.

### History tab — Version History

History protects exploratory editing.

- **Snapshot name** gives a snapshot a meaningful label.
- **Create snapshot** saves the current project state.
- Up to ten snapshots are kept with the project in the active workflow.
- Autosave recovery is stored on the current Windows computer.
- Canvas undo and redo provide short-term command recovery.

Create a snapshot before importing, repairing, renumbering, or replacing a
large layout.

### Production workspace

Production opens from **Production tools** in the desktop header or from the
canvas Production workspace bar. It works with an independent working copy
until the user explicitly returns the result to Canvas.
The workspace bundles Hall Map Production Studio v6.9 in a restricted,
offline iframe; file-library and autosave data use the desktop app's local
storage bridge.

- **Canvas editor** returns to the main venue editor.
- **Send canvas -> Production** sends the current SVG to Production.
- **Import into canvas ->** returns the reviewed SVG to the venue workflow.
- SVG loading validates size, viewBox, active content, geometry, layers,
  transforms, zones, seats, and quality information.
- Path-based seats use safe center estimation for diagnostics and fidelity
  checks.
- Production is the preferred workspace for externally authored large SVGs
  and delivery preparation.

### Top toolbar

The top toolbar is shared by the main canvas workflow:

- project name field;
- save state;
- Toolkit settings;
- Production tools;
- Open project;
- Save project (`Ctrl+S`);
- Export SVG.

### Canvas toolbar

- **Undo / Redo** (`Ctrl+Z` / `Ctrl+Y`) recover edits.
- **Fit** frames the map.
- **Zoom out / Zoom in** change the viewport.
- **Select** moves and selects geometry.
- **Rectangle**, **Ellipse**, and **Pen** create artwork.
- **Seat numbers** toggles labels.
- The status bar reports current operation and map statistics.
- Space plus drag pans; mouse wheel zooms.

### Inspector and settings

The right inspector reports the current selection and provides contextual
properties. **Toolkit settings** contains:

- Illustrator CEP extension management;
- unsigned-panel debug setting;
- CEP status and file-difference details;
- install, update, repair, uninstall, and restore controls;
- workspace side and width settings;
- inspector visibility;
- saved layout name;
- preview, save, and reset layout actions.

## 3B. CEP extension guide

The optional CEP extension connects Yousufweiji Toolkit with Adobe
Illustrator's legacy CEP panel system. It is useful when Illustrator is the
authoring or delivery environment, but the main Toolkit still works without
Illustrator.

### What the extension contains

The embedded extension bundle is identified as:

- bundle ID: `com.yousufweiji.toolkit`;
- embedded extension version: `6.5.1`;
- manifest: `CSXS/manifest.xml`;
- panel client: `client/index.html`;
- panel logic: `client/main.js`;
- bridge: `client/bridge.js`;
- builder UI: `client/builder-ui.js`;
- SOP validation: `client/sop-validator.js`;
- Illustrator JSX: `jsx/sopFunctions.jsx`;
- standalone master panel: `jsx/standalone/00_MASTER_PANEL.jsx`.

The desktop executable embeds and verifies this package. Users do not need
to manually copy a CEP directory.

### Installing the CEP extension

1. Close Adobe Illustrator completely.
2. Open **Toolkit settings**.
3. Review the CEP status.
4. Enable **unsigned CEP panels** only if the panel is unsigned and the
   organization permits that setting.
5. Click **Install**.
6. Reopen Illustrator.
7. Open **Window -> Extensions** or **Window -> Extensions (Legacy)**.
8. Select **Yousufweiji Toolkit**.

The installer verifies the embedded ZIP hash, validates required files and
manifest identity, stages the package, and then replaces the destination
atomically.

### Update

Close Illustrator, open Toolkit settings, and click **Update**. The existing
extension is moved to a timestamped backup before the new package is placed.
If replacement fails, the previous installation is restored.

### Repair

Use **Repair** when status reports **Needs repair**. The manager compares the
installed files with the embedded package and identifies missing, changed, or
unexpected files. Repair installs a verified copy and keeps a backup of the
previous state.

### Uninstall and restore

**Uninstall - keep backup** removes the active extension by moving it to the
Toolkit backup folder. The extension files are preserved for restore.
Shared CSXS `PlayerDebugMode` settings are intentionally retained because
other unsigned Adobe panels may depend on them.

Select a previous installation in the backup list and click **Restore
selected backup** to return it. The currently installed copy is backed up
before restoration.

### CEP debug setting

When enabled, Toolkit writes `PlayerDebugMode=1` for the current Windows user
under CSXS versions 9, 10, 11, and 12. This is a shared Adobe setting, not a
Toolkit-only switch. It can permit other unsigned panels. Use it only on
trusted development systems and follow organizational security policy.

The setting is not automatically removed during uninstall so another panel is
not unexpectedly broken.

### CEP safety behavior

The CEP manager:

- refuses installation while Illustrator is running;
- uses an install lock to prevent concurrent changes;
- rejects reparse points and linked installation paths;
- rejects path traversal and unexpected ZIP entries;
- rejects duplicate package paths;
- validates the manifest without external XML resolution;
- checks the embedded payload SHA-256;
- keeps timestamped backups;
- restores the old installation after replacement failure;
- reports file-level differences through Toolkit settings.

If an install error occurs, close Illustrator, do not delete the backup
folder, and use **Check status** followed by **Repair** or **Restore**.

### CEP troubleshooting

**Panel does not appear**

Confirm Illustrator is fully closed during installation, install or repair
again, enable the required unsigned-panel setting, and reopen Illustrator.

**Status says Needs repair**

Read the details list for missing, changed, unexpected, or invalid files.
Run Repair. If the new package is not desired, restore a listed backup.

**Install says Illustrator is running**

Close all Illustrator windows and background Illustrator processes, then
retry. The installer deliberately blocks changes while the host is active.

**Restore fails**

Keep the backup directory intact, close Illustrator, verify that the selected
backup is listed by Toolkit, and retry restore. The manager attempts to put
the replaced installation back if restoration fails.

**Unsigned setting concerns**

Disable the checkbox for future installs if unsigned panels are not allowed
by policy. Existing shared CSXS settings are not silently changed during
uninstall.

### Build

Build is the primary venue-map workspace. It is used to:

- create and edit zones, seats, stage geometry, and artwork;
- select one or many seats and move or modify them;
- apply seat templates and repair inconsistent seat geometry;
- number and renumber seats;
- import SVG seating plans and artwork;
- save, reopen, snapshot, undo, audit, and repair projects;
- export maps and seat data.

The canvas is venue-oriented. It is not currently a full node-based
Illustrator document editor.

### Edit

Edit provides the practical geometry and project-editing workflow around the
active venue model. It is intended for correcting imported or traced results,
cleaning zones, adjusting seats, and preparing a map for audit or production.

### Trace

Trace is an offline CPU image-tracing workflow. It supports controls for
palette, resolution, denoise, threshold, detail, curve, overlay, progress,
cancellation, and metrics. Results can be exported to:

- SVG;
- PDF;
- EPS;
- DXF;
- PNG.

The trace path is intended to be:

1. import an image or supported source;
2. tune the trace controls;
3. preview and validate the result;
4. export SVG or another required format;
5. import the SVG into Build or Production for cleanup.

Tracing remains CPU-only and retains practical resolution and memory limits.

### Audit

Audit detects common project problems such as duplicate or missing seat
numbers, invalid geometry, overlapping or incomplete data, invalid zone
references, and inconsistent templates. Repair operations are designed to be
explicit and recoverable rather than silently changing a project.

### History

History exposes project snapshots and undo/recovery operations. Import and
major repair paths create a snapshot before mutation. The intended behavior
is that a failed or unwanted operation can be reversed without losing the
previous document.

### Production

Production is the advanced SVG loading, inspection, transformation, quality
assurance, and delivery workspace. It is useful for large or externally
authored SVGs, including files produced by Illustrator, CAD tools, and other
venue systems.

Production validates and normalizes SVG input, reports zones, seats, layers,
transforms, and quality information, and provides export/delivery flows. A
recent reliability fix added safe path-center parsing for path-based seats,
including malformed-path protection.

## 4. Robust SVG import

Yousufweiji Toolkit has two related import paths:

- Build imports seating-oriented SVGs into the editable venue model.
- Production loads SVGs for advanced inspection and delivery.

### Build import detection order

The tolerant Build importer uses this hierarchy:

1. named groups containing `ZONE`, `SECTION`, `AREA`, `BLOCK`, or `SEATING`
   become zone candidates;
2. named groups containing `STAGE`, `PLATFORM`, or `PERFORMANCE` become
   stage candidates;
3. seat-sized rectangles, rounded rectangles, circles, and closed paths are
   inferred as seats;
4. large enclosing shapes containing many small seat-like shapes can become
   zone boundaries;
5. nearby numeric text can provide seat numbers and nearby labels can provide
   zone names;
6. everything not safely recognized as seating geometry is retained as
   artwork where possible.

The importer accepts ordinary, reasonably clean SVG rather than requiring
tool-generated `ZONE_*` and `STAGE` identifiers. DTD and entity declarations
are stripped before parsing. PDFs and SOP documents are not treated as SVG
geometry; they must first be converted to SVG or traced.

### Import behavior

Before applying an import, the user receives a summary including detected
zones, seats, stage geometry, artwork, warnings, and audit information. The
user can choose to replace the current project or add new zones and artwork.

Every application path creates an undo snapshot first. Imported projects run
auto-fix and audit before completion. If no seats are detected, the SVG may
still be imported as artwork instead of being rejected. The current hard
project limit is 20,000 seats, with a clear message when the limit would be
exceeded.

### Import limitations

Complex SVG content may require cleanup. The parser is not a complete
implementation of every SVG feature. The main limitations are advanced
transforms, strokes, text layout, masks, filters, gradients, symbols, and
multiple artboards. Complex concave or self-intersecting boolean geometry
also remains partial.

## 5. Seating model

The active project model is backward-compatible `venue-toolkit` version 1.
Its core concepts are:

- **project**: the complete venue document;
- **zones**: named seating or layout areas;
- **seats**: editable geometry with stable identifiers and numbers;
- **stage**: stage or performance geometry;
- **artwork**: imported or authored non-seat geometry;
- **seatTemplates**: reusable geometry defaults;
- **history/snapshots**: recoverable project states.

Seats can use configurable templates instead of one global hardcoded size.
Templates support rounded rectangles, circles, and custom-path identifiers.
Existing projects without templates continue to work through defaults and
migration logic.

Stable seat identifiers are preserved where possible during repair, import,
renumbering, and export. Duplicate or invalid numbering is reported and can
be repaired instead of silently discarded.

## 6. Numbering and repair

Numbering supports normal sequential numbering and preservation-oriented
repair. The repair path is designed to:

- preserve existing numbers where possible;
- detect duplicate numbers;
- fill missing numbers;
- apply zone-aware numbering;
- report changes;
- remain undoable.

The repair/audit system should be run after importing an externally authored
file and before production delivery.

## 7. Persistence and compatibility

The authoritative active file envelope is `venue-toolkit-file` version 1, with
the core project represented as `venue-toolkit` version 1. Optional template
fields were added without changing the existing top-level format version.
This keeps older projects usable while allowing newer builds to store richer
seat geometry.

Recommended operational practice:

1. keep the original imported file unchanged;
2. save a Yousufweiji Toolkit project copy;
3. run audit and repair;
4. create a named snapshot before major edits;
5. export the required production formats;
6. retain the source SVG and final project together.

The internal `toolkit-native-scene` model and `NativeCanvasForm` remain
compatibility and developer infrastructure. They are not the user-facing
primary editor in 3.0.0.

## 8. Export and delivery

Depending on the workspace and input, Yousufweiji Toolkit supports:

- editable project persistence;
- SVG;
- PDF;
- EPS;
- DXF;
- PNG;
- CSV/XLSX-oriented seat-list flows;
- Production SVG inspection and delivery.

Exports should be checked in Production or Audit where applicable. A
successful export does not replace a visual and data audit of a critical
venue plan.

## 9. Architecture

The current architecture is intentionally incremental:

```text
Windows executable (.NET Framework)
        |
        +-- local embedded web host
        |      +-- Build/Edit/Audit/History
        |      +-- Trace
        |      +-- Production
        |      +-- shared web geometry and project helpers
        |
        +-- native compatibility layer
               +-- NativeSceneGraph
               +-- NativeFormats
               +-- NativeCanvasForm
               +-- CEP management and safety code
```

The authoritative active venue data remains the web project model. Native
scene-graph code is retained for migration, compatibility, and regression
coverage rather than being exposed as a second competing document model.

The current renderer is GDI+/WinForms for the optional native surface and
DOM/SVG-oriented rendering for the active web workflows. Direct2D/Direct3D
and WinUI/C++20 are not part of 3.0.0.

## 10. Performance profile

The active canvas includes a simple spatial-index query so it can avoid
creating every seat element when only a viewport subset is visible. Labels
are reduced at low zoom. These are useful optimizations, but 3.0.0 is not a
guaranteed 60 FPS GPU renderer for every 10,000-seat document.

The hard project limit remains 20,000 seats. A future performance release
should consider a stronger spatial index, virtualized scene representation,
level-of-detail rendering, worker-based layout calculations, and eventually a
hardware-accelerated renderer.

## 11. Security and reliability

The application is intended to process untrusted external files safely.
Relevant protections include:

- local/offline operation;
- input normalization and validation;
- SVG active-content rejection in Production;
- DTD/entity stripping in tolerant Build import;
- malformed path handling;
- explicit file-size and seat-count limits;
- ZIP traversal protection;
- invalid image and malformed workbook rejection;
- CEP package integrity, locking, rollback, and repair checks;
- undo snapshots before mutating import and repair operations.

No importer should silently claim that unsupported or ambiguous geometry is
correct. Warnings and audit output should be reviewed for externally authored
files.

## 12. Build and installation

### Requirements

- Windows;
- .NET Framework build tools available to `build.ps1`;
- PowerShell;
- Node.js only for browser test suites and benchmarks;
- Microsoft Edge/WebView-compatible runtime for browser workspaces.

### Build

From the repository root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1
```

The output is:

```text
build\Toolkit.exe
```

The published Windows package is available from the v3.0.0 GitHub release.

### Run

Launch `build\Toolkit.exe`. The application hosts the active Build, Edit,
Trace, Audit, History, and Production workflows locally.

## 13. Tests and validation

Native validation:

```powershell
.\tests\run-native-scene-tests.ps1
.\tests\run-native-tests.ps1
.\tests\run-native-formats-tests.ps1
```

Browser and static validation, when Node.js and dependencies are installed:

```powershell
npm install
npm test
npm run benchmark
```

The native suites cover scene migration, save/reopen, stable IDs, handles,
layers, inheritance, transforms, arrange, SVG import/export, booleans,
installer safety, CEP safety, ZIP traversal, images, and workbook validation.

The browser suites cover the active web workflows, Production behavior,
static references, upgrade behavior, and regression fixtures. The benchmark
records local tracer behavior; it is not a benchmark against VectorMagic or
any other competitor.

## 14. Current guarantees

The Yousufweiji Toolkit 3.0.0 release preserves these guarantees:

- structured `ZONE_*`/`STAGE` SVG import;
- generic rectangle-seat SVG import;
- tolerant ordinary SVG import with artwork fallback;
- clear PDF/SOP non-geometry guidance;
- 20,000-seat hard-limit enforcement;
- project save/reopen and migration;
- snapshots, undo, audit, and repair;
- stable IDs and template-aware seat repair;
- Production SVG upload with path-center parsing;
- CEP install, update, repair, rollback, locking, and traversal protection;
- offline Build/Edit/Trace/Production workflow.

## 15. Known unfinished work

The following are intentionally not presented as complete:

- full Illustrator-style node editing and deep layer hierarchy;
- complete robust booleans for every concave and self-intersecting case;
- guaranteed 60 FPS for all large documents;
- GPU/WebGL/Direct2D/Direct3D renderer;
- GPU tracing;
- ROI retracing;
- tiled or spill-to-disk tracing;
- advanced typography;
- gradients and symbols;
- masks, filters, and all SVG transforms;
- multiple artboards;
- real-time collaboration and CRDT history;
- native Windows title-bar/menu modernization beyond the current host.

## 16. Recommended roadmap

### Next release: reliability and workflow

- add direct Production path-import browser regression coverage;
- improve transformed-path bounds and curve extrema;
- add more fixture-based import warnings;
- add a visible import report that can be saved with the project;
- improve large-document virtualization and seat-label throttling.

### Professional release

- unify geometry services behind a typed shared model;
- implement stronger spatial indexing and level-of-detail rendering;
- add group operations, alignment, distribution, and snapping;
- improve layer targeting, locking, isolation, and ordering;
- add named snapshots and branching history;
- improve native file dialogs, recent files, drag/drop, and printing.

### Future enterprise release

- hardware-accelerated rendering;
- background trace workers with tiled processing;
- CAD/PDF floorplan extraction improvements;
- production-mode web publishing;
- optional collaboration;
- expanded accessibility and screen-reader semantics.

## 17. Release checklist

Before distributing a new build:

1. update the product version in package and desktop metadata;
2. build the executable from a clean source tree;
3. run native scene, installer, and format suites;
4. run browser suites when Node.js is available;
5. manually test Build SVG import, Trace export, Production SVG upload,
   save/reopen, audit, repair, and undo;
6. verify no stale product name or version remains in active UI;
7. create a versioned ZIP containing the executable;
8. publish release notes that distinguish complete features from limitations;
9. retain the source commit and generated artifact together.

## 18. License and third-party notices

Project licensing is described in [`LICENSE`](../LICENSE). JSZip is included
with its MIT notice at
[`src/web/JSZip-LICENSE.txt`](../src/web/JSZip-LICENSE.txt).
