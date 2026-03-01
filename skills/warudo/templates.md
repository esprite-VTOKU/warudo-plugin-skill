# Warudo Plugin Code Templates

## Plugin Class

```csharp
using UnityEngine;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Plugins;
using Warudo.Core.Server;
using Warudo.Plugins.Core.Mixins;
using Warudo.Plugins.<PluginName>.Assets;
using Warudo.Plugins.<PluginName>.Nodes;

namespace Warudo.Plugins.<PluginName>
{
    [PluginType(
        Id          = "<plugin-id>",         // e.g. "com.author.myplugin"
        Name        = "<Display Name>",
        Description = "<description>",
        Author      = "<author>",
        Version     = "1.0.0",
        Icon        = "<svg viewBox='0 0 24 24' fill='none' xmlns='http://www.w3.org/2000/svg'>" +
                      "<path fill='currentColor' d='...'/></svg>",
        SupportUrl  = "",
        AssetTypes  = new[] { typeof(<PluginName>Asset) },
        NodeTypes   = new[] { typeof(MyNode) }
    )]
    public class <PluginName>Plugin : Plugin
    {
        // ── Toolbar Icon ────────────────────────────────────────────
        private const string ToolbarIcon =
            "<svg viewBox='0 0 24 24' fill='none' xmlns='http://www.w3.org/2000/svg'>" +
            "<path fill='currentColor' d='...'/></svg>";

        [Mixin]
        public ToolbarItemMixin ToolbarItem;

        // ── Settings (shown in Warudo plugin settings page) ─────────
        [Section("Connection")]

        [DataInput]
        [Label("API Endpoint")]
        [Description("Server endpoint URL")]
        public string ApiEndpoint = "http://localhost:8000";

        [DataInput]
        [Label("API Key")]
        public string ApiKey = "";

        [Section("Debug")]

        [DataInput]
        [Label("Debug Logging")]
        public bool DebugMode = false;

        // ── Feature status (checkmarks in settings panel) ───────────
        public override FeatureStatusData[] GetFeatureStatusData() => new[]
        {
            new FeatureStatusData(
                "Connection",
                string.IsNullOrEmpty(ApiEndpoint) ? FeatureStatus.INVALID : FeatureStatus.VALID
            ),
        };

        // ── Lifecycle ───────────────────────────────────────────────
        protected override void OnCreate()
        {
            base.OnCreate();
            ToolbarItem.SetIcon(ToolbarIcon);
            ToolbarItem.SetEnabled(true);   // REQUIRED or icon won't appear
            ToolbarItem.SetTooltip("<Display Name>");
            ToolbarItem.OnTrigger = () => Context.Service.NavigateToPlugin(PluginId);
            Debug.Log("[<PluginName>] Plugin loaded!");
        }

        protected override void OnDestroy()
        {
            Debug.Log("[<PluginName>] Plugin unloaded.");
            base.OnDestroy();
        }
    }
}
```

### [PluginType] properties

| Property | Type | Purpose |
|---|---|---|
| `Id` | `string` | Unique reverse-domain ID |
| `Name` | `string` | Sidebar display name |
| `Description` | `string` | Short description |
| `Author` | `string` | Author name |
| `Version` | `string` | Semver (e.g. `"1.0.0"`) |
| `Icon` | `string` | Inline SVG string |
| `SupportUrl` | `string` | URL for support link |
| `AssetTypes` | `Type[]` | Asset classes to register |
| `NodeTypes` | `Type[]` | Node classes to register |

### Plugin lifecycle methods

| Method | Signature | When |
|---|---|---|
| `OneTimeSetup()` | `public override void` | First install only (before OnCreate) |
| `OnCreate()` | `protected override void` | Every time plugin loads |
| `OnDestroy()` | `protected override void` | Plugin unloads |
| `OnUpdate()` | `public override void` | Every frame (**NO SetActive needed**) |
| `OnPreUpdate()` | `public override void` | Before OnUpdate each frame |
| `OnLateUpdate()` | `public override void` | After OnUpdate each frame |
| `OnFixedUpdate()` | `public override void` | Each fixed timestep |
| `OnEndOfFrame()` | `public override void` | End of each frame |
| `OnSceneLoaded(Scene, SerializedScene)` | `public override void` | Scene opens |
| `OnSceneUnloaded(Scene)` | `public override void` | Scene closes |
| `OnMessageReceived(string, string)` | `public override void` | External WS message |
| `OnApplicationQuit()` | `public override void` | App closing |

### ToolbarItemMixin

