---
name: warudo
description: Create a Warudo plugin with Plugin class, Assets, and Nodes. Includes full SDK reference, blueprint generation, and all node patterns. Use when creating any Warudo plugin, asset, or node. Also use when writing Warudo C# code, modifying assets, creating blueprint graphs, or working with Warudo's scripting API.
argument-hint: "PluginName Description"
user-invokable: true
---

# Warudo Plugin Development

**User request**: $ARGUMENTS

**Behavior**: If the user is creating a new plugin, follow the full workflow (Steps 1-9). If the user is modifying existing code, adding a single node/asset, or asking a Warudo API question, skip to the relevant step and use the reference files directly. Do NOT ask 12 requirements questions for a simple edit.

---

## Architecture Overview

Warudo plugins have three entity types. Choose the right one for each piece of functionality:

```
Plugin Class (settings page — global, not per-scene)
  → API keys, endpoints, connection configs, debug toggles
  → FeatureStatus indicators, toolbar icon
  → Lifecycle: OnCreate, OnDestroy, OnUpdate (NO SetActive needed)

Asset Class (asset panel — per-scene instance)
  → User-facing interactions (triggers, status, character selection)
  → Optional: "Generate Blueprint" / "Remove Blueprints" triggers
  → Public API for nodes (version counters, state properties, DataOutputs)
  → Lifecycle: OnCreate (MUST call SetActive(true)), OnDestroy, OnUpdate

Node Classes (blueprint editor — visual scripting)
  → Flow-driven nodes: Enter/Exit pattern, driven by blueprint wiring (preferred)
  → Event/receiver nodes: autonomous OnUpdate for external signals or asset state changes
  → Flow nodes: one-shot actions triggered by flow connections
  → Data nodes: pure read-only state exposure
  → Lifecycle: OnCreate, OnDestroy, OnUpdate (NO SetActive needed)
```

**Update order each frame**: Plugins → Assets → Nodes.

---

## Step 1 — Gather Requirements (new plugin only)

Skip this step if modifying existing code. Only ask about items not already clear from the user's request:
1. **Plugin name** and **ID** (reverse-domain, e.g. `com.author.pluginname`)
2. **Display name** — shown in Warudo settings sidebar
3. **Description** — one-line summary
4. **Author name**
5. **What assets** it should register
6. **What nodes** it should register
7. **Does it need plugin settings?** (DataInputs on Plugin class)
8. **Does it need FeatureStatus indicators?**
9. **Does it need a toolbar icon?** (ToolbarItemMixin)
10. **Does it need per-scene data?** (Plugin\<TSceneData\>)
11. **Does it need to load Unity assets from the mod?** (ModHost)
12. **Does it need blueprint generation?** (Generate/Remove triggers on Asset)

For each node, classify:
- **Flow-driven daemon** — Enter/Exit pattern, per-frame processing driven by blueprint (preferred)
- **Event/receiver node** — autonomous OnUpdate for external signals or asset state changes
- **Flow node** — triggered by flow connections, performs one-shot actions
- **Data node** — pure read-only state exposure

Generate a fresh GUID for each type:
```
powershell -Command "[guid]::NewGuid().ToString()"
```

---

## Step 2 — File Structure

```
Assets/<PluginName>/
  <PluginName>Plugin.cs        ← Plugin class ([PluginType])
  Assets/                      ← Asset classes ([AssetType])
  Nodes/                       ← Node classes ([NodeType])
  <SubSystems>/                ← Internal logic classes (optional)
  Localizations/               ← Optional: localization JSON files
```

**IMPORTANT**: Do NOT include any `.asmdef` files in the mod folder. UMod cannot export scripts inside assembly definitions.

### Initial setup in Unity
1. In Unity: **Warudo > New Mod** — enter the mod name, click "Create Mod!"
2. This creates the folder under Assets and sets up the UMod export profile
3. Alternatively, create the folder manually and set up the export profile in **Warudo > Mod Settings**

