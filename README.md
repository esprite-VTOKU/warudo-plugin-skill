# Warudo Plugin Skill for Claude Code

A Claude Code skill that provides full Warudo SDK reference, code templates, blueprint generation patterns, and best practices for developing Warudo plugins.

## What's Included

- **Plugin, Asset, and Node templates** — all 6 node patterns (Pure Data, Flow, Hybrid, Flow-Driven Daemon, Event, Signal Receiver)
- **SDK reference** — attributes, UI widgets, Context APIs, StructuredData, PlaybackMixin, external integration
- **Graph API** — C# blueprint generation from plugin code, built-in node ports, iFacialMocap animation pattern
- **Blueprint JSON generation** *(experimental)* — generate raw `.json` blueprints to import directly into Warudo, with node lookup from the live node catalog, addon plugin detection, and layout conventions. Test generated blueprints in Warudo before relying on them.
- **Warudo 0.15.x notes** — uMod/SDK compatibility, the new per-launch API auth token, the 19052 / 19053 / 19190 port map, Warudo Link (`warudo://`) managed callbacks, mod-to-mod interoperability, and the current restriction list
- **No-Unity mod packaging** — build a code-only plugin with `dotnet` and pack it straight into a `.warudo` bundle (`mod-packager/pack.py`), no Unity Editor needed
- **Web dashboard integration** — WebSocket control protocol, plugin-hosted HTTP server, embedded ScreenAsset
- **Gotchas** — lessons learned and common pitfalls from plugin development

## Installation

### As a project skill (recommended)

Copy the `skills/warudo/` folder into your project's `.claude/skills/` directory:

```
your-warudo-project/
  .claude/
    skills/
      warudo/
        SKILL.md
        sdk-reference.md
        templates.md
        graph-api.md
        gotchas.md
        blueprint-json.md
        warudo-0.15.md
        web-dashboard.md
        mod-packaging.md
        mod-packager/
```

### As a personal skill

Copy to `~/.claude/skills/warudo/` to make it available across all your projects.

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
| `warudo-0.15.md` | ~280 | Warudo 0.15.x: version/uMod compatibility, API auth token, port map, Warudo Link, mod interoperability, restriction list, launch flags |
| `web-dashboard.md` | ~660 | WebSocket protocol, plugin-hosted HTTP server, ScreenAsset, dashboard templates |
| `mod-packaging.md` | ~110 | `.warudo` bundle format; build + pack a code-only mod without Unity |
| `mod-packager/` | — | `pack.py` packager, `template.csproj`, working HelloWorld plugin |
| `gotchas.md` | ~130 | Common pitfalls and lessons learned |

Only `SKILL.md` loads by default. Supporting files are loaded on-demand when needed.

## Requirements

- [Claude Code](https://claude.ai/code) CLI
- A Unity project with the [Warudo Mod Tool SDK](https://github.com/HakuyaLabs/Warudo-Mod-Tool) —
  reference it as `https://github.com/HakuyaLabs/Warudo-Mod-Tool.git#upm/latest` in
  `Packages/manifest.json` (`#upm/<version>` to pin)
- …or, for a code-only plugin, just the .NET SDK and Python 3 — see `mod-packaging.md`

Written against **Warudo 0.15.2** (Unity 2021.3.45f2, uMod 2.9.9).

## License

MIT
