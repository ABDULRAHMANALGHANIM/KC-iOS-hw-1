# Static validation (no Unity Editor required)

Two scripts that check the project without launching Unity. They exist because the first
milestone was written in an environment where the editor could not run, and they remain useful
as a fast pre-commit / CI gate.

| Script | What it does | What it cannot do |
| --- | --- | --- |
| `validate.py` | Compiles every `.asmdef` with Roslyn against real Unity editor and package assemblies, in three configurations: `editor`, `android`, `ios`. Player configurations omit `UnityEditor`, so editor-only API use in runtime code is caught. | Import assets, compile shaders, load scenes, build a player |
| `run_logic_tests.py` | Runs the engine-independent unit tests (`Assets/_NeonRequiem/Tests/EditMode/Logic`) on the .NET runtime with NUnit. | Run tests that need the engine (`Tests/EditMode/Unity`, `Tests/PlayMode`) |

## Requirements

- Python 3.8+
- .NET SDK 8.0+ (`dotnet` on PATH)
- A Unity 6000.3 installation (only its files are read; no licence or editor launch is needed)
- Compiled package assemblies: any `Library/ScriptAssemblies` folder of a Unity 6000.3 URP project
  that contains `Unity.InputSystem.dll`, `UnityEngine.UI.dll`,
  `Unity.RenderPipelines.Core.Runtime.dll`, `Unity.RenderPipelines.Universal.Runtime.dll` and
  `UnityEngine.TestRunner.dll`. Once this project has been opened in Unity, its own
  `Library/ScriptAssemblies` works.

## Usage

```bash
export UNITY_EDITOR_DATA="/path/to/Unity/6000.3.28f1/Editor/Data"          # macOS: .../Unity.app/Contents
export UNITY_SCRIPT_ASSEMBLIES="/path/to/NeonRequiem/Library/ScriptAssemblies"

python3 Tools/StaticValidation/validate.py            # all configurations
python3 Tools/StaticValidation/validate.py --config android
python3 Tools/StaticValidation/run_logic_tests.py     # compiles, then runs the logic tests
```

Both scripts exit non-zero on failure. Output goes to `Tools/StaticValidation/.work/` (git-ignored).

## Notes

- C# language version is pinned to 9.0, matching Unity 6000.3.
- NUnit for `run_logic_tests.py` comes from NuGet (3.14); Unity bundles its own NUnit 3.5 build. The
  tests use only APIs common to both.
- Logic tests may only touch managed parts of `UnityEngine` (`Vector3`, `Mathf`, ...). Anything that
  calls into native engine code (`Debug.Log`, `Quaternion.Euler`, creating a `ScriptableObject`, ...)
  throws outside Unity; such tests belong in `Tests/EditMode/Unity` or `Tests/PlayMode`.
