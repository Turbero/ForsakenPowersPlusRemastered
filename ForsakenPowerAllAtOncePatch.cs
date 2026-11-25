using System.Collections.Generic;
using ForsakenPowersPlusRemastered.tools;
using HarmonyLib;
using UnityEngine;
using Logger = ForsakenPowersPlusRemastered.tools.Logger;

namespace ForsakenPowersPlusRemastered
{
    [HarmonyPatch(typeof(Player), "StartGuardianPower")]
    public class ActivateAllGuardianPowersPatch
    {
        private static bool altAtStart;
        
        public static bool Prefix(ref Player __instance)
        {
            if (ConfigurationFile.enablePassiveMode.Value == ConfigurationFile.TogglePassive.On &&
                __instance.m_guardianPowerCooldown <= 0)
            {
                //Clear previous passive buffs
                List<StatusEffect> statusEffectsToDelete = new List<StatusEffect>();
                
                SEMan seMan = Player.m_localPlayer.GetSEMan();
                foreach (StatusEffect se in seMan.GetStatusEffects())
                    if (IsBossPower(se.name))
                        statusEffectsToDelete.Add(se);

                Logger.Log("statusEffectsToDelete: "+statusEffectsToDelete.Count);
                foreach (StatusEffect se in statusEffectsToDelete)
                    seMan.RemoveStatusEffect(se);
            }
            
            Logger.Log("Detect alt");
            altAtStart = Input.GetKey(KeyCode.LeftAlt);
            Logger.Log("Alt " + altAtStart);
            return true;
        }
        
        public static void Postfix(ref Player __instance)
        {
            Logger.Log("Check activate all powers available at once");
            if (ConfigurationFile.enableAllAtOnceMode.Value == ConfigurationFile.Toggle.On && altAtStart)
            {
                Logger.Log("Activating everything available at once");
                string currentPower = __instance.GetGuardianPowerName();
                
                //1) remove existing except currently selected
                SEMan sem = __instance.GetSEMan();
                List<string> existingActivePowers = new List<string>();
                foreach (StatusEffect se in sem.GetStatusEffects())
                    if (IsBossPower(se.name) && !se.name.Equals(currentPower))
                        existingActivePowers.Add(se.name);

                //2) Calculate other powers available
                List<string> playerOtherPowersKeys = new List<string>();
                foreach (string playerKey in __instance.GetUniqueKeys())
                {
                    if (IsBossPower(playerKey) && !playerKey.Equals(currentPower))
                        playerOtherPowersKeys.Add(playerKey);
                }
                
                //3) Apply available powers
                foreach (string name in playerOtherPowersKeys)
                {
                    if (!existingActivePowers.Contains(name))
                    {
                        int m_guardianPowerHash = string.IsNullOrEmpty(name) ? 0 : name.GetStableHashCode();
                        StatusEffect m_guardianSE = ObjectDB.instance.GetStatusEffect(m_guardianPowerHash);
                        sem.AddStatusEffect(m_guardianSE.NameHash(), true); //resets timer if power exists on player
                    }
                }
                
                if (ConfigurationFile.enableAllAtOnceForEveryoneMode.Value == ConfigurationFile.Toggle.On) {
                    // 4) Grant other powers to other players around
                    List<Player> players = new List<Player>();
                    Player.GetPlayersInRange(__instance.transform.position, ConfigurationFile.guardianRadius.Value, players);
                    foreach (Player otherPlayer in players)
                        if (otherPlayer.GetPlayerName() != __instance.name) //Send to other people in range
                            ZRoutedRpc.instance.InvokeRoutedRPC(otherPlayer.GetZDOID().UserID, "RPC_GivePowersToLocalPlayer", playerOtherPowersKeys);
                }
            }
            altAtStart = false;
        }

        public static void RPC_GivePowersToLocalPlayer(long sender, List<string> powersToGrant)
        {
            Logger.Log($"{Player.m_localPlayer.GetPlayerName()} will be additionally granted with {string.Join(",", powersToGrant)}");
            SEMan sem = Player.m_localPlayer.GetSEMan();
            foreach (string name in powersToGrant)
            {
                Logger.Log($"Granting {name} to {Player.m_localPlayer.GetPlayerName()}");
                int m_guardianPowerHash = string.IsNullOrEmpty(name) ? 0 : name.GetStableHashCode();
                StatusEffect m_guardianSE = ObjectDB.instance.GetStatusEffect(m_guardianPowerHash);
                sem.AddStatusEffect(m_guardianSE.NameHash(), true); //resets timer if power exists on player
            }
        }

        private static bool IsBossPower(string name)
        {
            return name.StartsWith("GP_") || name.StartsWith("SE_Boss");
        }
    }
}