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
        private void OnGUI()
        {
            if (sourceMenuFolder == null)
            {
                EditorGUILayout.HelpBox("対象MenuFolderが存在しません。", MessageType.Error);
                return;
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.LabelField("設定先MenuFolder", sourceMenuFolder.name);
                EditorGUILayout.LabelField("動作", newItemSetupMode == ItemTogglerSetupMode.HideWhenOn ? "ONで非表示" : "OFFで非表示");
                DrawWorkspaceOptions();
                DrawAddArea();
                foreach (string message in statusMessages)
                    EditorGUILayout.HelpBox(message, MessageType.Warning);
                DrawMenuTable();
                EditorGUILayout.Space(6);
                DrawBottomButtons();
            }
        }

        private void DrawScopeOption()
        {
            bool requested = EditorGUILayout.ToggleLeft("子MenuFolderも編集対象に含める", includeChildMenuFolders);
            if (requested == includeChildMenuFolders)
            {
                return;
            }

            HandleScopeChange(requested);
        }

        private void HandleScopeChange(bool requested)
        {
            // Preserve drafts for entries that remain in scope; never edit a hidden child menu.
            var draft = CaptureViewState();
            RestoreImmediateChangesToSnapshot();
            includeChildMenuFolders = requested;
            RebuildFromHierarchy();
            draft.includeChildMenuFolders = requested;
            ApplyViewState(draft);
        }

        private void DrawMultiGroupHeader(ExistingGroup group, Rect rect)
        {
            EditorGUI.BeginDisabledGroup(group.structuralLocked);
            bool newMaintain = EditorGUI.ToggleLeft(rect, "既存の複数Target構成を維持", group.maintainMultiple);
            EditorGUI.EndDisabledGroup();
            if (!group.structuralLocked && newMaintain != group.maintainMultiple)
            {
                group.maintainMultiple = newMaintain;
                if (group.maintainMultiple && group.entries.Count > 0)
                {
                    GameObject selected = group.entries[0].selectedMenuFolder;
                    foreach (EditMeshEntry entry in group.entries)
                    {
                        entry.selectedMenuFolder = selected;
                    }
                }
            }

        }

        private void DrawEntry(EditMeshEntry entry, Rect[] cells)
        {
            GameObject meshObject = entry.gameObject;
            if (meshObject == null)
            {
                return;
            }

            DrawActive(entry, CenteredToggle(cells[1]));
            DrawEditorOnly(entry, CenteredToggle(cells[2]));
            var linkStyle = new GUIStyle(EditorStyles.linkLabel) { clipping = TextClipping.Clip, wordWrap = false, alignment = TextAnchor.MiddleLeft };
            if (GUI.Button(CellContent(cells[3]), new GUIContent(meshObject.name, meshObject.name), linkStyle))
            {
                Selection.activeGameObject = meshObject;
                EditorGUIUtility.PingObject(meshObject);
                SceneView.RepaintAll();
            }

            bool canEditStructure = CanEditStructure(entry);
            bool excluded = skipEditorOnlyMenuCreation && (entry.gameObject.CompareTag("EditorOnly") || HasEditorOnlyAncestor(entry.gameObject));
            for (int folderIndex = 0; folderIndex < menuFolders.Count; folderIndex++)
            {
                MenuFolderEntry folder = menuFolders[folderIndex];
                Rect cell = cells[folderIndex + 4];
                bool folderSelectable = ItemTogglerSetupWindow.IsSelectableMenuFolder(folder.gameObject);
                if (!folderSelectable || !IsLogicalFolderInScope(folder.gameObject) || excluded)
                {
                    GUI.Label(CellContent(cell), "-");
                    continue;
                }

                bool selected = entry.selectedMenuFolder == folder.gameObject;
                EditorGUI.BeginDisabledGroup(!canEditStructure);
                bool radio = GUI.Toggle(CenteredToggle(cell), selected, "", EditorStyles.radioButton);
                EditorGUI.EndDisabledGroup();
                if (canEditStructure && radio && !selected)
                {
                    SetSelectedFolder(entry, folder.gameObject);
                }
            }

            bool doNotRegister = excluded || entry.selectedMenuFolder == null;
            EditorGUI.BeginDisabledGroup(!canEditStructure || excluded);
            bool doNotRegisterRadio = GUI.Toggle(CenteredToggle(cells[cells.Length - 1]), doNotRegister, "", EditorStyles.radioButton);
            EditorGUI.EndDisabledGroup();
            if (canEditStructure && doNotRegisterRadio && !doNotRegister)
            {
                SetSelectedFolder(entry, null);
            }

        }

        private void DrawActive(EditMeshEntry entry, Rect rect)
        {
            GameObject meshObject = entry.gameObject;
            bool newActive = EditorGUI.Toggle(rect, meshObject.activeSelf);
            if (newActive == meshObject.activeSelf)
            {
                return;
            }

            Undo.RecordObject(meshObject, "表示状態を変更");
            meshObject.SetActive(newActive);
            PrefabUtility.RecordPrefabInstancePropertyModifications(meshObject);
            EditorUtility.SetDirty(meshObject);
        }

        private void DrawEditorOnly(EditMeshEntry entry, Rect rect)
        {
            GameObject meshObject = entry.gameObject;
            bool inheritedEditorOnly = HasEditorOnlyAncestor(meshObject);
            bool ownEditorOnly = meshObject.CompareTag("EditorOnly");
            EditorGUI.BeginDisabledGroup(inheritedEditorOnly);
            bool newEditorOnly = EditorGUI.Toggle(rect, inheritedEditorOnly || ownEditorOnly);
            EditorGUI.EndDisabledGroup();
            if (inheritedEditorOnly || newEditorOnly == ownEditorOnly)
            {
                return;
            }

            Undo.RecordObject(meshObject, "EditorOnlyを変更");
            if (newEditorOnly)
            {
                if (!meshObject.CompareTag("EditorOnly"))
                {
                    entry.lastNonEditorOnlyTag = meshObject.tag;
                }

                meshObject.tag = "EditorOnly";
            }
            else
            {
                meshObject.tag = string.IsNullOrEmpty(entry.lastNonEditorOnlyTag) ? "Untagged" : entry.lastNonEditorOnlyTag;
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(meshObject);
            EditorUtility.SetDirty(meshObject);
        }

        private bool CanEditStructure(EditMeshEntry entry)
        {
            if (entry.group == null)
            {
                return true;
            }

            return !entry.group.structuralLocked;
        }

        private void SetSelectedFolder(EditMeshEntry entry, GameObject folder)
        {
            ExistingGroup group = entry.group;
            if (group != null && group.entries.Count > 1 && group.maintainMultiple)
            {
                foreach (EditMeshEntry groupEntry in group.entries)
                {
                    groupEntry.selectedMenuFolder = folder;
                }

                return;
            }

            entry.selectedMenuFolder = folder;
        }

        private void DrawBottomButtons()
        {
            if (position.width < 560f)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawHistoryButton();
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    DrawWorkspaceActions();
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawHistoryButton();
                    GUILayout.FlexibleSpace();
                    DrawWorkspaceActions();
                }
            }
        }
    }
}
