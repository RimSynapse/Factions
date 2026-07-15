# Inspirations: Game MCP Architecture for RimSynapse Factions

This document outlines the refactoring guidelines for **RimSynapse Factions** utilizing the Model Context Protocol (MCP).

---

## 1. What Stays the Same
- **Faction Data Models**: Classes tracking faction trackers, relationship history, and perceived strength values remain in C#.
- **Local Greed / Perceived Strength Updates**: Background calculations updating perceived strength based on caravan visits or hostiles seen remain in C# to ensure data persistence.

---

## 2. What Changes (The MCP Shift)
- **Scrap Hardcoded Motivation Loops**: Remove C# logic loops that check thresholds like `greedRatio > motivatedRaidGreedRatioThreshold` and apply strength additions in C#.
- **Expose Factions Tools**: Register faction status endpoints as MCP tools. Let the storyteller LLM query faction motivations and relationships on-demand.

---

## 3. Proposed MCP Tools for Factions
- `get_motivated_factions`: Returns a list of hostile factions along with their perceived colony strength and greed ratio (wealth vs defense).
- `get_faction_relations_history`: Returns a summary of relations history and recent interactions (e.g. peace talks, caravans traded, raids sent).

---

## 4. LLM Narrative Workflow
1. The Storyteller LLM decides to trigger a `ThreatBig` event.
2. It queries `get_motivated_factions()` to see which hostiles perceive the colony as wealthy and defenseless.
3. It selects the faction with the highest greed motivation (e.g. *"The Pig Union sees your gold and weak turrets"*), and returns it directly to direct the game raid.