```csharp
[Mixin]
public ToolbarItemMixin ToolbarItem;  // auto-instantiated, do NOT new

// In OnCreate:
ToolbarItem.SetIcon(svgString);       // inline SVG
ToolbarItem.SetEnabled(true);         // MUST call or icon won't appear
ToolbarItem.SetTooltip("My Plugin");
ToolbarItem.OnTrigger = () => Context.Service.NavigateToPlugin(PluginId);
```

SVG viewBox must be tight — crop to the actual path bounds or the icon will appear tiny.

### Loading Unity Assets from Mod (ModHost)

```csharp
var prefab = Plugin.ModHost.Assets.Instantiate<GameObject>("Assets/MyModFolder/MyPrefab.prefab");
var json = Plugin.ModHost.Assets.Load<TextAsset>("Assets/MyModFolder/config.json");
```

ModHost is `null` in Playground mode.

### Per-Scene Plugin Data

```csharp
public class MySceneConfig { public string LastUsedPreset = ""; }

[PluginType(Id = "com.author.myplugin", ...)]
public class MyPlugin : Plugin<MySceneConfig>
{
    protected override void OnCreate() { base.OnCreate(); var preset = SceneData.LastUsedPreset; }
}
```

---

## Asset Class

```csharp
using System;
using UnityEngine;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;
using Warudo.Core.Scenes;
using Warudo.Core.Server;
using Warudo.Core.Utils;
using Warudo.Plugins.Core.Assets;
using Warudo.Plugins.Core.Assets.Character;

namespace Warudo.Plugins.<PluginName>.Assets
{
    [AssetType(
        Id        = "<fresh-guid>",
        Title     = "<Asset Title>",
        Category  = "<Category>",
        Singleton = false              // true = only one instance allowed
    )]
    public class <PluginName>Asset : Asset
    {
        // ── User Settings ───────────────────────────────────────────
        [Section("Settings")]

        [DataInput]
        [Label("Character")]
        public CharacterAsset Character;

        [DataInput]
        [Label("Enabled")]
        public bool Enabled = true;

        // ── Status Display ──────────────────────────────────────────
        [Markdown]
        public string Status = "Ready";

        // ── Actions ─────────────────────────────────────────────────
        [Trigger]
        [global::Warudo.Core.Attributes.Icon("refresh")]
        public void Refresh()
        {
            Status = "**Refreshed!**";
            BroadcastDataInput(nameof(Status));
        }

        // ── Public API (for nodes to read) ──────────────────────────
        public int StateVersion { get; private set; }
        public bool IsActive => Enabled;

        [DataOutput]
        [Label("Character")]
        public CharacterAsset GetCharacter() => Character;

        // ── Lifecycle ───────────────────────────────────────────────
        protected override void OnCreate()
        {
            base.OnCreate();
            SetActive(true);  // REQUIRED for OnUpdate to run

            Watch(nameof(Enabled), () => { StateVersion++; });
        }

        protected override void OnDestroy() { }

        public override void OnUpdate()
        {
            if (!Enabled) return;
            // Per-frame logic
        }
    }
}
```

### Asset with GameObject (3D presence)

```csharp
[AssetType(Id = "<guid>", Title = "My 3D Asset", Category = "Props")]
public class My3DAsset : GameObjectAsset
{
    protected override void OnCreate() { base.OnCreate(); SetActive(true); }
    protected override GameObject CreateGameObject() => new GameObject("My3DAsset");
}
```

### Asset with Blueprint Generation

Add these to the asset when you want programmatic blueprint creation:

```csharp
// ── Blueprint State (hidden, persisted with scene) ──────────
private bool _blueprintActive;

[DataInput][Hidden]
public string GeneratedGraphId = "";

// ── Blueprint Section ───────────────────────────────────────
[Section("Blueprint")]

[Trigger]
[global::Warudo.Core.Attributes.Icon("auto_fix_high")]
[Label("Generate Blueprint")]
public void GenerateBlueprint()
{
    RemoveBlueprintGraph();
    BuildBlueprintGraph();
    _blueprintActive = true;
    Context.Service.Toast(ToastSeverity.Success,
        "Blueprint Generated", "Custom node graph created.");
}

[Trigger]
[global::Warudo.Core.Attributes.Icon("delete")]
[Label("Remove Blueprints")]
public void RemoveBlueprints()
{
    RemoveBlueprintGraph();
    _blueprintActive = false;
    Context.Service.Toast(ToastSeverity.Success,
        "Blueprints Removed", "Reverted to built-in behavior.");
}

// In OnCreate — restore blueprint state:
if (!string.IsNullOrEmpty(GeneratedGraphId))
{
    if (Guid.TryParse(GeneratedGraphId, out var guid))
        _blueprintActive = Context.OpenedScene.GetGraph(guid) != null;
}

// In OnUpdate — guard legacy behavior:
if (!_blueprintActive)
{
    // Legacy: direct character control
}
// Engine logic always runs regardless of blueprint
```

