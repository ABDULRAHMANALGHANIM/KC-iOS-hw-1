#!/usr/bin/env python3
"""
NEON REQUIEM - static validation harness.

Compiles every assembly definition in the project with Roslyn against real Unity
editor assemblies, WITHOUT launching the Unity Editor. It mirrors what Unity's
script compilation does closely enough to catch:

  * C# syntax and type errors,
  * missing asmdef references,
  * use of Unity / package APIs that do not exist in the targeted editor version,
  * editor-only API use in runtime code (player configurations omit UnityEditor).

It does NOT replace opening the project in Unity: it cannot import assets, compile
shaders, run PlayMode tests or build a player.

Usage:
  python3 validate.py --editor-data <Unity>/Editor/Data \
                      --script-assemblies <AnyURPProject>/Library/ScriptAssemblies \
                      [--config editor|android|ios|all] [--project <path>]

  --editor-data        The Unity installation's Editor/Data directory
                       (macOS: Unity.app/Contents). Also read from UNITY_EDITOR_DATA.
  --script-assemblies  A Library/ScriptAssemblies folder that contains the compiled
                       package assemblies (Unity.InputSystem.dll, UnityEngine.UI.dll,
                       Unity.RenderPipelines.Universal.Runtime.dll, ...). After the
                       project has been opened once, its own Library/ScriptAssemblies
                       works. Also read from UNITY_SCRIPT_ASSEMBLIES.

Requires: Python 3.8+, a .NET SDK (6.0 or newer) on PATH.
"""
import argparse
import glob
import json
import os
import subprocess
import sys

LANG_VERSION = "9.0"

COMMON_DEFINES = [
    "UNITY_6000_3_OR_NEWER", "UNITY_6000_2_OR_NEWER", "UNITY_6000_1_OR_NEWER", "UNITY_6000_0_OR_NEWER",
    "UNITY_2023_1_OR_NEWER", "UNITY_2022_3_OR_NEWER", "UNITY_2021_3_OR_NEWER", "UNITY_2020_3_OR_NEWER",
    "UNITY_2019_4_OR_NEWER", "UNITY_5_3_OR_NEWER", "UNITY_6000", "UNITY_6000_3",
    "ENABLE_INPUT_SYSTEM", "NET_STANDARD_2_1", "NET_STANDARD", "CSHARP_7_3_OR_NEWER",
    "UNITY_ASSERTIONS", "DEBUG", "TRACE",
]

CONFIGS = {
    "editor": {
        "defines": ["UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_STANDALONE", "UNITY_INCLUDE_TESTS", "ENABLE_MONO"],
        "editor": True,
    },
    "android": {
        "defines": ["UNITY_ANDROID", "ENABLE_IL2CPP", "DEVELOPMENT_BUILD"],
        "editor": False,
    },
    "ios": {
        "defines": ["UNITY_IOS", "UNITY_IPHONE", "ENABLE_IL2CPP"],
        "editor": False,
    },
}


def fail(message):
    print("ERROR: " + message, file=sys.stderr)
    sys.exit(2)


def find_csc():
    try:
        out = subprocess.run(["dotnet", "--list-sdks"], capture_output=True, text=True, check=True).stdout
    except (OSError, subprocess.CalledProcessError):
        fail("a .NET SDK is required (dotnet not found on PATH)")
    best = None
    for line in out.strip().splitlines():
        version, _, rest = line.partition(" ")
        base = rest.strip().strip("[]")
        candidate = os.path.join(base, version, "Roslyn", "bincore", "csc.dll")
        if os.path.isfile(candidate):
            best = candidate
    if not best:
        fail("could not locate Roslyn (csc.dll) inside the installed .NET SDKs")
    return best


def load_asmdefs(project):
    asmdefs = {}
    pattern = os.path.join(project, "Assets", "**", "*.asmdef")
    for path in glob.glob(pattern, recursive=True):
        with open(path, encoding="utf-8-sig") as handle:
            data = json.load(handle)
        data["_dir"] = os.path.dirname(path)
        data["_path"] = path
        asmdefs[data["name"]] = data
    if not asmdefs:
        fail("no .asmdef files found under " + os.path.join(project, "Assets"))
    return asmdefs


def sources_for(asmdef, all_dirs):
    """All .cs files under the asmdef folder, excluding folders owned by a nested asmdef."""
    root = asmdef["_dir"]
    nested = [d for d in all_dirs if d != root and d.startswith(root + os.sep)]
    files = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames
                       if os.path.join(dirpath, d) not in nested and not d.endswith("~") and not d.startswith(".")]
        for name in sorted(filenames):
            if name.endswith(".cs"):
                files.append(os.path.join(dirpath, name))
    return sorted(files)


def topo_order(asmdefs):
    order, state = [], {}

    def visit(name, chain):
        if state.get(name) == 2:
            return
        if state.get(name) == 1:
            fail("cyclic assembly references: " + " -> ".join(chain + [name]))
        state[name] = 1
        for ref in asmdefs[name].get("references", []):
            if ref in asmdefs:
                visit(ref, chain + [name])
        state[name] = 2
        order.append(name)

    for name in sorted(asmdefs):
        visit(name, [])
    return order


def unity_references(editor_data, include_editor):
    managed = os.path.join(editor_data, "Managed", "UnityEngine")
    if not os.path.isdir(managed):
        fail("not a Unity Editor/Data directory (Managed/UnityEngine missing): " + editor_data)
    refs = sorted(glob.glob(os.path.join(managed, "UnityEngine*.dll")))
    if include_editor:
        refs += sorted(glob.glob(os.path.join(managed, "UnityEditor*.dll")))
    net = os.path.join(editor_data, "NetStandard")
    refs.append(os.path.join(net, "ref", "2.1.0", "netstandard.dll"))
    refs += sorted(glob.glob(os.path.join(net, "compat", "2.1.0", "shims", "netstandard", "*.dll")))
    refs += sorted(glob.glob(os.path.join(net, "compat", "2.1.0", "shims", "netfx", "*.dll")))
    return refs


