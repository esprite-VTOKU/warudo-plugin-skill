# Warudo 0.15.x — what changed for mods

Covers 0.14.4 → 0.15.0 → 0.15.2. Read this before touching anything that talks over a socket,
loads a `.warudo` built by an older SDK, or reaches another mod.

Everything in the **Ports** and **API security** sections below was read out of the shipped
`Warudo_Data/Managed/Warudo.Core.dll` / `Warudo.Plugins.Core.dll` (0.15.2), not from the
handbook — the handbook does not document the auth token yet.

---

## Version / compatibility matrix

| | Value |
|---|---|
| App version (beta channel) | **0.15.2** (0.15.1 on default, 0.14.3 on stable) |
| Unity runtime | **2021.3.45f2** — unchanged since 0.14.x |
| uMod | **2.9.9** in-app (was 2.9.0); build engine reports `2.9.9a` |
| Mod Tool SDK | **0.14.5.1+** |

**uMod cutoff:** mods exported with SDK 0.14.5.1 or newer load only on Warudo **0.14.5+**. The
loader check is `ModVersion.Parse(mod.ModCoreVersion).IsSameVersionOrLower(hostUModVersion)`, so a
bundle declaring an *older* uMod version still loads — the gate is one-directional. The Unity
version, by contrast, must match the host **exactly** (`Application.unityVersion != mod.ModUnityVersion`
→ refused). The SDK/tools version string in `modinfo.dat` is recorded but never validated.

**SDK install URL changed.** `Packages/manifest.json` should now be:

```json
"app.warudo.modtool": "https://github.com/HakuyaLabs/Warudo-Mod-Tool.git#upm/latest"
```

Pin a release with `#upm/<version>` instead of `#upm/latest`. The old
`...Warudo-Mod-Tool.git#0.14.3.5` style ref is stale — it still resolves, but it builds against
uMod 2.9.0 and will not pick up the security-check fixes below. Updating via the Package Manager
**Update** button now works.

### Breaking change for existing mods — async security checks

Before 0.14.4, uMod's code-security verifier did not look inside `async Task` / `async UniTask`
bodies. Blocked API calls there slipped through both compile and load. That hole is closed:

- Mods built with the **new** SDK: blocked calls inside async bodies now fail **at compile time**.
- Mods built with an **old** SDK: those calls are now intercepted **at load time** — the Asset or
  Node silently disappears from Warudo.

So "my asset vanished after updating Warudo" almost always means a forbidden call inside an
`async` body. Check `Player.log` for the interception, move the call out, and rebuild.

---

## API security (the big one)

0.15.x added a per-launch auth token in front of **both** local control surfaces.

From `Warudo.Core.Context`'s constructor:

```csharp
if (Application.isEditor || args.Any(it => it.ToLowerInvariant() == "--disable-api-security"))
    apiAuthToken = null;                                      // security off
else
    apiAuthToken = Guid.NewGuid().ToString("N").ToUpperInvariant();
// then: Debug.Log("One Time API Auth Token: " + apiAuthToken);
```

- The token is **new on every launch** — a 32-char uppercase hex GUID, no dashes.
- It is written to `Player.log` as `One Time API Auth Token: <TOKEN>`. That log line is the only
  way to obtain it from outside the app.
- `Context.IsApiSecurityEnabled()` is just `apiAuthToken != null`.
- `Context.CheckApiAuthToken(token)` is case-insensitive and **returns `true` when security is
  off**, so guarding with it is safe either way.

### Mods cannot read the token

```csharp
public string GetApiAuthToken(Assembly assembly) {
    if (!IsApiSecurityEnabled()) return null;
    if (assembly == typeof(Context).Assembly || assembly == Context.CoreAssembly
                                             || assembly == Context.PluginsCoreAssembly)
        return apiAuthToken;
    return "";                 // ← every mod assembly lands here
}
```

A mod asking for the token gets `""`. **A plugin-hosted dashboard therefore cannot authenticate
itself to port 19053.** Design around it:

1. Don't use 19053 from your own dashboard — drive your own Asset directly over your own HTTP
   server, or use a **WebSocket channel on 19190** (below), which has no auth.