---

## Step 3 — Write the Plugin Class

Use the Plugin class template from [templates.md](templates.md). The Plugin class is the entry point — it registers all assets and nodes, holds global settings, and manages the toolbar icon.

Key points:
- `[PluginType]` attribute with Id, Name, Description, Author, Version, AssetTypes, NodeTypes
- `[Mixin] public ToolbarItemMixin ToolbarItem` for toolbar icon
- `[DataInput]` fields for settings
- `GetFeatureStatusData()` for status indicators
- Optional: `Plugin<TSceneData>` for per-scene data
- Optional: `ModHost` for loading Unity assets from the mod

---

## Step 4 — Write Asset Classes

Use the Asset class template from [templates.md](templates.md). Assets appear in "Add Asset" menu with a settings panel.

Key points:
- **MUST call `SetActive(true)` in OnCreate** or OnUpdate never runs
- `[DataOutput]` methods expose data for node wiring
- Version counters (public int properties) for event nodes to poll
- Optional: Blueprint generation triggers — see [graph-api.md](graph-api.md)
- Optional: Inherit `GameObjectAsset` for 3D presence

---

## Step 5 — Write Node Classes

Use the node templates from [templates.md](templates.md). Nodes do NOT need `SetActive(true)`.

Six node patterns available:
- **A. Pure Data Node** — OnUpdate computes outputs, no flow ports
- **B. Flow Node** — FlowInput triggers one-shot action, returns Continuation
- **C. Hybrid Node** — Flow ports + OnUpdate for continuous data
- **D. Flow-Driven Daemon Node** — Enter/Exit flow-driven per-frame processing (preferred for complex plugins)
- **E. Event Node** — Polls version counters or internal buffers, fires flow on changes (the one legitimate use of autonomous `OnUpdate`)
- **E2. Signal Receiver Node** — Variant of E for external sources (WebSocket, OSC, MIDI, UDP, HTTP)
- **F. Bone Offset Node** — Direct Transform manipulation (legacy; prefer iFacialMocap pattern)

For animation plugins, prefer the **iFacialMocap blueprint pattern** — see [graph-api.md](graph-api.md).

---

## Step 6 — Attributes & UI

Full attribute reference in [sdk-reference.md](sdk-reference.md). Quick summary:

| Attribute | Purpose |
|-----------|---------|
| `[DataInput]` | Editable field (bool, int, float, string, enum, asset, array...) |
| `[DataOutput]` | Output method for blueprint wiring (NOT shown in panel) |
| `[FlowInput]` | Flow entry method, returns `Continuation` |
| `[FlowOutput]` | Flow exit field of type `Continuation` |
| `[Trigger]` | Action button |
| `[Markdown]` | Rich text display (standalone, NO `[DataInput]`) |
| `[Section("Name")]` | Group divider |
| `[Hidden]` / `[Disabled]` / `[Transient]` | Visibility control |
| `[HiddenIf]` / `[DisabledIf]` | Conditional visibility |
| `[Label]` / `[Description]` | Display text |
| `[FloatSlider]` / `[IntegerSlider]` | Slider widget |
| `[AutoComplete]` | Searchable dropdown |
| `[AutoCompleteResource]` | Resource picker |

---

## Step 7 — Context APIs

Full reference in [sdk-reference.md](sdk-reference.md). Key APIs:

- **`Context.Service`** — Toasts, dialogs, progress, navigation, BroadcastOpenedScene
- **`Context.EventBus`** — Subscribe/Unsubscribe/Broadcast events
- **`Context.PersistentDataManager`** — File I/O (no System.IO!)
- **`Context.OpenedScene`** — Asset/Graph CRUD
- **`Context.PluginManager`** — Get plugin instances
- **`Context.PluginRouter`** — Mod-to-mod signals and commands

---

## Step 8 — Blueprint Generation (Graph API)

