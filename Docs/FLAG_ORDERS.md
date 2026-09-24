# Written flag orders

Players can type orders in the **Flag orders** popup (the `ORDERS` box under the bounty) before placing a flag, for example:

> stay away from this flag unless you are level 9 or higher

The text is read **once, when the flag is posted**, and turned into a small fixed set of rules stored on the flag (`FlagHandle.Orders`). `SpecialistBrain` enforces them as a gate, the same way it applies the greed gate. The text is never re-read per tick, and `ScoreFlag` is unchanged. Heroes the orders exclude show an **ORDERS** refusal chip.

| Rule | Examples |
|---|---|
| Minimum level | "unless you're L9+", "level 5 and up", "no one below level 5", "at least level 4" |
| Maximum level | "level 3 or lower", "don't go if you're level 9 or higher" (→ L8 or lower) |
| Level range | "level 3-6 engineers only" |
| Only these classes | "mechs only", "stay away except medics", "sentinels and defense mechs only" |
| Banned classes | "no scouts", "no scouts and medics", "everyone except couriers", "keep couriers away" |
| Minimum health | "healthy" (70%), "stay away if hurt" (50%), "above 60% health only" |
| Strict | "strictly", "no exceptions", "must", "under no circumstances" |

Before posting, the box shows what was understood (`understood: L9+ · no scouts`), and the flag label and flag log show it afterwards. Text the parser can't read produces no rules, and the flag behaves normally.

**Soft vs strict:** by default, a greedy hero (greed ≥ 0.7) who is short of money (pay hunger ≥ 0.7) may ignore the orders. Majesty heroes are mercenaries, not soldiers. Strict wording binds everyone. The thresholds are `FlagOrdersRules.BendGreed` / `BendHunger`.

**Local model (optional):** if the built-in parser finds no rules and a Laya server is running (see [LAYA_LOCAL_AI.md](LAYA_LOCAL_AI.md)), the game asks Laya three fixed-menu questions (minimum level, which class, strict?) and applies only confident answers. The model can only choose menu entries, so the result is always a valid rule set. Orders are saved with the game and are never re-parsed on load.

Code: `Systems/FlagOrders.cs` (the rules and the gate), `Systems/FlagOrdersParser.cs` (the built-in parser), `Systems/Laya/LayaOrdersPolicy.cs` + `Runtime/Laya/LayaFlagOrders.cs` (the Laya fallback), and `Tests/EditMode/FlagOrdersTests.cs`.
