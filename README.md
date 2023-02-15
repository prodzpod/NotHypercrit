# Hypercrit 2: The Critening

This is a rewrite/continuation of [Hypercrit by Thinkinvis](https://thunderstore.io/package/ThinkInvis/Hypercrit/) and [Hyperbleed by TheMysticSword](https://thunderstore.io/package/TheMysticSword/Hyperbleed/). 

**Hypercrit**: Adds a highly configurable crit-stacking mechanic which gives an effect to crit chance past 100%.  
**Hyperbleed**: Stacking bleed chance past 100% increases bleed damage. Also adds support for collapse.
## Changes from the original

- Hypercrit
    - Hyperbolic Scaling: Just like Hopoo intended...
    - Proc config: "On-crit" effects apply multiple times on Hypercrit. Configurable.
    - Fractional Crit Damage Increase: instead of damage increasing every 100%, all percentage past 100% is counted partially, similar to original hyperbleed.
    - [BetterUI](https://thunderstore.io/package/XoXFaby/BetterUI/) support: adds `$hypercrit` that shows total current damage multiplier.

- Hyperbleed
    - Color Variation: Gets darker as bleed stacks. Configurable.
    - Hyperbleed Config: more sophisticated config like hypercrit's, on top of the bleed/collapse enables. Also contains non-fractional bleed configs.
    - Bleed Stack Config: multiple stacks of bleeds can now have diminishing (or exponential) returns. Configurable. Also contains separate config for Collapse.
    - Separated bleed and collapse configs to allow more control
    - Bleed "proc"s: Same with hypercrit's proc config, but more niche. Hook through `DotController.onDotInflictedServerGlobal`.
    - Built-in Bleed Damage support with extension methods. easy inter-mod ops.
    - [BetterUI](https://thunderstore.io/package/XoXFaby/BetterUI/) support: adds `$bleed`, `$collapse`, `$luckbleed`, `$luckcollapse`, `$bleeddamage`, `$collapsedamage`, `$hyperbleed`, `$hypercollapse` that shows bleed chance and current damage multiplier, similar to it's crit counterpart.

- Other Contents
    - [Moonglasses](https://thunderstore.io/package/TheMysticSword/MysticsItems/) rework: Instead of halving Crit chance, reduces it by -100% (Configurable) to be more impactful.
    - Shtterspleen rework: Instead of having a separate bleed proc, simply adds crit chance to bleed chance. (slight nerf)
    - BetterUI's `$luckcrit` and `$luckbleed` reflects reality with [Scratch Ticket](https://thunderstore.io/package/TheMysticSword/MysticsItems/)s better.
    - Hyperbolic Crit/Bleed/Collapse: opposite of hypercrit/bleed where all crit/bleed/collapse gain is capped to 100% with first 10% stack giving 10%. (Coefficient 1/9)

## BTW
The code is under GPL3, since the original hypercrit was.

also I did get permission from mystic btw  
![Image](https://cdn.discordapp.com/attachments/515678914316861451/1075218747457019984/image.png)

## Changelog
- 1.1.3: Bugfixes
- 1.1.2: Bugfixes
- 1.1.1: Added changelog, The Ultimate README Update Of All Time
- 1.1.0: Added hyperbolic scaling, every bleed/collapse related changes, fractional scaling, configurable decay
- 1.0.0: Initial Commit