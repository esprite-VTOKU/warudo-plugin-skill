# Warudo Web Dashboard Integration

How to build web dashboard interfaces that control Warudo from a browser or embedded ScreenAsset. Three tiers of integration, from zero-code to full plugin-hosted dashboards.

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│  Warudo                                                          │
│  ┌────────────────────┐   ┌────────────────────────────────────┐ │
│  │  WS Server :19053  │   │  REST API :19052                   │ │
│  │  (IClient protocol)│   │  GET /api/about, /api/scenes, etc. │ │
│  └─────────┬──────────┘   └────────────────────────────────────┘ │
│            │                                                      │
│  ┌─────────┴──────────┐   ┌────────────────────────────────────┐ │
│  │  EventBus           │   │  ServiceMessageQueue               │ │
│  │  WebSocketRawMsg    │   │  Push custom JSON to WS clients   │ │
│  │  OnScreenBrowserMsg │   │                                    │ │
│  └─────────────────────┘   └────────────────────────────────────┘ │
│                                                                    │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  Plugin HttpListener (Tier 2)                                 │ │
│  │  Serves HTML/JS/CSS from plugin folder on custom port         │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                    │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  ScreenAsset (Tier 3) — Vuplex embedded browser               │ │
│  │  JS ↔ C# via vuplex.postMessage / addEventListener            │ │
│  └───────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────┘
         ▲
         │ ws://localhost:19053
         ▼
┌──────────────────────┐
│  Browser / External  │
│  dashboard.html      │
└──────────────────────┘
```

---

## Tier 1 — WebSocket Protocol (No Plugin Code Needed)

Any web page can connect to `ws://localhost:19053` and send/receive JSON to control Warudo directly.

### Message Envelope

```json
{ "action": "actionName", "data": { ...params... } }
```

### IClient Protocol Actions

| Action | Required Data Fields | Effect |
|---|---|---|
| `setEntityDataInputPortValue` | `entityId`, `portKey`, `value`, `broadcast` | Set any DataInput on any asset/node |
| `invokeEntityTriggerPort` | `entityId`, `portKey` | Click any `[Trigger]` button |
| `invokeFlowAtInput` | `graphId`, `nodeId`, `inputPortKey` | Fire a FlowInput on a node |
| `sendPluginMessage` | `pluginId`, `action`, `payload` | Call `Plugin.OnMessageReceived` |
| `setSelectedAsset` | `assetId` | Select an asset in the panel |
| `setGraphEnabled` | `graphId`, `enabled` | Enable/disable a blueprint graph |
| `addAssetOfType` | `type` | Add an asset by type name |
| `removeAsset` | `assetId` | Remove an asset |
| `getPlugins` | _(none)_ | List all loaded plugins |

The `value` field in `setEntityDataInputPortValue` is a **JSON-serialized string** of the value:
- Boolean: `"true"` / `"false"`
- Number: `"2.5"`
- String: `"\"hello\""`
- Color: `"{\"r\":1,\"g\":0,\"b\":0,\"a\":1}"`

### Typed Action Data (Blueprint Integration)

External clients can trigger the built-in `On WebSocket Action` node:

```json
{ "action": "triggerAction", "data": { "action": "myAction", "type": "Float", "value": 0.75 } }
```

Supported types: `bool`, `int`, `float`, `string`, `Vector3`, `Quaternion`, and their list variants, plus raw `JToken`.

### Minimal Browser Client

```javascript
const ws = new WebSocket('ws://localhost:19053');

ws.onopen = () => {
    // Set a DataInput
    ws.send(JSON.stringify({
        action: 'setEntityDataInputPortValue',
        data: { entityId: 'asset-guid', portKey: 'Speed', value: '2.5', broadcast: true }
    }));

    // Invoke a trigger
    ws.send(JSON.stringify({
        action: 'invokeEntityTriggerPort',
        data: { entityId: 'asset-guid', portKey: 'DoThing' }
    }));

    // Send plugin message
    ws.send(JSON.stringify({
        action: 'sendPluginMessage',
        data: { pluginId: 'com.author.plugin', action: 'myAction', payload: '{"key":"value"}' }
    }));
};

ws.onmessage = e => {
    const msg = JSON.parse(e.data);
    if (msg.type === 'frameUpdate') return; // filter noisy per-frame updates
    console.log('From Warudo:', msg);
};
```

