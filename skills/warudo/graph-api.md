# Graph API & Blueprint Generation

## Graph Construction Order

1. `new Graph()` — creates graph with auto-generated Guid
2. `Context.OpenedScene.AddGraph(graph)` — **must be before AddNode** (sets Scene reference)
3. `graph.AddNode<T>()` — add nodes
4. `graph.AddDataConnection(...)` / `graph.AddFlowConnection(...)` — wire connections
5. `graph.Enabled = true` — enable **LAST** after all wiring

## Port Key Naming

| Port Type | Key Is | Example |
|-----------|--------|---------|
| DataInput | **field name** | `"Controller"`, `"Character"`, `"Sensitivity"` |
| DataOutput | **method name** | `"BlendShapes"`, `"GetCharacter"`, `"IsSpeaking"` |
| FlowInput | **method name** | `"Enter"`, `"StartRecording"`, `"Toggle"` |
| FlowOutput | **field name** | `"Exit"`, `"OnResponse"`, `"OnStartSpeaking"` |

## Blueprint Layout Conventions

When setting `GraphPosition` on generated nodes, follow these layout rules:

- **Left-to-right flow** — nodes follow the data/flow direction from left to right
- **Top = primary chain** — the per-frame chain (`OnUpdate → Enter → Exit → Smooth → Set → Override`) runs horizontally along the top
- **Vertical stacking by chain type** — same-column nodes are separated by function (e.g. cleanup chain below primary chain)
- **Same-chain nodes align horizontally** — nodes in the same flow chain share similar Y values
- **Connected pairs close together** — nodes with a direct flow connection go in adjacent columns (never skip a column)

Example layout grid (X spacing ~300, Y spacing ~300):
```
Y=0     OnUpdate → AICharNode → SmoothBS → SetTrackingBS → SmoothRot → OverrideBones
Y=300   OnDisable → ResetBS → ResetBones
Y=-300  (event chains: OnResponse → ToggleExpression)
```

---

## BuildBlueprintGraph Implementation

```csharp
private void BuildBlueprintGraph()
{
    var graph = new Graph();
    graph.Name = "<PluginName> Blueprint";

    // Add to scene FIRST
    Context.OpenedScene.AddGraph(graph);
    GeneratedGraphId = graph.Id.ToString();

    // Add nodes
    var daemon = graph.AddNode<MyDaemonNode>();
    daemon.GraphPosition = new Vector2(0, 0);
    daemon.SetDataInput("Controller", this, broadcast: false);

    var eventNode = graph.AddNode<MyEventNode>();
    eventNode.GraphPosition = new Vector2(0, 300);
    eventNode.SetDataInput("Controller", this, broadcast: false);

    // Wire connections
    // Data: output method name → input field name
    graph.AddDataConnection(daemon, "OutputData", targetNode, "InputFieldName");
    // Flow: output field name → input method name
    graph.AddFlowConnection(eventNode, "OnChanged", actionNode, "Enter");

    // Enable LAST
    graph.Enabled = true;
    BroadcastDataInput(nameof(GeneratedGraphId));
    Context.Service.BroadcastOpenedScene();
}

private void RemoveBlueprintGraph()
{
    if (string.IsNullOrEmpty(GeneratedGraphId)) return;
    if (Guid.TryParse(GeneratedGraphId, out var guid))
    {
        var graph = Context.OpenedScene.GetGraph(guid);
        if (graph != null) Context.OpenedScene.RemoveGraph(guid);
    }
    GeneratedGraphId = "";
    BroadcastDataInput(nameof(GeneratedGraphId));
    Context.Service.BroadcastOpenedScene();
}
```

## Asset → Node Communication

**Version counter pattern** (nodes poll in OnUpdate):
```csharp
// In Asset:
public int ResponseVersion { get; private set; }
private void OnNewResponse() { ResponseVersion++; }

// In Event Node:
private int _lastVersion = -1;
public override void OnUpdate()
{
    int v = Controller.ResponseVersion;
    if (v != _lastVersion) { _lastVersion = v; InvokeFlow(nameof(OnResponse)); }
}
```

