using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx.Bootstrap;

namespace NotHypercrit
{
    public static class BleedExtension
    {
        public static float GetBleedDamage(this CharacterBody body)
        {
            if (!Bleed.bleedDamage.ContainsKey(body)) Bleed.bleedDamage.Add(body, 1);
            return Bleed.bleedDamage[body];
        }

        public static CharacterBody SetBleedDamage(this CharacterBody body, float value)
        {
            if (!Bleed.bleedDamage.ContainsKey(body)) Bleed.bleedDamage.Add(body, value);
            else Bleed.bleedDamage[body] = value;
            return body;
        }
        public static float GetCollapseDamage(this CharacterBody body)
        {
            if (!Bleed.collapseDamage.ContainsKey(body)) Bleed.collapseDamage.Add(body, 1);
            return Bleed.collapseDamage[body];
        }

        public static CharacterBody SetCollapseDamage(this CharacterBody body, float value)
        {
            if (!Bleed.collapseDamage.ContainsKey(body)) Bleed.collapseDamage.Add(body, value);
            else Bleed.collapseDamage[body] = value;
            return body;
        }
    }

    public class Bleed
    {
        public class InflictHyperbleedInfo : Main.AdditionalProcInfo
        {
            public GameObject attacker;
            public DotController.DotIndex dotIndex;
            public void Deserialize(NetworkReader reader)
            {
                base.Deserialize(reader);
                dotIndex = (DotController.DotIndex)reader.ReadInt32();
            }
            public void Serialize(NetworkWriter writer)
            {
                base.Serialize(writer);
                writer.Write((int)dotIndex);
            }
        }


        public static List<InflictHyperbleedInfo> inflictHyperBleedInfos = new List<InflictHyperbleedInfo>();
        public static InflictHyperbleedInfo lastNetworkedBleedInfo = null;
        public static Dictionary<CharacterBody, float> bleedDamage = new();
        public static Dictionary<CharacterBody, float> collapseDamage = new();
        public static InflictDotInfo lastInfo;