Full reference in [graph-api.md](graph-api.md). Construction order:
1. `new Graph()` → `AddGraph()` → `AddNode<T>()` → wire connections → `Enabled = true`

Port key rules:
- DataInput = field name, DataOutput = method name
- FlowInput = method name, FlowOutput = field name

---

## Step 9 — Build & Deploy

1. **Warudo > Export Mod** in Unity
2. Confirm "BUILD SUCCEEDED!"
3. Copy `.warudo` to `Warudo_Data/StreamingAssets/Plugins/`
4. Launch Warudo

### Verify UMod .csproj (First Build Only)

If you get "No scripts found in .warudo file" errors, check `.csproj` format:
- Should start with `<Project ToolsVersion="4.0">` (NOT `<Project Sdk="Microsoft.NET.Sdk">`)
- Required IDE package versions in `Packages/manifest.json`:
  - `com.unity.ide.rider`: `3.0.28`
  - `com.unity.ide.visualstudio`: `2.0.17`
  - `com.unity.ide.vscode`: `1.2.4`
- Do NOT use `com.unity.ide.visualstudio` >= 2.0.22 or `com.unity.ide.vscode` >= 1.2.5

After downgrading: delete `.csproj`/`.sln` files → Edit > Preferences > External Tools > Regenerate → delete and recreate UMod export profile.

### Verify

- **Player.log** (`%LOCALAPPDATA%Low/HakuyaLabs/Warudo/Player.log`): look for `Found plugin type` and `Enabling`
- Set **Log Level** to "All" in Mod Settings
- Unity Editor.log: search for `error CS` during mod build

### Playground (hot-reload, no build needed)

Place scripts in `Warudo_Data/StreamingAssets/Playground/`. No namespace needed; auto-discovered.

---

## Required Using Statements

| Type | Using Statement |
|------|-----------------|
| `ToastSeverity` | `using Warudo.Core.Server;` |
| `IsNonNullAndActive()` | `using Warudo.Core.Utils;` |
| `Context`, `Context.Service` | `using Warudo.Core;` |
| `CharacterAsset` | `using Warudo.Plugins.Core.Assets.Character;` |
| `CameraAsset` | `using Warudo.Plugins.Core.Assets.Cinematography;` |
| `ToolbarItemMixin`, `PlaybackMixin` | `using Warudo.Plugins.Core.Mixins;` |
| `Asset`, `GameObjectAsset` | `using Warudo.Plugins.Core.Assets;` |
| `Node`, `Graph`, `Continuation` | `using Warudo.Core.Graphs;` |
| `PluginType`, `AssetType`, `NodeType` etc. | `using Warudo.Core.Attributes;` |
| `Plugin`, `FeatureStatusData`, `FeatureStatus` | `using Warudo.Core.Plugins;` |
| `AutoCompleteList`, `AutoCompleteEntry`, `ToggleAction` | `using Warudo.Core.Data;` |
| `.Localized()` | `using Warudo.Core.Localization;` |
| `.Forget()` (UniTask) | `using Cysharp.Threading.Tasks;` |

---

## Plugin Mod Restrictions

1. **No third-party DLLs** — cannot use NuGet packages or external DLLs
2. **No `.asmdef` files** — scripts must be in the default Assembly-CSharp
3. **No `System.Reflection`** — use `SendMessage()` / `BroadcastMessage()` instead
4. **No `System.IO`** — use `Context.PersistentDataManager` for file access
5. **`Context` requires `using Warudo.Core;`**
6. **`WarudoApp` is not accessible** in UMod mods — use `Context.Service` instead

---

## Critical Rules

