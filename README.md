# Warudo Plugin Skill for Claude Code

A Claude Code skill that provides full Warudo SDK reference, code templates, blueprint generation patterns, and best practices for developing Warudo plugins.

## What's Included

- **Plugin, Asset, and Node templates** — all 6 node patterns (Pure Data, Flow, Hybrid, Flow-Driven Daemon, Event, Signal Receiver)
- **SDK reference** — attributes, UI widgets, Context APIs, StructuredData, PlaybackMixin, external integration
- **Graph API** — blueprint generation, built-in node ports, iFacialMocap animation pattern
- **Gotchas** — lessons learned and common pitfalls from real plugin development

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
| `SKILL.md` | ~310 | Main workflow, architecture, critical rules |
| `templates.md` | ~700 | All code templates (Plugin, Asset, 6 node types) |
| `sdk-reference.md` | ~380 | Attributes, UI, Context APIs, StructuredData |
| `graph-api.md` | ~150 | Blueprint generation, Graph API, animation patterns |
| `gotchas.md` | ~75 | Common pitfalls and lessons learned |

Only `SKILL.md` loads by default (~310 lines). Supporting files are loaded on-demand when needed.

## Requirements

- [Claude Code](https://claude.ai/code) CLI
- A Unity project with the [Warudo Mod Tool SDK](https://github.com/HakuyaLabs/WarudoPlaygroundExamples)

## License

MIT
