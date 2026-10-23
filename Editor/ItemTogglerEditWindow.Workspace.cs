using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using jp.lilxyzw.lilycalinventory.runtime;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    public sealed partial class ItemTogglerEditWindow
    {
        [SerializeField]
        private bool overwriteMode;
        [SerializeField]
        private bool skipEditorOnlyMenuCreation = true;
        [SerializeField]
        private bool autoAdjustToEightItems = true;
        [SerializeField]
        private bool childFoldersAtTop = true;
        [SerializeField]
        private List<GameObject> outfitRoots = new List<GameObject>();
        private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        private MenuSceneHistory sceneHistory;
        private WorkspaceViewState baselineView;
        private bool applyingTransaction;
        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnExternalUndoRedo;
            EditorApplication.delayCall += InitializeAfterReload;
        }

        private void OnDisable()
        {
            CancelTableDrag();
            Undo.undoRedoPerformed -= OnExternalUndoRedo;
            EditorApplication.delayCall -= InitializeAfterReload;
            if (initialized && !closeAccepted)
                RestoreImmediateChangesToSnapshot();
            initialized = false;
            checkpoints.Clear();
        }

        private void InitializeAfterReload()
        {
            if (this == null || initialized || sourceMenuFolder == null)
                return;
            sceneHistory = new MenuSceneHistory(sourceMenuFolder);
            RebuildFromHierarchy();
            baselineView = CaptureViewState();
        }

        private void OnExternalUndoRedo()
        {
            if (applyingTransaction || sourceMenuFolder == null)
                return;
            // Native Undo can recreate components independently of our checkpoints.
            // Restart the local history from that scene state rather than mixing both histories.
            checkpoints.Clear();
            sceneHistory = new MenuSceneHistory(sourceMenuFolder);
            RebuildFromHierarchy();
            foreach (var entry in displayEntries)
            {
                if (overwriteMode)
                    entry.selectedMenuFolder = GetRelevantItemTogglers().FirstOrDefault(toggler => ReadTargets(toggler).Contains(entry.gameObject))is ItemToggler toggler ? ResolveLogicalMenuFolder(toggler) : null;
                entry.snapshotMenuFolder = entry.selectedMenuFolder;
                InitializeObjectSnapshot(entry);
            }

            baselineView = CaptureViewState();
        }

        private sealed class Checkpoint
        {
            internal MenuSceneHistory.Snapshot before;
            internal Dictionary<int, MenuSceneHistory.Node> changed;
            internal WorkspaceViewState view;
        }

        internal static void OpenWorkspace(GameObject folder, ItemTogglerSetupMode mode, bool overwrite)
        {
            if (folder == null || folder.GetComponent<MenuFolder>() == null || EditorUtility.IsPersistent(folder) || !folder.scene.IsValid())
                return;
            var window = CreateInstance<ItemTogglerEditWindow>();
            window.sourceMenuFolder = folder;
            window.newItemSetupMode = mode;
            window.overwriteMode = overwrite;
            window.includeChildMenuFolders = overwrite;
            window.titleContent = new GUIContent(overwrite ? "ON/OFFメニュー設定支援" : "ON/OFFメニュー編集");
            window.minSize = new Vector2(350, 400);
            window.sceneHistory = new MenuSceneHistory(folder);
            window.RebuildFromHierarchy();
            window.baselineView = window.CaptureViewState();
            window.Show();
        }

        private void DrawAddArea()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("衣装", EditorStyles.boldLabel, GUILayout.Width(35));
                var area = GUILayoutUtility.GetRect(80, 44, GUILayout.ExpandWidth(true));
                var style = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    padding = new RectOffset(6, 6, 4, 4)
                };
                style.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                EditorGUI.DrawRect(area, new Color(0.13f, 0.13f, 0.13f));
                GUI.Label(area, "Hierarchy上の衣装をここにドラッグ＆ドロップ", style);
                HandleOutfitDrop(area);
                if (GUILayout.Button("+", GUILayout.Width(28), GUILayout.Height(30)))
                    outfitRoots.Add(null);
            }
        }

        private void HandleOutfitDrop(Rect area)
        {
            var current = Event.current;
            if (!area.Contains(current.mousePosition) || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
                return;
            var roots = DragAndDrop.objectReferences.OfType<GameObject>().Where(IsValidOutfitRoot).ToList();
            DragAndDrop.visualMode = roots.Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (current.type == EventType.DragPerform && roots.Count > 0)
            {
                DragAndDrop.AcceptDrag();
                foreach (var root in roots)
                    AddOutfitRoot(root);
            }

            current.Use();
        }

        private bool IsValidOutfitRoot(GameObject root)
        {
            return root != null && !EditorUtility.IsPersistent(root) && root.scene.IsValid() && root.transform.IsChildOf(GetAvatarRootObject().transform);
        }

        private void AddOutfitRoot(GameObject root)
        {
            if (!IsValidOutfitRoot(root) || outfitRoots.Contains(root))
                return;
            int empty = outfitRoots.FindIndex(item => item == null);
            if (empty >= 0)
                outfitRoots[empty] = root;
            else
                outfitRoots.Add(root);
            BuildGlobalTargetRegistrationMap();
            AddMeshesFromRoot(root);
            SortEntriesByOutfit();
            Repaint();
        }

        private void AddMeshesFromRoot(GameObject root)
        {
            if (root == null)
                return;
            var displayed = new HashSet<GameObject>(displayEntries.Select(entry => entry.gameObject));
            var relevant = new HashSet<ItemToggler>(GetRelevantItemTogglers());
            foreach (var mesh in ItemTogglerSetupWindow.BuildUniqueMeshLists(new List<GameObject> { root })[0])
            {
                if (!displayed.Add(mesh))
                    continue;
                if (allTargetRegistrations.TryGetValue(mesh, out var registrations) && (!overwriteMode || registrations.Any(toggler => !relevant.Contains(toggler))))
                    continue;
                var entry = CreateUnregisteredEntry(mesh);
                entry.outfitRoot = root;
                displayEntries.Add(entry);
            }
        }

        private GameObject FindOutfitRoot(GameObject mesh)
        {
            var prefab = FindTargetPrefabRoot(mesh);
            if (prefab != null)
                return prefab;
            var avatar = GetAvatarRootObject().transform;
            var current = mesh.transform;
            while (current.parent != null && current.parent != avatar)
                current = current.parent;
            return current == avatar ? mesh : current.gameObject;
        }

        private void RefreshLoadedMeshes()
        {
            var retained = new HashSet<GameObject>(outfitRoots.Where(root => root != null).SelectMany(root => ItemTogglerSetupWindow.BuildUniqueMeshLists(new List<GameObject> { root })[0]));
            foreach (var entry in displayEntries.Where(entry => !retained.Contains(entry.gameObject)).ToArray())
            {
                RestoreTarget(entry);
                entry.selectedMenuFolder = null;
                displayEntries.Remove(entry);
            }
            foreach (var entry in displayEntries)
                if (!outfitRoots.Contains(entry.outfitRoot))
                    entry.outfitRoot = outfitRoots.FirstOrDefault(root => root != null && entry.gameObject.transform.IsChildOf(root.transform));

            BuildGlobalTargetRegistrationMap();
            foreach (var root in outfitRoots)
                AddMeshesFromRoot(root);
            SortEntriesByOutfit();
        }

        private void DrawWorkspaceOptions()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawScopeOption();
                skipEditorOnlyMenuCreation = EditorGUILayout.ToggleLeft(new GUIContent("EditorOnlyのMeshにはメニューを作成しない", "Mesh自身、または親がEditorOnlyなら除外します。MenuFolderのEditorOnly判定はこのチェックとは別です。"), skipEditorOnlyMenuCreation);
                autoAdjustToEightItems = EditorGUILayout.ToggleLeft("8項目以内に自動調整", autoAdjustToEightItems);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("子MenuFolderの位置", GUILayout.Width(140));
                    if (GUILayout.Toggle(childFoldersAtTop, "上 ▲", EditorStyles.miniButtonLeft, GUILayout.Width(55)))
                        childFoldersAtTop = true;
                    if (GUILayout.Toggle(!childFoldersAtTop, "下 ▼", EditorStyles.miniButtonRight, GUILayout.Width(55)))
                        childFoldersAtTop = false;
                }
            }

        }

        private void DrawHistoryButton()
        {
            using (new EditorGUI.DisabledScope(checkpoints.Count < 2))
                if (GUILayout.Button("一つ前に戻す", GUILayout.Width(110), GUILayout.Height(25)))
                {
                    RestorePreviousCheckpoint();
                    GUIUtility.ExitGUI();
                }
        }

        private void DrawWorkspaceActions()
        {
            bool emptyOverwrite = overwriteMode && CountSelectedMeshes() == 0;
            using (new EditorGUI.DisabledScope(emptyOverwrite || !HasWorkspaceChanges()))
            {
                if (GUILayout.Button("変更リセット", GUILayout.Width(94), GUILayout.Height(25)))
                {
                    ResetChanges();
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("変更適用", GUILayout.Width(78), GUILayout.Height(25)))
                {
                    ApplyWorkspaceChanges();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("完了", GUILayout.Width(46), GUILayout.Height(25)))
            {
                if (CompleteChanges())
                {
                    closeAccepted = true;
                    Close();
                }

                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("キャンセル", GUILayout.Width(76), GUILayout.Height(25)))
            {
                RequestClose();
                GUIUtility.ExitGUI();
            }
        }

        private GameObject EffectiveFolder(EditMeshEntry entry)
        {
            if (entry.gameObject == null)
                return null;
            if (entry.group != null && entry.group.structuralLocked)
                return entry.snapshotMenuFolder;
            if (skipEditorOnlyMenuCreation && (entry.gameObject.CompareTag("EditorOnly") || HasEditorOnlyAncestor(entry.gameObject)))
                return null;
            return entry.selectedMenuFolder != null && IsLogicalFolderInScope(entry.selectedMenuFolder) && ItemTogglerSetupWindow.IsSelectableMenuFolder(entry.selectedMenuFolder) ? entry.selectedMenuFolder : null;
        }

        private int CountSelectedMeshes() => displayEntries.Where(entry => EffectiveFolder(entry) != null).Select(entry => entry.gameObject).Distinct().Count();
        private bool HasWorkspaceChanges()
        {
            if (CountDirtyChanges() > 0)
                return true;
            if (baselineView == null)
                return false;
            return includeChildMenuFolders != baselineView.includeChildMenuFolders || skipEditorOnlyMenuCreation != baselineView.skipEditorOnly || autoAdjustToEightItems != baselineView.autoAdjust || childFoldersAtTop != baselineView.foldersAtTop || HasOrderChanges();
        }
    }
}
