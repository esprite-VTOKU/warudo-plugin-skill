# Blueprint JSON Reference

This file covers generating **raw blueprint JSON** — the graph files Warudo imports directly. For the C# Graph API (generating blueprints programmatically from plugin code), see [graph-api.md](graph-api.md).

---

## Node Catalog

**Path**: `c:\Program Files (x86)\Steam\steamapps\common\Warudo\Warudo_Data\StreamingAssets\Playground\node-catalog.json`

Total: ~1,011 entries (969 unique). Each entry:
```json
{
  "id": "scene.on.start",
  "title": "Scene / On Scene Start",
  "category": "",
  "namespace": "Warudo.Plugins.Core.Nodes",
  "className": "OnSceneStartNode",
  "width": 1.5,
  "dataInputs":  [ { "key": "PortKey", "label": "Display Label", "type": "TypeName", "description": "" } ],
  "dataOutputs": [ { "key": "PortKey", "label": "Display Label", "type": "TypeName", "description": "" } ],
  "flowInputs":  [ { "key": "PortKey", "label": "Display Label", "type": "Continuation", "description": "" } ],
  "flowOutputs": [ { "key": "PortKey", "label": "Display Label", "type": "Continuation", "description": "" } ],
  "triggers": []
}
```

**Use the catalog `id` as `typeId` in blueprint JSON.** Port keys come from `dataInputs[].key`, `dataOutputs[].key`, etc.

To look up a node: search the catalog by `title` or `className`. The catalog has duplicate entries — this is harmless; take the first match.

---

## Plugin vs Built-in Classification

| Class | `namespace` value | Ships with Warudo? |
|-------|---|----|
| Built-in | `""` (empty) | Yes |
| Warudo Core | starts with `"Warudo.Plugins.Core"` | Yes |
| **Addon plugin** | any other value | **No — must be installed** |

**Rule**: If any node you use is an addon plugin, warn the user:
> "This blueprint requires the **[plugin category/namespace]** plugin to be installed in Warudo."

Examples of addon plugins: `TwitchEventSub.*`, `veasu.*`, `Warudo.Plugins.AgentVT.*`, `Warudo.Plugins.ArtNetDMX.*`, and ~60+ other vendor namespaces in the catalog.

---

## Common Nodes (Quick Reference)

These are the most-used nodes. Verify port keys in the catalog before use.

| Node | ID | Namespace |
|------|----|-----------|
| On Update (every frame) | `on.update` | `""` |
| On Scene Start | `scene.on.start` | Core |
| On Hotkey Pressed | `hotkey.on.pressed` | Core |
| Flow: Branch (if/else) | `flow.branch` | Core |
| Flow: Delay | `flow.delay` | Core |
| Flow: For Each | `flow.foreach` | Core |
| Float: Smooth | `float.smooth` | Core |
| Float: Math | `float.math` | Core |
| String: Switch On String | `string.switch` | Core |
| String: Format | `string.format` | Core |
| Set Character Expression | look up by title "Set Character Expression" | Core |
| Play One Shot Animation | look up by title "Play Character One Shot Animation" | Core |
| Set Character Blend Shape | look up by title | Core |
| Smooth Character Blend Shape | look up by title | Core |
| Override Character Bone Rotation | look up by title | Core |
| Reset Character Bone Rotation | look up by title | Core |

---

## Blueprint JSON Schema

A **graph** (blueprint) is a standalone JSON object. When saving a blueprint file, it lives inside a scene's `graphs` array.

### Graph Object
```json
{
  "id": "aaaaaaaa-0000-0000-0000-000000000001",
  "enabled": true,
  "name": "My Blueprint",
  "order": 0,
  "group": "",
  "panningX": 0,
  "panningY": 0,
  "scaling": 1,
  "nodes": {
    "aaaaaaaa-0000-0000-0001-000000000001": { ...node... },
    "aaaaaaaa-0000-0000-0001-000000000002": { ...node... }
  },
  "dataConnections": [ ...data wire objects... ],
  "flowConnections": [ ...flow wire objects... ],
  "properties": {}
}
```