def find_nunit(editor_data):
    pattern = os.path.join(editor_data, "Resources", "PackageManager", "BuiltInPackages",
                           "com.unity.ext.nunit", "**", "nunit.framework.dll")
    hits = glob.glob(pattern, recursive=True)
    return hits[0] if hits else None


def compile_config(name, config, args, asmdefs, csc):
    out_dir = os.path.join(args.work, name)
    os.makedirs(out_dir, exist_ok=True)
    base_refs = unity_references(args.editor_data, config["editor"])
    defines = COMMON_DEFINES + config["defines"]
    all_dirs = [a["_dir"] for a in asmdefs.values()]
    built, skipped, errors, warnings = {}, [], 0, 0

    for asm_name in topo_order(asmdefs):
        asmdef = asmdefs[asm_name]
        include = asmdef.get("includePlatforms", [])
        constraints = asmdef.get("defineConstraints", [])
        if include == ["Editor"] and not config["editor"]:
            skipped.append(asm_name + " (editor-only)")
            continue
        if any(c not in defines for c in constraints if not c.startswith("!")):
            skipped.append(asm_name + " (define constraints)")
            continue

        refs = list(base_refs)
        missing = []
        for ref in asmdef.get("references", []):
            if ref in built:
                refs.append(built[ref])
            elif ref in asmdefs:
                missing.append(ref + " (project assembly was skipped or failed)")
            else:
                candidate = os.path.join(args.script_assemblies, ref + ".dll")
                if os.path.isfile(candidate):
                    refs.append(candidate)
                else:
                    missing.append(ref + ".dll (not in --script-assemblies)")
        for pre in asmdef.get("precompiledReferences", []):
            if pre == "nunit.framework.dll":
                nunit = find_nunit(args.editor_data)
                if nunit:
                    refs.append(nunit)
                else:
                    missing.append("nunit.framework.dll")
            else:
                missing.append(pre + " (unknown precompiled reference)")
        if missing:
            print(f"  [{name}] {asm_name}: FAILED - unresolved references: {', '.join(missing)}")
            errors += 1
            continue

        sources = sources_for(asmdef, all_dirs)
        if not sources:
            skipped.append(asm_name + " (no sources)")
            continue

        out_path = os.path.join(out_dir, asm_name + ".dll")
        rsp = os.path.join(out_dir, asm_name + ".rsp")
        lines = ["-nologo", "-nostdlib+", "-target:library", "-deterministic", "-debug-",
                 "-langversion:" + LANG_VERSION, "-nullable:disable", "-warn:4",
                 # CS1701/CS1702: assembly unification noise from the reference shims.
                 "-nowarn:1701,1702",
                 "-define:" + ";".join(defines),
                 "-out:" + out_path]
        if asmdef.get("allowUnsafeCode"):
            lines.append("-unsafe+")
        lines += ['-r:"' + r + '"' for r in refs]
        lines += ['"' + s + '"' for s in sources]
        with open(rsp, "w", encoding="utf-8") as handle:
            handle.write("\n".join(lines) + "\n")

        proc = subprocess.run(["dotnet", csc, "-noconfig", "@" + rsp], capture_output=True, text=True)
        output = (proc.stdout + proc.stderr).strip()
        asm_errors = sum(1 for line in output.splitlines() if ": error " in line)
        asm_warnings = sum(1 for line in output.splitlines() if ": warning " in line)
        status = "ok" if proc.returncode == 0 else "FAILED"
        print(f"  [{name}] {asm_name}: {status} - {len(sources)} files, {asm_errors} errors, {asm_warnings} warnings")
        if output:
            for line in output.splitlines():
                print("      " + line.replace(args.project + os.sep, ""))
        if proc.returncode == 0:
            built[asm_name] = out_path
        else:
            errors += max(1, asm_errors)
        warnings += asm_warnings

    for item in skipped:
        print(f"  [{name}] skipped: {item}")
    return errors, warnings


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser(description="Compile the project's assemblies against Unity without the editor.")
    parser.add_argument("--project", default=os.path.abspath(os.path.join(here, "..", "..")))
    parser.add_argument("--editor-data", default=os.environ.get("UNITY_EDITOR_DATA"))
    parser.add_argument("--script-assemblies", default=os.environ.get("UNITY_SCRIPT_ASSEMBLIES"))
    parser.add_argument("--config", default="all", choices=sorted(CONFIGS) + ["all"])
    parser.add_argument("--work", default=os.path.join(here, ".work"))
    args = parser.parse_args()

    if not args.editor_data:
        fail("--editor-data (or UNITY_EDITOR_DATA) is required")
    if not args.script_assemblies:
        fail("--script-assemblies (or UNITY_SCRIPT_ASSEMBLIES) is required")
    args.project = os.path.abspath(args.project)
    args.editor_data = os.path.abspath(args.editor_data)
    args.script_assemblies = os.path.abspath(args.script_assemblies)

    csc = find_csc()
    asmdefs = load_asmdefs(args.project)
    names = sorted(CONFIGS) if args.config == "all" else [args.config]
    total_errors = total_warnings = 0
    for name in names:
        print(f"== configuration: {name}")
        errors, warnings = compile_config(name, CONFIGS[name], args, asmdefs, csc)
        total_errors += errors
        total_warnings += warnings

    print(f"== result: {total_errors} error(s), {total_warnings} warning(s)")
    sys.exit(1 if total_errors else 0)


if __name__ == "__main__":
    main()
