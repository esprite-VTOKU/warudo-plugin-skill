# Warudo SDK Reference

## Attribute & UI Reference

### DataInput Types → UI Widgets

| C# Type | Widget |
|---|---|
| `bool` | Toggle checkbox |
| `int` | Number text field |
| `int` + `[IntegerSlider(min, max)]` | Drag slider |
| `float` | Number text field |
| `float` + `[FloatSlider(min, max, step)]` | Drag slider |
| `string` | Single-line text input |
| `string` + `[MultilineInput]` | Expandable text area |
| `string` + `[Markdown]` | Rendered rich text / HTML |
| `string` + `[PreviewGallery]` | Thumbnail grid picker |
| `string` + `[AutoComplete]` | Searchable dynamic list |
| `string` + `[AutoCompleteResource("Type")]` | Warudo resource library picker |
| `Vector2` | X / Y fields |
| `Vector3` | X / Y / Z fields |
| `Quaternion` | W / X / Y / Z fields |
| `Color` | RGBA color picker |
| `Color` + `[HDR]` | HDR color picker |
| `Enum` | Standard dropdown |
| `Enum` + `[CardSelect]` | Horizontal card buttons |
| `ToggleAction` | 3-way button (Toggle/Enable/Disable) — in `Warudo.Core.Data` |
| `Asset` / `CharacterAsset` / `CameraAsset` etc. | Type-filtered asset picker |
| `TransformData` | Position + Rotation + Scale |
| `PositionRotationData` | Position + Rotation |
| `T[]` | Expandable list with +/− buttons |

### DataOutput Valid Return Types

`bool`, `int`, `long`, `float`, `string`, `bool[]`, `int[]`, `float[]`, `string[]`, `Vector3[]`, `Quaternion[]`,
`Vector2`, `Vector3`, `Vector4`, `Quaternion`, `Color`,
`TransformData`, `PositionRotationData`, `ToggleAction`,
`Asset` (and subclasses), any `enum`, `T[]`

### Labels & Descriptions

```csharp
[Label("Display name")]         // override field name
[HideLabel]                     // remove label entirely
[Description("Tooltip text")]   // tooltip on hover
[DefaultLabel("None")]          // placeholder when null/empty
```

### Sections

```csharp
[Section("Group Name")]
[DataInput] public float FirstInGroup;  // all below until next [Section]

[Section("Advanced")]
[SectionHiddenIf(nameof(AdvancedMode), Is.False)]  // hide entire section
```

### Conditional Visibility

All conditional attributes (`[HiddenIf]`, `[DisabledIf]`, `[SectionHiddenIf]`) share three forms:

```csharp
// 1. Method predicate
[HiddenIf(nameof(ShouldHide))]
protected bool ShouldHide() => SomeThing && AnotherThing;

// 2. Is check (null/active/bool)
[HiddenIf(nameof(MyBool), Is.True)]
[HiddenIf(nameof(MyBool), Is.False)]
[HiddenIf(nameof(MyAsset), Is.Null)]
[HiddenIf(nameof(MyAsset), Is.NonNull)]
[HiddenIf(nameof(MyAsset), Is.NullOrInactive)]
[HiddenIf(nameof(MyAsset), Is.NonNullAndActive)]

// 3. If comparison (value)
[HiddenIf(nameof(MyInt), If.Equal, 0)]
[HiddenIf(nameof(MyInt), If.NotEqual, 0)]
[HiddenIf(nameof(MyFloat), If.Greater, 1f)]
[HiddenIf(nameof(MyFloat), If.Less, 0f)]
[HiddenIf(nameof(MyObj), If.InstanceOf, typeof(SpecificType))]
```

### Unconditional Hide/Disable

```csharp
[Hidden]    // always hidden — field still serialized to scene
[Disabled]  // always greyed out
[Transient] // NOT saved to scene; resets on scene reload
```

### AutoComplete Dropdowns

```csharp
[DataInput]
[AutoComplete(nameof(GetOptions), forceSelection: false, "Placeholder...")]
public string Choice = "";

public UniTask<AutoCompleteList> GetOptions() => UniTask.FromResult(
    AutoCompleteList.Single(new List<AutoCompleteEntry>
    {
        new AutoCompleteEntry { label = "Display Name", value = "stored_value" },
    }));

// Grouped (uses constructor — this is the ONE case where constructor works):
return UniTask.FromResult(new AutoCompleteList { categories = new List<AutoCompleteCategory> {
    new AutoCompleteCategory { title = "Group", entries = new List<AutoCompleteEntry> { ... } }
}});

// WARNING: AutoCompleteList uses static factory methods, NOT constructor for flat lists.
// Do NOT use `new AutoCompleteList { entries = ... }` — that field doesn't exist.
// Use AutoCompleteList.Single(entries) or AutoCompleteList.Message(text) instead.

// Status message:
return AutoCompleteList.Message("No devices found.");
```

### Resource Pickers