1. **`SetActive(true)` in OnCreate()** — ONLY for **Assets**. NOT for Plugins or Nodes
2. **`[FlowOutput]`** is a **field** of type `Continuation`, not a method
3. **`[DataOutput]`** must be a **method**, not a property or field
4. **`[Markdown]` fields** do NOT use `[DataInput]` — standalone
5. **`[Icon]`** — use `[global::Warudo.Core.Attributes.Icon("name")]` to avoid conflict
6. **All IDs must be unique GUIDs** — generate fresh ones, never reuse
7. **Namespace**: `Warudo.Plugins.<PluginName>`
8. **Always call `base.OnCreate()`** at the start of OnCreate
9. **StructuredData** — never use `new`, always `StructuredData.Create<T>()`
10. **Graph construction order**: AddGraph → AddNode → AddConnection → Enabled=true
11. **Port keys**: DataInput=field, DataOutput=method, FlowInput=method, FlowOutput=field
12. **Flow-driven nodes**: Prefer `Enter`/`Exit` FlowInputs over `OnUpdate` — lets users control execution via blueprint wiring
13. **Blend shape API**: `AddOverrideBlendShapeEntryProvider` / `RemoveOverrideBlendShapeEntryProvider`
14. **AutoCompleteEntry** uses `label`/`value` (NOT `name`)
15. **EventBus.Unsubscribe** requires explicit type arg
16. **`DataOutput` NOT shown in panel** — use disabled DataInput + BroadcastDataInput for live display
17. **`Encoding.Latin1`** doesn't exist in Unity 2021.3 — use `Encoding.ASCII`
18. **Frame-rate independent smoothing**: `t = 1f - Mathf.Exp(-rate * deltaTime)`

For more gotchas and lessons learned, see [gotchas.md](gotchas.md).

---

## Design Philosophy: Single-Responsibility Composable Nodes

When building complex plugins (AI agents, motion capture, interactive systems), follow these principles:

### Asset = Brain, Nodes = I/O
The asset handles domain logic (state, config, API connections). Everything else is a composable node that does one thing well. Nodes are **flow-driven** — they run when the blueprint tells them to, not autonomously.

### Producer/Consumer Pattern (Speech Queue)
For async I/O between asset and nodes, use a queue pattern:
- Asset **produces**: `QueueSpeech(text, expression)` enqueues `SpeechRequest` objects
- Node **consumes** when its `Enter` FlowInput fires: checks `HasPendingSpeech`, dequeues and processes
- Preserves streaming low-latency — sentences queued individually as they arrive

### Flow-Driven Nodes (Enter/Exit Pattern — Preferred)
Nodes should NOT auto-run via `OnUpdate`. Instead, expose `Enter` FlowInput + `Exit` FlowOutput:
- `Enter` does the per-frame work (queue polling, lip sync, timer checks) and returns `Exit`
- `Exit` chains to the next node in the flow
- A single `OnUpdate` event node drives all nodes in sequence:
  ```
  OnUpdate → NodeA.Enter → NodeA.Exit → NodeB.Enter → NodeB.Exit → ...
  ```
- Users control execution order, can insert conditions, or disconnect nodes to disable them
- Event FlowOutputs (`OnResponse`, `OnTranscription`) fire independently of the Enter/Exit chain
- This replaces the old "daemon mode" pattern — no more autonomous `OnUpdate` polling in nodes
- **Exception**: Event/Signal Receiver nodes (Template E/E2) legitimately use `OnUpdate` because they wait for data from external sources or asset state changes that arrive outside the flow chain

### Blueprint = Wiring Diagram
- Generated blueprint wires nodes together (TTS ← Asset, STT → Chat → Asset)
- Users can swap, rewire, or add custom nodes at any point in the flow
- No duplicated functions — each capability exists in ONE place (one node)
- Shared types live in a common file (e.g. `SpeechTypes.cs`)

---

## Supporting Reference Files

- [templates.md](templates.md) — All code templates (Plugin, Asset, 6 node types, brand assets)
- [sdk-reference.md](sdk-reference.md) — Attributes, UI widgets, Context APIs, StructuredData, external integration
- [graph-api.md](graph-api.md) — Blueprint generation, Graph API, built-in node ports, iFacialMocap pattern
- [gotchas.md](gotchas.md) — Lessons learned, common pitfalls, anti-patterns
