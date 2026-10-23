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
        private void EnsureInitialCheckpoint()
        {
            if (checkpoints.Count != 0)
                return;
            checkpoints.Add(new Checkpoint { view = baselineView ?? CaptureViewState() });
        }

        private void RestorePreviousCheckpoint()
        {
            if (checkpoints.Count < 2)
                return;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            applyingTransaction = true;
            try
            {
                if (HasWorkspaceChanges())
                {
                    RestoreImmediateChangesToSnapshot();
                    RestoreView(checkpoints[checkpoints.Count - 1].view);
                }
                else
                {
                    var applied = checkpoints[checkpoints.Count - 1];
                    sceneHistory.Restore(applied.before, applied.changed);
                    checkpoints.RemoveAt(checkpoints.Count - 1);
                    RestoreView(checkpoints[checkpoints.Count - 1].view);
                }
            }
            finally
            {
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(undoGroup);
                Undo.IncrementCurrentGroup();
                applyingTransaction = false;
            }
        }

        private void RestoreView(WorkspaceViewState state)
        {
            includeChildMenuFolders = state.includeChildMenuFolders;
            outfitRoots.Clear();
            outfitRoots.AddRange(state.outfitRoots);
            RebuildFromHierarchy();
            ApplyViewState(state);
            baselineView = state;
        }

        private static void RestoreTarget(EditMeshEntry entry)
        {
            if (entry.gameObject == null)
                return;
            Undo.RecordObject(entry.gameObject, "未適用の表示変更を戻す");
            entry.gameObject.SetActive(entry.snapshotActive);
            entry.gameObject.tag = entry.snapshotTag;
            PrefabUtility.RecordPrefabInstancePropertyModifications(entry.gameObject);
        }
    }
}
