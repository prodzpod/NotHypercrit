# Hypercrit 2: The Critening

This is a rewrite/continuation of [Hypercrit by Thinkinvis](https://thunderstore.io/package/ThinkInvis/Hypercrit/) and [Hyperbleed by TheMysticSword](https://thunderstore.io/package/TheMysticSword/Hyperbleed/). 

**Hypercrit**: Adds a highly configurable crit-stacking mechanic which gives an effect to crit chance past 100%.  
**Hyperbleed**: Stacking bleed chance past 100% increases bleed damage. Also adds support for collapse.
**Hypercollapse**: ????

## Config 
### Chance Calculation
The "crit stack" is calculated using a configurable formula that takes in the chance as argument.

- Rollover: Default behaviour, crit amount = chance / 100 with (chance % 100)% chance to get +1 amount. (500% = 5xCrit, 550% = 50% 5xCrit 50% 6xCrit, ...)
- Uniform: Each 100% chance, a new equal sized slice of the pie is added. 500% chance has 20% of 1xCrit, 20% 2xCrit ... 20% 5xCrit.
![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/images/6.png)
- Binomial: Same as uniform but with a different distribution.
![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/images/7.png)
- Halves: Same as uniform but with a different distribution. Top-heavy.
![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/images/8.png)
- ReverseHalves: Reverse of Halves, bottom-heavy.
![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/images/9.png)

### Damage Calculation
On crit, Damage is calculated by damage * (1 + bonus crit damage) * multiplier. This config determines the multiplier based on the "crit stack" and A/B value in config. the A/B value does different things based on the damage mode.
- Basic: ![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/images/1.png)
- Exponential: ![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/images/2.png)
- Cumulative: ![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/images/3.png)
- Asymptotic: ![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/images/4.png)
- Logarithmic: ![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/images/5.png)

## Changes from the original

- Proc config: "On-crit"/"On-bleed" effects apply multiple times on Hypercrit. Configurable.
- Color Variation: Gets darker as bleed stacks. Configurable.
- [LookingGlass](https://thunderstore.io/package/DropPod/LookingGlass/) support: adds `[collapseChance]`, `[bleedChanceWithLuck]`, `[collapseChanceWithLuck]`, `[bleedMultiplier]`, `[collapseMultiplier]`, `[hypercrit]`, `[hyperbleed]`, `[hypercollapse]` that shows crit, bleed and collapse chance and current damage multiplier.
- Built-in Bleed Damage support with extension methods. easy inter-mod ops.
- Ability to change base crit/bleed chance.
- Laser Scope rebalance: gives it crit chance, like other crit items.
- [Moonglasses](https://thunderstore.io/package/TheMysticSword/MysticsItems/) rework: Instead of halving Crit chance, reduces it by -100% (Configurable) to be more impactful.
- Shtterspleen rework: Instead of having a separate bleed proc, simply adds crit chance to bleed chance. (slight nerf)
- LookingGlass's `[critWithLuck]` and `[bleedChanceWithLuck]` reflects reality with [Scratch Ticket](https://thunderstore.io/package/TheMysticSword/MysticsItems/)s better.

## BTW
The code is under GPL3, since the original hypercrit was.

also I did get permission from mystic btw  
![Image](https://raw.githubusercontent.com/prodzpod/NotHypercrit/master/1.png)

## Changelog
- 2.0.0: made to work with AC / config overhaul
- 1.3.2: uwu teehee oopsies
- 1.3.1: thank @diegowde for this 1
- 1.3.0: riskymod compat fix, mysticitems compat fix, added more laser scope configs
- 1.2.9: the compat is reforged (mysticitems)
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