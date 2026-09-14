using UnityEditor;
using UnityEngine;
using jp.lilxyzw.lilycalinventory.runtime;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    public static class ItemTogglerSetupMenu
    {
        private const string HideWhenOnMenuPath =
            "GameObject/On／Off Menu Item Setup Supporter/ONで非表示";

        private const string HideWhenOffMenuPath =
            "GameObject/On／Off Menu Item Setup Supporter/OFFで非表示";


        [MenuItem(HideWhenOnMenuPath, false, 10)]
        private static void OpenHideWhenOn()
        {
            ItemTogglerSetupWindow.Open(
                Selection.activeGameObject,
                ItemTogglerSetupMode.HideWhenOn
            );
        }


        [MenuItem(HideWhenOnMenuPath, true)]
        private static bool ValidateHideWhenOn()
        {
            return IsMenuFolderSelected();
        }


        [MenuItem(HideWhenOffMenuPath, false, 11)]
        private static void OpenHideWhenOff()
        {
            ItemTogglerSetupWindow.Open(
                Selection.activeGameObject,
                ItemTogglerSetupMode.HideWhenOff
            );
        }


        [MenuItem(HideWhenOffMenuPath, true)]
        private static bool ValidateHideWhenOff()
        {
            return IsMenuFolderSelected();
        }


        private static bool IsMenuFolderSelected()
        {
            GameObject selectedObject =
                Selection.activeGameObject;


            if (selectedObject == null)
            {
                return false;
            }


            return selectedObject.GetComponent<MenuFolder>()
                != null;
        }
    }
}