        public static void Patch()
        {
            Main.Log.LogDebug("The Spirit of TheMysticSword Burns Alight...");
            Run.onRunStartGlobal += (run) => bleedDamage.Clear();
            On.RoR2.CharacterBody.Start += (orig, self) => { self.GetBleedDamage(); self.GetCollapseDamage(); orig(self); }; // initialize bleed damage so recalcs can use it
            RecalculateStatsAPI.GetStatCoefficients += (self, args) => { bleedDamage[self] = 1; collapseDamage[self] = 1; }; // ones that uses bleeddamage would have hypercrit2 as softdep so
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += (il) =>
            {
                ILCursor c = new(il);
                int damageInfoIndex = -1;
                c.GotoNext(x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.bleedChance)));
                c.GotoNext(x => x.MatchLdcI4((int)ProcType.BleedOnHit));
                c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt<ProcChainMask>(nameof(ProcChainMask.AddProc)));
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Action<DamageInfo>>((info) =>
                {
                    if (info.attacker)
                    {
                        CharacterBody self = info.attacker.GetComponent<CharacterBody>();
                        if (!self) return;
                        InflictHyperbleedInfo aci = RollHyperbleed(DotController.DotIndex.Bleed, self.GetBleedDamage() - 1f, self);
                        inflictHyperBleedInfos.Add(aci);
                    }
                });
                c.GotoNext(x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.BleedOnHitVoid)));
                c.GotoNext(x => x.MatchLdcI4((int)ProcType.FractureOnHit));
                c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt<ProcChainMask>(nameof(ProcChainMask.AddProc)));
                c.Emit(OpCodes.Ldarg, 1);
                c.EmitDelegate<Action<DamageInfo>>((info) =>
                {
                    if (info.attacker)
                    {
                        CharacterBody self = info.attacker.GetComponent<CharacterBody>();
                        if (!self) return;
                        InflictHyperbleedInfo aci = RollHyperbleed(DotController.DotIndex.Fracture, self.GetCollapseDamage() - 1f, self);
                        inflictHyperBleedInfos.Add(aci);
                    }
                });
            };
            IL.RoR2.DotController.InflictDot_refInflictDotInfo += (il) =>
            {
                ILCursor c = new(il);
                c.GotoNext(x => x.MatchCallOrCallvirt<DotController.OnDotInflictedServerGlobalDelegate>(nameof(DotController.OnDotInflictedServerGlobalDelegate.Invoke)));
                c.GotoPrev(x => x.MatchLdarg(0));
                c.Remove();
                c.GotoNext(x => x.MatchCallOrCallvirt<DotController.OnDotInflictedServerGlobalDelegate>(nameof(DotController.OnDotInflictedServerGlobalDelegate.Invoke)));
                c.Remove();
                c.EmitDelegate<Action<DotController.OnDotInflictedServerGlobalDelegate, DotController>>((inflictedServerGlobal, self) =>
                {
                    var attacker = lastInfo.attackerObject;
                    var dotIndex = lastInfo.dotIndex;
                    if (attacker && ((dotIndex == DotController.DotIndex.Bleed) || (dotIndex == DotController.DotIndex.Fracture)))
                    {
                        var inflictHyperbleedInfo = inflictHyperBleedInfos.Find(x => x.attacker == attacker && x.dotIndex == dotIndex);
                        if (inflictHyperbleedInfo == null) return;
                        if (inflictedServerGlobal != null) for (var i = 0; i < inflictHyperbleedInfo.numProcs; i++) inflictedServerGlobal(self, ref lastInfo);
                        inflictHyperBleedInfos.Remove(inflictHyperbleedInfo);
                    }
                });
            };
            if (Main.BleedColor.Value > 1) IL.RoR2.HealthComponent.HandleDamageDealt += (il) =>
            {
                ILCursor c = new(il);
                c.GotoNext(x => x.MatchCallOrCallvirt<DamageNumberManager>(nameof(DamageNumberManager.SpawnDamageNumber)));
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Pop); // I got gun for fake necklace
                c.Emit(OpCodes.Pop); // gun that send texts
                c.Emit(OpCodes.Pop); // gun that make breakfast
                c.Emit(OpCodes.Pop); // gun that sign breasts
                c.Emit(OpCodes.Pop);
                c.Remove();
                c.Emit(OpCodes.Ldloc_0);
                c.Emit(OpCodes.Ldloc_2);
                c.EmitDelegate<Action<DamageDealtMessage, TeamComponent>>((msg, team) =>
                {
                    if (msg.damageColorIndex == DamageColorIndex.Bleed && (bool)msg.victim && (bool)msg.attacker)
                    {
                        CharacterBody attacker = msg.attacker.GetComponent<CharacterBody>();
                        CharacterBody victim = msg.victim.GetComponent<CharacterBody>();
                        if (attacker && victim)
                        {
                            // weird spawndamagenumber variant!!
                            float mult = msg.damage / (victim.GetBuffCount(RoR2Content.Buffs.Bleeding) * 0.2f * attacker.baseDamage);
                            var stack = victim.GetBuffCount(RoR2Content.Buffs.Bleeding) + 1;
                            if (mult > 0)
                            {
                                float v = (stack - 1) % Main.BleedColor.Value / (Main.BleedColor.Value - 1);
                                Color c = Color.HSVToRGB(0f, 142f / 240f, 0.75f - (0.5f * v));
                                if (team.teamIndex == TeamIndex.None) c *= Color.gray;
                                if (team.teamIndex == TeamIndex.Monster) c *= new Color(0.5568628f, 0.2941177f, 0.6039216f);
                                DamageNumberManager.instance.ps.Emit(new ParticleSystem.EmitParams() { position = msg.position, startColor = c, applyShapeToPosition = true }, 1);
                                DamageNumberManager.instance.ps.GetCustomParticleData(DamageNumberManager.instance.customData, ParticleSystemCustomData.Custom1);
                                DamageNumberManager.instance.customData[DamageNumberManager.instance.customData.Count - 1] = new Vector4(1f, 0.0f, msg.damage, msg.crit ? 1f : 0.0f);
                                DamageNumberManager.instance.ps.SetCustomParticleData(DamageNumberManager.instance.customData, ParticleSystemCustomData.Custom1);
                                return;
                            }
                        }
                    }
                    DamageNumberManager.instance.SpawnDamageNumber(msg.damage, msg.position, msg.crit, (bool)team ? team.teamIndex : TeamIndex.None, msg.damageColorIndex);
                });
            };
        }

        public static void PatchStack()
        {
            On.RoR2.DotController.InflictDot_refInflictDotInfo += (On.RoR2.DotController.orig_InflictDot_refInflictDotInfo orig, ref InflictDotInfo info) =>
            {
                if (NetworkServer.active)
                {
                    var attacker = info.attackerObject;
                    var dotIndex = info.dotIndex;
                    if (attacker)
                    {
                        if ((dotIndex == DotController.DotIndex.Bleed) || (dotIndex == DotController.DotIndex.Fracture)) info.damageMultiplier *= HyperbleedMultiplier(attacker, dotIndex);
                    }
                }
                lastInfo = info;
                orig(ref info);
            };
            if (Main.LamerShatterspleen.Value)
            {
                IL.RoR2.GlobalEventManager.ProcessHitEnemy += (il) =>
                {
                    ILCursor c = new(il);
                    if (!c.TryGotoNext(x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.BleedOnHitAndExplode)), x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))) return;
                    c.Index += 2;
                    c.Emit(OpCodes.Pop);
                    c.Emit(OpCodes.Ldc_I4, 0);
                };
                On.RoR2.Language.GetLocalizedStringByToken += (orig, self, token) =>
                {
                    if (token == "ITEM_BLEEDONHITANDEXPLODE_DESC")
                    {
                        string ret = "Gain <style=cIsDamage>5%</style> critical chance. Gain <style=cIsDamage>Bleed chance</style> equal to your <style=cIsDamage>Critical chance</style>. <style=cIsDamage>Bleeding</style> enemies <style=cIsDamage>explode</style> on death for <style=cIsDamage>400%</style> <style=cStack>(+400% per stack)</style> damage";
                        if (!Main.Mods("Hayaku.VanillaRebalance")) ret += ", plus an additional <style=cIsDamage>15%</style> <style=cStack>(+15% per stack)</style> of their maximum health";
                        return ret + ".";
                    }
                    else return orig(self, token);
                };
            }
            RoR2Application.onLoad += () => On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                orig(self);
                if (self == null || self.inventory == null) return;
                if (Main.LamerShatterspleen.Value && self.inventory.GetItemCountEffective(RoR2Content.Items.BleedOnHitAndExplode) > 0) self.bleedChance += self.crit;
            };
        }
        public static float HyperbleedMultiplier(GameObject attacker, DotController.DotIndex dotIndex)
        {
            InflictHyperbleedInfo inflictHyperbleedInfo = inflictHyperBleedInfos.FirstOrDefault(x => x.attacker == attacker && x.dotIndex == dotIndex);
            if (inflictHyperbleedInfo != null && inflictHyperbleedInfo != default) return inflictHyperbleedInfo.damageMult; // this should cover mods that don't use hypercrit2?
            return 1;
        }

        public static InflictHyperbleedInfo RollHyperbleed(DotController.DotIndex dotIndex, float damage, CharacterBody body, bool forceSingleBleed = false)
        {
            var aci = new InflictHyperbleedInfo();
            if (body)
            {
                aci.attacker = body.gameObject;
                aci.dotIndex = dotIndex;
                aci.totalChance = body.bleedChance;
                float effectiveCount = GetEffectiveCount(body.bleedChance, body, dotIndex == DotController.DotIndex.Fracture, forceSingleBleed);
                aci.num = Main.CalcChance(Main.BleedChance.Value, effectiveCount, body.master.luck);
                if (aci.num == 0) aci.damageMult = 1f;
                else
                {
                    if (dotIndex == DotController.DotIndex.Bleed) aci.damageMult = (1 + damage) * Main.Calc(
                        Main.BleedMode.Value,
                        Main.BleedA.Value,
                        Main.BleedB.Value,
                        effectiveCount);
                    else if (dotIndex == DotController.DotIndex.Fracture) aci.damageMult = (1 + damage) * Main.Calc(
                        Main.CollapseMode.Value,
                        Main.CollapseA.Value,
                        Main.CollapseB.Value,
                        effectiveCount);
                }
                float proc = 0;
                if (aci.num != 0)
                {
                    if (dotIndex == DotController.DotIndex.Bleed) proc = Main.Calc(
                    Main.BleedProcMode.Value,
                    Main.BleedProcA.Value,
                    Main.BleedProcB.Value,
                    effectiveCount);
                    else if (dotIndex == DotController.DotIndex.Fracture) proc = Main.Calc(
                    Main.CollapseProcMode.Value,
                    Main.CollapseProcA.Value,
                    Main.CollapseProcB.Value,
                    effectiveCount);
                }
                aci.numProcs = (int)proc + (Util.CheckRoll(proc % 1 * 100) ? 1 : 0);
            }
            return aci;
        }

        public static float GetDamage(float chance, float damage, CharacterBody body, bool collapse = false)
        {
            if (!collapse) return (1 + damage) * Main.Calc(
                Main.BleedMode.Value,
                Main.BleedA.Value,
                Main.BleedB.Value,
                Mathf.Max(1, GetEffectiveCount(chance + (100 - (chance % 100f)), body)));
            else return (1 + damage) * Main.Calc(
                Main.CollapseMode.Value,
                Main.CollapseA.Value,
                Main.CollapseB.Value,
                Mathf.Max(1, GetEffectiveCount(chance + (100 - (chance % 100f)), body, true)));
        }

        public static float GetEffectiveCount(InflictHyperbleedInfo aci)
        {
            return aci.num;
        }

        public static float GetEffectiveCount(float chance, CharacterBody body, bool collapse = false, bool forceSingleBleed = false)
        {
            float ret = Mathf.Max(0, (chance - (forceSingleBleed ? 100 : 0)) / 100f);
            if ((collapse ? Main.CollapseCap.Value : Main.BleedCap.Value) >= 0) ret = Mathf.Min(ret, collapse ? Main.CollapseCap.Value : Main.BleedCap.Value);
            if (ret <= 0) return 0;
            // TODO: 
            return Main.CalcChance(collapse ? Main.CollapseChance.Value : Main.BleedChance.Value, ret, body.master.luck);
        }
    }
}
