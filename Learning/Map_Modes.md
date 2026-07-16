# Map Mode Overlays (Map Mode Framework)

The `RimSynapse-Factions` mod integrates with the **Map Mode Framework** (`NozoMe.MapModeFramework`) to display visual data overlays directly on the RimWorld world map. 

These overlays provide immediate graphical feedback regarding the current planet state, faction territories, and population propagation.

---

## 1. Registered Map Modes

We have implemented and registered two custom map modes under `Defs/MapModeDefs/MapModes_Synapse.xml`:

### A. Population Density Overlay (`SynapsePopulationDensity`)
- **Visuals**: A green-to-red color gradient displaying the density of humanlike settlements and their surrounding population.
- **Logic**: Uses a geodesic Breadth-First Search (BFS) starting from known settlements, propagating values through biomes, swamplands, mountains, and roads to simulate how populations disperse across the world grid.
- **C# Class**: `RimSynapse.Factions.MapModes.MapMode_PopulationDensity`

### B. Faction Borders & Territories Overlay (`SynapseFactionTerritory`)
- **Visuals**: Displays dynamic spheres/zones of control around NPC faction settlements, color-coded to match the respective faction's color.
- **Logic**: Maps any land tile on the planet to its closest NPC settlement within a maximum range of 18 tiles.
- **C# Class**: `RimSynapse.Factions.MapModes.MapMode_FactionTerritory`

---

## 2. Technical Overlay Architecture

Because the Map Mode Framework regenerates world map meshes asynchronously on a background thread (`PrepareMeshes` method), we follow a strict **main-thread pre-caching architecture** to prevent Unity native thread-safety exceptions:

1. **Overlay Registration**: Defs are registered in XML targeting custom subclasses of `MapMode` and `WorldLayer`.
2. **Main Thread Interception**: The `WorldLayer.Regenerate()` method executes on the Unity main thread. We override this to pre-calculate all spatial maps and instantiate and cache all faction colors/materials.
3. **Background Mesh Generation**: During background execution in `PrepareMeshes()`, the framework queries our pre-cached spatial map thread-safely, avoiding any calls to Unity main-thread-only APIs (like `SolidColorMaterials.SimpleSolidColorMaterial` or `Find.World`).

---

## 3. How to Register a New Map Mode

To add a new map mode to the project:
1. Create a `MapMode` subclass calculating the map values.
2. Create a `WorldLayer` subclass overriding `Regenerate()` to handle main-thread caching, and `PrepareMeshes(...)` to fetch grid data.
3. Register them in a `<MapModeFramework.MapModeDef>` inside the `Defs/MapModeDefs/` directory:
   ```xml
   <MapModeFramework.MapModeDef>
       <defName>YourMapModeDefName</defName>
       <label>Your Map Mode Label</label>
       <workerClass>YourNamespace.MapMode_YourClass</workerClass>
       <layerClass>YourNamespace.WorldLayer_YourClass</layerClass>
   </MapModeFramework.MapModeDef>
   ```
