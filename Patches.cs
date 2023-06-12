using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Queue_Me_Maybe;

public static class EquipPatchState
{
    public static bool shouldEquipItemsAfterAttack = false;
    public static bool shouldHideItemsAfterAttack = false;
    public static List<ItemDrop.ItemData> items = null;
}

/// <summary>
/// Queue weapon/item changes until attack is finished, instead of simply ignoring the change entirely
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.FixedUpdate))]
public static class PlayerFixedUpdatePatch
{
    private static void Postfix(Player __instance)
    {
        if (Queue_Me_MaybePlugin.queueWeaponChanges.Value == Queue_Me_MaybePlugin.Toggle.Off)
            return;

        // I'm not sure if the m_nview checks are necessary, but the original code performs them.
        // Note that we check !InAttack() to ensure we've waited until the attack has
        if (EquipPatchState.shouldEquipItemsAfterAttack && !__instance.InAttack() &&
            (__instance.m_nview == null || (__instance.m_nview != null && __instance.m_nview.IsOwner())))
        {
            foreach (ItemDrop.ItemData item in EquipPatchState.items)
            {
                float oldDuration = item.m_shared.m_equipDuration;
                item.m_shared.m_equipDuration = 0f;
                __instance.ToggleEquipped(item);
                item.m_shared.m_equipDuration = oldDuration;
            }

            EquipPatchState.shouldEquipItemsAfterAttack = false;
            EquipPatchState.items.Clear();
        }

        if (EquipPatchState.shouldHideItemsAfterAttack && !__instance.InAttack())
        {
            __instance.HideHandItems();
            EquipPatchState.shouldHideItemsAfterAttack = false;
        }
    }
}


[HarmonyPatch(typeof(Player), nameof(Player.Update))]
public static class PlayerUpdatePatch
{
    private static void Postfix(ref Player __instance, ref Vector3 ___m_moveDir, ref Vector3 ___m_lookDir,
        ref GameObject ___m_placementGhost, Transform ___m_eye)
    {
        if ((Queue_Me_MaybePlugin.queueWeaponChanges.Value == Queue_Me_MaybePlugin.Toggle.On) &&
            (ZInput.GetButtonDown("Hide") || ZInput.GetButtonDown("JoyHide")))
        {
            if (__instance.InAttack() && (__instance.GetRightItem() != null || __instance.GetLeftItem() != null))
            {
                // The game ignores this keypress, so queue it and take care of it when able (Player_FixedUpdate_Patch).
                EquipPatchState.shouldHideItemsAfterAttack = true;
            }
        }
    }
}

/// <summary>
/// Queue weapon/item changes until attack is finished, instead of simply ignoring the change entirely
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.ToggleEquipped))]
public static class PlayerToggleEquipedPatch
{
    private static void Postfix(Player __instance, bool __result, ItemDrop.ItemData item)
    {
        if (Queue_Me_MaybePlugin.queueWeaponChanges.Value == Queue_Me_MaybePlugin.Toggle.Off)
            return;

        if (!__result || !item.IsEquipable())
        {
            // Item is not equipable (second case should never happen as the original always returns false if not equipable)
            return;
        }

        if (__instance.InAttack())
        {
            // Store the item(s) to equip when the attack is finished
            EquipPatchState.items ??= new List<ItemDrop.ItemData>();

            if (!EquipPatchState.items.Contains(item))
                EquipPatchState.items.Add(item);

            EquipPatchState.shouldEquipItemsAfterAttack = true;
        }
    }
}