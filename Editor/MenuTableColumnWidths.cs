using System;
using UnityEngine;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    // Window-local state: Unity must not deserialize a partly initialized width model.
    internal sealed class MenuTableColumnWidths
    {
        internal const float HandleWidth = 28f;
        private readonly float[] widths;
        private readonly float[] minimums;
        private bool meshManuallySized;

        internal MenuTableColumnWidths(float display, float editorOnly, float mesh, float[] folders, float none)
        {
            widths = new float[folders.Length + 5];
            minimums = new float[widths.Length];
            widths[0] = minimums[0] = HandleWidth;
            widths[1] = display;
            widths[2] = editorOnly;
            widths[3] = mesh;
            minimums[1] = 44;
            minimums[2] = 90;
            minimums[3] = 80;
            for (int index = 0; index < folders.Length; index++)
            {
                widths[index + 4] = folders[index];
                minimums[index + 4] = 60;
            }
            widths[widths.Length - 1] = none;
            minimums[widths.Length - 1] = 80;
        }

        internal int Count => widths.Length;

        internal void SetMinimum(int column, float width)
        {
            minimums[column] = Math.Max(minimums[column], width);
            widths[column] = Math.Max(widths[column], minimums[column]);
        }

        internal void MeasureMesh(float width)
        {
            if (!meshManuallySized) widths[3] = Math.Max(minimums[3], width);
        }

        internal void Resize(int column, float width)
        {
            widths[column] = Math.Max(minimums[column], width);
            if (column == 3) meshManuallySized = true;
        }

        internal float Width(int column) => widths[column];

        internal float ContentWidth(float viewport)
        {
            float total = 0;
            foreach (float width in widths) total += width;
            return Math.Max(viewport, total);
        }

        internal Rect[] Columns(Rect row)
        {
            // The row may fill the viewport, but spare space belongs to no column.
            var result = new Rect[widths.Length];
            float x = row.x;
            for (int column = 0; column < result.Length; column++)
            {
                result[column] = new Rect(x, row.y, widths[column], row.height);
                x += widths[column];
            }
            return result;
        }
    }
}
