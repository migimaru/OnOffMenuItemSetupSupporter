using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using jp.lilxyzw.lilycalinventory.runtime;
using Migimaru.OnOffMenuItemSetupSupporter;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{

    public enum ItemTogglerSetupMode
    {
        HideWhenOn,
        HideWhenOff
    }

    public sealed class ItemTogglerSetupWindow : EditorWindow
    {

        private enum ChildFolderPosition
        {
            Top,
            Bottom
        }

        private sealed class MeshEntry
        {
            public GameObject gameObject;
            // null = 登録しない
            //
            // EditorOnlyによって一時的に選択不能になった場合も
            // 元の選択先は保持する。
            public GameObject selectedMenuFolder;
        }

        private sealed class OutfitEntry
        {
            public GameObject root;
            public readonly List<MeshEntry> meshObjects =
            new List<MeshEntry>();
        }

        private sealed class MenuFolderEntry
        {
            public GameObject gameObject;
            public string displayName;
        }

        private sealed class SelectionGroup
        {
            public GameObject menuFolder;
            public readonly List<GameObject> meshes =
            new List<GameObject>();
        }
        private GameObject sourceMenuFolder;
        private ItemTogglerSetupMode setupMode;
        private readonly List<OutfitEntry> outfits =
        new List<OutfitEntry>
        {
            new OutfitEntry()
            };
            private readonly List<MenuFolderEntry> menuFolders =
            new List<MenuFolderEntry>();
            private bool skipEditorOnlyMenuCreation = true;
            private bool autoAdjustToEightItems = true;
            private ChildFolderPosition childFolderPosition =
            ChildFolderPosition.Top;
            private Vector2 scrollPosition;
            private const float DisplayColumnWidth = 45f;
            private const float EditorOnlyColumnWidth = 75f;
            private const float MeshNameColumnWidth = 160f;
            private const float MenuFolderColumnWidth = 100f;
            private const float DoNotRegisterColumnWidth = 80f;
            public static void Open(
                GameObject sourceMenuFolder,
                ItemTogglerSetupMode setupMode
                )
            {
                ItemTogglerSetupWindow window =
                CreateInstance<ItemTogglerSetupWindow>();
                window.titleContent =
                new GUIContent(
                    "ON/OFFメニュー設定支援"
                    );
                window.sourceMenuFolder =
                sourceMenuFolder;
                window.setupMode =
                setupMode;
                window.minSize =
                new Vector2(
                    700,
                    400
                    );
                window.RefreshMenuFolderList();
                window.ShowUtility();
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField(
                    "ON/OFFメニュー設定支援",
                    EditorStyles.boldLabel
                    );
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "設定先MenuFolder",
                    sourceMenuFolder != null
                    ? sourceMenuFolder.name
                    : "なし"
                    );
                EditorGUILayout.LabelField(
                    "動作",
                    setupMode == ItemTogglerSetupMode.HideWhenOn
                    ? "ONで非表示"
                    : "OFFで非表示"
                    );
                EditorGUILayout.Space();
                DrawAddArea();
                EditorGUILayout.Space(4);
                DrawOptions();
                EditorGUILayout.Space(4);
                DrawUnusedMenuFolderWarning();
                EditorGUILayout.Space(6);
                scrollPosition =
                EditorGUILayout.BeginScrollView(
                    scrollPosition
                    );
                int removeIndex = -1;
                for (int i = 0; i < outfits.Count; i++)
                {
                    if (DrawOutfitEntry(outfits[i]))
                    {
                        removeIndex = i;
                    }
                }
                EditorGUILayout.EndScrollView();
                if (removeIndex >= 0)
                {
                    outfits.RemoveAt(
                        removeIndex
                        );
                    /*
                    * 削除されたRootによって隠れていた
                    * 重複Meshが別のRoot側に存在する可能性があるため、
                    * 全体を再走査する。
                    */
                    RefreshAllMeshLists();
                }
                EditorGUILayout.Space();
                DrawBottomButtons();
            }

            // =========================================================
            // オプション

            // =========================================================
            private void DrawOptions()
            {
                skipEditorOnlyMenuCreation =
                EditorGUILayout.ToggleLeft(
                    "EditorOnlyにはメニューを作成しない",
                    skipEditorOnlyMenuCreation
                    );
                bool canUseAutoAdjust =
                CanUseAutoAdjust();
                if (!canUseAutoAdjust)
                {
                    autoAdjustToEightItems =
                    false;
                }
                EditorGUI.BeginDisabledGroup(
                    !canUseAutoAdjust
                    );
                autoAdjustToEightItems =
                EditorGUILayout.ToggleLeft(
                    "8項目以内に自動調整",
                    autoAdjustToEightItems
                    );
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(
                    "子MenuFolderの位置"
                    );
                bool selectTop =
                GUILayout.Toggle(
                    childFolderPosition
                    == ChildFolderPosition.Top,
                    "上 ▲",
                    EditorStyles.radioButton,
                    GUILayout.Width(65)
                    );
                if (selectTop)
                {
                    childFolderPosition =
                    ChildFolderPosition.Top;
                }
                bool selectBottom =
                GUILayout.Toggle(
                    childFolderPosition
                    == ChildFolderPosition.Bottom,
                    "下 ▼",
                    EditorStyles.radioButton,
                    GUILayout.Width(65)
                    );
                if (selectBottom)
                {
                    childFolderPosition =
                    ChildFolderPosition.Bottom;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            // =========================================================
            // 未使用MenuFolder

            // =========================================================
            private void DrawUnusedMenuFolderWarning()
            {
                List<string> unusedFolders =
                GetUnusedMenuFolderDisplayNames();
                if (unusedFolders.Count == 0)
                {
                    return;
                }
                string folderNames =
                string.Join(
                    "、",
                    unusedFolders
                    );
                EditorGUILayout.HelpBox(
                    $"未使用のMenuFolder: {folderNames}\n"
                    + "使用しない場合はHierarchyから削除かEditorOnlyに変更してください。",
                    MessageType.Info
                    );
            }

            private List<string> GetUnusedMenuFolderDisplayNames()
            {
                List<string> result =
                new List<string>();
                foreach (
                    MenuFolderEntry entry
                    in menuFolders
                    )
                {
                    if (
                        entry.gameObject == null
                        ||
                        entry.gameObject == sourceMenuFolder
                        )
                    {
                        continue;
                    }
                    /*
                    * EditorOnlyの場合は
                    * ユーザーが意図的に使用しないものとして扱う。
                    */
                    if (
                        IsEffectivelyEditorOnly(
                        entry.gameObject
                        )
                        )
                    {
                        continue;
                    }
                    if (
                        HasMenuFolderUsage(
                        entry.gameObject
                        )
                        )
                    {
                        continue;
                    }
                    result.Add(
                        entry.displayName
                        );
                }
                return result;
            }

            private bool HasMenuFolderUsage(
                GameObject menuFolder
                )
            {
                if (menuFolder == null)
                {
                    return false;
                }
                ItemToggler[] existingTogglers =
                menuFolder
                .GetComponentsInChildren
                <ItemToggler>(true);
                if (existingTogglers.Length > 0)
                {
                    return true;
                }
                foreach (
                    OutfitEntry outfit
                    in outfits
                    )
                {
                    foreach (
                        MeshEntry meshEntry
                        in outfit.meshObjects
                        )
                    {
                        if (
                            meshEntry.gameObject == null
                            ||
                            meshEntry.selectedMenuFolder == null
                            )
                        {
                            continue;
                        }
                        if (
                            skipEditorOnlyMenuCreation
                            &&
                            IsEffectivelyEditorOnly(
                            meshEntry.gameObject
                            )
                            )
                        {
                            continue;
                        }
                        if (
                            !IsSelectableMenuFolder(
                            meshEntry.selectedMenuFolder
                            )
                            )
                        {
                            continue;
                        }
                        Transform selectedFolder =
                        meshEntry
                        .selectedMenuFolder
                        .transform;
                        if (
                            selectedFolder
                            == menuFolder.transform
                            ||
                            selectedFolder.IsChildOf(
                            menuFolder.transform
                            )
                            )
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // =========================================================
            // 衣装追加

            // =========================================================
            private void DrawAddArea()
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    "衣装",
                    EditorStyles.boldLabel,
                    GUILayout.Width(40)
                    );
                Rect dropArea =
                GUILayoutUtility.GetRect(
                    200,
                    24,
                    GUILayout.ExpandWidth(true)
                    );
                GUI.Box(
                    dropArea,
                    "Hierarchy上のPrefab / GameObjectをここにD&D"
                    );
                HandleDragAndDrop(
                    dropArea
                    );
                if (GUILayout.Button(
                    "+",
                    GUILayout.Width(30),
                    GUILayout.Height(24)
                    ))
                {
                    outfits.Add(
                        new OutfitEntry()
                        );
                }
                EditorGUILayout.EndHorizontal();
            }

            private void HandleDragAndDrop(
                Rect dropArea
                )
            {
                Event currentEvent =
                Event.current;
                if (!dropArea.Contains(
                    currentEvent.mousePosition
                    ))
                {
                    return;
                }
                if (
                    currentEvent.type != EventType.DragUpdated
                    &&
                    currentEvent.type != EventType.DragPerform
                    )
                {
                    return;
                }
                bool hasValidObject =
                false;
                foreach (
                    Object draggedObject
                    in DragAndDrop.objectReferences
                    )
                {
                    GameObject gameObject =
                    draggedObject as GameObject;
                    if (
                        gameObject != null
                        &&
                        !EditorUtility.IsPersistent(
                        gameObject
                        )
                        )
                    {
                        hasValidObject =
                        true;
                        break;
                    }
                }
                DragAndDrop.visualMode =
                hasValidObject
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;
                if (
                    currentEvent.type == EventType.DragPerform
                    &&
                    hasValidObject
                    )
                {
                    DragAndDrop.AcceptDrag();
                    foreach (
                        Object draggedObject
                        in DragAndDrop.objectReferences
                        )
                    {
                        GameObject gameObject =
                        draggedObject as GameObject;
                        if (
                            gameObject == null
                            ||
                            EditorUtility.IsPersistent(
                            gameObject
                            )
                            )
                        {
                            continue;
                        }
                        AddOutfitRoot(
                            gameObject
                            );
                    }
                }
                currentEvent.Use();
            }

            private void AddOutfitRoot(
                GameObject root
                )
            {
                if (root == null)
                {
                    return;
                }
                /*
                * 同一Rootそのものの重複登録も防止する。
                */
                foreach (
                    OutfitEntry outfit
                    in outfits
                    )
                {
                    if (outfit.root == root)
                    {
                        return;
                    }
                }
                foreach (
                    OutfitEntry outfit
                    in outfits
                    )
                {
                    if (outfit.root != null)
                    {
                        continue;
                    }
                    outfit.root =
                    root;
                    RefreshAllMeshLists();
                    return;
                }
                outfits.Add(
                    new OutfitEntry
                {
                        root = root
                }
                    );
                RefreshAllMeshLists();
            }

            private bool IsOutfitRootAlreadyRegistered(
                GameObject root,
                OutfitEntry except
                )
            {
                if (root == null)
                {
                    return false;
                }
                foreach (
                    OutfitEntry outfit
                    in outfits
                    )
                {
                    if (outfit == except)
                    {
                        continue;
                    }
                    if (outfit.root == root)
                    {
                        return true;
                    }
                }
                return false;
            }

            // =========================================================
            // 衣装 / Mesh UI

            // =========================================================
            private bool DrawOutfitEntry(
                OutfitEntry outfit
                )
            {
                bool removeRequested =
                false;
                EditorGUILayout.BeginHorizontal();
                GameObject newRoot =
                (GameObject)EditorGUILayout.ObjectField(
                    outfit.root,
                    typeof(GameObject),
                    true,
                    GUILayout.ExpandWidth(true)
                    );
                if (GUILayout.Button(
                    "×",
                    GUILayout.Width(30)
                    ))
                {
                    removeRequested =
                    true;
                }
                EditorGUILayout.EndHorizontal();
                if (newRoot != outfit.root)
                {
                    /*
                    * Project内のPrefab Assetは受け付けない。
                    */
                    if (
                        newRoot != null
                        &&
                        EditorUtility.IsPersistent(
                        newRoot
                        )
                        )
                    {
                        newRoot =
                        outfit.root;
                    }
                    /*
                    * 同一Rootの重複登録も受け付けない。
                    */
                    if (
                        newRoot != null
                        &&
                        IsOutfitRootAlreadyRegistered(
                        newRoot,
                        outfit
                        )
                        )
                    {
                        newRoot =
                        outfit.root;
                    }
                    if (newRoot != outfit.root)
                    {
                        outfit.root =
                        newRoot;
                        RefreshAllMeshLists();
                    }
                }
                if (outfit.root != null)
                {
                    DrawMeshHeader();
                    foreach (
                        MeshEntry meshEntry
                        in outfit.meshObjects
                        )
                    {
                        if (
                            meshEntry.gameObject == null
                            )
                        {
                            continue;
                        }
                        DrawMeshEntry(
                            meshEntry
                            );
                    }
                }
                EditorGUILayout.Space(8);
                return removeRequested;
            }

            private void DrawMeshHeader()
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(
                    "表示",
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(
                    DisplayColumnWidth
                    )
                    );
                GUILayout.Label(
                    "EditorOnly",
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(
                    EditorOnlyColumnWidth
                    )
                    );
                GUILayout.Label(
                    "メッシュ名",
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(
                    MeshNameColumnWidth
                    )
                    );
                foreach (
                    MenuFolderEntry menuFolder
                    in menuFolders
                    )
                {
                    GUILayout.Label(
                        menuFolder.displayName,
                        EditorStyles.miniBoldLabel,
                        GUILayout.Width(
                        MenuFolderColumnWidth
                        )
                        );
                }
                GUILayout.Label(
                    "登録しない",
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(
                    DoNotRegisterColumnWidth
                    )
                    );
                EditorGUILayout.EndHorizontal();
            }

            private void DrawMeshEntry(
                MeshEntry meshEntry
                )
            {
                GameObject meshObject =
                meshEntry.gameObject;
                if (meshObject == null)
                {
                    return;
                }
                EditorGUILayout.BeginHorizontal();
                // =====================================================
                // 表示
                // =====================================================
                bool currentActive =
                meshObject.activeSelf;
                bool newActive =
                EditorGUILayout.Toggle(
                    currentActive,
                    GUILayout.Width(
                    DisplayColumnWidth
                    )
                    );
                if (newActive != currentActive)
                {
                    Undo.RecordObject(
                        meshObject,
                        "メッシュの表示状態を変更"
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
                    EditorApplication
                    .RepaintHierarchyWindow();
                    SceneView.RepaintAll();
                }
                // =====================================================
                // EditorOnly
                // =====================================================
                bool hasEditorOnlyTag =
                meshObject.CompareTag(
                    "EditorOnly"
                    );
                bool hasEditorOnlyAncestor =
                HasEditorOnlyAncestor(
                    meshObject
                    );
                bool displayedEditorOnly =
                hasEditorOnlyTag
                ||
                hasEditorOnlyAncestor;
                bool inheritedEditorOnly =
                hasEditorOnlyAncestor
                &&
                !hasEditorOnlyTag;
                EditorGUI.BeginDisabledGroup(
                    inheritedEditorOnly
                    );
                bool newEditorOnly =
                EditorGUILayout.Toggle(
                    displayedEditorOnly,
                    GUILayout.Width(
                    EditorOnlyColumnWidth
                    )
                    );
                EditorGUI.EndDisabledGroup();
                if (
                    !inheritedEditorOnly
                    &&
                    newEditorOnly != hasEditorOnlyTag
                    )
                {
                    Undo.RecordObject(
                        meshObject,
                        "EditorOnlyを変更"
                        );
                    meshObject.tag =
                    newEditorOnly
                    ? "EditorOnly"
                    : "Untagged";
                    PrefabUtility
                    .RecordPrefabInstancePropertyModifications(
                        meshObject
                        );
                    EditorUtility.SetDirty(
                        meshObject
                        );
                    EditorApplication
                    .RepaintHierarchyWindow();
                    SceneView.RepaintAll();
                }
                bool effectivelyEditorOnly =
                newEditorOnly
                ||
                hasEditorOnlyAncestor;
                // =====================================================
                // Mesh名
                // =====================================================
                if (GUILayout.Button(
                    meshObject.name,
                    EditorStyles.linkLabel,
                    GUILayout.Width(
                    MeshNameColumnWidth
                    )
                    ))
                {
                    SelectMeshObject(
                        meshObject
                        );
                }
                // =====================================================
                // MenuFolder選択
                // =====================================================
                bool disableMenuSelection =
                skipEditorOnlyMenuCreation
                &&
                effectivelyEditorOnly;
                GameObject displayedMenuFolder =
                disableMenuSelection
                ? null
                : meshEntry.selectedMenuFolder;
                /*
                * 選択済みMenuFolderが後からEditorOnlyになった場合も、
                * 内部の選択情報は保持したまま画面上では
                * 「登録しない」として扱う。
                */
                if (
                    displayedMenuFolder != null
                    &&
                    !IsSelectableMenuFolder(
                    displayedMenuFolder
                    )
                    )
                {
                    displayedMenuFolder =
                    null;
                }
                EditorGUI.BeginDisabledGroup(
                    disableMenuSelection
                    );
                foreach (
                    MenuFolderEntry menuFolder
                    in menuFolders
                    )
                {
                    /*
                    * EditorOnlyのMenuFolderは
                    * 列は残すが選択肢を表示しない。
                    */
                    if (
                        !IsSelectableMenuFolder(
                        menuFolder.gameObject
                        )
                        )
                    {
                        GUILayout.Label(
                            "-",
                            EditorStyles.centeredGreyMiniLabel,
                            GUILayout.Width(
                            MenuFolderColumnWidth
                            )
                            );
                        continue;
                    }
                    bool selected =
                    displayedMenuFolder
                    == menuFolder.gameObject;
                    bool newSelected =
                    GUILayout.Toggle(
                        selected,
                        GUIContent.none,
                        EditorStyles.radioButton,
                        GUILayout.Width(
                        MenuFolderColumnWidth
                        )
                        );
                    if (
                        newSelected
                        &&
                        !selected
                        )
                    {
                        meshEntry.selectedMenuFolder =
                        menuFolder.gameObject;
                    }
                }
                bool doNotRegister =
                displayedMenuFolder == null;
                bool newDoNotRegister =
                GUILayout.Toggle(
                    doNotRegister,
                    GUIContent.none,
                    EditorStyles.radioButton,
                    GUILayout.Width(
                    DoNotRegisterColumnWidth
                    )
                    );
                if (
                    newDoNotRegister
                    &&
                    !doNotRegister
                    )
                {
                    meshEntry.selectedMenuFolder =
                    null;
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            // =========================================================
            // 下部ボタン

            // =========================================================
            private void DrawBottomButtons()
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                bool hasExistingItemTogglers =
                HasExistingItemTogglers();
                bool canOverwrite =
                hasExistingItemTogglers
                &&
                (
                    !autoAdjustToEightItems
                    ||
                    CanOverwriteWithAutoAdjust()
                    );
                EditorGUI.BeginDisabledGroup(
                    !canOverwrite
                    );
                if (GUILayout.Button(
                    "上書き",
                    GUILayout.Width(80)
                    ))
                {
                    OverwriteItemTogglers();
                }
                EditorGUI.EndDisabledGroup();
                /*
                * 有効な登録対象を一度だけ収集し、
                * 0件なら追加ボタンを無効にする。
                */
                List<SelectionGroup> selectionGroups =
                CollectSelectionGroups();
                int selectedItemCount =
                CountSelectedItems(
                    selectionGroups
                    );
                bool canAddWithAutoAdjust =
                !autoAdjustToEightItems
                ||
                CanAddWithAutoAdjust(
                    selectionGroups
                    );
                bool canAdd =
                CanEnableAddButton(
                    selectedItemCount,
                    autoAdjustToEightItems,
                    canAddWithAutoAdjust
                    );
                EditorGUI.BeginDisabledGroup(
                    !canAdd
                    );
                if (GUILayout.Button(
                    "追加",
                    GUILayout.Width(80)
                    ))
                {
                    AddItemTogglers();
                }
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button(
                    "キャンセル",
                    GUILayout.Width(80)
                    ))
                {
                    Close();
                }
                EditorGUILayout.EndHorizontal();
            }

            internal static bool CanEnableAddButton(
                int selectedItemCount,
                bool autoAdjustEnabled,
                bool autoAdjustPossible
                )
            {
                if (selectedItemCount <= 0)
                {
                    return false;
                }
                if (
                    autoAdjustEnabled
                    &&
                    !autoAdjustPossible
                    )
                {
                    return false;
                }
                return true;
            }

            private static int CountSelectedItems(
                List<SelectionGroup> groups
                )
            {
                int count =
                0;
                foreach (
                    SelectionGroup group
                    in groups
                    )
                {
                    count +=
                    group.meshes.Count;
                }
                return count;
            }

            private bool HasExistingItemTogglers()
            {
                if (sourceMenuFolder == null)
                {
                    return false;
                }
                return sourceMenuFolder
                .GetComponentsInChildren
                <ItemToggler>(true)
                .Length > 0;
            }

            private bool CanUseAutoAdjust()
            {
                if (sourceMenuFolder == null)
                {
                    return false;
                }
                int childFolderCount =
                CountDirectManualChildMenuFolders(
                    sourceMenuFolder
                    );
                return MenuPageCalculator
                .CanCreateNextPage(
                    childFolderCount,
                    0
                    );
            }

            private bool CanAddWithAutoAdjust(
                List<SelectionGroup> groups
                )
            {
                foreach (
                    SelectionGroup group
                    in groups
                    )
                {
                    if (
                        !CanAppendWithPagination(
                        group.menuFolder,
                        group.meshes.Count
                        )
                        )
                    {
                        return false;
                    }
                }
                return true;
            }

            private bool CanOverwriteWithAutoAdjust()
            {
                List<SelectionGroup> groups =
                CollectSelectionGroups();
                foreach (
                    SelectionGroup group
                    in groups
                    )
                {
                    if (group.meshes.Count == 0)
                    {
                        continue;
                    }
                    int manualFolderCount =
                    CountDirectManualChildMenuFolders(
                        group.menuFolder
                        );
                    bool canCreate =
                    MenuPageCalculator
                    .TryCreateNewPagePlan(
                        group.meshes.Count,
                        manualFolderCount,
                        0,
                        out _
                        );
                    if (!canCreate)
                    {
                        return false;
                    }
                }
                return true;
            }

            private bool CanAppendWithPagination(
                GameObject targetMenuFolder,
                int itemCount
                )
            {
                if (
                    targetMenuFolder == null
                    ||
                    itemCount <= 0
                    )
                {
                    return true;
                }
                GameObject currentPage =
                targetMenuFolder;
                int remainingItemCount =
                itemCount;
                HashSet<GameObject> visited =
                new HashSet<GameObject>();
                while (remainingItemCount > 0)
                {
                    if (
                        currentPage == null
                        ||
                        !visited.Add(
                        currentPage
                        )
                        )
                    {
                        return false;
                    }
                    GameObject existingNextPage =
                    GetDirectAutoNextPage(
                        currentPage
                        );
                    int childFolderCount =
                    CountDirectManualChildMenuFolders(
                        currentPage
                        );
                    int existingItemCount =
                    CountDirectItemTogglers(
                        currentPage
                        );
                    if (existingNextPage != null)
                    {
                        int availableSlots =
                        MenuPageCalculator
                        .GetAvailableItemSlots(
                            childFolderCount,
                            existingItemCount,
                            true
                            );
                        if (availableSlots < 0)
                        {
                            return false;
                        }
                        int itemCountForPage =
                        Mathf.Min(
                            remainingItemCount,
                            availableSlots
                            );
                        remainingItemCount -=
                        itemCountForPage;
                        if (remainingItemCount <= 0)
                        {
                            return true;
                        }
                        currentPage =
                        existingNextPage;
                        continue;
                    }
                    return MenuPageCalculator
                    .TryCreateNewPagePlan(
                        remainingItemCount,
                        childFolderCount,
                        existingItemCount,
                        out _
                        );
                }
                return true;
            }

            // =========================================================
            // 追加 / 上書き

            // =========================================================
            private void AddItemTogglers()
            {
                if (sourceMenuFolder == null)
                {
                    return;
                }
                /*
                * ボタン状態とは別に実行側でも0件を防止する。
                */
                if (
                    CountSelectedItems(
                    CollectSelectionGroups()
                    ) <= 0
                    )
                {
                    return;
                }
                Undo.IncrementCurrentGroup();
                int undoGroup =
                Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(
                    "ItemTogglerを追加"
                    );
                CreateSelectedItemTogglers();
                ApplyChildFolderOrderToAll();
                Undo.CollapseUndoOperations(
                    undoGroup
                    );
                Close();
            }

            private void OverwriteItemTogglers()
            {
                if (sourceMenuFolder == null)
                {
                    return;
                }
                Undo.IncrementCurrentGroup();
                int undoGroup =
                Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(
                    "ItemTogglerを上書き"
                    );
                DeleteExistingItemTogglers();
                DeleteAutoGeneratedNextPages();
                CreateSelectedItemTogglers();
                ApplyChildFolderOrderToAll();
                Undo.CollapseUndoOperations(
                    undoGroup
                    );
                Close();
            }

            private void CreateSelectedItemTogglers()
            {
                List<SelectionGroup> groups =
                CollectSelectionGroups();
                foreach (
                    SelectionGroup group
                    in groups
                    )
                {
                    if (autoAdjustToEightItems)
                    {
                        CreateItemsWithPagination(
                            group.menuFolder,
                            group.meshes
                            );
                    }
                    else
                    {
                        foreach (
                            GameObject meshObject
                            in group.meshes
                            )
                        {
                            CreateItemToggler(
                                meshObject,
                                group.menuFolder
                                );
                        }
                    }
                }
            }

            private List<SelectionGroup> CollectSelectionGroups()
            {
                List<SelectionGroup> groups =
                new List<SelectionGroup>();
                Dictionary<GameObject, SelectionGroup> lookup =
                new Dictionary<GameObject, SelectionGroup>();
                /*
                * UI側の重複排除に加え、
                * 生成直前にも同一GameObjectを二重登録しない。
                */
                HashSet<GameObject> collectedMeshes =
                new HashSet<GameObject>();
                foreach (
                    OutfitEntry outfit
                    in outfits
                    )
                {
                    foreach (
                        MeshEntry meshEntry
                        in outfit.meshObjects
                        )
                    {
                        if (
                            meshEntry.gameObject == null
                            ||
                            meshEntry.selectedMenuFolder == null
                            )
                        {
                            continue;
                        }
                        if (
                            skipEditorOnlyMenuCreation
                            &&
                            IsEffectivelyEditorOnly(
                            meshEntry.gameObject
                            )
                            )
                        {
                            continue;
                        }
                        if (
                            !IsSelectableMenuFolder(
                            meshEntry.selectedMenuFolder
                            )
                            )
                        {
                            continue;
                        }
                        if (
                            !collectedMeshes.Add(
                            meshEntry.gameObject
                            )
                            )
                        {
                            continue;
                        }
                        GameObject targetFolder =
                        meshEntry.selectedMenuFolder;
                        if (
                            !lookup.TryGetValue(
                            targetFolder,
                            out SelectionGroup group
                            )
                            )
                        {
                            group =
                            new SelectionGroup
                            {
                                menuFolder =
                                targetFolder
                                };
                                lookup.Add(
                                    targetFolder,
                                    group
                                    );
                                groups.Add(
                                    group
                                    );
                            }
                            group.meshes.Add(
                                meshEntry.gameObject
                                );
                        }
                    }
                    return groups;
                }

                // =========================================================
                // ページ分割

                // =========================================================
                private void CreateItemsWithPagination(
                    GameObject targetMenuFolder,
                    List<GameObject> meshes
                    )
                {
                    if (
                        targetMenuFolder == null
                        ||
                        meshes == null
                        ||
                        meshes.Count == 0
                        )
                    {
                        return;
                    }
                    GameObject currentPage =
                    targetMenuFolder;
                    int meshIndex =
                    0;
                    while (meshIndex < meshes.Count)
                    {
                        GameObject existingNextPage =
                        GetDirectAutoNextPage(
                            currentPage
                            );
                        int childFolderCount =
                        CountDirectManualChildMenuFolders(
                            currentPage
                            );
                        int existingItemCount =
                        CountDirectItemTogglers(
                            currentPage
                            );
                        if (existingNextPage != null)
                        {
                            int availableSlots =
                            MenuPageCalculator
                            .GetAvailableItemSlots(
                                childFolderCount,
                                existingItemCount,
                                true
                                );
                            while (
                                availableSlots > 0
                                &&
                                meshIndex < meshes.Count
                                )
                            {
                                CreateItemToggler(
                                    meshes[meshIndex],
                                    currentPage
                                    );
                                meshIndex++;
                                availableSlots--;
                            }
                            if (meshIndex < meshes.Count)
                            {
                                currentPage =
                                existingNextPage;
                            }
                            continue;
                        }
                        int remainingItemCount =
                        meshes.Count
                        - meshIndex;
                        int availableWithoutNextPage =
                        MenuPageCalculator
                        .GetAvailableItemSlots(
                            childFolderCount,
                            existingItemCount,
                            false
                            );
                        if (
                            remainingItemCount
                            <= availableWithoutNextPage
                            )
                        {
                            while (
                                meshIndex < meshes.Count
                                )
                            {
                                CreateItemToggler(
                                    meshes[meshIndex],
                                    currentPage
                                    );
                                meshIndex++;
                            }
                            break;
                        }
                        int availableWithNextPage =
                        MenuPageCalculator
                        .GetAvailableItemSlots(
                            childFolderCount,
                            existingItemCount,
                            true
                            );
                        if (availableWithNextPage < 0)
                        {
                            return;
                        }
                        while (
                            availableWithNextPage > 0
                            &&
                            meshIndex < meshes.Count
                            )
                        {
                            CreateItemToggler(
                                meshes[meshIndex],
                                currentPage
                                );
                            meshIndex++;
                            availableWithNextPage--;
                        }
                        currentPage =
                        CreateAutoNextPage(
                            currentPage
                            );
                    }
                }

                // =========================================================
                // NextPage

                // =========================================================
                private GameObject CreateAutoNextPage(
                    GameObject parentMenuFolder
                    )
                {
                    string nextPageName =
                    FindAvailableNextPageName(
                        parentMenuFolder
                        );
                    GameObject nextPage =
                    new GameObject(
                        nextPageName
                        );
                    Undo.RegisterCreatedObjectUndo(
                        nextPage,
                        "NextPageを作成"
                        );
                    Undo.SetTransformParent(
                        nextPage.transform,
                        parentMenuFolder.transform,
                        "NextPageを配置"
                        );
                    nextPage.transform.localPosition =
                    Vector3.zero;
                    nextPage.transform.localRotation =
                    Quaternion.identity;
                    nextPage.transform.localScale =
                    Vector3.one;
                    MenuFolder menuFolder =
                    Undo.AddComponent<MenuFolder>(
                        nextPage
                        );
                    SerializedObject serializedObject =
                    new SerializedObject(
                        menuFolder
                        );
                    SerializedProperty menuNameProperty =
                    serializedObject.FindProperty(
                        "menuName"
                        );
                    if (menuNameProperty != null)
                    {
                        menuNameProperty.stringValue =
                        nextPageName;
                        serializedObject
                        .ApplyModifiedProperties();
                    }
                    Undo.AddComponent
                    <AutoGeneratedNextPageMarker>(
                        nextPage
                        );
                    return nextPage;
                }

                private static string FindAvailableNextPageName(
                    GameObject parentMenuFolder
                    )
                {
                    int number =
                    1;
                    while (true)
                    {
                        string targetName =
                        number == 1
                        ? "NextPage"
                        : $"NextPage{number}";
                        bool exists =
                        false;
                        foreach (
                            Transform child
                            in parentMenuFolder.transform
                            )
                        {
                            if (child.name == targetName)
                            {
                                exists =
                                true;
                                break;
                            }
                        }
                        if (!exists)
                        {
                            return targetName;
                        }
                        number++;
                    }
                }

                private static GameObject GetDirectAutoNextPage(
                    GameObject menuFolder
                    )
                {
                    if (menuFolder == null)
                    {
                        return null;
                    }
                    foreach (
                        Transform child
                        in menuFolder.transform
                        )
                    {
                        if (
                            child.GetComponent
                            <AutoGeneratedNextPageMarker>()
                            == null
                            )
                        {
                            continue;
                        }
                        if (
                            child.GetComponent<MenuFolder>()
                            == null
                            )
                        {
                            continue;
                        }
                        return child.gameObject;
                    }
                    return null;
                }

                // =========================================================
                // Menu数

                // =========================================================
                private static int CountDirectManualChildMenuFolders(
                    GameObject menuFolder
                    )
                {
                    if (menuFolder == null)
                    {
                        return 0;
                    }
                    int count =
                    0;
                    foreach (
                        Transform child
                        in menuFolder.transform
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
                        /*
                        * EditorOnlyのMenuFolderは
                        * VRChat側のメニュー項目として使用しないため
                        * 8項目制限の計算にも含めない。
                        */
                        if (
                            IsEffectivelyEditorOnly(
                            child.gameObject
                            )
                            )
                        {
                            continue;
                        }
                        count++;
                    }
                    return count;
                }

                private static int CountDirectItemTogglers(
                    GameObject menuFolder
                    )
                {
                    if (menuFolder == null)
                    {
                        return 0;
                    }
                    int count =
                    0;
                    foreach (
                        Transform child
                        in menuFolder.transform
                        )
                    {
                        if (
                            child.GetComponent<ItemToggler>()
                            != null
                            )
                        {
                            count++;
                        }
                    }
                    return count;
                }

                // =========================================================
                // 子MenuFolder順

                // =========================================================
                private void ApplyChildFolderOrderToAll()
                {
                    if (sourceMenuFolder == null)
                    {
                        return;
                    }
                    MenuFolder[] folders =
                    sourceMenuFolder
                    .GetComponentsInChildren
                    <MenuFolder>(true);
                    foreach (
                        MenuFolder folder
                        in folders
                        )
                    {
                        if (folder == null)
                        {
                            continue;
                        }
                        ApplyChildFolderOrder(
                            folder.gameObject
                            );
                    }
                }

                private void ApplyChildFolderOrder(
                    GameObject menuFolder
                    )
                {
                    List<Transform> allChildren =
                    new List<Transform>();
                    List<Transform> manualFolders =
                    new List<Transform>();
                    List<Transform> itemTogglers =
                    new List<Transform>();
                    List<Transform> autoNextPages =
                    new List<Transform>();
                    foreach (
                        Transform child
                        in menuFolder.transform
                        )
                    {
                        allChildren.Add(
                            child
                            );
                        if (
                            child.GetComponent
                            <AutoGeneratedNextPageMarker>()
                            != null
                            &&
                            child.GetComponent<MenuFolder>()
                            != null
                            )
                        {
                            autoNextPages.Add(
                                child
                                );
                            continue;
                        }
                        if (
                            child.GetComponent<MenuFolder>()
                            != null
                            )
                        {
                            manualFolders.Add(
                                child
                                );
                            continue;
                        }
                        if (
                            child.GetComponent<ItemToggler>()
                            != null
                            )
                        {
                            itemTogglers.Add(
                                child
                                );
                        }
                    }
                    List<Transform> orderedMenuChildren =
                    new List<Transform>();
                    if (
                        childFolderPosition
                        == ChildFolderPosition.Top
                        )
                    {
                        orderedMenuChildren.AddRange(
                            manualFolders
                            );
                        orderedMenuChildren.AddRange(
                            itemTogglers
                            );
                    }
                    else
                    {
                        orderedMenuChildren.AddRange(
                            itemTogglers
                            );
                        orderedMenuChildren.AddRange(
                            manualFolders
                            );
                    }
                    // NextPageは常に末尾
                    orderedMenuChildren.AddRange(
                        autoNextPages
                        );
                    List<int> menuSlots =
                    new List<int>();
                    for (
                        int i = 0;
                        i < allChildren.Count;
                        i++
                        )
                    {
                        Transform child =
                        allChildren[i];
                        bool isMenuChild =
                        child.GetComponent<MenuFolder>()
                        != null
                        ||
                        child.GetComponent<ItemToggler>()
                        != null;
                        if (isMenuChild)
                        {
                            menuSlots.Add(
                                i
                                );
                        }
                    }
                    if (
                        menuSlots.Count
                        != orderedMenuChildren.Count
                        )
                    {
                        return;
                    }
                    List<Transform> desiredOrder =
                    new List<Transform>(
                        allChildren
                        );
                    for (
                        int i = 0;
                        i < menuSlots.Count;
                        i++
                        )
                    {
                        desiredOrder[
                        menuSlots[i]
                        ] =
                        orderedMenuChildren[i];
                    }
                    bool needsReorder =
                    false;
                    for (
                        int i = 0;
                        i < desiredOrder.Count;
                        i++
                        )
                    {
                        if (
                            desiredOrder[i]
                            .GetSiblingIndex()
                            != i
                            )
                        {
                            needsReorder =
                            true;
                            break;
                        }
                    }
                    if (!needsReorder)
                    {
                        return;
                    }
                    Undo.RegisterFullObjectHierarchyUndo(
                        menuFolder,
                        "Menu項目の並び順を変更"
                        );
                    for (
                        int i = 0;
                        i < desiredOrder.Count;
                        i++
                        )
                    {
                        desiredOrder[i]
                        .SetSiblingIndex(i);
                    }
                }

                // =========================================================
                // 上書き削除

                // =========================================================
                private void DeleteExistingItemTogglers()
                {
                    ItemToggler[] itemTogglers =
                    sourceMenuFolder
                    .GetComponentsInChildren
                    <ItemToggler>(true);
                    foreach (
                        ItemToggler itemToggler
                        in itemTogglers
                        )
                    {
                        if (itemToggler == null)
                        {
                            continue;
                        }
                        if (
                            CanDeleteItemTogglerGameObject(
                            itemToggler
                            )
                            )
                        {
                            Undo.DestroyObjectImmediate(
                                itemToggler.gameObject
                                );
                        }
                        else
                        {
                            Undo.DestroyObjectImmediate(
                                itemToggler
                                );
                        }
                    }
                }

                private static bool CanDeleteItemTogglerGameObject(
                    ItemToggler itemToggler
                    )
                {
                    if (itemToggler == null)
                    {
                        return false;
                    }
                    GameObject targetObject =
                    itemToggler.gameObject;
                    if (targetObject == null)
                    {
                        return false;
                    }
                    if (
                        targetObject.transform.childCount
                        > 0
                        )
                    {
                        return false;
                    }
                    Component[] components =
                    targetObject.GetComponents
                    <Component>();
                    foreach (
                        Component component
                        in components
                        )
                    {
                        if (component == null)
                        {
                            return false;
                        }
                        if (component is Transform)
                        {
                            continue;
                        }
                        if (component == itemToggler)
                        {
                            continue;
                        }
                        return false;
                    }
                    return true;
                }

                private void DeleteAutoGeneratedNextPages()
                {
                    AutoGeneratedNextPageMarker[] markers =
                    sourceMenuFolder
                    .GetComponentsInChildren
                    <AutoGeneratedNextPageMarker>(true);
                    foreach (
                        AutoGeneratedNextPageMarker marker
                        in markers
                        )
                    {
                        if (marker == null)
                        {
                            continue;
                        }
                        Undo.DestroyObjectImmediate(
                            marker.gameObject
                            );
                    }
                }

                // =========================================================
                // ItemToggler生成

                // =========================================================
                private void CreateItemToggler(
                    GameObject meshObject,
                    GameObject targetMenuFolder
                    )
                {
                    GameObject togglerObject =
                    new GameObject(
                        meshObject.name
                        );
                    Undo.RegisterCreatedObjectUndo(
                        togglerObject,
                        "ItemTogglerを作成"
                        );
                    Undo.SetTransformParent(
                        togglerObject.transform,
                        targetMenuFolder.transform,
                        "ItemTogglerを配置"
                        );
                    togglerObject.transform.localPosition =
                    Vector3.zero;
                    togglerObject.transform.localRotation =
                    Quaternion.identity;
                    togglerObject.transform.localScale =
                    Vector3.one;
                    ItemToggler itemToggler =
                    Undo.AddComponent<ItemToggler>(
                        togglerObject
                        );
                    SerializedObject serializedObject =
                    new SerializedObject(
                        itemToggler
                        );
                    bool initiallyActive =
                    meshObject.activeSelf;
                    SerializedProperty defaultValueProperty =
                    serializedObject.FindProperty(
                        "defaultValue"
                        );
                    if (initiallyActive)
                    {
                        defaultValueProperty.boolValue =
                        setupMode
                        == ItemTogglerSetupMode.HideWhenOff;
                    }
                    else
                    {
                        defaultValueProperty.boolValue =
                        setupMode
                        == ItemTogglerSetupMode.HideWhenOn;
                    }
                    SerializedProperty parameterProperty =
                    serializedObject.FindProperty(
                        "parameter"
                        );
                    SerializedProperty objectsProperty =
                    parameterProperty
                    .FindPropertyRelative(
                        "objects"
                        );
                    objectsProperty.arraySize =
                    1;
                    SerializedProperty objectElement =
                    objectsProperty
                    .GetArrayElementAtIndex(
                        0
                        );
                    SerializedProperty objectProperty =
                    objectElement
                    .FindPropertyRelative(
                        "obj"
                        );
                    SerializedProperty valueProperty =
                    objectElement
                    .FindPropertyRelative(
                        "value"
                        );
                    objectProperty.objectReferenceValue =
                    meshObject;
                    /*
                    * Active初期:
                    *   default clip = 表示
                    *
                    * Inactive初期:
                    *   default clip = 非表示
                    */
                    valueProperty.boolValue =
                    !initiallyActive;
                    serializedObject
                    .ApplyModifiedProperties();
                }

                // =========================================================
                // Mesh一覧

                // =========================================================
                private void RefreshAllMeshLists()
                {
                    /*
                    * 再走査によって既存のMenuFolder選択が失われないよう、
                    * GameObjectごとに選択先を保存しておく。
                    */
                    Dictionary<GameObject, GameObject>
                    previousSelections =
                    new Dictionary<GameObject, GameObject>();
                    foreach (
                        OutfitEntry outfit
                        in outfits
                        )
                    {
                        foreach (
                            MeshEntry meshEntry
                            in outfit.meshObjects
                            )
                        {
                            if (meshEntry.gameObject == null)
                            {
                                continue;
                            }
                            if (
                                previousSelections.ContainsKey(
                                meshEntry.gameObject
                                )
                                )
                            {
                                continue;
                            }
                            previousSelections.Add(
                                meshEntry.gameObject,
                                meshEntry.selectedMenuFolder
                                );
                        }
                    }
                    List<GameObject> roots =
                    new List<GameObject>();
                    foreach (
                        OutfitEntry outfit
                        in outfits
                        )
                    {
                        roots.Add(
                            outfit.root
                            );
                    }
                    List<List<GameObject>> uniqueMeshLists =
                    BuildUniqueMeshLists(
                        roots
                        );
                    for (
                        int i = 0;
                        i < outfits.Count;
                        i++
                        )
                    {
                        OutfitEntry outfit =
                        outfits[i];
                        outfit.meshObjects.Clear();
                        foreach (
                            GameObject meshObject
                            in uniqueMeshLists[i]
                            )
                        {
                            GameObject selectedMenuFolder =
                            null;
                            previousSelections.TryGetValue(
                                meshObject,
                                out selectedMenuFolder
                                );
                            outfit.meshObjects.Add(
                                new MeshEntry
                            {
                                    gameObject =
                                    meshObject,
                                    selectedMenuFolder =
                                    selectedMenuFolder
                            }
                                );
                        }
                    }
                }
                /*
                * 複数Rootが同じMeshを含む場合、
                * outfitsの上から順に最初に見つかったRootへだけ所属させる。
                */
                internal static List<List<GameObject>>
                BuildUniqueMeshLists(
                    IList<GameObject> roots
                    )
                {
                    List<List<GameObject>> result =
                    new List<List<GameObject>>();
                    HashSet<GameObject> seenMeshes =
                    new HashSet<GameObject>();
                    foreach (
                        GameObject root
                        in roots
                        )
                    {
                        List<GameObject> meshes =
                        new List<GameObject>();
                        result.Add(
                            meshes
                            );
                        if (root == null)
                        {
                            continue;
                        }
                        Transform[] transforms =
                        root.GetComponentsInChildren
                        <Transform>(true);
                        foreach (
                            Transform targetTransform
                            in transforms
                            )
                        {
                            GameObject targetObject =
                            targetTransform.gameObject;
                            if (
                                !HasMesh(
                                targetObject
                                )
                                )
                            {
                                continue;
                            }
                            if (
                                !seenMeshes.Add(
                                targetObject
                                )
                                )
                            {
                                continue;
                            }
                            meshes.Add(
                                targetObject
                                );
                        }
                    }
                    return result;
                }

                private static bool HasMesh(
                    GameObject targetObject
                    )
                {
                    if (
                        targetObject.GetComponent
                        <SkinnedMeshRenderer>()
                        != null
                        )
                    {
                        return true;
                    }
                    MeshRenderer meshRenderer =
                    targetObject.GetComponent
                    <MeshRenderer>();
                    MeshFilter meshFilter =
                    targetObject.GetComponent
                    <MeshFilter>();
                    return meshRenderer != null
                    &&
                    meshFilter != null;
                }

                // =========================================================
                // MenuFolder

                // =========================================================
                private void RefreshMenuFolderList()
                {
                    menuFolders.Clear();
                    if (sourceMenuFolder == null)
                    {
                        return;
                    }
                    MenuFolder[] foundFolders =
                    sourceMenuFolder
                    .GetComponentsInChildren
                    <MenuFolder>(true);
                    Dictionary<string, int> nameCounts =
                    new Dictionary<string, int>();
                    foreach (
                        MenuFolder folder
                        in foundFolders
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
                        in foundFolders
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
                        GameObject folderObject =
                        folder.gameObject;
                        string displayName;
                        if (
                            folderObject
                            == sourceMenuFolder
                            )
                        {
                            displayName =
                            folderObject.name;
                        }
                        else if (
                            nameCounts[
                            folderObject.name
                            ] > 1
                            )
                        {
                            displayName =
                            GetRelativePath(
                                sourceMenuFolder.transform,
                                folderObject.transform
                                );
                        }
                        else
                        {
                            displayName =
                            folderObject.name;
                        }
                        menuFolders.Add(
                            new MenuFolderEntry
                        {
                                gameObject =
                                folderObject,
                                displayName =
                                displayName
                        }
                            );
                    }
                }

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
                /*
                * MenuFolderは列としては残すが、
                * EditorOnlyなら登録先には使用できない。
                */
                internal static bool IsSelectableMenuFolder(
                    GameObject menuFolder
                    )
                {
                    if (menuFolder == null)
                    {
                        return false;
                    }
                    if (
                        menuFolder.GetComponent<MenuFolder>()
                        == null
                        )
                    {
                        return false;
                    }
                    return !IsEffectivelyEditorOnly(
                        menuFolder
                        );
                }

                // =========================================================
                // EditorOnly

                // =========================================================
                private static bool IsEffectivelyEditorOnly(
                    GameObject targetObject
                    )
                {
                    if (targetObject == null)
                    {
                        return false;
                    }
                    if (
                        targetObject.CompareTag(
                        "EditorOnly"
                        )
                        )
                    {
                        return true;
                    }
                    return HasEditorOnlyAncestor(
                        targetObject
                        );
                }

                private static bool HasEditorOnlyAncestor(
                    GameObject targetObject
                    )
                {
                    if (targetObject == null)
                    {
                        return false;
                    }
                    Transform current =
                    targetObject.transform.parent;
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
                // 選択

                // =========================================================
                private static void SelectMeshObject(
                    GameObject meshObject
                    )
                {
                    if (meshObject == null)
                    {
                        return;
                    }
                    Selection.activeGameObject =
                    meshObject;
                    EditorGUIUtility.PingObject(
                        meshObject
                        );
                    SceneView.RepaintAll();
                }
            }
        }
