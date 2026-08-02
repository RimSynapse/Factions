# Changelog

Full version history for RimSynapse - Factions. The mod page and Workshop description show only the latest release; every earlier version is recorded here.

## v0.7.0 - Regions and Territories Compatibility
- NEW - Regional economy: production and taxation computed against Empire's own figures, scaled by how much of a region a faction actually holds.
- NEW - Settlement size classification, military reach and supply, and a published faction standing surface other mods can read.
- IMPORTANT - Taxation is modelled but not yet connected to Empire's levy. Tithes are unaffected this release; the wiring lands in a later version.
- IMPORTANT - Regions and Territories must load BEFORE Factions. Reversed, every Factions type silently disappears and the mod does nothing. RimWorld obeys the order in your mod list, not the declared dependency.
- Fixed: Harmony debug output was shipped enabled, writing an IL dump to your desktop and slowing patching for every mod loaded after this one.

## v0.6.1
- Fixed - mod list metadata: the in-game mod list still showed v0.5.2 with no v0.6.0 notes. Version and changelog now agree in every place they are stated.
- Roadmap updated: 0.7 is now Regions and Territories compatibility - the groundwork the Factions work depends on. Everything after it shifts up one release.

## v0.6.0
- Requires RimSynapse Core v0.6.0. This release moves in step with Core's Agent and Tool Foundation update - your saves and settings carry over unchanged.
- Documentation: in-game wiki guides updated; "MCP" renamed to game tools throughout, matching Core's native tool-calling engine.

## v0.5.2
- Maintenance release: no gameplay changes. Version aligned with the rest of the RimSynapse suite, which carries fixes in Core and Psychology.
- Licence: now PolyForm Noncommercial 1.0.0. Free to use, modify and share for any noncommercial purpose.

## v0.5.1
- Playtest improvements: general stability enhancements and compatibility optimizations.
- Thread-safety constraints met: pre-caches all tile ownership and textures on the Unity main thread before `PrepareMeshes(...)` is called asynchronously, preventing Unity API crashes on background threads.

## v0.4.0
- Updated to support RimSynapse Core v0.4.0 (Multi-provider routing and Image generation).