### Receiving WS Messages in C#

```csharp
// Subscribe to ALL raw WS messages:
_wsSub = Context.EventBus.Subscribe<WebSocketRawMessageEvent>(e => {
    Debug.Log($"Raw WS: {e.RawMessage}");
});

// In OnDestroy — explicit type arg required:
Context.EventBus.Unsubscribe<WebSocketRawMessageEvent>(_wsSub);
```

Or use the `On WebSocket Raw Message` blueprint node (no code needed).

---

## Tier 2 — Plugin-Hosted HTTP Server (Serve Dashboard Files)

Bundle a web dashboard with your plugin by hosting a `System.Net.HttpListener` inside an Asset. The served page connects to Warudo's WS on port 19053 for live control.

### Dashboard Asset Template

```csharp
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Scenes;
using Warudo.Core.Server;
using Warudo.Plugins.Core.Events;

[AssetType(Id = "<fresh-guid>", Title = "Web Dashboard", Category = "<Category>")]
public class WebDashboardAsset : Asset
{
    // ── Settings ──────────────────────────────────────────────────
    [Section("HTTP Server")]

    [DataInput]
    [Label("Port")]
    [IntegerSlider(1024, 65535)]
    [Description("HTTP port for the dashboard. Access at http://localhost:<port>/")]
    public int Port = 7000;

    [DataInput]
    [Label("Auto-start")]
    [Description("Start the HTTP server when this asset is created.")]
    public bool AutoStart = true;

    // ── Status ────────────────────────────────────────────────────
    [Section("Status")]

    [DataInput][Label("HTTP server")][Disabled]
    public string ServerStatus = "Stopped";

    [DataInput][Label("Last WS message")][Disabled]
    public string LastWsMessage = "—";

    // ── Actions ───────────────────────────────────────────────────
    [Section("Actions")]

    [Trigger]
    [Label("Start server")]
    [global::Warudo.Core.Attributes.Icon("play_arrow")]
    public void StartServer() => StartHttpServer();

    [Trigger]
    [Label("Stop server")]
    [global::Warudo.Core.Attributes.Icon("stop")]
    public void StopServer() => StopHttpServer();

    [Trigger]
    [Label("Open in browser")]
    [global::Warudo.Core.Attributes.Icon("open_in_browser")]
    public void OpenBrowser() =>
        System.Diagnostics.Process.Start($"http://localhost:{Port}/dashboard.html");

    // ── WS Broadcast ─────────────────────────────────────────────
    [Section("WS Broadcast")]

    [DataInput][Label("Payload")]
    public string DemoPayload = "{\"hello\":\"from Warudo\"}";

    [Trigger]
    [Label("Broadcast to clients")]
    [global::Warudo.Core.Attributes.Icon("wifi_tethering")]
    public void BroadcastDemo() =>
        Context.ServiceMessageQueue.QueueMessage("dashboardDemo", DemoPayload);

    // ── Implementation ────────────────────────────────────────────
    private HttpListener _listener;
    private CancellationTokenSource _cts;
    private System.Guid _wsSub;

    protected override void OnCreate()
    {
        base.OnCreate();
        SetActive(true);

        // Tier 1: subscribe to raw WS messages
        _wsSub = Context.EventBus.Subscribe<WebSocketRawMessageEvent>(e => {
            LastWsMessage = e.RawMessage.Length > 80
                ? e.RawMessage.Substring(0, 80) + "…"
                : e.RawMessage;
            BroadcastDataInput(nameof(LastWsMessage));
        });

        Watch(nameof(Port), () => {
            StopHttpServer();
            if (AutoStart) StartHttpServer();
        });

        if (AutoStart) StartHttpServer();
    }

    protected override void OnDestroy()
    {
        Context.EventBus.Unsubscribe<WebSocketRawMessageEvent>(_wsSub);
        StopHttpServer();
        base.OnDestroy();
    }

    void StartHttpServer()
    {
        if (_listener != null) return;
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        try
        {
            _listener.Start();
            ServerStatus = $"Serving on http://localhost:{Port}/";
            BroadcastDataInput(nameof(ServerStatus));
            ServeLoopAsync(_cts.Token).Forget();
        }
        catch (System.Exception ex)
        {
            _listener = null;
            ServerStatus = $"Error: {ex.Message}";
            BroadcastDataInput(nameof(ServerStatus));
        }
    }

    void StopHttpServer()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        _listener = null;
        ServerStatus = "Stopped";
        BroadcastDataInput(nameof(ServerStatus));
    }

    async UniTaskVoid ServeLoopAsync(CancellationToken ct)
    {
        // Serve files from the plugin's folder
        var root = System.IO.Path.Combine(
            Application.streamingAssetsPath, "Playground", "Reference");

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                // RunOnThreadPool prevents blocking Unity's main thread
                ctx = await UniTask.RunOnThreadPool(
                    () => _listener.GetContext(), cancellationToken: ct);
            }
            catch { break; }

            // CORS — allow the page to also call ws://localhost:19053
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");

            var path = ctx.Request.Url.LocalPath.TrimStart('/');
            if (string.IsNullOrEmpty(path)) path = "dashboard.html";
            var file = System.IO.Path.Combine(root, path);

            if (System.IO.File.Exists(file))
            {
                var bytes = System.IO.File.ReadAllBytes(file);
                ctx.Response.ContentType = GetMimeType(path);
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            else
            {
                ctx.Response.StatusCode = 404;
                var msg = System.Text.Encoding.UTF8.GetBytes($"404 Not Found: {path}");
                ctx.Response.OutputStream.Write(msg, 0, msg.Length);
            }
            ctx.Response.OutputStream.Close();
        }
    }

    static string GetMimeType(string path) =>
        System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".js"             => "application/javascript",
            ".css"            => "text/css",
            ".json"           => "application/json",
            ".png"            => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg"            => "image/svg+xml",
            _                 => "application/octet-stream"
        };
}
```

