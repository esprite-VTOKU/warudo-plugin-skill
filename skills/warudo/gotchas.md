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
- **`[DataOutput]` NOT shown in panel** — use disabled DataInput + `BroadcastDataInput()` for live display
- **`AutoCompleteEntry`** uses `label`/`value` (NOT `name`)
- **`EventBus.Unsubscribe`** requires explicit type arg: `Unsubscribe<MyEvent>(id)`
- **`CharacterAsset.EnterExpression(string, bool)`** — second param is `bool`, NOT `float`
- **`Encoding.Latin1`** does NOT exist in Unity 2021.3 — use `Encoding.ASCII` or `Encoding.GetEncoding("iso-8859-1")`
- **Node/asset IDs that collide** silently fail — always generate fresh GUIDs

## UMod / Build Restrictions

- **UMod blocks `System.IO`** — use `Context.PersistentDataManager` for all file operations
- **No third-party DLLs** — cannot use NuGet packages or external DLLs
- **No `.asmdef` files** in the mod folder — scripts must be in default Assembly-CSharp
- **No `System.Reflection`** — use `SendMessage()` / `BroadcastMessage()` instead
- **`WarudoApp` is not accessible** in UMod mods — use `Context.Service` instead
- **Markdown images**: `file:///` is blocked by Vuplex — use `data:image/jpeg;base64,...` in `<img>` tags

## JSON Parsing

- Unity's `JsonUtility` chokes on `null` values and unknown fields — use manual parsing for external API responses
- Manual JSON string parsing must handle `\uXXXX` Unicode escapes (common in LLM/STT responses)

## StructuredData

- **Never use `new`** — always `StructuredData.Create<T>()`
- Parent access via generic: `StructuredData<MyAsset>` → `Parent.DoSomething()`
