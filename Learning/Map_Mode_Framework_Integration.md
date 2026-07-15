# Map Mode Framework Integration Guidelines

This guide documents how the Map Mode Framework is integrated into `RimSynapse-Factions` and details the implementation patterns necessary to maintain thread safety.

---

## 1. Mod Configuration & Build Target
- **Mod Package ID**: `NozoMe.MapModeFramework`
- **Assemblies Target Framework**: Must target `.NET Framework 4.8` to match the target of the `MapModeFramework.dll`.
- **References**: Include `MapModeFramework.dll` reference in the `.csproj`.
- **Dependencies**: The `NozoMe.MapModeFramework` mod must be listed under `<modDependencies>` and `<loadAfter>` in `About/About.xml`.

---

## 2. Thread Safety Rules (Asynchronous Mesh Generation)
The Map Mode Framework executes `PrepareMeshes(...)` asynchronously on a background worker thread. Calling any Unity main-thread-restricted API (such as `SolidColorMaterials`, `Find.World`, or instantiating materials/textures) during this background phase will throw native thread exceptions.

### Developer Pattern:
1. **Regenerate (Main Thread)**:
   - Override `WorldLayer.Regenerate()` (which executes on the Unity main thread).
   - Resolve and cache all data maps, faction instances, tiles-to-settlement indices, and materials in a thread-safe static collection.
   - Example:
     ```csharp
     public override void Regenerate()
     {
         MapMode_FactionTerritory.CacheData(); // Main thread execution
         base.Regenerate(); // Triggers background thread execution
     }
     ```
2. **PrepareMeshes (Background Thread)**:
   - Overriden `PrepareMeshes(...)` must query only the pre-cached static dictionaries.
   - Example:
     ```csharp
     protected override void PrepareMeshes(ref List<Submesh> submeshes)
     {
         // Thread-safe lookup from pre-cached static collections
         var material = MapMode_FactionTerritory.GetMaterialForTile(tileId);
         // Build submeshes safely...
     }
     ```

---

## 3. Class Implementations
- **MapMode**: Serves as the database model containing labels, category keys, and data-caching logic.
- **WorldLayer**: Handles the mesh generation using submeshes and material assignments.

Refer to the source files for complete reference implementations:
- [MapMode_PopulationDensity.cs](file:///d:/github/RimSynapse-Factions/Source/MapModes/MapMode_PopulationDensity.cs)
- [MapMode_FactionTerritory.cs](file:///d:/github/RimSynapse-Factions/Source/MapModes/MapMode_FactionTerritory.cs)
