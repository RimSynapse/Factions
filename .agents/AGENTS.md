# RimSynapse Factions Agent Rules

## Map Mode Framework Customizations & Thread Safety
When working with map overlays and the Map Mode Framework (`NozoMe.MapModeFramework`):

1. **Thread-Safety Constraints**:
   - `PrepareMeshes(...)` is called asynchronously on a background thread.
   - **Rule**: NEVER call `SolidColorMaterials`, `MaterialPool`, `Find.World`, `Find.FactionManager`, or any Unity-restricted APIs within `PrepareMeshes(...)`.
   - **Rule**: ALWAYS pre-cache all materials, textures, tile owners, and grid computations inside `WorldLayer.Regenerate()` on the Unity main thread, before calling the base `Regenerate()` method.
   
2. **Build Target Consistency**:
   - **Rule**: Keep the target framework of `RimSynapseFactions.csproj` at `<TargetFramework>net48</TargetFramework>` to matches the compiled framework of the Map Mode Framework assembly.

3. **XML Def Registrations**:
   - **Rule**: All map modes must be registered using `<MapModeFramework.MapModeDef>` under `Defs/MapModeDefs/`.
   - **Rule**: NEVER use ampersands (`&` or `&amp;`) in ANY RimWorld files (including `About.xml` and descriptions). Always spell out the word "and".

## Command & Terminal Execution Rules
- **Rule**: Do not use PowerShell to save or persist variables. Instead, write a small `.bat` file, run it from the project directory, and clean it up when appropriate.
- **Rule**: When launching RimWorld, use `Start-Process 'steam://run/294100'` to start the game. This ensures Steam integration is active and Workshop mods are loaded correctly.

