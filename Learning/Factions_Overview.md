# RimSynapse Factions Overview

The `RimSynapse-Factions` module expands RimWorld's world generation and interactions by introducing deep faction histories, ideologies, and dynamic diplomacy powered by the LLM.

## Faction Lore Generation
*(Formerly part of the deprecated `RimSynapse-StoryTeller` module)*

When the world is generated, the LLM intercepts the vanilla faction generation to write a rich background for each major faction. This history accounts for:
- The faction's ideological precepts (if Ideology DLC is active).
- Their tech level and relationship with the player's colony.
- Historical conflicts with other generated factions.

## Dynamic Diplomacy
Faction relationships are no longer static numbers. As your colony grows in perceived wealth and strength, factions will react dynamically based on their generated lore. A warlike tribal faction might respect your strength, while a civilized outlander union might view your sudden wealth as a threat.

## Map Overlays (Map Mode Framework)
We integrate with the Map Mode Framework to provide custom map modes on the world screen (such as population density propagation and zones of control/faction territories). For technical implementation details, see [Map Mode Overlays](Map_Modes.md).
