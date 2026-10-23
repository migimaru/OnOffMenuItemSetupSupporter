using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using jp.lilxyzw.lilycalinventory.runtime;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    public sealed partial class ItemTogglerEditWindow
    {
        private readonly Dictionary<EditMeshEntry, ItemToggler> createdItems = new Dictionary<EditMeshEntry, ItemToggler>();

        private IEnumerable<EditMeshEntry> EntriesForOutfit(GameObject root)
        {
            return displayEntries.Where(entry => entry.outfitRoot == root && entry.gameObject != null);
        }

        private void SortEntriesByOutfit()
        {
            var ordered = outfitRoots.SelectMany(EntriesForOutfit).ToList();
            ordered.AddRange(displayEntries.Where(entry => !ordered.Contains(entry)));
            displayEntries.Clear();
            displayEntries.AddRange(ordered);
        }

        private bool HasOrderChanges()
        {
            return baselineView != null &&
                (!outfitRoots.SequenceEqual(baselineView.outfitRoots) ||
                 !displayEntries.Select(entry => sceneHistory.GetId(entry.gameObject)).SequenceEqual(baselineView.entryOrder));
        }

        private bool CanReorderEntry(EditMeshEntry entry)
        {
            if (entry.group == null) return true;
            return !entry.group.structuralLocked && CanSafelyMoveWholeToggler(entry.group.toggler) &&
                entry.group.logicalMenuFolder != null && entry.group.toggler.transform.IsChildOf(entry.group.logicalMenuFolder.transform) &&
                entry.group.entries.Select(item => item.outfitRoot).Distinct().Count() <= 1;
        }

        private bool MoveOutfit(int from, int to)
        {
            if (from < 0 || to < 0 || from >= outfitRoots.Count || to >= outfitRoots.Count || from == to)
                return false;
            // Locked menu objects stay as anchors; a drag must not cross them either.
            for (int index = Mathf.Min(from, to); index <= Mathf.Max(from, to); index++)
                if (EntriesForOutfit(outfitRoots[index]).Any(entry => !CanReorderEntry(entry))) return false;
            var root = outfitRoots[from];
            outfitRoots.RemoveAt(from);
            outfitRoots.Insert(to, root);
            SortEntriesByOutfit();
            Repaint();
            return true;
        }

        private bool MoveMeshEntry(GameObject root, int from, int to)
        {
            var entries = EntriesForOutfit(root).ToList();
            if (from < 0 || to < 0 || from >= entries.Count || to >= entries.Count || from == to)
                return false;
            for (int index = Mathf.Min(from, to); index <= Mathf.Max(from, to); index++)
                if (!CanReorderEntry(entries[index])) return false;
            var moved = entries[from];
            entries.RemoveAt(from);
            entries.Insert(to, moved);
            int next = 0;
            for (int index = 0; index < displayEntries.Count; index++)
                if (displayEntries[index].outfitRoot == root) displayEntries[index] = entries[next++];
            Repaint();
            return true;
        }

        private void ApplyMenuOrder()
        {
            var ordered = displayEntries.Select(entry => createdItems.TryGetValue(entry, out var created) ? created : entry.group?.toggler)
                .Where(item => item != null && CanSafelyMoveWholeToggler(item)).Distinct().ToList();
            foreach (var folder in ordered.GroupBy(ResolveLogicalMenuFolder))
            {
                if (folder.Key == null || !IsLogicalFolderInScope(folder.Key)) continue;
                var desired = folder.ToList();
                var included = new HashSet<ItemToggler>(desired);
                var slots = folder.Key.GetComponentsInChildren<ItemToggler>(true)
                    .Where(item => included.Contains(item) && ResolveLogicalMenuFolder(item) == folder.Key).ToList();
                if (slots.Count != desired.Count) continue;
                if (slots.SequenceEqual(desired)) continue;

                // Reuse only the selected menu slots. Other components, manual folders,
                // page sizes and the source outfit/Mesh hierarchy keep their positions.
                var children = slots.Select(item => item.transform.parent).Distinct()
                    .ToDictionary(parent => parent, parent => parent.Cast<Transform>().ToList());
                for (int index = 0; index < slots.Count; index++)
                {
                    Transform slot = slots[index].transform;
                    children[slot.parent][slot.GetSiblingIndex()] = desired[index].transform;
                }
                foreach (var parent in children.Keys)
                    Undo.RegisterFullObjectHierarchyUndo(parent.gameObject, "メニューの順序を変更");
                foreach (var parent in children)
                    foreach (var child in parent.Value)
                        if (child.parent != parent.Key) Undo.SetTransformParent(child, parent.Key, "メニューの順序を変更");
                foreach (var parent in children)
                    for (int index = 0; index < parent.Value.Count; index++) parent.Value[index].SetSiblingIndex(index);
            }
        }
    }
}
