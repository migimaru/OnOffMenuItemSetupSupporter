using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using jp.lilxyzw.lilycalinventory.runtime;
using Migimaru.OnOffMenuItemSetupSupporter;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    public sealed class ItemTogglerEditWindow : EditorWindow
    {
        private sealed class MenuFolderEntry
        {
            public GameObject gameObject;
            public string displayName;
        }


        private sealed class ExistingGroup
        {
            public ItemToggler toggler;

            public readonly List<EditMeshEntry> entries =
                new List<EditMeshEntry>();

            public GameObject logicalMenuFolder;

            public bool maintainMultiple =
                true;

            public bool structuralLocked;
            public string lockReason;

            public bool isSave;
            public bool isLocalOnly;
            public bool autoFixDuplicate;
            public Texture2D icon;

            public bool originalDefaultValue;

            public Transform originalParent;
            public int originalSiblingIndex;
        }


        private sealed class EditMeshEntry
        {
            public GameObject gameObject;

            public ExistingGroup group;

            public int objectIndex =
                -1;

            public bool originalObjectValue;

            public ItemTogglerSetupMode existingMode;

            public GameObject selectedMenuFolder;
            public GameObject snapshotMenuFolder;

            public bool snapshotActive;
            public string snapshotTag;

            public string lastNonEditorOnlyTag;

            public bool duplicateRegistration;

            public bool IsExisting
            {
                get
                {
                    return group != null;
                }
            }
        }


        private sealed class ReopenState
        {
            public GameObject sourceMenuFolder;
            public ItemTogglerSetupMode setupMode;
            public bool includeChildMenuFolders;
            public Vector2 scrollPosition;

            public readonly Dictionary<string, int>
                selectedFolders =
                    new Dictionary<string, int>();

            public readonly Dictionary<string, int>
                snapshotFolders =
                    new Dictionary<string, int>();

            public readonly Dictionary<int, bool>
                groupMaintain =
                    new Dictionary<int, bool>();

            public readonly Dictionary<int, bool>
                snapshotActive =
                    new Dictionary<int, bool>();

            public readonly Dictionary<int, string>
                snapshotTag =
                    new Dictionary<int, string>();
        }


        private GameObject sourceMenuFolder;

        private ItemTogglerSetupMode
            newItemSetupMode;

        private bool includeChildMenuFolders;

        private readonly List<MenuFolderEntry>
            menuFolders =
                new List<MenuFolderEntry>();

        private readonly List<ExistingGroup>
            existingGroups =
                new List<ExistingGroup>();

        private readonly List<EditMeshEntry>
            displayEntries =
                new List<EditMeshEntry>();

        private readonly List<string>
            statusMessages =
                new List<string>();

        private Dictionary
            <GameObject, List<ItemToggler>>
            allTargetRegistrations =
                new Dictionary
                <GameObject, List<ItemToggler>>();

        private Vector2 scrollPosition;

        private bool initialized;
        private bool suppressDestroyPrompt;


        private const float DisplayColumnWidth =
            45f;

        private const float EditorOnlyColumnWidth =
            75f;

        private const float MeshNameColumnWidth =
            180f;

        private const float ModeColumnWidth =
            90f;

        private const float MenuFolderColumnWidth =
            110f;

        private const float DoNotRegisterColumnWidth =
            80f;


        public static void Open(
            GameObject sourceMenuFolder,
            ItemTogglerSetupMode newItemSetupMode
        )
        {
            if (sourceMenuFolder == null)
            {
                return;
            }

            ItemTogglerEditWindow window =
                CreateInstance<ItemTogglerEditWindow>();

            window.titleContent =
                new GUIContent(
                    "ON/OFFメニュー編集"
                );

            window.sourceMenuFolder =
                sourceMenuFolder;

            window.newItemSetupMode =
                newItemSetupMode;

            window.includeChildMenuFolders =
                false;

            window.minSize =
                new Vector2(
                    900,
                    450
                );

            window.RebuildFromHierarchy();

            window.ShowUtility();
        }


        private static void Reopen(
            ReopenState state
        )
        {
            if (
                state == null
                ||
                state.sourceMenuFolder == null
            )
            {
                return;
            }

            ItemTogglerEditWindow window =
                CreateInstance<ItemTogglerEditWindow>();

            window.titleContent =
                new GUIContent(
                    "ON/OFFメニュー編集"
                );

            window.sourceMenuFolder =
                state.sourceMenuFolder;

            window.newItemSetupMode =
                state.setupMode;

            window.includeChildMenuFolders =
                state.includeChildMenuFolders;

            window.minSize =
                new Vector2(
                    900,
                    450
                );

            window.RebuildFromHierarchy();

            window.ApplyReopenState(
                state
            );

            window.ShowUtility();
        }


        private void OnGUI()
        {
            if (
                sourceMenuFolder == null
            )
            {
                EditorGUILayout.HelpBox(
                    "対象MenuFolderが存在しません。",
                    MessageType.Error
                );

                return;
            }

            EditorGUILayout.LabelField(
                "ON/OFFメニュー編集",
                EditorStyles.boldLabel
            );

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(
                "基準MenuFolder",
                sourceMenuFolder.name
            );

            EditorGUILayout.LabelField(
                "新規追加時の動作",
                newItemSetupMode
                    == ItemTogglerSetupMode
                        .HideWhenOn
                    ? "ONで非表示"
                    : "OFFで非表示"
            );

            EditorGUILayout.Space(6);

            DrawScopeOption();

            EditorGUILayout.Space(6);

            foreach (
                string message
                in statusMessages
            )
            {
                EditorGUILayout.HelpBox(
                    message,
                    MessageType.Warning
                );
            }

            DrawHeader();

            scrollPosition =
                EditorGUILayout.BeginScrollView(
                    scrollPosition
                );

            DrawEntries();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);

            DrawBottomButtons();
        }


        // =========================================================
        // Scope
        // =========================================================

        private void DrawScopeOption()
        {
            bool requested =
                EditorGUILayout.ToggleLeft(
                    "子MenuFolderも編集対象に含める",
                    includeChildMenuFolders
                );

            if (
                requested
                    == includeChildMenuFolders
            )
            {
                return;
            }

            HandleScopeChange(
                requested
            );
        }


        private void HandleScopeChange(
            bool requested
        )
        {
            if (!HasAnyChanges())
            {
                includeChildMenuFolders =
                    requested;

                RebuildFromHierarchy();

                return;
            }

            int choice =
                ShowSaveDiscardCancelDialog(
                    "編集対象を変更する前に、現在の変更をどうするか選択してください。"
                );

            switch (choice)
            {
                // 保存
                case 0:
                {
                    if (
                        !ApplyChanges(
                            false
                        )
                    )
                    {
                        return;
                    }

                    includeChildMenuFolders =
                        requested;

                    RebuildFromHierarchy();

                    break;
                }

                // 保存しない
                case 1:
                {
                    RestoreImmediateChangesToSnapshot();

                    includeChildMenuFolders =
                        requested;

                    RebuildFromHierarchy();

                    break;
                }

                // キャンセル
                default:
                {
                    break;
                }
            }
        }


        // =========================================================
        // Drawing
        // =========================================================

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(
                "表示",
                EditorStyles.boldLabel,
                GUILayout.Width(
                    DisplayColumnWidth
                )
            );

            EditorGUILayout.LabelField(
                "EditorOnly",
                EditorStyles.boldLabel,
                GUILayout.Width(
                    EditorOnlyColumnWidth
                )
            );

            EditorGUILayout.LabelField(
                "Mesh",
                EditorStyles.boldLabel,
                GUILayout.Width(
                    MeshNameColumnWidth
                )
            );

            EditorGUILayout.LabelField(
                "動作",
                EditorStyles.boldLabel,
                GUILayout.Width(
                    ModeColumnWidth
                )
            );

            foreach (
                MenuFolderEntry folder
                in menuFolders
            )
            {
                EditorGUILayout.LabelField(
                    folder.displayName,
                    EditorStyles.boldLabel,
                    GUILayout.Width(
                        MenuFolderColumnWidth
                    )
                );
            }

            EditorGUILayout.LabelField(
                "登録しない",
                EditorStyles.boldLabel,
                GUILayout.Width(
                    DoNotRegisterColumnWidth
                )
            );

            EditorGUILayout.EndHorizontal();
        }


        private void DrawEntries()
        {
            HashSet<ExistingGroup> drawnGroups =
                new HashSet<ExistingGroup>();

            for (
                int i = 0;
                i < displayEntries.Count;
                i++
            )
            {
                EditMeshEntry entry =
                    displayEntries[i];

                ExistingGroup group =
                    entry.group;

                if (
                    group != null
                    &&
                    group.entries.Count > 1
                    &&
                    !drawnGroups.Contains(
                        group
                    )
                )
                {
                    DrawMultiGroupHeader(
                        group
                    );

                    drawnGroups.Add(
                        group
                    );
                }

                DrawEntry(
                    entry
                );

                if (
                    group != null
                    &&
                    group.entries.Count > 1
                )
                {
                    bool isLastInGroup =
                        i + 1
                            >= displayEntries.Count
                        ||
                        displayEntries[i + 1]
                            .group != group;

                    if (isLastInGroup)
                    {
                        EditorGUILayout.Space(
                            8
                        );
                    }
                }
            }
        }


        private void DrawMultiGroupHeader(
            ExistingGroup group
        )
        {
            EditorGUI.BeginDisabledGroup(
                group.structuralLocked
            );

            bool newMaintain =
                EditorGUILayout.ToggleLeft(
                    "既存の複数Target構成を維持",
                    group.maintainMultiple
                );

            EditorGUI.EndDisabledGroup();

            if (
                !group.structuralLocked
                &&
                newMaintain
                    != group.maintainMultiple
            )
            {
                group.maintainMultiple =
                    newMaintain;

                if (
                    group.maintainMultiple
                    &&
                    group.entries.Count > 0
                )
                {
                    GameObject selected =
                        group.entries[0]
                            .selectedMenuFolder;

                    foreach (
                        EditMeshEntry entry
                        in group.entries
                    )
                    {
                        entry.selectedMenuFolder =
                            selected;
                    }
                }
            }

            if (
                group.structuralLocked
                &&
                !string.IsNullOrEmpty(
                    group.lockReason
                )
            )
            {
                EditorGUILayout.HelpBox(
                    group.lockReason,
                    MessageType.Warning
                );
            }
        }


        private void DrawEntry(
            EditMeshEntry entry
        )
        {
            GameObject meshObject =
                entry.gameObject;

            if (meshObject == null)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();

            DrawActive(
                entry
            );

            DrawEditorOnly(
                entry
            );

            if (
                GUILayout.Button(
                    meshObject.name,
                    EditorStyles.linkLabel,
                    GUILayout.Width(
                        MeshNameColumnWidth
                    )
                )
            )
            {
                Selection.activeGameObject =
                    meshObject;

                EditorGUIUtility.PingObject(
                    meshObject
                );

                SceneView.RepaintAll();
            }

            EditorGUILayout.LabelField(
                GetModeDisplayName(
                    entry
                ),
                GUILayout.Width(
                    ModeColumnWidth
                )
            );

            bool canEditStructure =
                CanEditStructure(
                    entry
                );

            foreach (
                MenuFolderEntry folder
                in menuFolders
            )
            {
                bool folderSelectable =
                    ItemTogglerSetupWindow
                        .IsSelectableMenuFolder(
                            folder.gameObject
                        );

                if (!folderSelectable)
                {
                    EditorGUILayout.LabelField(
                        "-",
                        GUILayout.Width(
                            MenuFolderColumnWidth
                        )
                    );

                    continue;
                }

                bool selected =
                    entry.selectedMenuFolder
                        == folder.gameObject;

                EditorGUI.BeginDisabledGroup(
                    !canEditStructure
                );

                bool radio =
                    GUILayout.Toggle(
                        selected,
                        "",
                        EditorStyles.radioButton,
                        GUILayout.Width(
                            MenuFolderColumnWidth
                        )
                    );

                EditorGUI.EndDisabledGroup();

                if (
                    canEditStructure
                    &&
                    radio
                    &&
                    !selected
                )
                {
                    SetSelectedFolder(
                        entry,
                        folder.gameObject
                    );
                }
            }

            bool doNotRegister =
                entry.selectedMenuFolder
                    == null;

            EditorGUI.BeginDisabledGroup(
                !canEditStructure
            );

            bool doNotRegisterRadio =
                GUILayout.Toggle(
                    doNotRegister,
                    "",
                    EditorStyles.radioButton,
                    GUILayout.Width(
                        DoNotRegisterColumnWidth
                    )
                );

            EditorGUI.EndDisabledGroup();

            if (
                canEditStructure
                &&
                doNotRegisterRadio
                &&
                !doNotRegister
            )
            {
                SetSelectedFolder(
                    entry,
                    null
                );
            }

            EditorGUILayout.EndHorizontal();

            if (
                entry.duplicateRegistration
            )
            {
                EditorGUILayout.HelpBox(
                    $"{meshObject.name}: 同じTargetが複数のItemTogglerに登録されています。"
                    + " MenuFolder変更・登録解除・分割は無効です。",
                    MessageType.Warning
                );
            }
            else if (
                entry.group != null
                &&
                entry.group.structuralLocked
                &&
                entry.group.entries.Count == 1
                &&
                !string.IsNullOrEmpty(
                    entry.group.lockReason
                )
            )
            {
                EditorGUILayout.HelpBox(
                    entry.group.lockReason,
                    MessageType.Warning
                );
            }
        }


        private void DrawActive(
            EditMeshEntry entry
        )
        {
            GameObject meshObject =
                entry.gameObject;

            bool newActive =
                EditorGUILayout.Toggle(
                    meshObject.activeSelf,
                    GUILayout.Width(
                        DisplayColumnWidth
                    )
                );

            if (
                newActive
                    == meshObject.activeSelf
            )
            {
                return;
            }

            Undo.RecordObject(
                meshObject,
                "表示状態を変更"
            );

            meshObject.SetActive(
                newActive
            );

            PrefabUtility
                .RecordPrefabInstancePropertyModifications(
                    meshObject
                );

            EditorUtility.SetDirty(
                meshObject
            );
        }


        private void DrawEditorOnly(
            EditMeshEntry entry
        )
        {
            GameObject meshObject =
                entry.gameObject;

            bool inheritedEditorOnly =
                HasEditorOnlyAncestor(
                    meshObject
                );

            bool ownEditorOnly =
                meshObject.CompareTag(
                    "EditorOnly"
                );

            EditorGUI.BeginDisabledGroup(
                inheritedEditorOnly
            );

            bool newEditorOnly =
                EditorGUILayout.Toggle(
                    inheritedEditorOnly
                    || ownEditorOnly,
                    GUILayout.Width(
                        EditorOnlyColumnWidth
                    )
                );

            EditorGUI.EndDisabledGroup();

            if (
                inheritedEditorOnly
                ||
                newEditorOnly
                    == ownEditorOnly
            )
            {
                return;
            }

            Undo.RecordObject(
                meshObject,
                "EditorOnlyを変更"
            );

            if (newEditorOnly)
            {
                if (
                    !meshObject.CompareTag(
                        "EditorOnly"
                    )
                )
                {
                    entry.lastNonEditorOnlyTag =
                        meshObject.tag;
                }

                meshObject.tag =
                    "EditorOnly";
            }
            else
            {
                meshObject.tag =
                    string.IsNullOrEmpty(
                        entry.lastNonEditorOnlyTag
                    )
                    ? "Untagged"
                    : entry.lastNonEditorOnlyTag;
            }

            PrefabUtility
                .RecordPrefabInstancePropertyModifications(
                    meshObject
                );

            EditorUtility.SetDirty(
                meshObject
            );
        }


        private string GetModeDisplayName(
            EditMeshEntry entry
        )
        {
            ItemTogglerSetupMode mode =
                entry.IsExisting
                    ? entry.existingMode
                    : newItemSetupMode;

            return mode
                == ItemTogglerSetupMode.HideWhenOn
                ? "ONで非表示"
                : "OFFで非表示";
        }


        private bool CanEditStructure(
            EditMeshEntry entry
        )
        {
            if (entry.group == null)
            {
                return true;
            }

            return !entry.group.structuralLocked;
        }


        private void SetSelectedFolder(
            EditMeshEntry entry,
            GameObject folder
        )
        {
            ExistingGroup group =
                entry.group;

            if (
                group != null
                &&
                group.entries.Count > 1
                &&
                group.maintainMultiple
            )
            {
                foreach (
                    EditMeshEntry groupEntry
                    in group.entries
                )
                {
                    groupEntry
                        .selectedMenuFolder =
                            folder;
                }

                return;
            }

            entry.selectedMenuFolder =
                folder;
        }


        // =========================================================
        // Bottom
        // =========================================================

        private void DrawBottomButtons()
        {
            int dirtyCount =
                CountDirtyChanges();

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(
                dirtyCount == 0
            );

            if (
                GUILayout.Button(
                    "変更をリセット"
                )
            )
            {
                ResetChanges();

                GUIUtility.ExitGUI();
            }

            if (
                GUILayout.Button(
                    dirtyCount == 0
                        ? "変更を適用"
                        : $"変更を適用 ({dirtyCount})"
                )
            )
            {
                ApplyChanges(
                    true
                );

                GUIUtility.ExitGUI();
            }

            EditorGUI.EndDisabledGroup();

            if (
                GUILayout.Button(
                    "キャンセル"
                )
            )
            {
                RequestClose();

                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }


        private void ResetChanges()
        {
            RestoreImmediateChangesToSnapshot();

            foreach (
                ExistingGroup group
                in existingGroups
            )
            {
                group.maintainMultiple =
                    true;
            }

            foreach (
                EditMeshEntry entry
                in displayEntries
            )
            {
                entry.selectedMenuFolder =
                    entry.snapshotMenuFolder;
            }

            Repaint();
        }


        // =========================================================
        // Snapshot / dirty
        // =========================================================

        private bool HasAnyChanges()
        {
            return CountDirtyChanges()
                > 0;
        }


        private int CountDirtyChanges()
        {
            int count =
                0;

            HashSet<GameObject> checkedObjects =
                new HashSet<GameObject>();

            foreach (
                ExistingGroup group
                in existingGroups
            )
            {
                if (
                    group.entries.Count > 1
                    &&
                    !group.maintainMultiple
                )
                {
                    count++;
                }
            }

            foreach (
                EditMeshEntry entry
                in displayEntries
            )
            {
                if (
                    entry.selectedMenuFolder
                        != entry.snapshotMenuFolder
                )
                {
                    count++;
                }

                if (
                    entry.gameObject == null
                    ||
                    !checkedObjects.Add(
                        entry.gameObject
                    )
                )
                {
                    continue;
                }

                if (
                    entry.gameObject.activeSelf
                        != entry.snapshotActive
                )
                {
                    count++;
                }

                if (
                    entry.gameObject.tag
                        != entry.snapshotTag
                )
                {
                    count++;
                }
            }

            return count;
        }


        private void RestoreImmediateChangesToSnapshot()
        {
            HashSet<GameObject> restored =
                new HashSet<GameObject>();

            foreach (
                EditMeshEntry entry
                in displayEntries
            )
            {
                GameObject target =
                    entry.gameObject;

                if (
                    target == null
                    ||
                    !restored.Add(
                        target
                    )
                )
                {
                    continue;
                }

                Undo.RecordObject(
                    target,
                    "編集内容を破棄"
                );

                target.SetActive(
                    entry.snapshotActive
                );

                if (
                    target.tag
                        != entry.snapshotTag
                )
                {
                    target.tag =
                        entry.snapshotTag;
                }

                PrefabUtility
                    .RecordPrefabInstancePropertyModifications(
                        target
                    );

                EditorUtility.SetDirty(
                    target
                );
            }
        }


        // =========================================================
        // Apply
        // =========================================================

        private bool ApplyChanges(
            bool rebuildAfterApply
        )
        {
            if (
                !ValidateActiveStateSynchronization()
            )
            {
                return false;
            }

            Undo.IncrementCurrentGroup();

            int undoGroup =
                Undo.GetCurrentGroup();

            Undo.SetCurrentGroupName(
                "ItemToggler編集を適用"
            );

            bool success =
                true;

            foreach (
                ExistingGroup group
                in existingGroups
            )
            {
                if (
                    group.toggler == null
                    ||
                    group.entries.Count == 0
                )
                {
                    continue;
                }

                bool shouldSplit =
                    group.entries.Count > 1
                    &&
                    !group.maintainMultiple
                    &&
                    !group.structuralLocked;

                if (shouldSplit)
                {
                    if (
                        !SplitGroup(
                            group
                        )
                    )
                    {
                        success =
                            false;

                        break;
                    }

                    continue;
                }

                GameObject desiredFolder =
                    group.entries[0]
                        .selectedMenuFolder;

                if (
                    !group.structuralLocked
                    &&
                    desiredFolder == null
                )
                {
                    DeleteItemTogglerSafely(
                        group.toggler
                    );

                    continue;
                }

                SyncExistingTogglerActiveState(
                    group
                );

                if (
                    !group.structuralLocked
                    &&
                    desiredFolder
                        != group.logicalMenuFolder
                )
                {
                    if (
                        !MoveExistingToggler(
                            group.toggler,
                            desiredFolder
                        )
                    )
                    {
                        success =
                            false;

                        break;
                    }
                }
            }

            if (success)
            {
                foreach (
                    EditMeshEntry entry
                    in displayEntries
                )
                {
                    if (
                        entry.group != null
                        ||
                        entry.selectedMenuFolder
                            == null
                    )
                    {
                        continue;
                    }

                    if (
                        !CreateNewSingleToggler(
                            entry
                        )
                    )
                    {
                        success =
                            false;

                        break;
                    }
                }
            }

            if (!success)
            {
                Undo.RevertAllDownToGroup(
                    undoGroup
                );

                return false;
            }

            Undo.CollapseUndoOperations(
                undoGroup
            );

            if (rebuildAfterApply)
            {
                RebuildFromHierarchy();
            }

            return true;
        }


        private bool ValidateActiveStateSynchronization()
        {
            foreach (
                ExistingGroup group
                in existingGroups
            )
            {
                if (
                    group.entries.Count == 0
                )
                {
                    continue;
                }

                bool split =
                    group.entries.Count > 1
                    &&
                    !group.maintainMultiple
                    &&
                    !group.structuralLocked;

                if (split)
                {
                    continue;
                }

                if (
                    !group.structuralLocked
                    &&
                    group.entries[0]
                        .selectedMenuFolder == null
                )
                {
                    continue;
                }

                bool requiredDefault;

                if (
                    !TryComputeRequiredDefaultValue(
                        group,
                        out requiredDefault
                    )
                )
                {
                    EditorUtility.DisplayDialog(
                        "複数Targetの表示状態",
                        "この複数Target ItemTogglerは、現在の構成を維持したまま"
                        + "各Targetの表示状態と既存のON/OFF動作を同時に維持できません。\n\n"
                        + "「既存の複数Target構成を維持」をOFFにして分割してから適用してください。",
                        "OK"
                    );

                    return false;
                }
            }

            return true;
        }


        private static bool TryComputeRequiredDefaultValue(
            ExistingGroup group,
            out bool requiredDefault
        )
        {
            requiredDefault =
                false;

            bool hasValue =
                false;

            foreach (
                EditMeshEntry entry
                in group.entries
            )
            {
                if (entry.gameObject == null)
                {
                    continue;
                }

                bool newObjectValue =
                    !entry.gameObject.activeSelf;

                bool thisRequiredDefault =
                    entry.existingMode
                        == ItemTogglerSetupMode
                            .HideWhenOn
                    ? newObjectValue
                    : !newObjectValue;

                if (!hasValue)
                {
                    requiredDefault =
                        thisRequiredDefault;

                    hasValue =
                        true;

                    continue;
                }

                if (
                    requiredDefault
                        != thisRequiredDefault
                )
                {
                    return false;
                }
            }

            return true;
        }


        private static void
            SyncExistingTogglerActiveState(
                ExistingGroup group
            )
        {
            if (
                group.toggler == null
                ||
                group.entries.Count == 0
            )
            {
                return;
            }

            bool requiredDefault;

            if (
                !TryComputeRequiredDefaultValue(
                    group,
                    out requiredDefault
                )
            )
            {
                return;
            }

            Undo.RecordObject(
                group.toggler,
                "ItemTogglerの表示状態を更新"
            );

            SerializedObject serializedObject =
                new SerializedObject(
                    group.toggler
                );

            serializedObject.Update();

            SerializedProperty defaultValue =
                serializedObject.FindProperty(
                    "defaultValue"
                );

            if (defaultValue != null)
            {
                defaultValue.boolValue =
                    requiredDefault;
            }

            SerializedProperty objects =
                serializedObject
                    .FindProperty("parameter")
                    .FindPropertyRelative(
                        "objects"
                    );

            foreach (
                EditMeshEntry entry
                in group.entries
            )
            {
                if (
                    entry.objectIndex < 0
                    ||
                    entry.objectIndex
                        >= objects.arraySize
                    ||
                    entry.gameObject == null
                )
                {
                    continue;
                }

                SerializedProperty value =
                    objects
                        .GetArrayElementAtIndex(
                            entry.objectIndex
                        )
                        .FindPropertyRelative(
                            "value"
                        );

                value.boolValue =
                    !entry.gameObject.activeSelf;
            }

            serializedObject
                .ApplyModifiedProperties();
        }


        private bool SplitGroup(
            ExistingGroup group
        )
        {
            if (
                group.toggler == null
            )
            {
                return true;
            }

            DeleteItemTogglerSafely(
                group.toggler
            );

            foreach (
                EditMeshEntry entry
                in group.entries
            )
            {
                if (
                    entry.gameObject == null
                    ||
                    entry.selectedMenuFolder
                        == null
                )
                {
                    continue;
                }

                if (
                    !CreateSplitSingleToggler(
                        group,
                        entry
                    )
                )
                {
                    return false;
                }
            }

            return true;
        }


        private bool CreateSplitSingleToggler(
            ExistingGroup group,
            EditMeshEntry entry
        )
        {
            bool objectValue =
                !entry.gameObject.activeSelf;

            bool defaultValue =
                entry.existingMode
                    == ItemTogglerSetupMode.HideWhenOn
                ? objectValue
                : !objectValue;

            return CreateSingleToggler(
                entry.gameObject,
                entry.selectedMenuFolder,
                defaultValue,
                objectValue,
                group
            );
        }


        private bool CreateNewSingleToggler(
            EditMeshEntry entry
        )
        {
            bool initiallyActive =
                entry.gameObject.activeSelf;

            bool objectValue =
                !initiallyActive;

            bool defaultValue;

            if (
                newItemSetupMode
                    == ItemTogglerSetupMode.HideWhenOn
            )
            {
                defaultValue =
                    !initiallyActive;
            }
            else
            {
                defaultValue =
                    initiallyActive;
            }

            return CreateSingleToggler(
                entry.gameObject,
                entry.selectedMenuFolder,
                defaultValue,
                objectValue,
                null
            );
        }


        private bool CreateSingleToggler(
            GameObject target,
            GameObject logicalFolder,
            bool defaultValue,
            bool objectValue,
            ExistingGroup template
        )
        {
            GameObject page =
                FindDestinationPage(
                    logicalFolder
                );

            if (page == null)
            {
                EditorUtility.DisplayDialog(
                    "メニューを追加できません",
                    $"{logicalFolder.name} に8項目以内で追加できる空きがありません。",
                    "OK"
                );

                return false;
            }

            GameObject itemObject =
                new GameObject(
                    target.name
                );

            Undo.RegisterCreatedObjectUndo(
                itemObject,
                "ItemTogglerを作成"
            );

            PlaceUnderPage(
                itemObject,
                page
            );

            ItemToggler toggler =
                Undo.AddComponent<ItemToggler>(
                    itemObject
                );

            SerializedObject serializedObject =
                new SerializedObject(
                    toggler
                );

            /*
             * menuNameは空のまま。
             * GameObject名をlilycalInventory側に使用させる。
             */

            if (template != null)
            {
                SetBoolProperty(
                    serializedObject,
                    "isSave",
                    template.isSave
                );

                SetBoolProperty(
                    serializedObject,
                    "isLocalOnly",
                    template.isLocalOnly
                );

                SetBoolProperty(
                    serializedObject,
                    "autoFixDuplicate",
                    template.autoFixDuplicate
                );

                SerializedProperty icon =
                    serializedObject.FindProperty(
                        "icon"
                    );

                if (icon != null)
                {
                    icon.objectReferenceValue =
                        template.icon;
                }
            }

            SerializedProperty defaultProperty =
                serializedObject.FindProperty(
                    "defaultValue"
                );

            defaultProperty.boolValue =
                defaultValue;

            SerializedProperty objects =
                serializedObject
                    .FindProperty("parameter")
                    .FindPropertyRelative(
                        "objects"
                    );

            objects.arraySize =
                1;

            SerializedProperty element =
                objects.GetArrayElementAtIndex(
                    0
                );

            element
                .FindPropertyRelative("obj")
                .objectReferenceValue =
                    target;

            element
                .FindPropertyRelative("value")
                .boolValue =
                    objectValue;

            serializedObject
                .ApplyModifiedProperties();

            return true;
        }


        private static void SetBoolProperty(
            SerializedObject serializedObject,
            string propertyName,
            bool value
        )
        {
            SerializedProperty property =
                serializedObject.FindProperty(
                    propertyName
                );

            if (property != null)
            {
                property.boolValue =
                    value;
            }
        }


        private bool MoveExistingToggler(
            ItemToggler toggler,
            GameObject logicalFolder
        )
        {
            if (
                toggler == null
                ||
                logicalFolder == null
            )
            {
                return false;
            }

            GameObject page =
                FindDestinationPage(
                    logicalFolder
                );

            if (page == null)
            {
                EditorUtility.DisplayDialog(
                    "メニューを移動できません",
                    $"{logicalFolder.name} に8項目以内で移動できる空きがありません。",
                    "OK"
                );

                return false;
            }

            PlaceUnderPage(
                toggler.gameObject,
                page
            );

            return true;
        }


        private static void PlaceUnderPage(
            GameObject itemObject,
            GameObject page
        )
        {
            Undo.SetTransformParent(
                itemObject.transform,
                page.transform,
                "ItemTogglerを移動"
            );

            itemObject.transform.localPosition =
                Vector3.zero;

            itemObject.transform.localRotation =
                Quaternion.identity;

            itemObject.transform.localScale =
                Vector3.one;

            GameObject nextPage =
                GetDirectAutoNextPage(
                    page
                );

            if (nextPage != null)
            {
                itemObject.transform
                    .SetSiblingIndex(
                        nextPage.transform
                            .GetSiblingIndex()
                    );
            }
            else
            {
                itemObject.transform
                    .SetAsLastSibling();
            }
        }


        private static void DeleteItemTogglerSafely(
            ItemToggler toggler
        )
        {
            if (toggler == null)
            {
                return;
            }

            if (
                CanSafelyMoveWholeToggler(
                    toggler
                )
            )
            {
                Undo.DestroyObjectImmediate(
                    toggler.gameObject
                );
            }
            else
            {
                Undo.DestroyObjectImmediate(
                    toggler
                );
            }
        }


        // =========================================================
        // Pagination for edit mode
        // =========================================================

        private GameObject FindDestinationPage(
            GameObject logicalFolder
        )
        {
            if (
                logicalFolder == null
                ||
                logicalFolder.GetComponent<MenuFolder>()
                    == null
            )
            {
                return null;
            }

            GameObject page =
                logicalFolder;

            while (page != null)
            {
                GameObject next =
                    GetDirectAutoNextPage(
                        page
                    );

                int childFolders =
                    CountDirectManualChildMenuFolders(
                        page
                    );

                int itemCount =
                    CountDirectItemTogglers(
                        page
                    );

                if (next != null)
                {
                    int available =
                        MenuPageCalculator
                            .GetAvailableItemSlots(
                                childFolders,
                                itemCount,
                                true
                            );

                    if (available > 0)
                    {
                        return page;
                    }

                    page =
                        next;

                    continue;
                }

                int availableWithoutNext =
                    MenuPageCalculator
                        .GetAvailableItemSlots(
                            childFolders,
                            itemCount,
                            false
                        );

                if (
                    availableWithoutNext
                        > 0
                )
                {
                    return page;
                }

                if (
                    MenuPageCalculator
                        .CanCreateNextPage(
                            childFolders,
                            itemCount
                        )
                )
                {
                    return CreateAutoNextPage(
                        page
                    );
                }

                ItemToggler lastItem =
                    GetLastDirectItemToggler(
                        page
                    );

                if (lastItem == null)
                {
                    return null;
                }

                GameObject overflowPage =
                    CreateAutoNextPage(
                        page
                    );

                PlaceUnderPage(
                    lastItem.gameObject,
                    overflowPage
                );

                return overflowPage;
            }

            return null;
        }


        private GameObject CreateAutoNextPage(
            GameObject parent
        )
        {
            string name =
                FindAvailableNextPageName(
                    parent
                );

            GameObject next =
                new GameObject(
                    name
                );

            Undo.RegisterCreatedObjectUndo(
                next,
                "NextPageを作成"
            );

            Undo.SetTransformParent(
                next.transform,
                parent.transform,
                "NextPageを配置"
            );

            next.transform.localPosition =
                Vector3.zero;

            next.transform.localRotation =
                Quaternion.identity;

            next.transform.localScale =
                Vector3.one;

            MenuFolder folder =
                Undo.AddComponent<MenuFolder>(
                    next
                );

            SerializedObject serializedObject =
                new SerializedObject(
                    folder
                );

            SerializedProperty menuName =
                serializedObject.FindProperty(
                    "menuName"
                );

            if (menuName != null)
            {
                menuName.stringValue =
                    name;

                serializedObject
                    .ApplyModifiedProperties();
            }

            Undo.AddComponent
                <AutoGeneratedNextPageMarker>(
                    next
                );

            next.transform.SetAsLastSibling();

            return next;
        }


        private static string
            FindAvailableNextPageName(
                GameObject parent
            )
        {
            int number =
                1;

            while (true)
            {
                string candidate =
                    number == 1
                        ? "NextPage"
                        : $"NextPage{number}";

                bool exists =
                    false;

                foreach (
                    Transform child
                    in parent.transform
                )
                {
                    if (
                        child.name
                            == candidate
                    )
                    {
                        exists =
                            true;

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


        private static GameObject
            GetDirectAutoNextPage(
                GameObject parent
            )
        {
            foreach (
                Transform child
                in parent.transform
            )
            {
                if (
                    child.GetComponent<MenuFolder>()
                        != null
                    &&
                    child.GetComponent
                        <AutoGeneratedNextPageMarker>()
                        != null
                )
                {
                    return child.gameObject;
                }
            }

            return null;
        }


        private static int
            CountDirectItemTogglers(
                GameObject parent
            )
        {
            int result =
                0;

            foreach (
                Transform child
                in parent.transform
            )
            {
                if (
                    child.GetComponent<ItemToggler>()
                        != null
                )
                {
                    result++;
                }
            }

            return result;
        }


        private static int
            CountDirectManualChildMenuFolders(
                GameObject parent
            )
        {
            int result =
                0;

            foreach (
                Transform child
                in parent.transform
            )
            {
                if (
                    child.GetComponent<MenuFolder>()
                        == null
                )
                {
                    continue;
                }

                if (
                    child.GetComponent
                        <AutoGeneratedNextPageMarker>()
                        != null
                )
                {
                    continue;
                }

                result++;
            }

            return result;
        }


        private static ItemToggler
            GetLastDirectItemToggler(
                GameObject parent
            )
        {
            for (
                int i =
                    parent.transform.childCount - 1;
                i >= 0;
                i--
            )
            {
                ItemToggler toggler =
                    parent.transform
                        .GetChild(i)
                        .GetComponent
                        <ItemToggler>();

                if (toggler != null)
                {
                    return toggler;
                }
            }

            return null;
        }


        // =========================================================
        // Hierarchy read
        // =========================================================

        private void RebuildFromHierarchy()
        {
            initialized =
                false;

            menuFolders.Clear();
            existingGroups.Clear();
            displayEntries.Clear();
            statusMessages.Clear();

            if (
                sourceMenuFolder == null
            )
            {
                return;
            }

            BuildMenuFolderList();

            BuildGlobalTargetRegistrationMap();

            List<ItemToggler> relevantTogglers =
                GetRelevantItemTogglers();

            HashSet<GameObject> prefabRoots =
                new HashSet<GameObject>();

            List<GameObject> orderedPrefabRoots =
                new List<GameObject>();

            foreach (
                ItemToggler toggler
                in relevantTogglers
            )
            {
                ExistingGroup group =
                    BuildExistingGroup(
                        toggler
                    );

                existingGroups.Add(
                    group
                );

                if (
                    group.entries.Count == 0
                )
                {
                    statusMessages.Add(
                        $"{toggler.gameObject.name}: "
                        + "Object Targetを取得できないため、"
                        + "このItemTogglerは構造編集対象外です。"
                    );

                    continue;
                }

                foreach (
                    EditMeshEntry entry
                    in group.entries
                )
                {
                    displayEntries.Add(
                        entry
                    );

                    GameObject prefabRoot =
                        FindTargetPrefabRoot(
                            entry.gameObject
                        );

                    if (
                        prefabRoot != null
                        &&
                        prefabRoots.Add(
                            prefabRoot
                        )
                    )
                    {
                        orderedPrefabRoots.Add(
                            prefabRoot
                        );
                    }
                }
            }

            HashSet<GameObject> alreadyDisplayed =
                new HashSet<GameObject>();

            foreach (
                EditMeshEntry entry
                in displayEntries
            )
            {
                if (entry.gameObject != null)
                {
                    alreadyDisplayed.Add(
                        entry.gameObject
                    );
                }
            }

            foreach (
                GameObject prefabRoot
                in orderedPrefabRoots
            )
            {
                List<GameObject> roots =
                    new List<GameObject>
                    {
                        prefabRoot
                    };

                List<List<GameObject>> lists =
                    ItemTogglerSetupWindow
                        .BuildUniqueMeshLists(
                            roots
                        );

                foreach (
                    GameObject mesh
                    in lists[0]
                )
                {
                    if (
                        mesh == null
                        ||
                        alreadyDisplayed
                            .Contains(mesh)
                    )
                    {
                        continue;
                    }

                    /*
                     * Scope外を含め、
                     * 既にどこかのItemTogglerに登録済みなら
                     * 「未登録」としては表示しない。
                     */
                    if (
                        allTargetRegistrations
                            .ContainsKey(mesh)
                    )
                    {
                        continue;
                    }

                    EditMeshEntry entry =
                        CreateUnregisteredEntry(
                            mesh
                        );

                    displayEntries.Add(
                        entry
                    );

                    alreadyDisplayed.Add(
                        mesh
                    );
                }
            }

            initialized =
                true;

            Repaint();
        }


        private void BuildMenuFolderList()
        {
            MenuFolder[] found =
                sourceMenuFolder
                    .GetComponentsInChildren
                    <MenuFolder>(true);

            Dictionary<string, int> nameCounts =
                new Dictionary<string, int>();

            foreach (
                MenuFolder folder
                in found
            )
            {
                if (
                    folder == null
                    ||
                    folder.GetComponent
                        <AutoGeneratedNextPageMarker>()
                        != null
                )
                {
                    continue;
                }

                string name =
                    folder.gameObject.name;

                if (
                    nameCounts.ContainsKey(
                        name
                    )
                )
                {
                    nameCounts[name]++;
                }
                else
                {
                    nameCounts[name] =
                        1;
                }
            }

            foreach (
                MenuFolder folder
                in found
            )
            {
                if (
                    folder == null
                    ||
                    folder.GetComponent
                        <AutoGeneratedNextPageMarker>()
                        != null
                )
                {
                    continue;
                }

                GameObject go =
                    folder.gameObject;

                string displayName;

                if (
                    go == sourceMenuFolder
                )
                {
                    displayName =
                        go.name;
                }
                else if (
                    nameCounts[go.name]
                        > 1
                )
                {
                    displayName =
                        GetRelativePath(
                            sourceMenuFolder.transform,
                            go.transform
                        );
                }
                else
                {
                    displayName =
                        go.name;
                }

                menuFolders.Add(
                    new MenuFolderEntry
                    {
                        gameObject =
                            go,

                        displayName =
                            displayName
                    }
                );
            }
        }


        private void BuildGlobalTargetRegistrationMap()
        {
            allTargetRegistrations =
                new Dictionary
                <GameObject, List<ItemToggler>>();

            GameObject avatarRoot =
                GetAvatarRootObject();

            if (avatarRoot == null)
            {
                return;
            }

            ItemToggler[] togglers =
                avatarRoot
                    .GetComponentsInChildren
                    <ItemToggler>(true);

            foreach (
                ItemToggler toggler
                in togglers
            )
            {
                List<GameObject> targets =
                    ReadTargets(
                        toggler
                    );

                foreach (
                    GameObject target
                    in targets
                )
                {
                    if (target == null)
                    {
                        continue;
                    }

                    List<ItemToggler> list;

                    if (
                        !allTargetRegistrations
                            .TryGetValue(
                                target,
                                out list
                            )
                    )
                    {
                        list =
                            new List<ItemToggler>();

                        allTargetRegistrations.Add(
                            target,
                            list
                        );
                    }

                    list.Add(
                        toggler
                    );
                }
            }
        }


        private List<ItemToggler>
            GetRelevantItemTogglers()
        {
            List<ItemToggler> result =
                new List<ItemToggler>();

            ItemToggler[] candidates =
                sourceMenuFolder
                    .GetComponentsInChildren
                    <ItemToggler>(true);

            foreach (
                ItemToggler toggler
                in candidates
            )
            {
                GameObject logicalFolder =
                    ResolveLogicalMenuFolder(
                        toggler
                    );

                if (
                    !IsLogicalFolderInScope(
                        logicalFolder
                    )
                )
                {
                    continue;
                }

                result.Add(
                    toggler
                );
            }

            return result;
        }


        private ExistingGroup BuildExistingGroup(
            ItemToggler toggler
        )
        {
            ExistingGroup group =
                new ExistingGroup();

            group.toggler =
                toggler;

            group.logicalMenuFolder =
                ResolveLogicalMenuFolder(
                    toggler
                );

            group.originalParent =
                toggler.transform.parent;

            group.originalSiblingIndex =
                toggler.transform
                    .GetSiblingIndex();

            SerializedObject serializedObject =
                new SerializedObject(
                    toggler
                );

            serializedObject.Update();

            SerializedProperty defaultValue =
                serializedObject.FindProperty(
                    "defaultValue"
                );

            group.originalDefaultValue =
                defaultValue != null
                &&
                defaultValue.boolValue;

            group.isSave =
                ReadBoolProperty(
                    serializedObject,
                    "isSave",
                    true
                );

            group.isLocalOnly =
                ReadBoolProperty(
                    serializedObject,
                    "isLocalOnly",
                    false
                );

            group.autoFixDuplicate =
                ReadBoolProperty(
                    serializedObject,
                    "autoFixDuplicate",
                    true
                );

            SerializedProperty icon =
                serializedObject.FindProperty(
                    "icon"
                );

            if (icon != null)
            {
                group.icon =
                    icon.objectReferenceValue
                        as Texture2D;
            }

            SerializedProperty parameter =
                serializedObject.FindProperty(
                    "parameter"
                );

            SerializedProperty objects =
                parameter.FindPropertyRelative(
                    "objects"
                );

            for (
                int i = 0;
                i < objects.arraySize;
                i++
            )
            {
                SerializedProperty element =
                    objects
                        .GetArrayElementAtIndex(i);

                GameObject target =
                    element
                        .FindPropertyRelative("obj")
                        .objectReferenceValue
                        as GameObject;

                if (target == null)
                {
                    continue;
                }

                bool value =
                    element
                        .FindPropertyRelative("value")
                        .boolValue;

                EditMeshEntry entry =
                    new EditMeshEntry();

                entry.gameObject =
                    target;

                entry.group =
                    group;

                entry.objectIndex =
                    i;

                entry.originalObjectValue =
                    value;

                entry.existingMode =
                    group.originalDefaultValue
                        == value
                    ? ItemTogglerSetupMode
                        .HideWhenOn
                    : ItemTogglerSetupMode
                        .HideWhenOff;

                entry.selectedMenuFolder =
                    group.logicalMenuFolder;

                entry.snapshotMenuFolder =
                    group.logicalMenuFolder;

                InitializeObjectSnapshot(
                    entry
                );

                List<ItemToggler> registrations;

                if (
                    allTargetRegistrations
                        .TryGetValue(
                            target,
                            out registrations
                        )
                    &&
                    registrations.Count > 1
                )
                {
                    entry.duplicateRegistration =
                        true;

                    AddLockReason(
                        group,
                        "同じTargetが複数のItemTogglerに登録されています。"
                    );
                }

                group.entries.Add(
                    entry
                );
            }

            if (
                HasNonObjectParameterSettings(
                    serializedObject
                )
            )
            {
                AddLockReason(
                    group,
                    "Object Target以外の設定も含まれているため、"
                    + "MenuFolder変更・登録解除・分割は無効です。"
                );
            }

            if (
                HasMenuParentOverride(
                    serializedObject
                )
            )
            {
                AddLockReason(
                    group,
                    "MenuFolder Override / Modular Avatar指定があるため、"
                    + "構造編集は無効です。"
                );
            }

            if (
                !CanSafelyMoveWholeToggler(
                    toggler
                )
            )
            {
                AddLockReason(
                    group,
                    "ItemTogglerと同じGameObjectに他Componentまたは子Objectがあるため、"
                    + "安全のため構造編集は無効です。"
                );
            }

            if (
                group.logicalMenuFolder
                    == null
                ||
                !ContainsMenuFolder(
                    group.logicalMenuFolder
                )
            )
            {
                AddLockReason(
                    group,
                    "現在の登録先MenuFolderを安全に再現できないため、"
                    + "構造編集は無効です。"
                );
            }

            return group;
        }


        private EditMeshEntry CreateUnregisteredEntry(
            GameObject mesh
        )
        {
            EditMeshEntry entry =
                new EditMeshEntry();

            entry.gameObject =
                mesh;

            entry.selectedMenuFolder =
                null;

            entry.snapshotMenuFolder =
                null;

            InitializeObjectSnapshot(
                entry
            );

            return entry;
        }


        private static void InitializeObjectSnapshot(
            EditMeshEntry entry
        )
        {
            if (entry.gameObject == null)
            {
                return;
            }

            entry.snapshotActive =
                entry.gameObject.activeSelf;

            entry.snapshotTag =
                entry.gameObject.tag;

            entry.lastNonEditorOnlyTag =
                entry.gameObject.CompareTag(
                    "EditorOnly"
                )
                ? "Untagged"
                : entry.gameObject.tag;
        }


        private bool IsLogicalFolderInScope(
            GameObject folder
        )
        {
            if (folder == null)
            {
                return false;
            }

            if (
                folder == sourceMenuFolder
            )
            {
                return true;
            }

            if (!includeChildMenuFolders)
            {
                return false;
            }

            return folder.transform.IsChildOf(
                sourceMenuFolder.transform
            );
        }


        private GameObject ResolveLogicalMenuFolder(
            ItemToggler toggler
        )
        {
            if (toggler == null)
            {
                return null;
            }

            SerializedObject serializedObject =
                new SerializedObject(
                    toggler
                );

            SerializedProperty parentOverride =
                serializedObject.FindProperty(
                    "parentOverride"
                );

            if (
                parentOverride != null
                &&
                parentOverride.objectReferenceValue
                    != null
            )
            {
                MenuFolder folder =
                    parentOverride
                        .objectReferenceValue
                        as MenuFolder;

                if (folder != null)
                {
                    return folder.gameObject;
                }
            }

            Transform current =
                toggler.transform.parent;

            while (current != null)
            {
                MenuFolder folder =
                    current.GetComponent<MenuFolder>();

                if (folder != null)
                {
                    if (
                        current.GetComponent
                            <AutoGeneratedNextPageMarker>()
                            != null
                    )
                    {
                        current =
                            current.parent;

                        continue;
                    }

                    return current.gameObject;
                }

                current =
                    current.parent;
            }

            return null;
        }


        private GameObject FindTargetPrefabRoot(
            GameObject target
        )
        {
            if (target == null)
            {
                return null;
            }

            GameObject prefabRoot =
                PrefabUtility
                    .GetNearestPrefabInstanceRoot(
                        target
                    );

            if (prefabRoot == null)
            {
                return null;
            }

            GameObject avatarRoot =
                GetAvatarRootObject();

            /*
             * Avatar全体がPrefab Instanceだった場合に
             * Avatar本体全Meshを列挙しない。
             */
            if (
                prefabRoot == avatarRoot
            )
            {
                return null;
            }

            return prefabRoot;
        }


        private GameObject GetAvatarRootObject()
        {
            if (sourceMenuFolder == null)
            {
                return null;
            }

            VRCAvatarDescriptor descriptor =
                sourceMenuFolder
                    .GetComponentInParent
                    <VRCAvatarDescriptor>();

            if (descriptor != null)
            {
                return descriptor.gameObject;
            }

            return sourceMenuFolder
                .transform.root.gameObject;
        }


        // =========================================================
        // Serialized helpers
        // =========================================================

        private static List<GameObject> ReadTargets(
            ItemToggler toggler
        )
        {
            List<GameObject> result =
                new List<GameObject>();

            if (toggler == null)
            {
                return result;
            }

            SerializedObject serializedObject =
                new SerializedObject(
                    toggler
                );

            SerializedProperty objects =
                serializedObject
                    .FindProperty("parameter")
                    .FindPropertyRelative(
                        "objects"
                    );

            for (
                int i = 0;
                i < objects.arraySize;
                i++
            )
            {
                GameObject target =
                    objects
                        .GetArrayElementAtIndex(i)
                        .FindPropertyRelative("obj")
                        .objectReferenceValue
                        as GameObject;

                if (target != null)
                {
                    result.Add(
                        target
                    );
                }
            }

            return result;
        }


        private static bool ReadBoolProperty(
            SerializedObject serializedObject,
            string propertyName,
            bool defaultValue
        )
        {
            SerializedProperty property =
                serializedObject.FindProperty(
                    propertyName
                );

            if (property == null)
            {
                return defaultValue;
            }

            return property.boolValue;
        }


        private static bool
            HasNonObjectParameterSettings(
                SerializedObject serializedObject
            )
        {
            SerializedProperty parameter =
                serializedObject.FindProperty(
                    "parameter"
                );

            if (parameter == null)
            {
                return false;
            }

            string[] propertyNames =
            {
                "blendShapeModifiers",
                "materialReplacers",
                "materialPropertyModifiers",
                "clips"
            };

            foreach (
                string propertyName
                in propertyNames
            )
            {
                SerializedProperty property =
                    parameter.FindPropertyRelative(
                        propertyName
                    );

                if (
                    property != null
                    &&
                    property.isArray
                    &&
                    property.arraySize > 0
                )
                {
                    return true;
                }
            }

            return false;
        }


        private static bool HasMenuParentOverride(
            SerializedObject serializedObject
        )
        {
            SerializedProperty parentOverride =
                serializedObject.FindProperty(
                    "parentOverride"
                );

            if (
                parentOverride != null
                &&
                parentOverride.objectReferenceValue
                    != null
            )
            {
                return true;
            }

            SerializedProperty parentOverrideMA =
                serializedObject.FindProperty(
                    "parentOverrideMA"
                );

            return parentOverrideMA != null
                &&
                parentOverrideMA.objectReferenceValue
                    != null;
        }


        // =========================================================
        // Safety
        // =========================================================

        private static bool CanSafelyMoveWholeToggler(
            ItemToggler toggler
        )
        {
            if (
                toggler == null
                ||
                toggler.gameObject == null
            )
            {
                return false;
            }

            GameObject target =
                toggler.gameObject;

            if (
                target.transform.childCount
                    > 0
            )
            {
                return false;
            }

            Component[] components =
                target.GetComponents<Component>();

            foreach (
                Component component
                in components
            )
            {
                if (component == null)
                {
                    return false;
                }

                if (
                    component is Transform
                    ||
                    component == toggler
                )
                {
                    continue;
                }

                return false;
            }

            return true;
        }


        private static void AddLockReason(
            ExistingGroup group,
            string reason
        )
        {
            group.structuralLocked =
                true;

            if (
                string.IsNullOrEmpty(
                    group.lockReason
                )
            )
            {
                group.lockReason =
                    reason;
            }
            else if (
                !group.lockReason.Contains(
                    reason
                )
            )
            {
                group.lockReason +=
                    "\n"
                    + reason;
            }
        }


        private bool ContainsMenuFolder(
            GameObject target
        )
        {
            foreach (
                MenuFolderEntry entry
                in menuFolders
            )
            {
                if (
                    entry.gameObject
                        == target
                )
                {
                    return true;
                }
            }

            return false;
        }


        // =========================================================
        // EditorOnly
        // =========================================================

        private static bool HasEditorOnlyAncestor(
            GameObject target
        )
        {
            if (target == null)
            {
                return false;
            }

            Transform current =
                target.transform.parent;

            while (current != null)
            {
                if (
                    current.CompareTag(
                        "EditorOnly"
                    )
                )
                {
                    return true;
                }

                current =
                    current.parent;
            }

            return false;
        }


        // =========================================================
        // Menu folder path
        // =========================================================

        private static string GetRelativePath(
            Transform root,
            Transform target
        )
        {
            Stack<string> names =
                new Stack<string>();

            Transform current =
                target;

            while (
                current != null
                &&
                current != root
            )
            {
                names.Push(
                    current.name
                );

                current =
                    current.parent;
            }

            return string.Join(
                "/",
                names
            );
        }


        // =========================================================
        // Close / dialog
        // =========================================================

        private void RequestClose()
        {
            if (!HasAnyChanges())
            {
                suppressDestroyPrompt =
                    true;

                Close();

                return;
            }

            int choice =
                ShowSaveDiscardCancelDialog(
                    "変更があります。変更を保存しますか？"
                );

            switch (choice)
            {
                // 保存
                case 0:
                {
                    if (
                        !ApplyChanges(
                            false
                        )
                    )
                    {
                        return;
                    }

                    suppressDestroyPrompt =
                        true;

                    Close();

                    break;
                }

                // 保存しない
                case 1:
                {
                    RestoreImmediateChangesToSnapshot();

                    suppressDestroyPrompt =
                        true;

                    Close();

                    break;
                }

                // キャンセル
                default:
                {
                    break;
                }
            }
        }


        private static int ShowSaveDiscardCancelDialog(
            string message
        )
        {
            return EditorUtility.DisplayDialogComplex(
                "変更の確認",
                message,
                "保存",
                "保存しない",
                "キャンセル"
            );
        }


        private void OnDestroy()
        {
            if (
                suppressDestroyPrompt
                ||
                !initialized
                ||
                sourceMenuFolder == null
                ||
                !HasAnyChanges()
            )
            {
                return;
            }

            int choice =
                ShowSaveDiscardCancelDialog(
                    "変更があります。変更を保存しますか？"
                );

            if (choice == 0)
            {
                if (
                    ApplyChanges(
                        false
                    )
                )
                {
                    return;
                }

                ReopenState failedState =
                    CaptureReopenState();

                EditorApplication.delayCall +=
                    delegate
                    {
                        Reopen(
                            failedState
                        );
                    };

                return;
            }

            if (choice == 1)
            {
                RestoreImmediateChangesToSnapshot();

                return;
            }

            ReopenState state =
                CaptureReopenState();

            EditorApplication.delayCall +=
                delegate
                {
                    Reopen(
                        state
                    );
                };
        }


        // =========================================================
        // Reopen after X -> Cancel
        // =========================================================

        private ReopenState CaptureReopenState()
        {
            ReopenState state =
                new ReopenState();

            state.sourceMenuFolder =
                sourceMenuFolder;

            state.setupMode =
                newItemSetupMode;

            state.includeChildMenuFolders =
                includeChildMenuFolders;

            state.scrollPosition =
                scrollPosition;

            foreach (
                ExistingGroup group
                in existingGroups
            )
            {
                if (group.toggler == null)
                {
                    continue;
                }

                state.groupMaintain[
                    group.toggler.GetInstanceID()
                ] =
                    group.maintainMultiple;
            }

            foreach (
                EditMeshEntry entry
                in displayEntries
            )
            {
                string key =
                    GetEntryKey(
                        entry
                    );

                state.selectedFolders[key] =
                    entry.selectedMenuFolder
                        != null
                    ? entry.selectedMenuFolder
                        .GetInstanceID()
                    : 0;

                state.snapshotFolders[key] =
                    entry.snapshotMenuFolder
                        != null
                    ? entry.snapshotMenuFolder
                        .GetInstanceID()
                    : 0;

                if (
                    entry.gameObject != null
                )
                {
                    int id =
                        entry.gameObject
                            .GetInstanceID();

                    state.snapshotActive[id] =
                        entry.snapshotActive;

                    state.snapshotTag[id] =
                        entry.snapshotTag;
                }
            }

            return state;
        }


        private void ApplyReopenState(
            ReopenState state
        )
        {
            includeChildMenuFolders =
                state.includeChildMenuFolders;

            scrollPosition =
                state.scrollPosition;

            foreach (
                ExistingGroup group
                in existingGroups
            )
            {
                if (group.toggler == null)
                {
                    continue;
                }

                bool maintain;

                if (
                    state.groupMaintain
                        .TryGetValue(
                            group.toggler
                                .GetInstanceID(),
                            out maintain
                        )
                )
                {
                    group.maintainMultiple =
                        maintain;
                }
            }

            foreach (
                EditMeshEntry entry
                in displayEntries
            )
            {
                string key =
                    GetEntryKey(
                        entry
                    );

                int selectedId;

                if (
                    state.selectedFolders
                        .TryGetValue(
                            key,
                            out selectedId
                        )
                )
                {
                    entry.selectedMenuFolder =
                        selectedId == 0
                        ? null
                        : EditorUtility
                            .InstanceIDToObject(
                                selectedId
                            ) as GameObject;
                }

                int snapshotId;

                if (
                    state.snapshotFolders
                        .TryGetValue(
                            key,
                            out snapshotId
                        )
                )
                {
                    entry.snapshotMenuFolder =
                        snapshotId == 0
                        ? null
                        : EditorUtility
                            .InstanceIDToObject(
                                snapshotId
                            ) as GameObject;
                }

                if (
                    entry.gameObject == null
                )
                {
                    continue;
                }

                int objectId =
                    entry.gameObject
                        .GetInstanceID();

                bool snapshotActive;

                if (
                    state.snapshotActive
                        .TryGetValue(
                            objectId,
                            out snapshotActive
                        )
                )
                {
                    entry.snapshotActive =
                        snapshotActive;
                }

                string snapshotTag;

                if (
                    state.snapshotTag
                        .TryGetValue(
                            objectId,
                            out snapshotTag
                        )
                )
                {
                    entry.snapshotTag =
                        snapshotTag;
                }
            }

            Repaint();
        }


        private static string GetEntryKey(
            EditMeshEntry entry
        )
        {
            if (entry.group != null)
            {
                int togglerId =
                    entry.group.toggler != null
                    ? entry.group.toggler
                        .GetInstanceID()
                    : 0;

                return
                    $"G:{togglerId}:{entry.objectIndex}";
            }

            int targetId =
                entry.gameObject != null
                ? entry.gameObject
                    .GetInstanceID()
                : 0;

            return
                $"N:{targetId}";
        }
    }
}