### Key Implementation Notes

- **`UniTask.RunOnThreadPool`** for `_listener.GetContext()` — prevents blocking Unity's main thread
- **`async UniTaskVoid`** + `.Forget()` — fire-and-forget pattern for the serve loop
- **CORS headers** — required so the dashboard page can also open `ws://localhost:19053`
- **`CancellationTokenSource`** — clean shutdown via `_cts.Cancel()` in `StopHttpServer`
- **`Watch(nameof(Port), ...)`** — restart server when user changes the port
- **`System.IO` is allowed** for Playground scripts; for UMod plugins use `Context.PersistentDataManager` or bundle files in the mod

### File Serving Path

For Playground scripts, serve from `StreamingAssets/Playground/<YourFolder>/`:
```csharp
var root = System.IO.Path.Combine(Application.streamingAssetsPath, "Playground", "YourFolder");
```

For UMod plugins, load HTML from the mod bundle:
```csharp
var html = Plugin.ModHost.Assets.Load<TextAsset>("Assets/MyMod/dashboard.html");
```

---

## Tier 3 — ScreenAsset Embedded Browser (Vuplex)

Display a web dashboard *inside* Warudo as a 3D screen overlay, with bidirectional C# ↔ JS messaging.

### JS → C# (ScreenAsset page sends data to Warudo)

```javascript
// In your HTML page running inside a ScreenAsset:
window.vuplex.postMessage(JSON.stringify({ type: 'buttonClick', value: 'hello' }));
```

```csharp
// In your Asset — subscribe to messages from the page:
Context.EventBus.Subscribe<OnScreenBrowserMessageEvent>(e => {
    Debug.Log($"From page JS: {e.RawMessage}");
    // e.RawMessage is the JSON string from postMessage
});
```

Or use the `On Screen Browser Message` blueprint node (no code needed).

### C# → JS (Warudo sends data to ScreenAsset page)

Use the `Send Message To Screen Browser` blueprint node to push data:

```javascript
// In your HTML page — listen for messages from C#:
window.vuplex.addEventListener('message', e => {
    const data = JSON.parse(e.data);
    console.log('From Warudo:', data);
});
```

### Vuplex Sandbox Limitations

- Only `https://` and `data:` URIs work
- `file:///` is **blocked** — use base64 data URIs or remote URLs
- `http://localhost` is **blocked** in some Vuplex builds
- External `<script src>` and `<iframe>` may be blocked
- For local dashboards, prefer Tier 2 (HTTP server) + Tier 1 (WS control), then point the ScreenAsset at `http://localhost:<port>/dashboard.html`

---