See [graph-api.md](graph-api.md) for `BuildBlueprintGraph()` / `RemoveBlueprintGraph()` implementation.

---

## Node Templates

Nodes are used in Warudo's blueprint editor. They do NOT need `SetActive(true)`.

### A. Pure Data Node

Runs every frame via OnUpdate. No flow ports.

```csharp
using UnityEngine;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;

namespace Warudo.Plugins.<PluginName>.Nodes
{
    [NodeType(
        Id       = "<fresh-guid>",
        Title    = "<Category> / <Node Name>",
        Category = "<Category>",
        Width    = 1f
    )]
    public class <NodeName> : Node
    {
        [DataInput]
        [Label("Input Value")]
        public float InputValue = 0f;

        private float _result;

        [DataOutput]
        [Label("Result")]
        public float GetResult() => _result;

        public override void OnUpdate()
        {
            _result = InputValue * 2f;
        }
    }
}
```

### B. Flow Node

Triggered by flow connections. Performs one-shot actions.

```csharp
[NodeType(Id = "<fresh-guid>", Title = "<Category> / <Name>", Category = "<Cat>", Width = 1.5f)]
public class <NodeName> : Node
{
    [DataInput][Label("Value")]
    public float Value = 0f;

    [FlowInput]
    public Continuation Enter()
    {
        _result = Value * 2f;
        return Exit;     // pass flow to Exit
        // return null;  // terminate flow
    }

    [FlowOutput] public Continuation Exit;

    private float _result;

    [DataOutput][Label("Result")]
    public float GetResult() => _result;
}
```

### C. Hybrid Node (Flow + OnUpdate)

Has flow ports AND runs OnUpdate for continuous data output.

```csharp
[NodeType(Id = "<fresh-guid>", Title = "<Category> / <Name>", Category = "<Cat>", Width = 1.5f)]
public class <NodeName> : Node
{
    [DataInput] public Asset Target;
    [DataInput] public bool Enabled = true;

    [FlowInput]
    public Continuation Execute()
    {
        if (Target == null) return null;
        _lastAction = "Executed";
        return Done;
    }

    [FlowOutput] public Continuation Done;

    private string _lastAction = "None";
    private bool _isReady;

    public override void OnUpdate() { _isReady = Target != null && Enabled; }

    [DataOutput] public bool GetIsReady() => _isReady;
    [DataOutput] public string GetLastAction() => _lastAction;
}
```

### D. Flow-Driven Daemon Node (Enter/Exit pattern — preferred)

Runs per-frame work when driven by a flow connection. Does NOT use OnUpdate for its main logic.
The blueprint controls execution order via `OnUpdate → Enter → Exit → NextNode.Enter → ...`.

```csharp
using System.Collections.Generic;
using UnityEngine;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;
using Warudo.Core.Utils;
using Warudo.Plugins.<PluginName>.Assets;
using Warudo.Plugins.Core.Assets.Character;

namespace Warudo.Plugins.<PluginName>.Nodes
{
    [NodeType(Id = "<fresh-guid>", Title = "<PluginName> / My Daemon", Category = "<Cat>", Width = 1.5f)]
    public class MyDaemonNode : Node
    {
        // ── Inputs ──────────────────────────────────────────────────
        [DataInput][Label("Controller")]
        public <PluginName>Asset Controller;

        [DataInput][Label("Character")]
        public CharacterAsset Character;

        [DataInput][Label("Intensity")]
        [FloatSlider(0.1f, 2f, 0.1f)]
        public float Intensity = 1.0f;

        // ── Flow: Enter/Exit (driven by OnUpdate event node) ────────
        [FlowInput]
        public Continuation Enter()
        {
            if (Controller == null || !Controller.IsNonNullAndActive()) return Exit;

            _output.Clear();
            // ... per-frame computation logic ...

            // Check queue if asset has pending work
            // if (Controller.HasPendingWork) ProcessWork(Controller.DequeueWork());

            return Exit;
        }

        [FlowOutput] public Continuation Exit;

        // ── Event outputs (fire independently of Enter/Exit) ────────
        [FlowOutput] public Continuation OnStateChanged;

        // ── Data outputs ────────────────────────────────────────────
        private Dictionary<string, float> _output = new Dictionary<string, float>();

        [DataOutput][Label("Output Data")]
        public Dictionary<string, float> OutputData() => _output;

        [DataOutput][Label("Character")]
        public CharacterAsset GetCharacter() => Character;
    }
}
```