2. Or have the user paste the token into a `[DataInput]` / into the web page.
3. Or tell the user to launch Warudo with `--disable-api-security` (document the trade-off: it
   reopens the whole local control surface to any page in their browser).

### How each surface enforces it

**WebSocket :19053** — the token is checked as a **WebSocket subprotocol**, not a header or a
query parameter:

```csharp
wsServer.AddWebSocketService("/", it => {
    if (Context.IsApiSecurityEnabled()) it.Protocol = apiAuthToken;
    service = it;
});
// on connect:
if (Context.IsApiSecurityEnabled()
    && !Context.SecWebSocketProtocols.Any(it => Context.CheckApiAuthToken(it))) /* reject */
```

From a browser, pass it as the second `WebSocket` argument:

```javascript
const ws = new WebSocket('ws://localhost:19053', [TOKEN]);   // Sec-WebSocket-Protocol
```

**HTTP :19052** — an `X-Token` request header, enforced in `OnBeforeHandler`:

```csharp
if (Context.IsApiSecurityEnabled() && !Request.Url.LocalPath.StartsWith("/api/thumbnail")) {
    if (!Context.CheckApiAuthToken(Request.Headers["X-Token"])) throw HttpException.Unauthorized();
}
```

`/api/thumbnail` is the only unauthenticated route. Everything else returns **401** without the
header.

```javascript
fetch('http://localhost:19052/api/about', { headers: { 'X-Token': TOKEN } })
```

---

## Ports

| Port | URL | Auth | What it is |
|---|---|---|---|
| **19052** | `http://localhost:19052` | `X-Token` header | REST API (`/api/about`, `/api/scenes`, …) and the OAuth callback endpoint. Bound to **localhost**. `Warudo.Core.Server.Service.Port` |
| **19053** | `ws://localhost:19053` | WS subprotocol | Editor / client control channel — the `IClient` action protocol (`setEntityDataInputPortValue`, `invokeEntityTriggerPort`, …). Bound to **localhost**. |
| **19190** | `ws://0.0.0.0:19190` | **none** | Raw message server + mod WebSocket channels. Bound to **0.0.0.0** — reachable from the LAN. `CoreSettings.WebSocketMessageServicePort` |

Port 19190 is the one to reach for in a mod. Its single service (path `/`) routes on a `Channel`
handshake header:

```csharp
protected override void OnMessage(MessageEventArgs args) {
    if (Context.Headers.ContainsKey("Channel")) {
        var channelId = Context.Headers.GetValues("Channel").First();
        Context.ExternalCallbackManager.CallWebSocketChannel(channelId, args, async m => Send(m));
    } else {
        Context.EventBus.Broadcast(new WebSocketRawMessageEvent(args.Data));   // → "On WebSocket Message Received"
    }
}
```

- **With** a `Channel` header → delivered to the owning mod's handler, which can reply.
- **Without** one → broadcast as `WebSocketRawMessageEvent`, i.e. the long-standing
  *On WebSocket Message Received* blueprint node. Unchanged.

> 19190 has **no authentication and binds to all interfaces**. Treat anything arriving there as
> untrusted and validate it before acting. Don't put a control surface on it that you would not
> expose to the local network.

---

## Warudo Link — `warudo://` and managed callbacks

New in 0.15.x: `Context.ExternalCallbackManager`, which hands a mod a **managed HTTP callback URL
or WebSocket channel** without the mod opening a socket of its own. This is the sanctioned way to
finish an OAuth flow, and it replaces hand-rolled `HttpListener` loopback redirects.

`Plugin` exposes callbacks directly (auto-revoked on destroy):

```csharp
// On Plugin: pluginId is filled in from GetTypeMeta().Id automatically.
var cb = CreateExternalCallback(async args => {
    // args: Dictionary<string,string> of the query string
    if (!args.TryGetValue("code", out var code)) return false;   // false → error page
    await ExchangeCodeForToken(code);
    return true;                                                 // true → success page
}, "Signing in to Example");                                     // optional page description

Debug.Log(cb.url);          // http://localhost:19052/callback/<pluginId>-<guid>
Debug.Log(cb.warudoLink);   // warudo://callback/<pluginId>-<guid>
Debug.Log(cb.callbackId);

RevokeExternalCallback(cb); // or let Plugin.Destroy() revoke all of this plugin's callbacks
```

