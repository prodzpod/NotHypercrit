# Hypercrit 2: The Critening

This is a rewrite/continuation of [Hypercrit by Thinkinvis](https://thunderstore.io/package/ThinkInvis/Hypercrit/) and [Hyperbleed by TheMysticSword](https://thunderstore.io/package/TheMysticSword/Hyperbleed/). 

**Hypercrit**: Adds a highly configurable crit-stacking mechanic which gives an effect to crit chance past 100%.  
**Hyperbleed**: Stacking bleed chance past 100% increases bleed damage. Also adds support for collapse.
## Changes from the original

- Hypercrit
    - Hyperbolic Scaling: Just like Hopoo intended...
    - Proc config: "On-crit" effects apply multiple times on Hypercrit. Configurable.
    - Fractional Crit Damage Increase: instead of damage increasing every 100%, all percentage past 100% is counted partially, similar to original hyperbleed.
    - [LookingGlass](https://thunderstore.io/package/DropPod/LookingGlass/) support: adds `[hypercrit]` that shows total current damage multiplier.

- Hyperbleed
    - Color Variation: Gets darker as bleed stacks. Configurable.
    - Hyperbleed Config: more sophisticated config like hypercrit's, on top of the bleed/collapse enables. Also contains non-fractional bleed configs.
    - Bleed Stack Config: multiple stacks of bleeds can now have diminishing (or exponential) returns. Configurable. Also contains separate config for Collapse.
    - Separated bleed and collapse configs to allow more control
    - Bleed "proc"s: Same with hypercrit's proc config, but more niche. Hook through `DotController.onDotInflictedServerGlobal`.
    - Built-in Bleed Damage support with extension methods. easy inter-mod ops.
    - [LookingGlass](https://thunderstore.io/package/DropPod/LookingGlass/) support: adds `[collapseChance]`, `[bleedChanceWithLuck]`, `[collapseChanceWithLuck]`, `[bleedMultiplier]`, `[collapseMultiplier]`, `[hyperbleed]`, `[hypercollapse]` that shows bleed chance and current damage multiplier, similar to it's crit counterpart.

- Other Contents
    - Ability to change base crit/bleed chance.
    - Laser Scope rebalance: gives it crit chance, like other crit items.
    - [Moonglasses](https://thunderstore.io/package/TheMysticSword/MysticsItems/) rework: Instead of halving Crit chance, reduces it by -100% (Configurable) to be more impactful.
    - Shtterspleen rework: Instead of having a separate bleed proc, simply adds crit chance to bleed chance. (slight nerf)
    - LookingGlass's `[critWithLuck]` and `[bleedChanceWithLuck]` reflects reality with [Scratch Ticket](https://thunderstore.io/package/TheMysticSword/MysticsItems/)s better.
    - Hyperbolic Crit/Bleed/Collapse: opposite of hypercrit/bleed where all crit/bleed/collapse gain is capped to 100% with first 10% stack giving 10%. (Coefficient 1/9)

## BTW
The code is under GPL3, since the original hypercrit was.

also I did get permission from mystic btw  
![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/1.png)

## Changelog
- 1.2.8: the compat is reforged
- 1.2.7: made to work with SotS
- 1.2.6: The Incident
- 1.2.5: base crit/bleed chance
- 1.2.4: Bugfixes
- 1.2.3: Bugfixes, Starstorm 2 (needles) compat, Buffed moonglasses default
- 1.2.2: Bugfixes
- 1.2.1: Bugfixes
- 1.2.0: added Laser Focus rebalance
- 1.1.3: Bugfixes
- 1.1.2: Bugfixes
- 1.1.1: Added changelog, The Ultimate README Update Of All Time
- 1.1.0: Added hyperbolic scaling, every bleed/collapse related changes, fractional scaling, configurable decay
- 1.0.0: Initial Commit