### Node Object (inside `nodes` map)
```json
{
  "typeId": "<catalog node id>",
  "name": "Display Name",
  "x": 0,
  "y": 0,
  "dataInputs": {
    "PortKey": { "value": <default value>, "type": "<TypeName>" }
  },
  "dataOutputs": {},
  "flowInputs": {},
  "flowOutputs": {}
}
```

- `typeId` — the `id` from the catalog (e.g. `"on.update"`, `"652503f6-8ec3-4e41-a9e9-06ac934a5011"`)
- `name` — display label shown on the node card in the blueprint editor
- `x`, `y` — position in canvas coordinates
- `dataInputs` — only include ports with non-default values. The `type` must match the catalog's type string. Omit `dataOutputs`, `flowInputs`, `flowOutputs` keys (or leave empty) unless setting defaults

**Value formats by type:**
| Type | JSON format |
|------|-------------|
| `bool` | `true` / `false` |
| `int`, `float` | number |
| `string` | `"text"` |
| `Vector2` | `{"x":0,"y":0}` |
| `Vector3` | `{"x":0,"y":0,"z":0}` |
| `Color` | `{"r":1,"g":1,"b":1,"a":1}` |
| Asset reference | `null` (user fills in) |
| Enum | `"EnumValueName"` string |

### Data Connection (wires a data output to a data input)
```json
{
  "outputNode": "<source node uuid>",
  "outputPort": "PortKey",
  "inputNode":  "<dest node uuid>",
  "inputPort":  "PortKey"
}
```

### Flow Connection (wires a flow output to a flow input)
```json
{
  "outputNode": "<source node uuid>",
  "outputPort": "PortKey",
  "inputNode":  "<dest node uuid>",
  "inputPort":  "PortKey"
}
```

---

## Layout Conventions

- **Left to right**: event/trigger nodes on the far left (~x=-1200), terminal action nodes on the right (~x=0)
- **Primary chain**: y=0; branches at y=±300
- **Horizontal spacing**: ~400px between nodes
- **Node width**: each node is ~300px wide (`width` from catalog is in grid units; 1.0 ≈ 200px)

---

## Workflow: Generating Blueprint JSON

1. **Identify nodes needed** — for each node, search the catalog by `title` or `className`
2. **Record node IDs and port keys** from the catalog
3. **Check namespaces** — flag any addon plugins required
4. **Assign UUIDs** to each node instance (`powershell -Command "[guid]::NewGuid()"`)
5. **Layout**: assign x/y positions left-to-right
6. **Wire connections**: list data and flow connections by UUID + port key
7. **Output the graph JSON**

---

## Example: On Scene Start → Toast

```json
{
  "id": "bbbbbbbb-0000-0000-0000-000000000001",
  "enabled": true,
  "name": "Startup Toast",
  "order": 0,
  "group": "",
  "panningX": 0,
  "panningY": 0,
  "scaling": 1,
  "nodes": {
    "cccccccc-0000-0000-0001-000000000001": {
      "typeId": "scene.on.start",
      "name": "On Scene Start",
      "x": -800,
      "y": 0,
      "dataInputs": {},
      "dataOutputs": {},
      "flowInputs": {},
      "flowOutputs": {}
    },
    "cccccccc-0000-0000-0001-000000000002": {
      "typeId": "show.toast",
      "name": "Show Toast",
      "x": -400,
      "y": 0,
      "dataInputs": {
        "Content": { "value": "Scene loaded!", "type": "string" }
      },
      "dataOutputs": {},
      "flowInputs": {},
      "flowOutputs": {}
    }
  },
  "dataConnections": [],
  "flowConnections": [
    {
      "outputNode": "cccccccc-0000-0000-0001-000000000001",
      "outputPort": "OnStart",
      "inputNode":  "cccccccc-0000-0000-0001-000000000002",
      "inputPort":  "Enter"
    }
  ],
  "properties": {}
}
```

> Always verify `OnStart` and `Enter` port keys against the actual catalog entries before finalizing.