## ServiceMessageQueue — Push Custom Data to WS Clients

Push a custom JSON message from C# to all connected WebSocket clients (Electron UI + external browsers):

```csharp
// C# side — queue a message:
Context.ServiceMessageQueue.QueueMessage("myPluginUpdate", jsonPayload);
// Clients receive: { "type": "myPluginUpdate", "data": "...json..." }
```

```javascript
// Browser side — listen for custom messages:
ws.onmessage = e => {
    const msg = JSON.parse(e.data);
    if (msg.type === 'myPluginUpdate') {
        const data = JSON.parse(msg.data);
        updateDashboard(data);
    }
};
```

---

## Dashboard HTML Template

A zero-dependency, dark-themed browser UI that connects to Warudo's WebSocket. Adapt this template for your plugin's dashboard.

```html
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>My Plugin Dashboard</title>
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    font-family: system-ui, sans-serif;
    background: #1a1a2e;
    color: #e0e0e0;
    padding: 1rem;
  }
  h1 { font-size: 1.2rem; margin-bottom: 1rem; color: #7eb8f7; }
  h2 {
    font-size: 0.9rem;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    color: #888;
    margin: 1rem 0 0.5rem;
  }
  .card {
    background: #16213e;
    border: 1px solid #0f3460;
    border-radius: 8px;
    padding: 1rem;
    margin-bottom: 0.75rem;
  }
  .row {
    display: flex;
    gap: 0.5rem;
    align-items: center;
    margin-bottom: 0.5rem;
    flex-wrap: wrap;
  }
  input[type=text], input[type=number] {
    background: #0f3460;
    color: #e0e0e0;
    border: 1px solid #1a5276;
    border-radius: 4px;
    padding: 4px 8px;
    font-size: 0.85rem;
    flex: 1;
    min-width: 100px;
  }
  label { font-size: 0.8rem; color: #aaa; min-width: 90px; }
  button {
    background: #0f3460;
    color: #7eb8f7;
    border: 1px solid #1a5276;
    border-radius: 4px;
    padding: 4px 12px;
    cursor: pointer;
    font-size: 0.85rem;
    transition: background 0.15s;
  }
  button:hover { background: #1a5276; }
  #status { font-size: 0.78rem; padding: 4px 8px; border-radius: 4px; }
  #status.ok { background: #0a3d2a; color: #7effa0; }
  #status.err { background: #3d0a0a; color: #ff8080; }
  #log {
    background: #0a0a1a;
    border: 1px solid #0f3460;
    border-radius: 4px;
    padding: 0.5rem;
    height: 180px;
    overflow-y: auto;
    font-family: monospace;
    font-size: 0.75rem;
    line-height: 1.5;
  }
  .log-in  { color: #7eb8f7; }
  .log-out { color: #7effa0; }
  .log-err { color: #ff8080; }
  .log-info{ color: #aaa; }
</style>
</head>
<body>

<h1>My Plugin Dashboard</h1>

<!-- Connection -->
<div class="card">
  <h2>Connection</h2>
  <div class="row">
    <label>WS endpoint</label>
    <input type="text" id="wsUrl" value="ws://localhost:19053">
    <button id="btnConnect">Connect</button>
    <button id="btnDisconnect" disabled>Disconnect</button>
  </div>
  <div id="status" class="err">Disconnected</div>
</div>

<!-- Your plugin controls go here -->
<div class="card">
  <h2>Controls</h2>
  <div class="row">
    <label>Asset GUID</label>
    <input type="text" id="entityId" placeholder="paste asset GUID here">
  </div>
  <div class="row">
    <label>Port key</label>
    <input type="text" id="portKey" placeholder="e.g. Speed">
    <label>Value</label>
    <input type="text" id="portVal" placeholder="e.g. 2.5">
    <button onclick="setDataInput()">Set</button>
  </div>
  <div class="row">
    <label>Trigger</label>
    <input type="text" id="triggerKey" placeholder="e.g. DoThing">
    <button onclick="invokeTrigger()">Invoke</button>
  </div>
</div>

<h2>Message log</h2>
<div id="log"></div>

<script>
let ws = null;
const $ = id => document.getElementById(id);

const log = (msg, cls = 'log-info') => {
  const d = $('log');
  const line = document.createElement('div');
  line.className = cls;
  line.textContent = `[${new Date().toLocaleTimeString()}] ${msg}`;
  d.appendChild(line);
  d.scrollTop = d.scrollHeight;
};

function connect() {
  const url = $('wsUrl').value.trim();
  ws = new WebSocket(url);

  ws.onopen = () => {
    $('status').textContent = `Connected to ${url}`;
    $('status').className = 'ok';
    $('btnConnect').disabled = true;
    $('btnDisconnect').disabled = false;
    log(`Connected to ${url}`);
  };

  ws.onmessage = e => {
    try {
      const msg = JSON.parse(e.data);
      if (msg.type === 'frameUpdate') return; // filter noisy per-frame broadcasts
      log(`← ${JSON.stringify(msg).substring(0, 200)}`, 'log-in');
    } catch {
      log(`← ${e.data.substring(0, 200)}`, 'log-in');
    }
  };

  ws.onerror = () => log('WebSocket error', 'log-err');

  ws.onclose = () => {
    $('status').textContent = 'Disconnected';
    $('status').className = 'err';
    $('btnConnect').disabled = false;
    $('btnDisconnect').disabled = true;
    log('Disconnected');
    ws = null;
  };
}

function disconnect() { ws?.close(); }

function send(action, data) {
  if (!ws || ws.readyState !== WebSocket.OPEN) {
    log('Not connected', 'log-err');
    return;
  }
  const msg = JSON.stringify({ action, data });
  ws.send(msg);
  log(`→ ${msg.substring(0, 200)}`, 'log-out');
}

function setDataInput() {
  send('setEntityDataInputPortValue', {
    entityId:  $('entityId').value.trim(),
    portKey:   $('portKey').value.trim(),
    value:     $('portVal').value.trim(),
    broadcast: true
  });
}

function invokeTrigger() {
  send('invokeEntityTriggerPort', {
    entityId: $('entityId').value.trim(),
    portKey:  $('triggerKey').value.trim()
  });
}

$('btnConnect').onclick = connect;
$('btnDisconnect').onclick = disconnect;

// Auto-connect on load
connect();
</script>
</body>
</html>
```

