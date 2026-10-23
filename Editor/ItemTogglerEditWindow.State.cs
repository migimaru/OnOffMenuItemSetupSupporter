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
        private void ResetChanges()
        {
            RestoreImmediateChangesToSnapshot();
            // Reset per-Mesh settings without changing the user's outfit list.
            foreach (EditMeshEntry entry in displayEntries)
            {
                entry.selectedMenuFolder = entry.snapshotMenuFolder;
                entry.lastNonEditorOnlyTag = entry.snapshotTag == "EditorOnly" ? "Untagged" : entry.snapshotTag;
            }

            Repaint();
        }

        private int CountDirtyChanges()
        {
            int count = 0;
            HashSet<GameObject> checkedObjects = new HashSet<GameObject>();
            foreach (ExistingGroup group in existingGroups)
            {
                if (group.entries.Count > 1 && !group.maintainMultiple)
                {
                    count++;
                }
            }

            foreach (EditMeshEntry entry in displayEntries)
            {
                if (entry.selectedMenuFolder != entry.snapshotMenuFolder)
                {
                    count++;
                }

                if (entry.gameObject == null || !checkedObjects.Add(entry.gameObject))
                {
                    continue;
                }

                if (entry.gameObject.activeSelf != entry.snapshotActive)
                {
                    count++;
                }

                if (entry.gameObject.tag != entry.snapshotTag)
                {
                    count++;
                }
            }

            return count;
        }

        private void RestoreImmediateChangesToSnapshot()
        {
            HashSet<GameObject> restored = new HashSet<GameObject>();
            foreach (EditMeshEntry entry in displayEntries)
            {
                GameObject target = entry.gameObject;
                if (target == null || !restored.Add(target))
                {
                    continue;
                }

                Undo.RecordObject(target, "編集内容を破棄");
                target.SetActive(entry.snapshotActive);
                if (target.tag != entry.snapshotTag)
                {
                    target.tag = entry.snapshotTag;
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                EditorUtility.SetDirty(target);
            }
        }

        private WorkspaceViewState CaptureViewState()
        {
            var state = new WorkspaceViewState
            {
                sourceMenuFolder = sourceMenuFolder,
                setupMode = newItemSetupMode,
                includeChildMenuFolders = includeChildMenuFolders,
                overwriteMode = overwriteMode,
                skipEditorOnly = skipEditorOnlyMenuCreation,
                autoAdjust = autoAdjustToEightItems,
                foldersAtTop = childFoldersAtTop,
                scrollPosition = scrollPosition,
                displayColumnWidth = displayColumnWidth,
                editorOnlyColumnWidth = editorOnlyColumnWidth,
                meshNameColumnWidth = meshNameColumnWidth,
                menuFolderColumnWidth = menuFolderColumnWidth,
                doNotRegisterColumnWidth = doNotRegisterColumnWidth
            };
            state.outfitRoots.AddRange(outfitRoots);
            foreach (var entry in existingGroups.SelectMany(group => group.entries).Except(displayEntries))
                state.omittedMeshes.Add(sceneHistory.GetId(entry.gameObject));
            foreach (var group in existingGroups)
                if (group.toggler != null)
                    state.groupMaintain[sceneHistory.GetId(group.toggler.gameObject)] = group.maintainMultiple;
            foreach (var entry in displayEntries)
            {
                int meshId = sceneHistory.GetId(entry.gameObject);
                state.entryOrder.Add(meshId);
                state.meshOutfits[meshId] = sceneHistory.GetId(entry.outfitRoot);
                string key = GetEntryKey(entry);
                state.selectedFolders[key] = sceneHistory.GetId(entry.selectedMenuFolder);
                state.snapshotFolders[key] = sceneHistory.GetId(entry.snapshotMenuFolder);
                if (entry.gameObject == null)
                    continue;
                int id = sceneHistory.GetId(entry.gameObject);
                state.snapshotActive[id] = entry.snapshotActive;
                state.snapshotTag[id] = entry.snapshotTag;
                state.activeValues[id] = entry.gameObject.activeSelf;
                state.tagValues[id] = entry.gameObject.tag;
            }

            return state;
        }

        private void ApplyViewState(WorkspaceViewState state)
        {
            includeChildMenuFolders = state.includeChildMenuFolders;
            skipEditorOnlyMenuCreation = state.skipEditorOnly;
            autoAdjustToEightItems = state.autoAdjust;
            childFoldersAtTop = state.foldersAtTop;
            scrollPosition = state.scrollPosition;
            var omitted = displayEntries.Where(entry => state.omittedMeshes.Contains(sceneHistory.GetId(entry.gameObject))).ToArray();
            foreach (var entry in omitted)
            {
                entry.selectedMenuFolder = null;
                displayEntries.Remove(entry);
            }
            outfitRoots.RemoveAll(root => !state.outfitRoots.Contains(root) && omitted.Any(entry => entry.outfitRoot == root));
            var roots = state.outfitRoots.Where(root => outfitRoots.Contains(root)).ToList();
            roots.AddRange(outfitRoots.Where(root => !roots.Contains(root)));
            outfitRoots.Clear();
            outfitRoots.AddRange(roots);
            foreach (var group in existingGroups)
                if (group.toggler != null && state.groupMaintain.TryGetValue(sceneHistory.GetId(group.toggler.gameObject), out bool maintain))
                    group.maintainMultiple = maintain;
            foreach (var entry in displayEntries)
            {
                if (state.meshOutfits.TryGetValue(sceneHistory.GetId(entry.gameObject), out int rootId))
                    entry.outfitRoot = sceneHistory.Resolve(rootId);
                string key = GetEntryKey(entry);
                if (state.selectedFolders.TryGetValue(key, out int selected))
                    entry.selectedMenuFolder = sceneHistory.Resolve(selected);
                if (state.snapshotFolders.TryGetValue(key, out int original))
                    entry.snapshotMenuFolder = sceneHistory.Resolve(original);
                int id = sceneHistory.GetId(entry.gameObject);
                if (state.snapshotActive.TryGetValue(id, out bool active))
                    entry.snapshotActive = active;
                if (state.snapshotTag.TryGetValue(id, out string tag))
                    entry.snapshotTag = tag;
                if (entry.gameObject == null)
                    continue;
                Undo.RecordObject(entry.gameObject, "編集中の表示を復元");
                if (state.activeValues.TryGetValue(id, out bool currentActive))
                    entry.gameObject.SetActive(currentActive);
                if (state.tagValues.TryGetValue(id, out string currentTag))
                    entry.gameObject.tag = currentTag;
                PrefabUtility.RecordPrefabInstancePropertyModifications(entry.gameObject);
            }

            var ordered = displayEntries.OrderBy(entry =>
            {
                int index = state.entryOrder.IndexOf(sceneHistory.GetId(entry.gameObject));
                return index >= 0 ? index : int.MaxValue;
            }).ToArray();
            displayEntries.Clear();
            displayEntries.AddRange(ordered);
            SortEntriesByOutfit();

            Repaint();
        }

        private string GetEntryKey(EditMeshEntry entry)
        {
            return entry.group != null ? $"G:{sceneHistory.GetId(entry.group.toggler == null ? null : entry.group.toggler.gameObject)}:{entry.objectIndex}" : $"N:{sceneHistory.GetId(entry.gameObject)}";
        }
    }
}
