using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using jp.lilxyzw.lilycalinventory.runtime;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    public static class ItemOffMenuCreator
    {
        private const string MenuPath =
            "GameObject/OnOff Menu Item Setup Supporter/Create ItemOffMenu";

        [MenuItem(MenuPath, false, 0)]
        private static void CreateItemOffMenu()
        {
            GameObject avatarRoot = Selection.activeGameObject;

            int number = FindAvailableNumber(avatarRoot);

            string itemOffMenuName =
                number == 1 ? "ItemOffMenu" : $"ItemOffMenu{number}";

            string accessoryName =
                number == 1 ? "Accessory" : $"Accessory{number}";

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create ItemOffMenu");

            GameObject itemOffMenu = new GameObject(itemOffMenuName);
            itemOffMenu.transform.SetParent(avatarRoot.transform, false);
            itemOffMenu.AddComponent<MenuFolder>();

            Undo.RegisterCreatedObjectUndo(
                itemOffMenu,
                "Create ItemOffMenu"
            );

            GameObject accessory = new GameObject(accessoryName);
            accessory.transform.SetParent(itemOffMenu.transform, false);
            accessory.AddComponent<MenuFolder>();

            Undo.RegisterCreatedObjectUndo(
                accessory,
                "Create Accessory"
            );

            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject = itemOffMenu;
            EditorGUIUtility.PingObject(itemOffMenu);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateCreateItemOffMenu()
        {
            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject == null)
            {
                return false;
            }

            return selectedObject.GetComponent<VRCAvatarDescriptor>() != null;
        }

        private static int FindAvailableNumber(GameObject avatarRoot)
        {
            int number = 1;

            while (true)
            {
                string targetName =
                    number == 1 ? "ItemOffMenu" : $"ItemOffMenu{number}";

                bool exists = false;

                foreach (Transform child in avatarRoot.transform)
                {
                    if (child.name == targetName)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    return number;
                }

                number++;
            }
        }
    }
}