### Dashboard HTML Tips

- **Filter `frameUpdate`** messages — Warudo broadcasts these every frame and they flood the log
- **Auto-connect on load** — call `connect()` at the bottom of the script
- **Dark theme** — matches Warudo's aesthetic; adapt the CSS variables to your plugin's branding
- **Getting asset GUIDs** — in Warudo, click an asset and copy the GUID from the inspector. Or from code: `Context.OpenedScene.GetAssetsOfType<T>()` and log the IDs

---

## Combining Tiers (Recommended Pattern)

The most common production pattern combines Tier 1 + Tier 2:

1. **Tier 2 Asset** hosts an `HttpListener` to serve your dashboard HTML/JS/CSS
2. **Dashboard HTML** connects to `ws://localhost:19053` (Tier 1) for all Warudo control
3. Optionally, the asset also subscribes to `WebSocketRawMessageEvent` for logging/status
4. Optionally, use `ServiceMessageQueue.QueueMessage()` to push plugin-specific data to the dashboard

For in-Warudo display, add a **ScreenAsset** pointed at `http://localhost:<port>/dashboard.html` — this gives you Tier 3 embedded display using your Tier 2 server.

---

## Gotchas

- **`System.IO` works in Playground** but is **blocked in UMod plugins** — for plugins, bundle HTML files in the mod and load via `ModHost.Assets.Load<TextAsset>()`
- **`HttpListener` requires Windows admin** on some port ranges — use ports 1024+ to avoid permission issues
- **Always add CORS headers** (`Access-Control-Allow-Origin: *`) or the dashboard page can't connect to `ws://localhost:19053`
- **`EventBus.Unsubscribe<WebSocketRawMessageEvent>(_wsSub)`** — the type arg must be explicit; the compiler cannot infer `T` from `Guid`
- **`_listener.GetContext()`** is blocking — always wrap in `UniTask.RunOnThreadPool()` to avoid freezing Unity
- **Vuplex ScreenAsset blocks `file:///` and some `http://` URLs** — for embedded display, serve via Tier 2 HTTP server
- **`Context.ServiceMessageQueue.QueueMessage()`** sends to ALL connected WS clients including the Electron editor — use a unique message type string to filter on the receiver side
