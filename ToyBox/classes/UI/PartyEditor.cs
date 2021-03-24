﻿// Copyright < 2021 > Narria(github user Cabarius) - License: MIT
using UnityEngine;
using UnityModManagerNet;
using UnityEngine.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Armors;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Items.Shields;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Blueprints.Quests;
using Kingmaker.Blueprints.Root;
using Kingmaker.Cheats;
using Kingmaker.Controllers.Rest;
using Kingmaker.Designers;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.GameModes;
using Kingmaker.Items;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.Utility;

namespace ToyBox {
    public class PartyEditor {
        static int showStatsBitfield = 0;
        static int showDetailsBitfield = 0;
        static String searchText = "";
        private static NamedFunc<List<UnitEntityData>>[] _partyFilterChoices = null;
        public static NamedFunc<List<UnitEntityData>>[] GetPartyFilterChoices() {
            var player = Game.Instance.Player;
            if (player != null && _partyFilterChoices == null) {
                _partyFilterChoices = new NamedFunc<List<UnitEntityData>>[] {
                    new NamedFunc<List<UnitEntityData>>("Party", () => player.Party),
                    new NamedFunc<List<UnitEntityData>>("Party & Pets", () => player.m_PartyAndPets),
                    new NamedFunc<List<UnitEntityData>>("All Characters", () => player.AllCharacters),
                    new NamedFunc<List<UnitEntityData>>("Active Companions", () => player.ActiveCompanions),
                    new NamedFunc<List<UnitEntityData>>("Remote Companions", () => player.m_RemoteCompanions),
                    new NamedFunc<List<UnitEntityData>>("Custom (Mercs)", PartyUtils.GetCustomCompanions),
                    new NamedFunc<List<UnitEntityData>>("Pets", PartyUtils.GetPets)
                };
            }
            return _partyFilterChoices;
        }
        static List<UnitEntityData> characterList = null;
        public static List<UnitEntityData> GetCharacterList() {
            var partyFilterChoices = GetPartyFilterChoices();
            if (partyFilterChoices == null) { return null; }
            return partyFilterChoices[Main.settings.selectedPartyFilter].func();
        }
        public static void OnGUI(UnityModManager.ModEntry modEntry) {
            var player = Game.Instance.Player;
            var filterChoices = GetPartyFilterChoices();
            if (filterChoices == null) { return; }
            UI.Space(25);

            UnitEntityData charToAdd = null;
            UnitEntityData charToRemove = null;
            characterList = UI.TypePicker<List<UnitEntityData>>(
                null,
                ref Main.settings.selectedPartyFilter,
                filterChoices
                );
            int chIndex = 0;
            int respecableCount = 0;
            foreach (UnitEntityData ch in characterList) {
                UnitProgressionData progression = ch.Descriptor.Progression;
                BlueprintStatProgression xpTable = BlueprintRoot.Instance.Progression.XPTable;
                int level = progression.CharacterLevel;
                int mythicLevel = progression.MythicExperience;
                UI.BeginHorizontal();

                UI.Label(ch.CharacterName.orange().bold(), UI.Width(250f));
                UI.Label("level".green() + $": {level}", UI.Width(125f));
                // Level up code adapted from Bag of Tricks https://www.nexusmods.com/pathfinderkingmaker/mods/2
                if (progression.Experience < xpTable.GetBonus(level + 1) && level < 20) {
                    UI.ActionButton(" +1 Level", () => {
                        progression.AdvanceExperienceTo(xpTable.GetBonus(level + 1), true);
                    }, UI.Width(150));
                }
                else if (progression.Experience >= xpTable.GetBonus(level + 1) && level < 20) {
                    UI.Label("Level Up".cyan().italic(), UI.Width(150));
                }
                else {
                    UI.Space(153);
                }
                UI.Space(30);
                UI.Label($"mythic".green() + $": {mythicLevel}", UI.Width(125));
                if (progression.MythicExperience < 10) {
                    UI.ActionButton(" +1 Mythic", () => {
                        progression.AdvanceMythicExperience(progression.MythicExperience + 1, true);
                    }, UI.Width(150));
                }
                else {
                    UI.Label("Max", UI.Width(150));
                }
                UI.Space(25);
                UI.DisclosureBitFieldToggle("Stats", ref showStatsBitfield, chIndex, false);
                UI.Space(25);
                UI.DisclosureBitFieldToggle("Details", ref showDetailsBitfield, chIndex, false);
                UI.Space(50);

                if (player.Party.Contains(ch)) {
                    respecableCount++;
                    UI.ActionButton("Respec", () => { Actions.ToggleModWindow(); UnitHelper.Respec(ch); }, UI.Width(150));
                }
                else {
                    UI.Space(153);
                }
                UI.Space(50);
                if (!player.PartyAndPets.Contains(ch)) {
                    UI.ActionButton("Add To Party", () => { charToAdd = ch; }, UI.AutoWidth());
                }
                else if (player.ActiveCompanions.Contains(ch)) {
                    UI.ActionButton("Remove From Party", () => { charToRemove = ch; }, UI.AutoWidth());
                }
                UI.EndHorizontal();

                if (((1 << chIndex) & showStatsBitfield) != 0) {
                    foreach (object obj in Enum.GetValues(typeof(StatType))) {
                        StatType statType = (StatType)obj;
                        ModifiableValue modifiableValue = ch.Stats.GetStat(statType);
                        if (modifiableValue != null) {
                            UI.BeginHorizontal();
                            UI.Space(69);   // the best number...
                            UI.Label(statType.ToString().green().bold(), UI.Width(400f));
                            UI.Space(25f);
                            UI.ActionButton(" < ", () => { modifiableValue.BaseValue -= 1; }, UI.AutoWidth());
                            UI.Space(20f);
                            UI.Label($"{modifiableValue.BaseValue}".orange().bold(), UI.Width(50f));
                            UI.ActionButton(" > ", () => { modifiableValue.BaseValue += 1; }, UI.AutoWidth());
                            UI.EndHorizontal();
                        }
                    }

                }

                if (((1 << chIndex) & showDetailsBitfield) != 0) {
                    UI.BeginHorizontal();
                    UI.Space(100);
                    UI.TextField(ref searchText, null, UI.Width(200));
                    UI.EndHorizontal();
                    var facts = ch.Descriptor.Progression.Features;
                    Feature factToRemove = null;
                    Feature factToRankUp = null;
                    Feature factToRankDown = null;
                    foreach (Feature fact in facts) {
                        String name = fact.Name;
                        if (name == null) { name = $"{fact.Blueprint.name}"; }
                        if (name != null && name.Length > 0 && (searchText.Length == 0 || name.Contains(searchText))) {
                            UI.BeginHorizontal();
                            UI.Space(100);
                            UI.Label($"{fact.Name}".cyan().bold(), UI.Width(400));
                            UI.Space(30);
                            try {
                                var rank = fact.GetRank();
                                var max = fact.Blueprint.Ranks;
                                if (rank > 1) {
                                    UI.ActionButton("<", () => { factToRankDown = fact; }, UI.Width(50));
                                }
                                else { UI.Space(53); }
                                UI.Space(10f);
                                UI.Label($"{rank}".orange().bold(), UI.Width(30f));
                                if (rank < max) {
                                    UI.ActionButton(">", () => { factToRankUp = fact; }, UI.Width(50));
                                }
                                else { UI.Space(53); }
                            }
                            catch { }
                            UI.Space(30);
                            UI.ActionButton("Remove", () => { factToRemove = fact; }, UI.Width(150));
                            String description = fact.Description;
                            if (description != null) {
                                UI.Space(30);
                                UI.Label(description.green(), UI.AutoWidth());
                            }
                            UI.EndHorizontal();
                        }
                    }
                    if (factToRankDown != null) { try { factToRankDown.RemoveRank(); } catch { } }
                    if (factToRankUp != null) { try { factToRankUp.AddRank(); } catch { } }
                    if (factToRemove != null) {
                        ch.Descriptor.Progression.Features.RemoveFact(factToRemove);
                    }
                }
                chIndex += 1;
            }
            UI.Space(25);
            if (respecableCount > 0) {
                UI.Label($"{respecableCount} characters".yellow().bold() + " can be respecced. Pressing Respec will close the mod window and take you to character level up".orange());
                UI.Label("WARNING".yellow().bold() + " this feature is ".orange() + "EXPERIMENTAL".yellow().bold() + " and uses unreleased and likely buggy code.".orange());
                UI.Label("BACK UP".yellow().bold() + " before playing with this feature.You will lose your mythic ranks but you can restore them in this Party Editor.".orange());

            }
            UI.Space(25);
            if (charToAdd != null) { UnitEntityDataUtils.AddCompanion(charToAdd); }
            if (charToRemove != null) { UnitEntityDataUtils.RemoveCompanion(charToRemove); }
        }
    }
}