Hand `cb.url` to the provider as the redirect URI; Warudo serves a styled success/failure page
from its own HTTP server and invokes your handler. `cb.warudoLink` is the same callback reached
through the registered **`warudo://` custom URL protocol** (backed by an NNG IPC listener at
`ipc:///tmp/warudo-link-callback.ipc`), for providers or desktop flows that can only deep-link.

Any `Entity` — `Plugin`, `Asset` or `Node` — can open a WebSocket channel on 19190:

```csharp
var ch = CreateWebSocketChannel(async (send, args) => {
    var reply = Handle(args.Data);               // args is websocket-sharp MessageEventArgs
    send(Encoding.UTF8.GetBytes(reply));         // two-way
});

Debug.Log(ch.channelId);     // ← the client must send this as the "Channel" header
Debug.Log(ch.websocketUrl);  // ws://0.0.0.0:19190

RemoveWebSocketChannel(ch);  // auto-removed when the entity is destroyed
```

```javascript
// the external client side — Channel is a handshake header, so it needs a real WS client,
// not the browser WebSocket API (which cannot set custom headers)
const ws = new WebSocket('ws://HOST:19190', { headers: { Channel: channelId } });
```

That header restriction is the catch: **browser pages cannot set `Channel`**, so a channel is for
native/Node/Python clients. A browser dashboard should use your own `HttpListener` (see
[web-dashboard.md](web-dashboard.md)) or the authenticated 19053 route.

---

## Mod interoperability — `Context.PluginRouter`

Mods call each other without a uMod reference dependency. Two shapes: **signals** (fire-and-forget
broadcast) and **commands** (request/response). Available on every `Entity`, so usable from a
`Plugin`, `Asset` or `Node`.

Request/response/event types are matched **structurally**, not by name — each mod declares its own
copy of the class.

```csharp
// ── Sender ────────────────────────────────────────────────────────────────
public class StatusChanged : Warudo.Core.Events.Event { public string state; }
this.EmitSignal(new StatusChanged { state = "live" });
// static form: Context.PluginRouter.EmitSignal(this, evt)

// ── Receiver ──────────────────────────────────────────────────────────────
var link = ConnectSlot<StatusChanged>("AMod.Asset", (evt, sender) => Debug.Log(evt.state));
DisconnectSlot(link);                      // also auto-disconnected on destroy

// ── Command provider ──────────────────────────────────────────────────────
var id = RegisterCommand<StatusRequest, StatusReply>("GetStatus",
             req => new StatusReply { code = 1, value = req.key });
UnregisterCommand(id);                     // also auto-unregistered on destroy

// ── Command consumer ──────────────────────────────────────────────────────
[DataInput][TypeIdFilter("AMod.Asset")] public Asset Target;   // user picks it in the editor

var result = Context.PluginRouter.ExecuteCommand<StatusRequest, StatusReply>(
                 Target, "GetStatus", new StatusRequest { key = "x" });
if (result.Status == CommandResultStatus.SUCCESS) Use(result.Data);
```

`CommandResultStatus`: `SUCCESS`, `ENTITY_NOT_FOUND`, `COMMAND_NOT_FOUND`, `EXECUTION_ERROR`.
Probe first with `Context.PluginRouter.HasCommand<TArgs>(target, "GetStatus")`.

Pro-only: the built-in Art-Net DMX plugin publishes live DMX data over this API, so an environment
mod can consume light-control data without referencing it.

Docs: <https://docs.warudo.app/docs/scripting/mod-interoperability>

---

## Updated restriction list

