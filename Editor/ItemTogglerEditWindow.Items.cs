using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using jp.lilxyzw.lilycalinventory.runtime;
using Migimaru.OnOffMenuItemSetupSupporter;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    public sealed partial class ItemTogglerEditWindow
    {
        private bool ApplyEditChanges()
        {
            if (!ValidateActiveStateSynchronization())
                return false;
            foreach (var group in existingGroups)
            {
                if (group.toggler == null || group.entries.Count == 0)
                    continue;
                if (group.entries.Count > 1 && !group.maintainMultiple && !group.structuralLocked)
                {
                    if (!SplitGroup(group))
                        return false;
                    continue;
                }

                var destination = EffectiveFolder(group.entries[0]);
                if (!group.structuralLocked && destination == null)
                {
                    DeleteItemTogglerSafely(group.toggler);
                    continue;
                }

                SyncExistingTogglerActiveState(group);
                if (!group.structuralLocked && destination != group.logicalMenuFolder && !MoveExistingToggler(group.toggler, destination))
                    return false;
            }

            foreach (var entry in displayEntries)
            {
                if (entry.group == null && EffectiveFolder(entry) != null && !CreateNewSingleToggler(entry))
                    return false;
            }

            return true;
        }

        private bool ValidateActiveStateSynchronization()
        {
            foreach (ExistingGroup group in existingGroups)
            {
                if (group.entries.Count == 0)
                {
                    continue;
                }

                bool split = group.entries.Count > 1 && !group.maintainMultiple && !group.structuralLocked;
                if (split)
                {
                    continue;
                }

                if (!group.structuralLocked && group.entries[0].selectedMenuFolder == null)
                {
                    continue;
                }

                bool requiredDefault;
                if (!TryComputeRequiredDefaultValue(group, out requiredDefault))
                {
                    EditorUtility.DisplayDialog("複数Targetの表示状態", "この複数Target ItemTogglerは、現在の構成を維持したまま" + "各Targetの表示状態と既存のON/OFF動作を同時に維持できません。\n\n" + "「既存の複数Target構成を維持」をOFFにして分割してから適用してください。", "OK");
                    return false;
                }
            }

            return true;
        }

        private static bool TryComputeRequiredDefaultValue(ExistingGroup group, out bool requiredDefault)
        {
            requiredDefault = false;
            bool hasValue = false;
            foreach (EditMeshEntry entry in group.entries)
            {
                if (entry.gameObject == null)
                {
                    continue;
                }

                bool newObjectValue = !entry.gameObject.activeSelf;
                bool thisRequiredDefault = entry.existingMode == ItemTogglerSetupMode.HideWhenOn ? newObjectValue : !newObjectValue;
                if (!hasValue)
                {
                    requiredDefault = thisRequiredDefault;
                    hasValue = true;
                    continue;
                }

                if (requiredDefault != thisRequiredDefault)
                {
                    return false;
                }
            }

            return true;
        }

        private static void SyncExistingTogglerActiveState(ExistingGroup group)
        {
            if (group.toggler == null || group.entries.Count == 0)
            {
                return;
            }

            bool requiredDefault;
            if (!TryComputeRequiredDefaultValue(group, out requiredDefault))
            {
                return;
            }

            Undo.RecordObject(group.toggler, "ItemTogglerの表示状態を更新");
            SerializedObject serializedObject = new SerializedObject(group.toggler);
            serializedObject.Update();
            SerializedProperty defaultValue = serializedObject.FindProperty("defaultValue");
            if (defaultValue != null)
            {
                defaultValue.boolValue = requiredDefault;
            }

            SerializedProperty objects = serializedObject.FindProperty("parameter").FindPropertyRelative("objects");
            foreach (EditMeshEntry entry in group.entries)
            {
                if (entry.objectIndex < 0 || entry.objectIndex >= objects.arraySize || entry.gameObject == null)
                {
                    continue;
                }

                SerializedProperty value = objects.GetArrayElementAtIndex(entry.objectIndex).FindPropertyRelative("value");
                value.boolValue = !entry.gameObject.activeSelf;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private bool SplitGroup(ExistingGroup group)
        {
            if (group.toggler == null)
            {
                return true;
            }

            DeleteItemTogglerSafely(group.toggler);
            foreach (EditMeshEntry entry in group.entries)
            {
                if (entry.gameObject == null || EffectiveFolder(entry) == null)
                {
                    continue;
                }

                if (!CreateSplitSingleToggler(group, entry))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CreateSplitSingleToggler(ExistingGroup group, EditMeshEntry entry)
        {
            bool objectValue = !entry.gameObject.activeSelf;
            bool defaultValue = entry.existingMode == ItemTogglerSetupMode.HideWhenOn ? objectValue : !objectValue;
            bool success = CreateSingleToggler(entry.gameObject, entry.selectedMenuFolder, defaultValue, objectValue, group, out var created);
            if (success) createdItems[entry] = created;
            return success;
        }

        private bool CreateNewSingleToggler(EditMeshEntry entry)
        {
            bool initiallyActive = entry.gameObject.activeSelf;
            bool objectValue = !initiallyActive;
            bool defaultValue;
            if (newItemSetupMode == ItemTogglerSetupMode.HideWhenOn)
            {
                defaultValue = !initiallyActive;
            }
            else
            {
                defaultValue = initiallyActive;
            }

            bool success = CreateSingleToggler(entry.gameObject, entry.selectedMenuFolder, defaultValue, objectValue, null, out var created);
            if (success) createdItems[entry] = created;
            return success;
        }

        private bool CreateSingleToggler(GameObject target, GameObject logicalFolder, bool defaultValue, bool objectValue, ExistingGroup template, out ItemToggler created)
        {
            created = null;
            GameObject page = FindDestinationPage(logicalFolder);
            if (page == null)
            {
                EditorUtility.DisplayDialog("メニューを追加できません", $"{logicalFolder.name} に8項目以内で追加できる空きがありません。", "OK");
                return false;
            }

            GameObject itemObject = new GameObject(target.name);
            Undo.RegisterCreatedObjectUndo(itemObject, "ItemTogglerを作成");
            PlaceUnderPage(itemObject, page);
            ItemToggler toggler = Undo.AddComponent<ItemToggler>(itemObject);
            SerializedObject serializedObject = new SerializedObject(toggler);
            /*
             * menuNameは空のまま。
             * GameObject名をlilycalInventory側に使用させる。
             */
            if (template != null)
            {
                SetBoolProperty(serializedObject, "isSave", template.isSave);
                SetBoolProperty(serializedObject, "isLocalOnly", template.isLocalOnly);
                SetBoolProperty(serializedObject, "autoFixDuplicate", template.autoFixDuplicate);
                SerializedProperty icon = serializedObject.FindProperty("icon");
                if (icon != null)
                {
                    icon.objectReferenceValue = template.icon;
                }
            }

            SerializedProperty defaultProperty = serializedObject.FindProperty("defaultValue");
            defaultProperty.boolValue = defaultValue;
            SerializedProperty objects = serializedObject.FindProperty("parameter").FindPropertyRelative("objects");
            objects.arraySize = 1;
            SerializedProperty element = objects.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("obj").objectReferenceValue = target;
            element.FindPropertyRelative("value").boolValue = objectValue;
            serializedObject.ApplyModifiedProperties();
            created = toggler;
            return true;
        }

        private static void SetBoolProperty(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private bool MoveExistingToggler(ItemToggler toggler, GameObject logicalFolder)
        {
            if (toggler == null || logicalFolder == null)
                return false;
            var page = FindDestinationPage(logicalFolder);
            if (page == null)
                return false;
            AssignTogglerToPage(toggler, page);
            return true;
        }

        private static void PlaceUnderPage(GameObject itemObject, GameObject page)
        {
            Undo.SetTransformParent(itemObject.transform, page.transform, "ItemTogglerを移動");
            itemObject.transform.localPosition = Vector3.zero;
            itemObject.transform.localRotation = Quaternion.identity;
            itemObject.transform.localScale = Vector3.one;
            GameObject nextPage = GetDirectAutoNextPage(page);
            if (nextPage != null)
            {
                itemObject.transform.SetSiblingIndex(nextPage.transform.GetSiblingIndex());
            }
            else
            {
                itemObject.transform.SetAsLastSibling();
            }
        }

        private static void DeleteItemTogglerSafely(ItemToggler toggler)
        {
            if (toggler == null)
            {
                return;
            }

            if (CanSafelyMoveWholeToggler(toggler))
            {
                Undo.DestroyObjectImmediate(toggler.gameObject);
            }
            else
            {
                Undo.DestroyObjectImmediate(toggler);
            }
        }

        private GameObject FindDestinationPage(GameObject logicalFolder)
        {
            if (logicalFolder == null || logicalFolder.GetComponent<MenuFolder>() == null)
            {
                return null;
            }

            if (!autoAdjustToEightItems)
                return logicalFolder;
            GameObject page = logicalFolder;
            while (page != null)
            {
                GameObject next = GetDirectAutoNextPage(page);
                int childFolders = CountDirectManualChildMenuFolders(page);
                int itemCount = CountDirectItemTogglers(page);
                if (next != null)
                {
                    int available = MenuPageCalculator.GetAvailableItemSlots(childFolders, itemCount, true);
                    if (available > 0)
                    {
                        return page;
                    }

                    page = next;
                    continue;
                }

                int availableWithoutNext = MenuPageCalculator.GetAvailableItemSlots(childFolders, itemCount, false);
                if (availableWithoutNext > 0)
                {
                    return page;
                }

                if (MenuPageCalculator.CanCreateNextPage(childFolders, itemCount))
                {
                    return CreateAutoNextPage(page);
                }

                ItemToggler lastItem = GetLastDirectItemToggler(page);
                if (lastItem == null)
                {
                    return null;
                }

                GameObject overflowPage = CreateAutoNextPage(page);
                AssignTogglerToPage(lastItem, overflowPage);
                return overflowPage;
            }

            return null;
        }

        private GameObject CreateAutoNextPage(GameObject parent)
        {
            string name = FindAvailableNextPageName(parent);
            GameObject next = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(next, "NextPageを作成");
            Undo.SetTransformParent(next.transform, parent.transform, "NextPageを配置");
            next.transform.localPosition = Vector3.zero;
            next.transform.localRotation = Quaternion.identity;
            next.transform.localScale = Vector3.one;
            MenuFolder folder = Undo.AddComponent<MenuFolder>(next);
            SerializedObject serializedObject = new SerializedObject(folder);
            SerializedProperty menuName = serializedObject.FindProperty("menuName");
            if (menuName != null)
            {
                menuName.stringValue = name;
                serializedObject.ApplyModifiedProperties();
            }

            Undo.AddComponent<AutoGeneratedNextPageMarker>(next);
            next.transform.SetAsLastSibling();
            return next;
        }

        private static string FindAvailableNextPageName(GameObject parent)
        {
            int number = 1;
            while (true)
            {
                string candidate = number == 1 ? "NextPage" : $"NextPage{number}";
                bool exists = false;
                foreach (Transform child in parent.transform)
                {
                    if (child.name == candidate)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    return candidate;
                }

                number++;
            }
        }

        private static GameObject GetDirectAutoNextPage(GameObject parent)
        {
            foreach (Transform child in parent.transform)
            {
                if (child.GetComponent<MenuFolder>() != null && child.GetComponent<AutoGeneratedNextPageMarker>() != null)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private int CountDirectItemTogglers(GameObject parent)
        {
            return GetAvatarRootObject().GetComponentsInChildren<ItemToggler>(true).Count(toggler =>
            {
                var parentOverride = new SerializedObject(toggler).FindProperty("parentOverride");
                var folder = parentOverride == null ? null : parentOverride.objectReferenceValue as MenuFolder;
                return folder != null ? folder.gameObject == parent : toggler.transform.parent == parent.transform;
            });
        }

        private static int CountDirectManualChildMenuFolders(GameObject parent)
        {
            int result = 0;
            foreach (Transform child in parent.transform)
            {
                if (child.GetComponent<MenuFolder>() == null)
                {
                    continue;
                }

                if (child.GetComponent<AutoGeneratedNextPageMarker>() != null)
                {
                    continue;
                }

                result++;
            }

            return result;
        }

        private static ItemToggler GetLastDirectItemToggler(GameObject parent)
        {
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                ItemToggler toggler = parent.transform.GetChild(i).GetComponent<ItemToggler>();
                if (toggler != null)
                {
                    var parentOverride = new SerializedObject(toggler).FindProperty("parentOverride").objectReferenceValue as MenuFolder;
                    if (parentOverride != null && parentOverride.gameObject != parent)
                        continue;
                    return toggler;
                }
            }

            return null;
        }
    }
}
