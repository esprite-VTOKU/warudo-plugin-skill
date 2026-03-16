# Warudo Plugin Gotchas & Lessons Learned

Hard-won patterns from production plugin development. Read this before writing any non-trivial plugin.

## Lifecycle & Architecture

- **`SetActive(true)` in OnCreate()** — ONLY needed for **Assets**. NOT for Plugins or Nodes. Without it, Asset's OnUpdate never runs.
- **Always call `base.OnCreate()` / `base.OnDestroy()`** — base class cleanup depends on it
- **Update order each frame**: Plugins → Assets → Nodes
- **Full lifecycle order**: OnUpdate → Unity animation evaluation → OnLateUpdate → PostLateUpdate → EndOfFrame

## Watch() Gotcha

- `Watch(nameof(stringField), callback)` fires on **every keystroke** for text DataInputs
- Cannot use Watch for "submit on Enter" — Warudo doesn't distinguish Enter from typing
- This makes Watch unsuitable for chat input auto-submit; use a `[Trigger]` button instead

## Async Safety

- **`async void` methods** (required for Warudo Triggers) MUST wrap body in `try/catch` — unhandled exceptions in `async void` crash the application silently
- **TOCTOU race in fire-and-forget**: Set guard flags (`_isProcessing = true`) **synchronously** in the calling method (e.g. OnUpdate), NOT inside the async method called via `.Forget()` — another frame may run first
- Always use `try/finally` for `_isProcessing` flags to prevent stuck states on exception
- **Generation counter for stale callbacks**: When a trigger can fire multiple times (e.g. TTS button, transcription), stale async completions can overwrite the current result. Fix: increment `int _generation` on each trigger, capture it locally, check `if (_generation != captured) return;` before applying results:
  ```csharp
  private int _generation;
  [Trigger] public async void Speak() {
      int gen = ++_generation;
      var clip = await FetchAudio();
      if (_generation != gen) return; // superseded
      PlayClip(clip);
  }
  ```

## Animation & Bone Rotation

- **AnimationJob offset arrays do NOT work from plugin code** — writing to `ICharacterAnimationJob.BoneRotationOffsets` from OnUpdate doesn't propagate
- **Data connections through `OverrideCharacterBoneRotationOffsetsNode` DO work** — `Quaternion[]` flows through the graph system correctly (proven in production)
- **Direct Transform manipulation in OnLateUpdate works** but is a legacy fallback — prefer the iFacialMocap blueprint pattern (see [graph-api.md](graph-api.md))
- **Frame-rate independent smoothing**: NEVER use `Lerp(a, b, deltaTime * rate)` — use `t = 1f - Mathf.Exp(-rate * deltaTime)` then `Lerp(a, b, t)`
- **Slerp rates for bone rotation**: Rate 10+ = jittery, Rate 2 = laggy, Rate 3.5-4 = good balance for idle/speech

## Resource Management

- **Unity AudioClip is a native object** — must call `Object.Destroy(clip)` after playback or before reassignment
- **Texture2D, RenderTexture same** — always use `try/finally` to ensure cleanup on exception paths
- **`Microphone.Start()` returns an AudioClip** that persists until `Microphone.End()`
- **AudioSource parenting for scene reload resilience**: Create AudioSource under the AudioListener if one exists; otherwise use `DontDestroyOnLoad`. Prevents audio cutting out on scene transitions:
  ```csharp
  var listener = Object.FindObjectOfType<AudioListener>();
  var parent = listener != null ? listener.gameObject : /* DontDestroyOnLoad obj */;
  _audioSource = parent.AddComponent<AudioSource>();
  ```

## Per-Frame Performance

- Cache `float[]` buffers as instance fields, resize with `if (buffer.Length < needed)` — avoid per-frame allocation
- When using oversized cached buffers, iterate with explicit count, not `.Length`
- Avoid `new List<T>(dict.Keys)` — iterate known arrays/lists directly
- **Phase accumulator wrapping**: Wrap oscillator phases at `2*PI`, not at large values — large phases cause floating-point precision loss and jitter in `Mathf.Sin()`

## API Gotchas

- **`[Icon]` conflicts** — always use `[global::Warudo.Core.Attributes.Icon("name")]` to avoid UnityEngine conflict
- **`[DataOutput]`** must be a **method**, not a property or field
- **`[FlowOutput]`** must be a public **field** of type `Continuation`, not a method
- **`[Markdown]` fields** do NOT use `[DataInput]` — they are standalone
- **Use arrays, not lists, for array DataInputs** — `Quaternion[]` and `Vector3[]` work correctly as `[DataInput]` connection ports; `List<T>` also works but shows an editable list editor
- **`[Hidden]` on a `[DataInput]`** removes the port entirely (not just the inline editor)
- **`[DataOutput]` NOT shown in panel** — use disabled DataInput + `BroadcastDataInput()` for live display
- **`AutoCompleteEntry`** uses `label`/`value` (NOT `name`)
- **`EventBus.Unsubscribe`** requires explicit type arg: `Unsubscribe<MyEvent>(id)`
- **`CharacterAsset.EnterExpression(string, bool)`** — second param is `bool`, NOT `float`
- **`Encoding.Latin1`** does NOT exist in Unity 2021.3 — use `Encoding.ASCII` or `Encoding.GetEncoding("iso-8859-1")`
- **Node/asset IDs that collide** silently fail — always generate fresh GUIDs
- **NEVER use inheritance for Asset UI fields** — Warudo processes base class fields and subclass fields in separate passes. Base class fields ALWAYS render AFTER subclass fields, regardless of `order:` values. Explicit `order:` only works within the same class level. Section assignment is by source position (whichever `[Section]` is above it in source code), not by `order:`. **Keep all `[DataInput]`/`[Trigger]`/`[Section]` in a single class file** — `[CallerLineNumber]` default ordering works perfectly when everything is in one file. Use composition (helper classes) for code reuse, not inheritance

