# Warudo Plugin Skill for Claude Code

A Claude Code skill that provides full Warudo SDK reference, code templates, blueprint generation patterns, and best practices for developing Warudo plugins.

## What's Included

- **Plugin, Asset, and Node templates** — all 6 node patterns (Pure Data, Flow, Hybrid, Flow-Driven Daemon, Event, Signal Receiver)
- **SDK reference** — attributes, UI widgets, Context APIs, StructuredData, PlaybackMixin, external integration
- **Graph API** — C# blueprint generation from plugin code, built-in node ports, iFacialMocap animation pattern
- **Blueprint JSON generation** *(experimental)* — generate raw `.json` blueprints to import directly into Warudo, with node lookup from the live node catalog, addon plugin detection, and layout conventions. Test generated blueprints in Warudo before relying on them.
- **Web dashboard integration** — 3-tier architecture for building browser-based UIs that control Warudo
- **Build plugins without Unity** — `dotnet build` + a bundled `pack.py` produce a `.warudo` bundle directly from C#, no Unity Editor or SDK project required (code-only plugins: assets, nodes, HTTP/UDP servers)
- **Node catalog** — bundled catalog of 969 unique Warudo nodes for blueprint generation and node lookup
- **Gotchas** — lessons learned and common pitfalls from plugin development

## Installation

### As a plugin (recommended)

In Claude Code, add this repo as a marketplace and install:

```
/plugin marketplace add esprite-VTOKU/warudo-plugin-skill
/plugin install warudo-plugin-skill@warudo-plugin-skill
```

The `/warudo` skill then auto-loads in any project.

### As a personal skill (manual)

Clone the repo and copy the skill into your user skills folder:

```bash
git clone https://github.com/esprite-VTOKU/warudo-plugin-skill
cp -r warudo-plugin-skill/skills/warudo ~/.claude/skills/warudo
```

### As a project skill

Or copy `skills/warudo/` into a single project's `.claude/skills/` directory so it's
scoped to that project.

## Usage

In Claude Code, invoke the skill with:

```
/warudo PluginName Description of what you want to build
```

Or just start asking Warudo plugin development questions — the skill auto-triggers when Claude detects Warudo-related work.

## Skill Structure

| File | Lines | Purpose |
|------|-------|---------|
| `SKILL.md` | ~340 | Main workflow, architecture, critical rules |
| `templates.md` | ~700 | All code templates (Plugin, Asset, 6 node types) |
| `sdk-reference.md` | ~380 | Attributes, UI, Context APIs, StructuredData |
| `graph-api.md` | ~150 | C# Graph API, blueprint generation from plugin code, animation patterns |
| `blueprint-json.md` | ~150 | *(experimental)* Raw blueprint JSON schema, node catalog lookup, plugin detection |
| `web-dashboard.md` | ~650 | Web dashboard integration, 3-tier architecture |
| `mod-packaging.md` | ~140 | Build & pack a `.warudo` without Unity; referencing extra Managed DLLs; DOTween transitions |
| `mod-packager/` | — | Bundled tooling: `pack.py` packager, `template.csproj`, `HelloWorldPlugin.cs` example |
| `node-catalog.json` | ~45k | Bundled catalog of 969 unique Warudo nodes |
| `gotchas.md` | ~130 | Common pitfalls and lessons learned |

Only `SKILL.md` loads by default. Supporting files are loaded on-demand when needed. `node-catalog.json` is referenced during blueprint generation for node lookup.

## Requirements

- [Claude Code](https://claude.ai/code) CLI
- For **code-only plugins** (assets, nodes, servers): just the [.NET SDK](https://dotnet.microsoft.com/download) and a Warudo install — build with `dotnet` + `pack.py`, no Unity needed (see `mod-packaging.md`).
- For plugins that ship **Unity assets** (prefabs, meshes, textures, animations): a Unity project with the [Warudo Mod Tool SDK](https://github.com/HakuyaLabs/WarudoPlaygroundExamples).

## License

MIT