**Producer/Consumer pattern** (speech queue, async I/O):
```csharp
// In Asset — producer:
private Queue<SpeechRequest> _queue = new Queue<SpeechRequest>();
public void QueueSpeech(string text, string expr) => _queue.Enqueue(new SpeechRequest { Text = text, Expression = expr });
public bool HasPendingSpeech => _queue.Count > 0;
public SpeechRequest DequeueSpeech() => _queue.Dequeue();

// In Daemon Node — consumer (OnUpdate):
if (Controller.HasPendingSpeech)
{
    var req = Controller.DequeueSpeech();
    PlayAsync(req).Forget();
}
```
- Preserves streaming low-latency — sentences queued individually as they arrive
- `StopResponse()` clears queue + sets cancellation token; node checks `IsCancellationRequested`

**Blueprint guard pattern** (backward compat):
```csharp
if (!_blueprintActive) { UpdateLipSync(); UpdateGestures(); }
// Engine logic always runs
```

## Blend Shape Provider API

```csharp
target.AddOverrideBlendShapeEntryProvider(myProvider);     // register
target.RemoveOverrideBlendShapeEntryProvider(myProvider);   // unregister
// NOT Register/Unregister — actual names use Add/Remove + Override prefix
```

---

## iFacialMocap Blueprint Pattern (Recommended)

Use standard Warudo nodes for animation application — the same pattern Warudo uses for face tracking. This is the **preferred** approach for any plugin that drives blend shapes or bone rotations.

```
Cleanup:    OnDisableGraph → ResetCharacterTrackingBlendShapes → ResetOverrideCharacterBones
Update:     OnUpdate → SetCharacterTrackingBlendShapes → OverrideCharacterBoneRotationOffsets
Data:       CustomDaemonNode → SmoothBlendShapeList → SetCharacterTrackingBlendShapes
            CustomDaemonNode → SmoothRotationList → OverrideCharacterBoneRotationOffsets
Expression: CustomDaemonNode.OnResponse → ToggleCharacterExpression
```

- Character reference flows via data connection from daemon node's `GetCharacter()` output
- All built-in nodes are in `Warudo.Plugins.Core.Nodes` (event nodes in `.Event` sub-namespace)
- `OverrideCharacterBoneRotationOffsetsNode` accepts `Quaternion[]` via data connections from plugin daemon nodes — this works correctly (proven in production)

### Built-in Animation Node Ports

| Node | DataInputs | DataOutputs | FlowIn | FlowOut |
|------|-----------|-------------|--------|---------|
| `OnUpdateNode` | — | — | — | `Exit` |
| `OnDisableGraphNode` | — | — | — | `Exit` |
| `SetCharacterTrackingBlendShapesNode` | `Character`, `BlendShapes`, `ApplyToAllSkinnedMeshes` | — | `Enter` | `Exit` |
| `OverrideCharacterBoneRotationOffsetsNode` | `Character`, `BoneRotationOffsets`, `Immediate` | — | `Enter` | `Exit` |
| `ResetCharacterTrackingBlendShapesNode` | `Character`, `ApplyToAllSkinnedMeshes` | — | `Enter` | `Exit` |
| `ResetOverrideCharacterBonesNode` | `Character` | — | `Enter` | `Exit` |
| `SmoothBlendShapeListNode` | `BlendShapes` (inherited), `SmoothTime` | `SmoothedBlendShapes()` | — | — |
| `SmoothRotationListNode` | `Rotations`, `SmoothTime` | `SmoothedRotations()` | — | — |
| `ToggleCharacterExpressionNode` | `Character`, `Expression`, `IsTransient`, `Action` | — | `Enter` | `Exit` |

### Graph API Gotchas

- `Graph` does NOT have a `.Nodes` property — cannot query node count after creation
- `BroadcastOpenedScene()` must be called after programmatic graph creation/removal to refresh UI
- All custom nodes must be listed in `PluginType.NodeTypes` array
