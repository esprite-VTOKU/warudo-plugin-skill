# Warudo Mod Packaging (No Unity Required)

Code-only Warudo plugins can be built and packaged without Unity using `dotnet` + `pack.py`.

## Toolchain

- **Compiler:** `dotnet build` (.NET SDK) targeting `netstandard2.0`
- **Packager:** `mod-packager/pack.py` (bundled with this skill)
- **References:** Warudo DLLs from your install's `Warudo_Data/Managed/` folder. The
  default Steam path is `C:\Program Files (x86)\Steam\steamapps\common\Warudo\Warudo_Data\Managed\` —
  adjust the `<HintPath>`s in the `.csproj` if your install lives elsewhere.
- **Donor asset bundle:** none needed — `pack.py` has a minimal `sharedassets.bin`
  (just a Readme.txt) baked in as base64, so it is fully self-contained.

Copy `mod-packager/template.csproj` and `mod-packager/HelloWorldPlugin.cs` from this
skill as your starting point.

## Workflow

```bash
# Set up a build folder anywhere, e.g.:
#   warudo-build/
#     MyPlugin.csproj   (from mod-packager/template.csproj — rename AssemblyName)
#     pack.py           (from mod-packager/pack.py)
#     src/*.cs          (your plugin source)

# 1. Write C# source in src/*.cs
# 2. Build DLL
cd warudo-build
dotnet build MyPlugin.csproj -o output/ -c Release

# 3. Pack into .warudo  (no --id flag: the id comes from [PluginType] in the DLL)
python pack.py <PluginName> output/<Name>.dll \
  --version 1.0.0 \
  --author "Your Name" \
  --desc "Description" \
  --output output/<Name>.warudo

# 4. Deploy
cp output/<Name>.warudo "c:/Program Files (x86)/Steam/steamapps/common/Warudo/Warudo_Data/StreamingAssets/Plugins/"
# Warudo hot-reloads automatically — no restart needed
```

## .warudo File Format

```
UMOD header (12 bytes):
  magic:     b'UMOD'
  unknown:   b'\xf3\xf0\x1c\x04\x00\x00\x00\x00'  (constant across all plugins)

ZIP archive (STORED, no compression — SharpZipLib crashes on deflate):
  modinfo.dat
  sharedassets.bin  (reused from donor)
  sharedassets.meta (reused from donor)
  assemblymodules.dat
```

### modinfo.dat format
```
magic:         b'UMOD'
timestamp:     int64 LE  (Windows FILETIME)
int32:         3
int32:         4
int32:         3
VarString:     umod_version     ("2.9.0")
VarString:     unity_version    ("2021.3.45f2")
VarString:     sdk_version      ("0.14.3")
VarString:     plugin_name
VarString:     plugin_version
VarString:     description
VarString:     author
bytes:         b'\x00\x00'
```

VarString = C# `BinaryWriter.Write(string)` = 7-bit VarInt length + UTF-8 bytes.

### assemblymodules.dat format
```
int32:      count = 1
VarString:  "umod-compiled-{guid}"
VarString:  "<Unknown>"
int32:      EntryFlags = 1  (AssemblyImage only; 3 = also has debug symbols)
int32:      assemblyImage size = SizeOfCode + SizeOfInitializedData + SizeOfHeaders  (from PE Optional Header)
bytes:      DLL  (assemblyImage.size bytes)
[if flags & 2: int32 symbolsSize + bytes symbolsData]
```

PE header fields at: `dll[e_lfanew + 0x18 + offset]`:
- SizeOfCode:            offset +0x04
- SizeOfInitializedData: offset +0x08
- SizeOfHeaders:         offset +0x3C

## Limitations

- **No Unity assets** — no prefabs, meshes, textures, animations. The donor sharedassets.bin (World2Screen) is reused verbatim. If your plugin calls `ModHost.Assets.Load<>()` it will fail.
- **No AssetBundle content** — use this only for pure code: assets, nodes, HTTP servers, UDP receivers, etc.
- **UMod restriction still applies** — `System.IO`, `System.Reflection.GetType().Name` are blocked at runtime (UMod sandbox).

## Files

All tooling is bundled with this skill under `mod-packager/`:

- `pack.py` — the packager
- `template.csproj` — dotnet project template
- `HelloWorldPlugin.cs` — working example Plugin + Node

## Project Template

`template.csproj` — reference template with all Warudo SDK DLLs.

Key references:
```xml
<Reference Include="Warudo.Core">
  <HintPath>c:\Program Files (x86)\Steam\steamapps\common\Warudo\Warudo_Data\Managed\Warudo.Core.dll</HintPath>
</Reference>
<Reference Include="Warudo.Plugins.Core">
  <HintPath>c:\Program Files (x86)\Steam\steamapps\common\Warudo\Warudo_Data\Managed\Warudo.Plugins.Core.dll</HintPath>
</Reference>
<Reference Include="UnityEngine.CoreModule">
  <HintPath>c:\Program Files (x86)\Steam\steamapps\common\Warudo\Warudo_Data\Managed\UnityEngine.CoreModule.dll</HintPath>
</Reference>
<Reference Include="UMod">
  <HintPath>c:\Program Files (x86)\Steam\steamapps\common\Warudo\Warudo_Data\Managed\UMod.dll</HintPath>
</Reference>
```

### Referencing other DLLs already loaded in Warudo

You can reference **any** assembly in `Warudo_Data/Managed/`, not just the four
SDK DLLs — add a `<Reference>` with its `HintPath` and `<Private>false</Private>`
(compile against it, don't bundle it; Warudo already has it loaded at runtime).
UMod's code-security verifier permits these references (it rejects forbidden
namespaces like `System.IO`/`System.Reflection`, not whole in-app assemblies).

Common case — **DOTween** (Warudo bundles it; its transition nodes use it):
```xml
<Reference Include="DOTween">
  <HintPath>...\Warudo_Data\Managed\DOTween.dll</HintPath>
  <Private>false</Private>
</Reference>
<!-- DOTween's DOVirtual.* signatures pull in AnimationCurve/Vector3, which live
     in the monolithic UnityEngine.dll — reference it too or you get CS0012: -->
<Reference Include="UnityEngine">
  <HintPath>...\Warudo_Data\Managed\UnityEngine.dll</HintPath>
  <Private>false</Private>
</Reference>
```

### Animating a value like Warudo's built-in transition nodes

Warudo's transition nodes expose `TransitionTime` (Single) + `TransitionEasing`
(`DG.Tweening.Ease`). To match them, take a `float TransitionTime` and an
`Ease Easing` data input, and fire a one-shot tween from a flow input — no
per-frame `OnUpdate` needed:

```csharp
_tween?.Kill();
_tween = DOVirtual.Float(_current, target, TransitionTime, v => Apply(v))
    .SetEase(Easing)
    .OnComplete(() => { _tween = null; InvokeFlow(nameof(OnDone)); });
// guard TransitionTime <= 0 → apply instantly; kill the tween in OnDestroy().
```

## Extracting Existing Plugins

Any `.warudo` file can be decompiled for reference:

```python
import zipfile, io, struct

with open('Plugin.warudo', 'rb') as f:
    data = f.read()
zf = zipfile.ZipFile(io.BytesIO(data[12:]))   # skip 12-byte UMOD prefix
asm = zf.read('assemblymodules.dat')
mz = asm.find(b'MZ')
with open('plugin.dll', 'wb') as f:
    f.write(asm[mz:])
# then: ilspycmd plugin.dll
```
