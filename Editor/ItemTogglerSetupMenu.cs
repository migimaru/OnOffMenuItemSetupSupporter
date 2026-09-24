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

        private const string EditHideWhenOnMenuPath =
            "GameObject/On／Off Menu Item Setup Supporter/[編集] ONで非表示";

        private const string EditHideWhenOffMenuPath =
            "GameObject/On／Off Menu Item Setup Supporter/[編集] OFFで非表示";


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


        [MenuItem(EditHideWhenOnMenuPath, false, 20)]
        private static void OpenEditHideWhenOn()
        {
            ItemTogglerEditWindow.Open(
                Selection.activeGameObject,
                ItemTogglerSetupMode.HideWhenOn
            );
        }


        [MenuItem(EditHideWhenOnMenuPath, true)]
        private static bool ValidateEditHideWhenOn()
        {
            return HasItemTogglerInSelfOrDescendants(
                Selection.activeGameObject
            );
        }


        [MenuItem(EditHideWhenOffMenuPath, false, 21)]
        private static void OpenEditHideWhenOff()
        {
            ItemTogglerEditWindow.Open(
                Selection.activeGameObject,
                ItemTogglerSetupMode.HideWhenOff
            );
        }


        [MenuItem(EditHideWhenOffMenuPath, true)]
        private static bool ValidateEditHideWhenOff()
        {
            return HasItemTogglerInSelfOrDescendants(
                Selection.activeGameObject
            );
        }


        internal static bool HasItemTogglerInSelfOrDescendants(
            GameObject selectedObject
        )
        {
            if (selectedObject == null)
            {
                return false;
            }

            if (
                selectedObject.GetComponent<MenuFolder>()
                == null
            )
            {
                return false;
            }

            return selectedObject
                .GetComponentsInChildren<ItemToggler>(true)
                .Length > 0;
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
