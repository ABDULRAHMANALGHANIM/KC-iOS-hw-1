#!/usr/bin/env python3
"""
NEON REQUIEM - run the engine-independent unit tests WITHOUT the Unity Editor.

The tests under Assets/_NeonRequiem/Tests/EditMode/Logic exercise pure C# logic
(movement rules, camera model, settings, math, ring buffer, adaptive resolution).
That code only uses managed parts of UnityEngine (Vector3, Mathf, ...), so it can be
executed on the regular .NET runtime against the assemblies produced by validate.py.

This is a fast pre-check, not a substitute for the Unity Test Runner:
  * tests under Tests/EditMode/Unity and Tests/PlayMode need the engine and are NOT run here;
  * NUnit comes from NuGet (3.14) instead of Unity's bundled NUnit 3.5.

Usage (same arguments / environment variables as validate.py):
  python3 run_logic_tests.py --editor-data <Unity>/Editor/Data \
                             --script-assemblies <Project>/Library/ScriptAssemblies

Requires: Python 3.8+, .NET SDK 8.0+, network access to nuget.org on first run.
"""
import argparse
import glob
import os
import subprocess
import sys

CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AssemblyName>NeonRequiem.LogicTests</AssemblyName>
    <RootNamespace>NeonRequiem.Tests</RootNamespace>
    <!-- MSB3277/CS1701/CS1702: version unification noise between Unity's netstandard facades and net8.0. -->
    <NoWarn>$(NoWarn);MSB3277;MSB3243;CS1701;CS1702;NU1701</NoWarn>
    <DefineConstants>$(DefineConstants);UNITY_6000_3_OR_NEWER;UNITY_6000_0_OR_NEWER;ENABLE_INPUT_SYSTEM;UNITY_INCLUDE_TESTS</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="@TESTS@/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
@REFERENCES@
  </ItemGroup>
</Project>
"""


def fail(message):
    print("ERROR: " + message, file=sys.stderr)
    sys.exit(2)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser(description="Run the engine-independent unit tests on the .NET runtime.")
    parser.add_argument("--project", default=os.path.abspath(os.path.join(here, "..", "..")))
    parser.add_argument("--editor-data", default=os.environ.get("UNITY_EDITOR_DATA"))
    parser.add_argument("--script-assemblies", default=os.environ.get("UNITY_SCRIPT_ASSEMBLIES"))
    parser.add_argument("--work", default=os.path.join(here, ".work"))
    parser.add_argument("--filter", default=None, help="Optional 'dotnet test --filter' expression.")
    args = parser.parse_args()
    if not args.editor_data or not args.script_assemblies:
        fail("--editor-data and --script-assemblies (or UNITY_EDITOR_DATA / UNITY_SCRIPT_ASSEMBLIES) are required")

    project = os.path.abspath(args.project)
    tests = os.path.join(project, "Assets", "_NeonRequiem", "Tests", "EditMode", "Logic")
    if not os.path.isdir(tests):
        fail("logic tests folder not found: " + tests)

    # 1. Build the project assemblies in the editor configuration.
    validate = [sys.executable, os.path.join(here, "validate.py"), "--project", project,
                "--editor-data", args.editor_data, "--script-assemblies", args.script_assemblies,
                "--config", "editor", "--work", args.work]
    if subprocess.run(validate).returncode != 0:
        fail("the project does not compile; fix that first")

    built = os.path.join(args.work, "editor")
    references = [os.path.join(built, "NeonRequiem.Core.dll"), os.path.join(built, "NeonRequiem.Runtime.dll")]
    for path in references:
        if not os.path.isfile(path):
            fail("expected assembly was not built: " + path)
    references += sorted(glob.glob(os.path.join(os.path.abspath(args.editor_data), "Managed", "UnityEngine", "UnityEngine*.dll")))
    for name in ("Unity.InputSystem", "UnityEngine.UI", "Unity.RenderPipelines.Core.Runtime",
                 "Unity.RenderPipelines.Universal.Runtime"):
        candidate = os.path.join(os.path.abspath(args.script_assemblies), name + ".dll")
        if os.path.isfile(candidate):
            references.append(candidate)

    lines = []
    for path in references:
        name = os.path.splitext(os.path.basename(path))[0]
        lines.append(f'    <Reference Include="{name}"><HintPath>{path}</HintPath></Reference>')

    out = os.path.join(args.work, "logic-tests")
    os.makedirs(out, exist_ok=True)
    csproj = os.path.join(out, "NeonRequiem.LogicTests.csproj")
    with open(csproj, "w", encoding="utf-8") as handle:
        handle.write(CSPROJ.replace("@TESTS@", tests).replace("@REFERENCES@", "\n".join(lines)))

    # 2. Compile and run.
    command = ["dotnet", "test", csproj, "--nologo", "-v", "q",
               "--logger", "console;verbosity=normal",
               "--logger", "trx;LogFileName=logic-tests.trx",
               "--results-directory", os.path.join(out, "results")]
    if args.filter:
        command += ["--filter", args.filter]
    sys.exit(subprocess.run(command).returncode)


if __name__ == "__main__":
    main()