Current blocklist (<https://docs.warudo.app/docs/scripting/mod-limitation>) — this replaces the
shorter list in [SKILL.md](SKILL.md):

- **Packages:** `UnityEditor`, `Mono.Cecil`, `UMod-ModTools`
- **Namespaces:** `System.IO.*`, `System.Reflection`, `System.Reflection.Emit`,
  `Vexe.Fast.Reflection`, `System.Runtime.InteropServices`, `RoslynCSharp`, `RoslynCSharp.*`,
  `UMod.AssetSharing`, `UMod.Bridge`, `UMod.Debugging`, `UMod.Moddable`,
  `UMod.Moddable.Providers`, `UMod.Scripting`, `UMod.Scripting.Runtime`, `UMod.Settings`
- **Types:** `System.AppDomain`, `System.Threading.Process`, `System.Diagnostics.Process`,
  `UMod.Mod` (but **`UMod.ModHost` is allowed**)
- **Members:** `UnityEngine.Application.Quit`, `UMod.ModHost.AllLoadedModHosts`,
  `UMod.ModHost.AllModHosts`
- **P/Invoke is disabled globally** — no native interop from a mod, at all.

Still fine, and worth remembering: `System.Net.Sockets`, `System.Net.HttpListener`,
`UnityWebRequest` and `websocket-sharp` are **not** blocked. Networking is allowed; filesystem and
reflection are not.

### `Application.OpenURL` — unblocked but constrained

No longer on the blacklist. It now behaves like the old `ApplicationHelper.SafeOpenURL`:

- `http`, `https`, `mailto`, `file` only
- fully-qualified URLs only
- `file` restricted to existing paths **inside the Warudo data directory**, and the `Binaries`,
  `Clients`, `Playground` and `Plugins` directories (and their subdirectories) are off-limits
- no `.` / `..` traversal

### Relaxed Mode

0.15.x adds a consent-gated "Relaxed Mode" for risky APIs. The list of APIs it covers is
**currently empty** — nothing opts in yet. The patch notes say more of `System.IO` may move here
over time, so don't assume `System.IO` stays blocked forever, and keep using
`Context.PersistentDataManager` until it does.

---

## Mod loader controller + launch flags

0.15.2 added per-mod enable/disable and a mod list in the UI, plus recovery flags. Useful when
telling a user how to isolate a failing mod:

| Flag | Effect |
|---|---|
| `--disable-mod` | Disable **all** mods at startup. Recovery path when a mod blocks launch. |
| `--session-mod` | Mod management mode: all mods off, user enables what they need, state is **not** saved. |
| `--disable-api-security` | No auth token; 19052/19053 open. See above. |
| `--enable-webrtc` | Enables WebRTC in the embedded WebView. |

Related 0.15.2 fix: a mod throwing during load no longer prevents Warudo from starting, and
unknown Node/Asset placeholders now **preserve** the data of a missing mod instead of dropping it.

---

## Other 0.15.x items worth knowing when writing a mod

- **`.warudo` double-click install** — users can install a bundle without opening the Plugins
  folder. Still fine to tell them to copy it manually.
- **Camera Background Input** (0.15.2) — Camera Asset > Basic Properties > Background Input accepts
  **Image, Video, Spout or NDI**. A return-video path into Warudo that previously needed a
  ScreenAsset or a mod.
- **`Require Camera Depth Data`** — Camera Asset toggle, both BiRP and URP.
- **Create Key Motion** — Character Asset > Animation, authors and saves motions in-app.
- **Fixed:** `BroadcastDataInputProperties` together with `Broadcast` in the API did not refresh
  editor values. If you worked around that, the workaround can go.
- **Fixed:** nested enum classes failed to load on hot reload.
- **Fixed:** `Animancer.Lite.dll` GUID mismatch in the SDK — re-pull the SDK if you hit it.
- **Fixed:** Unity 2021 UnityTLS bug (UUM-83744 / UUM-83468) — TLS connections no longer fail with
  `UNITYTLS_INTERNAL_ERROR` after ~1665 connections. Relevant to any mod that polls an HTTPS API.
- **Director Asset is now limited to one instance** per scene (Pro). Scenes with several will not
  open on 0.15.2.
- **Scene deletion** from the Load Scene page.

---

## Sources

- Steam: [0.15.2 Update (Beta Branch)](https://store.steampowered.com/news/app/2079120) ·
  [0.15.0 Update](https://store.steampowered.com/news/app/2079120/view/695390848783747451) ·
  0.14.4 Update (Beta Branch)
- Docs: [mod-limitation](https://docs.warudo.app/docs/scripting/mod-limitation) ·
  [mod-interoperability](https://docs.warudo.app/docs/scripting/mod-interoperability)
- Ports, token handling and channel routing read from `Warudo.Core.dll` /
  `Warudo.Plugins.Core.dll` in a 0.15.2 install.