## UMod / Build Restrictions

- **UMod blocks `System.IO`** — use `Context.PersistentDataManager` for all file operations
- **No third-party DLLs** — cannot use NuGet packages or external DLLs
- **No `.asmdef` files** in the mod folder — scripts must be in default Assembly-CSharp
- **No `System.Reflection`** — UMod rejects ALL reflection APIs including sneaky ones like `GetType().Name` (compiles to `MemberInfo.get_Name()` → "illegal member reference"). Use virtual string properties or hardcoded strings instead of reflection for type names
- **`WarudoApp` is not accessible** in UMod mods — use `Context.Service` instead
- **Markdown images**: `file:///` is blocked by Vuplex — use `data:image/jpeg;base64,...` in `<img>` tags

## Scene Persistence

- **`BroadcastOpenedScene()`** only refreshes the UI — it does NOT save to disk
- **`Context.OpenedScene.Save()`** (returns UniTask) actually persists scene state to disk
- **Do NOT auto-save** after blueprint gen/remove — saving is the user's responsibility. Only call `Save()` if the user explicitly triggers a save action
- On scene load, validate that saved graph IDs still point to real graphs — auto-regenerate if the user manually deleted the graph

## PlayOneShotCharacterAnimationNode Masking

- Has BOTH `Masked` (bool) AND `MaskedBodyParts` (`AnimationMaskedBodyPart[]`)
- **Both must be set** for masking to work — `Masked=true` with empty `MaskedBodyParts` silently does nothing
- Upper body parts: `Body`, `Head`, `LeftArm`, `RightArm`, `LeftFingers`, `RightFingers`
- Full enum (`CharacterAsset.AnimationMaskedBodyPart`): `Root`, `Body`, `Head`, `LeftLeg`, `RightLeg`, `LeftArm`, `RightArm`, `LeftFingers`, `RightFingers`, `LeftFoot`, `RightFoot`

## Graph & Blueprint Patterns

- **SwitchOnStringNode: modify cases, don't rebuild** — When the list of cases changes at runtime (e.g. user edits a list of actions), update the existing Switch node's cases in-place rather than destroying and recreating the whole graph. Rebuilding destroys all user-made connections to that node.

## JSON Parsing

- Unity's `JsonUtility` chokes on `null` values and unknown fields — use manual parsing for external API responses
- Manual JSON string parsing must handle `\uXXXX` Unicode escapes (common in LLM/STT responses)

## StructuredData

- **Never use `new`** — always `StructuredData.Create<T>()`
- Parent access via generic: `StructuredData<MyAsset>` → `Parent.DoSomething()`

## Unity vs Warudo Namespace Conflicts

`Scene` and `SceneManager` both exist in `UnityEngine.SceneManagement` AND `Warudo.Core.Scenes`. Always alias them:
```csharp
using UnityScene = UnityEngine.SceneManagement.Scene;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
```
Use `UnityScene` / `UnitySceneManager` throughout instead of bare names.

## Attributes on Asset/Plugin Types

- **`[AssetType]` has no `Description` parameter** — only `Id`, `Title`, `Category`. Same for `[NodeType]` and `[PluginType]` (PluginType has `Description` but AssetType/NodeType do not)
- **`[Description("...")]` field attribute does not exist** in Warudo — use `[Label]` for field labels only
- **`OnCreate()` is `protected`** in the base class — override as `protected override void OnCreate()`
- **`OnUpdate()` is `public`** in the base `BehavioralEntity` — override as `public override void OnUpdate()`

## AutoCompleteList

- **`[AutoComplete]` methods must return `UniTask<AutoCompleteList>`**, NOT `AutoCompleteList` — use `UniTask.FromResult(...)` to wrap. Requires `using Cysharp.Threading.Tasks;`
- **`AutoCompleteList.Single()` takes `IEnumerable<AutoCompleteEntry>`**, NOT a bare `AutoCompleteEntry`
- For a single fallback entry, wrap it: `AutoCompleteList.Single(new[] { new AutoCompleteEntry { label = "...", value = "" } })`

## UniTask WhenAny with Mixed Types

- **`UniTask.WhenAny(UniTask, UniTask<T>)`** (void + typed) returns `int` (winner index), NOT a destructurable tuple
- Tuple destructure `var (isTimeout, _, response) = await UniTask.WhenAny(...)` **only works when both tasks have the same type**
- Fix: capture the result via `ContinueWith` to convert to `UniTask` (void), then read the captured value after WhenAny:
  ```csharp
  string response = null;
  var captureTask = responseTask.ContinueWith(r => response = r);
  int winner = await UniTask.WhenAny(timeoutTask, captureTask);
  ```