Key points:
- `Enter` is called each frame by the blueprint's flow chain (`OnUpdate → Enter → Exit → ...`)
- No `OnUpdate` override for main logic — the blueprint controls when this runs
- Event FlowOutputs (`OnStateChanged`) fire independently via `InvokeFlow()` when needed
- Users control execution order by wiring the Enter/Exit chain in the blueprint editor
- To disable the node, users simply disconnect its flow — no "Enabled" toggle needed

### E. Event Node (polling version counters)

Fires flow outputs when asset state changes.

```csharp
[NodeType(Id = "<fresh-guid>", Title = "<PluginName> / On State Changed", Category = "<Cat>", Width = 1.5f)]
public class MyEventNode : Node
{
    [DataInput][Label("Controller")]
    public <PluginName>Asset Controller;

    [FlowOutput][Label("On Changed")]
    public Continuation OnChanged;

    [DataOutput][Label("Is Active")]
    public bool IsActive() => Controller != null && Controller.IsActive;

    private int _lastVersion = -1;

    public override void OnUpdate()
    {
        if (Controller == null || !Controller.IsNonNullAndActive()) return;
        int v = Controller.StateVersion;
        if (v != _lastVersion) { _lastVersion = v; InvokeFlow(nameof(OnChanged)); }
    }
}
```

### E2. Signal Receiver Node (external source — WebSocket, OSC, MIDI, UDP, etc.)

Listens for data from an external source. This is the one legitimate case for autonomous `OnUpdate` —
the data arrives outside the blueprint's flow chain, so the node must poll its own buffer.

```csharp
using System.Collections.Concurrent;
using UnityEngine;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;

namespace Warudo.Plugins.<PluginName>.Nodes
{
    [NodeType(Id = "<fresh-guid>", Title = "<PluginName> / Signal Receiver", Category = "<Cat>", Width = 1.5f)]
    public class SignalReceiverNode : Node
    {
        [DataInput][Label("Port")]
        public int Port = 9000;

        [DataInput][Label("Enabled")]
        public bool Enabled = true;

        // ── Event outputs (fire when data arrives) ──────────────────
        [FlowOutput] public Continuation OnReceived;

        // ── Data outputs ────────────────────────────────────────────
        private string _lastMessage = "";
        private bool _connected;

        [DataOutput][Label("Last Message")]
        public string GetLastMessage() => _lastMessage;

        [DataOutput][Label("Connected")]
        public bool GetConnected() => _connected;

        // ── Internal: thread-safe buffer ────────────────────────────
        // Async callbacks (network thread) enqueue here,
        // OnUpdate (main thread) dequeues and fires flow.
        private ConcurrentQueue<string> _buffer = new ConcurrentQueue<string>();

        protected override void OnCreate()
        {
            base.OnCreate();
            Watch(nameof(Port), () => { Disconnect(); if (Enabled) Connect(); });
            Watch(nameof(Enabled), () => { if (Enabled) Connect(); else Disconnect(); });
        }

        public override void OnUpdate()
        {
            if (!Enabled) return;

            // Drain buffer — fire flow for each message
            while (_buffer.TryDequeue(out var msg))
            {
                _lastMessage = msg;
                InvokeFlow(nameof(OnReceived));
            }
        }

        protected override void OnDestroy()
        {
            Disconnect();
            base.OnDestroy();
        }

        // ── Connection lifecycle ────────────────────────────────────
        private void Connect()
        {
            // Start listener (WebSocket, UDP, OSC, etc.)
            // On message received (from network thread):
            //   _buffer.Enqueue(message);
            _connected = true;
        }

        private void Disconnect()
        {
            // Stop listener, clean up
            _connected = false;
        }
    }
}
```

Key points:
- Uses `ConcurrentQueue<T>` for thread-safe handoff from network thread to main thread
- `OnUpdate` drains the buffer and fires `OnReceived` for each message — this is the **only** legitimate autonomous `OnUpdate` pattern
- Everything downstream is still flow-driven — `OnReceived` connects to other nodes' FlowInputs
- Cleanup in `OnDestroy` — always disconnect the listener

### F. Bone Offset Apply Node (direct Transform manipulation)

> **Preferred approach**: Wire `Quaternion[]` output from your daemon node through
> Warudo's built-in `OverrideCharacterBoneRotationOffsetsNode` via data connections.
> This works correctly from plugin code. Use this direct Transform approach only as
> a legacy fallback when you need to bypass the animation system entirely.

