# Faction Motivations and Diplomacy

RimSynapse - Factions replaces static, arbitrary faction logic in RimWorld with dynamic, context-aware AI motivations.

---

## 1. Dynamic Faction Strength and Greed

The mod monitors colony developments and registers two main indexes that shape how hostile factions interact with you:
*   **Perceived Colony Strength:** Instead of magic math knowing exactly what defenses you have, factions update their perception of your strength based on caravan visits, scouts, or hostile raids.
*   **Colony Greed Ratio:** A comparison of your visible wealth (stockpiles, gold, item value) against your defensive capabilities (turrets, armor, weapons). Factions become highly motivated to attack if they perceive you as extremely rich and defenseless.

---

## 2. MCP Tool Endpoints

The Factions submod exposes tools that the Storyteller LLM can query on-demand:
*   `get_motivated_factions`: Returns a list of hostile factions, their perceived strength of your colony, and their greed ratio.
*   `get_faction_relations_history`: Gathers recent faction diplomatic interactions, peace talks, caravans traded, and past raid outcomes.

---

## 3. Storyteller Integration

The AI Storyteller queries these tools dynamically before launching events. Rather than random raids, the LLM selects the pig union or outlanders because their greed ratio is high and their perceived strength of your colony is low. The storyteller then writes a narrative explaining the raid (e.g. *"Scouts saw your silver piles and lack of security, launching a motivated assault"*).