```csharp
[AutoCompleteResource("Image", "None")]    // Image, Sound, Music, Video, Particle,
[AutoCompleteResource("CharacterAnimation")] // Prop, Character, Environment, LUT, LensDirt
```

### Triggers

```csharp
[Trigger]
[global::Warudo.Core.Attributes.Icon("play_arrow")]  // Material Design icon names
public void DoThing() { }

// Async trigger
[Trigger]
public async void DoAsyncWork() { await UniTask.Delay(1000); }
```

### Markdown Display

```csharp
[Markdown]  // NO [DataInput] — standalone
public string Info = "## Heading\n**bold**, *italic*";

[DataInput][HideLabel][Markdown(primary: true)]
public string BigHeader = "## Big Header";

// Images: file:/// is BLOCKED by Vuplex. Use base64 data URIs:
[DataInput][HideLabel][Markdown]
public string Image = "<img src='data:image/jpeg;base64,...' style='width:100%' />";
// Remote https:// URLs also work
```

### Live-Updating Read-Only Display

`[DataOutput]` is NOT shown in the asset panel (blueprint wires only). For live panel display:

```csharp
[DataInput][Label("Status")][DisabledIf(nameof(AlwaysTrue))]
public string Status = "—";
protected bool AlwaysTrue() => true;

// Update: Status = "new"; BroadcastDataInput(nameof(Status));
```

### Type-Filtered Asset Picker

```csharp
[DataInput]
[TypeIdFilter("com.warudo.core.camera")]
public Asset TargetCamera;
```

### Watchers

```csharp
Watch(nameof(MyInput), () => { /* changed */ });
Watch<float>(nameof(MyFloat), (from, to) => { /* with old/new */ });
WatchAll(new[] { nameof(A), nameof(B) }, () => { /* any changed */ });
WatchAsset(nameof(MyCharacter), () => { /* asset changed or active state changed */ });
WatchAssetState(nameof(MyAsset), active => { /* active state toggled */ });
```

### Programmatic Data Access

```csharp
SetDataInput(nameof(Status), "New value", broadcast: true);
// Or:
Status = "New value";
BroadcastDataInput(nameof(Status));
```

---

## PlaybackMixin

Adds a media player control bar (seek slider, play/pause/stop, volume, loop) to a Plugin or Asset panel. Used for audio/video playback assets.

```csharp
[Mixin]
public PlaybackMixin Playback;  // auto-instantiated, do NOT new

// In OnCreate — enable with total duration:
Playback.Enable(clipLengthInSeconds);
Playback.SetUseVolume(true);     // show volume slider (default hidden)
Playback.SetUseLoop(true);       // show loop toggle (default hidden)
Playback.SetSeekInRealtime(true); // seek fires continuously while dragging (vs on release)

// Wire callbacks:
Playback.OnPlay  = () => { /* start playback */ };
Playback.OnPause = () => { /* pause playback */ };
Playback.OnStop  = () => { /* stop playback */ };
Playback.OnSeek  = (float time) => { /* seek to time in seconds */ };

// In OnUpdate — feed current state:
Playback.Update(isPlaying: true, currentTime: audioSource.time);

// Read state:
float vol = Playback.Volume;     // 0-1, default 0.5
bool loop = Playback.IsLooping;

// Hide/show the entire control bar:
Playback.IsHidden = true;

// Disable when done:
Playback.Disable();
```

---

## CharacterAsset Expression API

Direct expression control without blueprint nodes:

```csharp
// Enter a VRM expression (bypasses ToggleCharacterExpressionNode)
Character.EnterExpression("happy", transient: true);  // second param is bool, NOT float

// Exit an expression
Character.ExitExpression("happy");

// Guard against re-entering the same expression
if (_currentExpression != newExpression)
{
    if (!string.IsNullOrEmpty(_currentExpression))
        Character.ExitExpression(_currentExpression);
    Character.EnterExpression(newExpression, transient: true);
    _currentExpression = newExpression;
}
```

---

## StructuredData

For complex embedded data in Assets or Nodes:

```csharp
public class MyData : StructuredData
{
    [DataInput] public Vector3 Position;
    [DataInput] public float Speed = 1f;

    [Trigger]
    public void Reset() { Position = Vector3.zero; Broadcast(); }
}

// Usage:
[DataInput] public MyData Settings;        // single instance
[DataInput] public MyData[] SettingsList;   // array (users can add/remove in UI)

// Programmatic creation — NEVER use new:
var data = StructuredData.Create<MyData>();

// Parent access:
public class MyData : StructuredData<MyAsset> { void Foo() => Parent.DoSomething(); }

// Initializer:
[StructuredDataInitializer(nameof(Init))]
[DataInput] public float Speed = 1f;
private void Init() { Speed = 2f; }
```

---

## Context APIs

### Context.Service (requires `Warudo.Core` + `Warudo.Core.Server`)

