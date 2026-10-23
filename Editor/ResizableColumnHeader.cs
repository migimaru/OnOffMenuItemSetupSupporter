using UnityEditor;
using UnityEngine;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    internal static class ResizableColumnHeader
    {
        private const float GripWidth = 8f;

        // Returns true when a drag changes the width and the window needs repainting.
        internal static bool Draw(string label, ref float width, float minimumWidth)
        {
            Rect rect = EditorGUILayout.GetControlRect(
                false, EditorGUIUtility.singleLineHeight, EditorStyles.boldLabel,
                GUILayout.Width(width));
            return Draw(rect, label, ref width, minimumWidth);
        }

        internal static bool Draw(Rect rect, string label, ref float width, float minimumWidth)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Rect grip = new Rect(rect.xMax - GripWidth, rect.y, GripWidth, rect.height);
            Rect labelRect = new Rect(rect.x + 7, rect.y, Mathf.Max(0, rect.width - GripWidth - 14), rect.height);

            GUI.Label(labelRect,
                new GUIContent(label, label + "（右端をドラッグして列幅を変更）"),
                new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip, wordWrap = false, alignment = TextAnchor.MiddleLeft });
            EditorGUI.DrawRect(
                new Rect(grip.center.x, grip.y + 2f, 1f, grip.height - 4f),
                EditorGUIUtility.isProSkin
                    ? new Color(0.6f, 0.6f, 0.6f)
                    : new Color(0.4f, 0.4f, 0.4f));
            EditorGUIUtility.AddCursorRect(grip, MouseCursor.ResizeHorizontal, controlId);

            Event current = Event.current;
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (current.button == 0 && grip.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        current.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        width = Mathf.Max(minimumWidth, width + current.delta.x);
                        GUI.changed = true;
                        current.Use();
                        return true;
                    }
                    break;

                case EventType.MouseUp:
                    if (current.button == 0 && GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                    break;
            }

            return false;
        }
    }
}