```csharp
[NodeType(Id = "<fresh-guid>", Title = "<PluginName> / Apply Bone Offsets", Category = "<Cat>", Width = 1f)]
public class ApplyBoneOffsetsNode : Node
{
    [DataInput] public CharacterAsset Character;
    [DataInput] public Quaternion[] BoneRotationOffsets;
    [DataInput][FloatSlider(1f, 20f, 0.5f)] public float SmoothRate = 6f;

    private Transform[] _boneTransforms;
    private Quaternion[] _smoothed;
    private Animator _cachedAnimator;

    public override void OnLateUpdate()
    {
        if (Character == null || !Character.IsNonNullAndActive()) return;
        if (BoneRotationOffsets == null || BoneRotationOffsets.Length == 0) return;
        var animator = Character.Animator;
        if (animator == null) return;

        if (animator != _cachedAnimator)
        {
            _cachedAnimator = animator;
            int count = (int)HumanBodyBones.LastBone;
            _boneTransforms = new Transform[count];
            _smoothed = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                _boneTransforms[i] = animator.GetBoneTransform((HumanBodyBones)i);
                _smoothed[i] = Quaternion.identity;
            }
        }

        float t = 1f - Mathf.Exp(-SmoothRate * Time.deltaTime);
        int len = Mathf.Min(BoneRotationOffsets.Length, _boneTransforms.Length);
        for (int i = 0; i < len; i++)
        {
            if (_boneTransforms[i] == null) continue;
            _smoothed[i] = Quaternion.Slerp(_smoothed[i], BoneRotationOffsets[i], t);
            if (Quaternion.Dot(_smoothed[i], Quaternion.identity) > 0.9999f) continue;
            _boneTransforms[i].localRotation *= _smoothed[i];
        }
    }
}
```

### Flow Patterns

```csharp
// Branching
[FlowInput]
public Continuation Check() => Condition ? OnTrue : OnFalse;
[FlowOutput] public Continuation OnTrue;
[FlowOutput] public Continuation OnFalse;

// Multi-output (fire several)
[FlowInput]
public Continuation Process()
{
    InvokeFlow(nameof(First));
    InvokeFlow(nameof(Second));
    return null;  // return null since we invoked manually
}
[FlowOutput] public Continuation First;
[FlowOutput] public Continuation Second;

// Delayed/manual flow
[FlowInput]
public Continuation Enter() { StartAsync(); return null; }
private void OnComplete() { InvokeFlow(nameof(Exit)); }
```

### [NodeType] properties

| Property | Type | Purpose |
|---|---|---|
| `Id` | `string` | Unique GUID |
| `Title` | `string` | Display name (use `"Category / Name"` for sub-grouping) |
| `Category` | `string` | Grouping (`CATEGORY_INPUT`, `CATEGORY_CINEMATOGRAPHY`, `CATEGORY_MOTION_CAPTURE`, or custom) |
| `Width` | `float` | Node width (default 1f, use 1.5f for wide nodes) |

### Node lifecycle

| Method | When |
|---|---|
| `OnCreate()` | Node created |
| `OnDestroy()` | Node removed |
| `OnUpdate()` | Every frame |
| `OnLateUpdate()` | After OnUpdate |
| `OnAllNodesDeserialized()` | After all nodes in blueprint deserialized |
| `OnUserAddToScene()` | User drags node into blueprint editor |

---

## Brand Assets

### VTOKU Icon

```csharp
private const string VtokuIcon =
    "<svg viewBox='68 117 706 362' xmlns='http://www.w3.org/2000/svg'>" +
    "<path fill='currentColor' d='M763.47,201.65l-53.59-74.6l-266.64,0c-29.78,0-55.87,19.92-63.7,48.65c-14.4,52.78-36.72,130.49-45.15,137.34" +
    "c-10.14,8.24-44.65,33.57-58.82,43.93c-3.46,2.53-8.28,1.93-11.01-1.38c-9.16-11.12-28.23-34.47-29.91-38.13" +
    "c-3.42-7.44-0.4-16.49,4.02-30.16c2.86-8.84,23.37-73.23,37.51-117.65c6.71-21.09-9.03-42.6-31.15-42.6l-166.61,0l57.91,75.2h47.25" +
    "c0,0-35.59,114.81-38.2,120.64c-2.61,5.83-2.01,27.75,5.03,35.99c7.04,8.24,85.2,109.33,85.2,109.33s151.46-107.93,159.5-114.56" +
    "c8.04-6.64,19.5-22.52,24.93-41.42s34.18-110.19,34.18-110.19h70.58l-80.63,255.96h82.84l79.42-256.36H763.47z'/>" +
    "</svg>";
```