```csharp
// Toasts (ToastSeverity: Success | Info | Warning | Error)
Context.Service.Toast(ToastSeverity.Success, "Done!", "Operation completed.");

// Dialogs
Context.Service.PromptMessage("Title", "Message body", dismissable: true);
bool ok = await Context.Service.PromptConfirmation("Sure?", "This cannot be undone.");
await Context.Service.PromptStructuredDataInput("Configure", myStructuredData);

// Progress
Context.Service.ShowProgress("Loading...", 0.5f, timeout: TimeSpan.FromSeconds(10));
Context.Service.HideProgress();

// Navigation
Context.Service.NavigateToPlugin(PluginId);
Context.Service.NavigateToAsset(assetGuid, portName);
Context.Service.NavigateToGraph(graphGuid, nodeGuid);

// Sync
Context.Service.BroadcastOpenedScene();
```

### Context.EventBus

```csharp
var subId = Context.EventBus.Subscribe<KeystrokePressedEvent>(e => Debug.Log(e.Keystroke));
Context.EventBus.Unsubscribe<KeystrokePressedEvent>(subId);  // type arg REQUIRED
Context.EventBus.Broadcast(new MyCustomEvent { Value = 42 });
```

**Built-in events** (from `Warudo.Plugins.Core.Events`):
`KeystrokePressedEvent`, `KeystrokeReleasedEvent`, `MouseMovedEvent`, `MousePressedEvent`,
`ClientConnectedEvent`, `AssetAddEvent`, `AssetRemoveEvent`, `GraphEnableEvent`, `GraphDisableEvent`,
`EnvironmentWillLoadEvent`, `EnvironmentLoadedEvent`, `SceneSaveEvent`,
`OnScreenBrowserMessageEvent`, `WebSocketRawMessageEvent`

### Context.PersistentDataManager (no System.IO!)

```csharp
var pdm = Context.PersistentDataManager;
pdm.WriteFile("config.json", jsonString);
string text = pdm.ReadFile("config.json");
pdm.WriteFileBytes("data.bin", bytes);
byte[] data = pdm.ReadFileBytes("data.bin");
await pdm.WriteFileAsync("config.json", jsonString);
bool exists = pdm.HasFile("config.json");
string full = pdm.GetFullPath("config.json");
pdm.DeleteFile("config.json");
```

### Context.OpenedScene

```csharp
var scene = Context.OpenedScene;
IReadOnlyList<Asset> all = scene.GetAssetList();
Asset byGuid = scene.GetAsset(someGuid);
MyAsset a = scene.AddAsset<MyAsset>();
MyAsset a = scene.GetOrAddAsset<MyAsset>();  // singleton-safe
scene.RemoveAsset(asset.Id);

IReadOnlyList<Graph> graphs = scene.GetGraphList();
Graph g = scene.GetGraph(graphGuid);
scene.AddGraph(myGraph);
scene.RemoveGraph(graphGuid);
```

### Context.PluginManager

```csharp
MyPlugin p = Context.PluginManager.GetPlugin<MyPlugin>();
Plugin p = Context.PluginManager.GetPlugin("com.vtoku.artnet");
```

### Context.PluginRouter (mod-to-mod)

```csharp
// Signals (fire-and-forget)
Context.PluginRouter.EmitSignal(this, new MySignal { Value = 42 });
var slotId = Context.PluginRouter.ConnectSlot<MySignal>(sourceEntity, "MySignal", signal => { });

// Commands (request-response)
var id = Context.PluginRouter.RegisterCommand(this, "myCommand",
    (MyArgs args) => new CommandResult<MyResult> { Data = new MyResult() });
```

### Localization

```csharp
// In Localizations/ folder:
// { "en": { "MY_KEY": "English" }, "ja": { "MY_KEY": "Japanese" } }
"MY_KEY".Localized()  // requires using Warudo.Core.Localization;

// Runtime registration:
Context.LocalizationManager.SetLocalizedString("en", "my.key", "Hello {0}!");
```

---

## External Integration

### WebSocket Protocol (port 19053)

External apps control Warudo by connecting to `ws://localhost:19053`:

```json
{ "action": "setEntityDataInputPortValue", "data": { "entityId": "guid", "portKey": "Speed", "value": "2.5", "broadcast": true } }
{ "action": "invokeEntityTriggerPort", "data": { "entityId": "guid", "portKey": "DoThing" } }
{ "action": "sendPluginMessage", "data": { "pluginId": "com.vtoku.artnet", "action": "setUniverse", "payload": "1" } }
```

Receive in plugin: `public override void OnMessageReceived(string action, string payload) { }`

### REST API (port 19052)

```
GET  /api/about          → version, plugins
GET  /api/scenes         → all scenes
PUT  /api/openedScene    → switch scene
```

### Push to WS clients from C#

```csharp
Context.ServiceMessageQueue.QueueMessage("myUpdate", jsonPayload);
```

### ScreenAsset JavaScript Bridge

```javascript
// JS → C#:
window.vuplex.postMessage(JSON.stringify({ type: 'status', value: 42 }));
// C# → JS: via "Send Message To Screen Browser" node
window.vuplex.addEventListener('message', e => { const data = JSON.parse(e.data); });
```
