using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using ForsakenPowersPlusRemastered.tools;
using Logger = ForsakenPowersPlusRemastered.tools.Logger;

namespace ForsakenPowersPlusRemastered
{

    public enum BossPowerEnum
    {
        GP_Eikthyr = 1,
        GP_TheElder = 2,
        GP_Bonemass = 3,
        GP_Moder = 4,
        GP_Yagluth = 5,
        GP_Queen = 6,
        GP_Fader = 7
    }

    public class BossPower
    {
        public readonly string name;
        public readonly int order;

        public BossPower(string name, int order)
        {
            this.name = name;
            this.order = order;
        }
    }
    
    [HarmonyPatch(typeof(Player), "Update")]
    public class ForsakenPower_Patch
    {
        private static void Prefix(Player __instance)
        {
            if (__instance == null)
            {
                return;
            }

            Player player = __instance;
            bool hasAnyPower = player.GetGuardianPowerName() != null && player.GetGuardianPowerName().Length > 0;
            if (Input.GetKeyDown(ConfigurationFile.ForsakenPowerHotkey.Value) && hasAnyPower)
            {
                BossPower nextAvailablePower = findNextAvailablePower(player);
                player.SetGuardianPower(nextAvailablePower.name);
                player.Message(MessageHud.MessageType.Center, $"{ConfigurationFile.messagePowerSelected.Value}: {nextAvailablePower.name.Replace("GP_", "")}");
            }

            if (Input.GetKeyDown(ConfigurationFile.ResetPowerHotkey.Value) && hasAnyPower)
            {
                // resets the active power and gets the player ready for a new power
                player.m_guardianPowerCooldown = 0.1f;

                if (ConfigurationFile.enabledReset.Value == ConfigurationFile.Toggle.On)
                {
                    SEMan sem = player.GetSEMan();
                    List<int> removePowers = new List<int>();
                    foreach (StatusEffect se in sem.GetStatusEffects())
                        if (se.name.StartsWith("GP_"))
                            removePowers.Add(se.NameHash());

                    foreach (int power in removePowers)
                    {
                        sem.RemoveStatusEffect(power, true);
                        Logger.Log($"Removed {power} forsaken power.");
                    }
                    player.Message(MessageHud.MessageType.Center, ConfigurationFile.messagePowerReset.Value);
                } else
                    player.Message(MessageHud.MessageType.Center, ConfigurationFile.messagePowerReady.Value);
            }
        }

        private static BossPower findNextAvailablePower(Player player)
        {
            List<BossPower> playerTotalPowers = new List<BossPower>();
            int nextNonVanillaOrder = 9;
            foreach (string playerKey in player.GetUniqueKeys())
            {
                if (playerKey.StartsWith("GP_")) {
                    bool isVanilla = Enum.TryParse(playerKey, out BossPowerEnum power);
                    if (isVanilla)
                        playerTotalPowers.Add(new BossPower(power.ToString(), (int) power));
                    else
                        playerTotalPowers.Add(new BossPower(playerKey, nextNonVanillaOrder++));
                }
            }
            Logger.Log("Available powers size: "+playerTotalPowers.Count);
            
            bool currentIsVanillaPower = Enum.TryParse(player.GetGuardianPowerName(), out BossPowerEnum currentPower);
            int currentPowerPower = currentIsVanillaPower 
                ? (int) currentPower 
                : playerTotalPowers.First(p => p.name.Equals(player.GetGuardianPowerName())).order;
            List<BossPower> powersAfterCurrent = playerTotalPowers
                    .FindAll(p => p.order > currentPowerPower)
                    .OrderBy(x => x.order)
                    .ToList();
            Logger.Log("powersAfterCurrent sorted list size: "+powersAfterCurrent.Count);

            if (powersAfterCurrent.Count == 0)
            {
                //return first known power of the sequence
                List<BossPower> allSortedList = playerTotalPowers
                    .OrderBy(x => x.order)
                    .ToList();
                return allSortedList[0];
            }

            //return next available power of the sequence
            BossPower firstAvailablePower = powersAfterCurrent[0];
            Logger.Log("Next available: " + firstAvailablePower);

            return firstAvailablePower;
        }
    }
}