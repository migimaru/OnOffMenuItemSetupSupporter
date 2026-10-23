using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    public sealed partial class ItemTogglerEditWindow
    {
        private enum TableRowKind { Outfit, Mesh, Group, Warning }

        private sealed class TableRow
        {
            internal TableRowKind kind;
            internal Rect rect;
            internal GameObject root;
            internal EditMeshEntry entry;
            internal ExistingGroup group;
            internal string message;
            internal int outfitIndex, entryIndex;
        }

        private const float TableHeaderHeight = 30;
        private readonly List<TableRow> tableRows = new List<TableRow>();
        private MenuTableColumnWidths tableColumns;
        private GameObject[] tableColumnFolders;
        [Serializable]
        private sealed class FolderColumnWidth
        {
            public GameObject folder;
            public float width;
        }
        [SerializeField] private List<FolderColumnWidth> folderColumnWidths = new List<FolderColumnWidth>();
        private GUIStyle tableLabel;
        private Rect tableRect;
        private TableRow draggedRow, dropTarget;
        private int tableDragControl;
        private int dropIndex;
        private bool dropAfter;
        [SerializeField] private int manuallySizedColumns;

        private static Rect CellContent(Rect cell)
        {
            float height = EditorGUIUtility.singleLineHeight;
            return new Rect(cell.x + 7, cell.center.y - height / 2, Mathf.Max(0, cell.width - 14), height);
        }

        private static Rect CenteredToggle(Rect cell)
        {
            return new Rect(cell.center.x - 9, cell.center.y - 9, 18, 18);
        }

        private string[] TableHeaders()
        {
            return new[] { "", "表示", "EditorOnly", "Mesh" }
                .Concat(menuFolders.Select(folder => folder.displayName)).Concat(new[] { "登録しない" }).ToArray();
        }

        private void InitializeTableColumns(string[] headers)
        {
            if (tableLabel == null)
                tableLabel = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip, wordWrap = false, alignment = TextAnchor.MiddleLeft };
            var folders = menuFolders.Select(folder => folder.gameObject).ToArray();
            if (tableColumns == null || !folders.SequenceEqual(tableColumnFolders))
            {
                if (tableColumns != null) SaveColumnWidths();
                tableColumnFolders = folders;
                float[] folderWidths = folders.Select(folder => folderColumnWidths.FirstOrDefault(saved => saved.folder == folder)?.width ?? menuFolderColumnWidth).ToArray();
                tableColumns = new MenuTableColumnWidths(displayColumnWidth, editorOnlyColumnWidth, meshNameColumnWidth, folderWidths, doNotRegisterColumnWidth);
                if ((manuallySizedColumns & (1 << 2)) != 0) tableColumns.Resize(3, meshNameColumnWidth);
            }
            for (int column = 1; column < headers.Length; column++)
                tableColumns.SetMinimum(column,
                    Mathf.Ceil(EditorStyles.boldLabel.CalcSize(new GUIContent(headers[column])).x) + 22);
            float meshWidth = 80;
            foreach (var entry in displayEntries)
                if (entry.gameObject != null) meshWidth = Mathf.Max(meshWidth, Mathf.Ceil(tableLabel.CalcSize(new GUIContent(entry.gameObject.name)).x) + 14);
            tableColumns.MeasureMesh(meshWidth);
        }

        private void SaveColumnWidths()
        {
            displayColumnWidth = tableColumns.Width(1);
            editorOnlyColumnWidth = tableColumns.Width(2);
            meshNameColumnWidth = tableColumns.Width(3);
            doNotRegisterColumnWidth = tableColumns.Width(tableColumns.Count - 1);
            for (int index = 0; index < tableColumnFolders.Length; index++)
            {
                var saved = folderColumnWidths.FirstOrDefault(item => item.folder == tableColumnFolders[index]);
                if (saved == null)
                {
                    saved = new FolderColumnWidth { folder = tableColumnFolders[index] };
                    folderColumnWidths.Add(saved);
                }
                saved.width = tableColumns.Width(index + 4);
            }
        }

        private void DrawMenuTable()
        {
            string[] headers = TableHeaders();
            InitializeTableColumns(headers);
            Rect table = GUILayoutUtility.GetRect(0, 100000, 80, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            tableRect = table;
            float scrollbar = Mathf.Max(16, GUI.skin.verticalScrollbar.fixedWidth + GUI.skin.verticalScrollbar.margin.left);
            Rect body = new Rect(table.x + 1, table.y + TableHeaderHeight, Mathf.Max(0, table.width - 2), Mathf.Max(0, table.height - TableHeaderHeight - 1));
            float viewportWidth = Mathf.Max(0, body.width - scrollbar);
            float contentWidth = tableColumns.ContentWidth(viewportWidth);
            scrollPosition.x = Mathf.Clamp(scrollPosition.x, 0, Mathf.Max(0, contentWidth - viewportWidth));
            BuildTableRows(contentWidth);
            float height = tableRows.Count == 0 ? 0 : tableRows[tableRows.Count - 1].rect.yMax;
            int dragControl = GUIUtility.GetControlID("MenuTableReorder".GetHashCode(), FocusType.Passive);
            GUI.Box(table, GUIContent.none, EditorStyles.helpBox);
            scrollPosition = GUI.BeginScrollView(body, scrollPosition, new Rect(0, 0, contentWidth, height), false, true);
            foreach (var row in tableRows) DrawTableRow(row, dragControl);
            HandleTableDrag(dragControl, body.height, height);
            GUI.EndScrollView();

            // Only the rows scroll vertically. Headers use the body's horizontal offset.
            Rect header = new Rect(table.x + 1, table.y + 1, viewportWidth, TableHeaderHeight - 1);
            GUI.BeginGroup(header);
            EditorGUI.DrawRect(new Rect(0, 0, header.width, header.height), EditorGUIUtility.isProSkin ? new Color(.25f, .25f, .25f) : new Color(.8f, .8f, .8f));
            Rect[] columns = tableColumns.Columns(new Rect(-scrollPosition.x, 0, contentWidth, header.height));
            for (int column = 1; column < columns.Length; column++)
            {
                float width = columns[column].width;
                float minimum = Mathf.Ceil(EditorStyles.boldLabel.CalcSize(new GUIContent(headers[column])).x) + 22;
                if (ResizableColumnHeader.Draw(columns[column], headers[column], ref width, minimum))
                {
                    tableColumns.Resize(column, width);
                    if (column == 3) manuallySizedColumns |= 1 << 2;
                    SaveColumnWidths();
                    Repaint();
                }
            }
            GUI.EndGroup();
        }

        private void BuildTableRows(float width)
        {
            tableRows.Clear();
            var drawnGroups = new HashSet<ExistingGroup>();
            float y = 0;
            for (int outfitIndex = 0; outfitIndex < outfitRoots.Count; outfitIndex++)
            {
                GameObject root = outfitRoots[outfitIndex];
                tableRows.Add(new TableRow { kind = TableRowKind.Outfit, root = root, outfitIndex = outfitIndex, rect = new Rect(0, y, width, 35) });
                y += 35;
                int entryIndex = 0;
                foreach (var entry in EntriesForOutfit(root))
                {
                    if (entry.gameObject == null) continue;
                    if (entry.group != null && drawnGroups.Add(entry.group))
                    {
                        if (entry.group.entries.Count > 1)
                        {
                            tableRows.Add(new TableRow { kind = TableRowKind.Group, group = entry.group, rect = new Rect(28, y, width - 28, 25) });
                            y += 25;
                        }
                        if (entry.group.structuralLocked && !string.IsNullOrEmpty(entry.group.lockReason))
                        {
                            float warningHeight = Mathf.Max(40, EditorStyles.helpBox.CalcHeight(new GUIContent(entry.group.lockReason), width - 28));
                            tableRows.Add(new TableRow { kind = TableRowKind.Warning, message = entry.group.lockReason, rect = new Rect(28, y, width - 28, warningHeight) });
                            y += warningHeight;
                        }
                    }
                    tableRows.Add(new TableRow { kind = TableRowKind.Mesh, root = root, entry = entry, outfitIndex = outfitIndex, entryIndex = entryIndex++, rect = new Rect(0, y, width, 31) });
                    y += 31;
                }
            }
        }

        private void DrawTableRow(TableRow row, int dragControl)
        {
            if (row.kind == TableRowKind.Warning) { EditorGUI.HelpBox(row.rect, row.message, MessageType.Warning); return; }
            if (row.kind == TableRowKind.Group) { DrawMultiGroupHeader(row.group, CellContent(row.rect)); return; }
            bool outfit = row.kind == TableRowKind.Outfit;
            Color background = EditorGUIUtility.isProSkin ? new Color(.2f, .2f, .2f) : new Color(.78f, .78f, .78f);
            if (outfit) background = EditorGUIUtility.isProSkin ? new Color(.24f, .24f, .24f) : new Color(.84f, .84f, .84f);
            if (IsTableRowGrabbed(row)) background = EditorGUIUtility.isProSkin ? new Color(.27f, .34f, .41f) : new Color(.65f, .76f, .85f);
            EditorGUI.DrawRect(row.rect, background);
            EditorGUI.DrawRect(new Rect(row.rect.x, row.rect.yMax - 1, row.rect.width, 1), EditorGUIUtility.isProSkin ? new Color(.27f, .27f, .27f) : new Color(.7f, .7f, .7f));
            Rect handle = new Rect(row.rect.x + (outfit ? 4 : 9), row.rect.y + 4, 19, row.rect.height - 8);
            bool movable = outfit ? EntriesForOutfit(row.root).All(CanReorderEntry) : CanReorderEntry(row.entry);
            using (new EditorGUI.DisabledScope(!movable))
                GUI.Label(handle, new GUIContent("≡", movable ? (outfit ? "衣装全体を移動" : "同じ衣装の中で移動") : "他の機能や複数衣装にまたがる構成を維持するため、順序は変更できません。"), tableLabel);
            if (movable) EditorGUIUtility.AddCursorRect(handle, MouseCursor.Pan);
            Event current = Event.current;
            if (movable && GUI.enabled && current.type == EventType.MouseDown && current.button == 0 && handle.Contains(current.mousePosition))
            {
                draggedRow = row;
                dropTarget = null;
                tableDragControl = dragControl;
                GUIUtility.hotControl = dragControl;
                current.Use();
                Repaint();
            }
            if (outfit) DrawTableOutfit(row);
            else DrawEntry(row.entry, tableColumns.Columns(row.rect));
            if (dropTarget != null && dropTarget.root == row.root && dropTarget.entry == row.entry)
                EditorGUI.DrawRect(new Rect(row.rect.x, dropAfter ? row.rect.yMax - 2 : row.rect.y, row.rect.width, 2), new Color(.4f, .65f, .85f));
        }

        private bool IsTableRowGrabbed(TableRow row)
        {
            return draggedRow != null && draggedRow.kind == row.kind &&
                (row.kind == TableRowKind.Outfit ? draggedRow.outfitIndex == row.outfitIndex : ReferenceEquals(draggedRow.entry, row.entry));
        }

        private void CancelTableDrag()
        {
            if (draggedRow == null) return;
            if (GUIUtility.hotControl == tableDragControl) GUIUtility.hotControl = 0;
            draggedRow = dropTarget = null;
            Repaint();
        }

        private void OnLostFocus() => CancelTableDrag();

        private void DrawTableOutfit(TableRow row)
        {
            Rect field = new Rect(33, row.rect.y + 7, Mathf.Max(0, row.rect.width - 75), EditorGUIUtility.singleLineHeight);
            bool editable = EntriesForOutfit(row.root).All(CanReorderEntry);
            using (new EditorGUI.DisabledScope(!editable))
            {
                var root = (GameObject)EditorGUI.ObjectField(field, row.root, typeof(GameObject), true);
                if (root != row.root && (root == null || IsValidOutfitRoot(root)) && (root == null || !outfitRoots.Contains(root)))
                {
                    outfitRoots[row.outfitIndex] = root;
                    RefreshLoadedMeshes();
                    GUIUtility.ExitGUI();
                }
                if (GUI.Button(new Rect(row.rect.xMax - 35, row.rect.y + 5, 28, 25), "−"))
                {
                    outfitRoots.RemoveAt(row.outfitIndex);
                    RefreshLoadedMeshes();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void UpdateTableDropTarget(Vector2 mouse)
        {
            dropTarget = null;
            var row = tableRows.FirstOrDefault(candidate => candidate.rect.Contains(mouse) && (candidate.kind == TableRowKind.Outfit || candidate.kind == TableRowKind.Mesh));
            if (row == null) return;
            if (draggedRow.kind == TableRowKind.Outfit)
            {
                dropTarget = tableRows.First(candidate => candidate.kind == TableRowKind.Outfit && candidate.outfitIndex == row.outfitIndex);
                dropAfter = row.kind == TableRowKind.Mesh || mouse.y >= row.rect.center.y;
                int index = row.outfitIndex + (dropAfter ? 1 : 0);
                dropIndex = index - (draggedRow.outfitIndex < index ? 1 : 0);
            }
            else if (row.kind == TableRowKind.Mesh && row.root == draggedRow.root)
            {
                dropTarget = row;
                dropAfter = mouse.y >= row.rect.center.y;
                int index = row.entryIndex + (dropAfter ? 1 : 0);
                dropIndex = index - (draggedRow.entryIndex < index ? 1 : 0);
            }
        }

        private void HandleTableDrag(int control, float viewportHeight, float contentHeight)
        {
            if (draggedRow == null) return;
            if (GUIUtility.hotControl != control) { CancelTableDrag(); return; }
            Event current = Event.current;
            if (current.type == EventType.MouseDrag || current.type == EventType.MouseUp)
            {
                UpdateTableDropTarget(current.mousePosition);
                if (current.type == EventType.MouseUp)
                {
                    if (dropTarget != null)
                    {
                        if (draggedRow.kind == TableRowKind.Outfit) MoveOutfit(draggedRow.outfitIndex, dropIndex);
                        else MoveMeshEntry(draggedRow.root, draggedRow.entryIndex, dropIndex);
                    }
                    CancelTableDrag();
                }
                else
                {
                    if (current.mousePosition.y < scrollPosition.y + 20) scrollPosition.y -= 15;
                    if (current.mousePosition.y > scrollPosition.y + viewportHeight - 35) scrollPosition.y += 15;
                    scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0, Mathf.Max(0, contentHeight - viewportHeight));
                }
                current.Use();
                Repaint();
            }
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                CancelTableDrag();
                current.Use();
                Repaint();
            }
        }
    }
}
