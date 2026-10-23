using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using jp.lilxyzw.lilycalinventory.runtime;
using Object = UnityEngine.Object;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    // Window-local identities survive recreating a deleted menu object. No global Undo
    // command is used: restoring a checkpoint must not undo work in another inspector.
    internal sealed class MenuSceneHistory
    {
        private readonly GameObject root;
        private readonly Dictionary<GameObject, int> identities = new Dictionary<GameObject, int>();
        private readonly Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();
        private int nextIdentity = 1;
        internal MenuSceneHistory(GameObject root)
        {
            this.root = root;
        }

        internal int GetId(GameObject target)
        {
            if (target == null)
                return 0;
            if (!identities.TryGetValue(target, out int id))
            {
                id = nextIdentity++;
                identities[target] = id;
            }

            objects[id] = target;
            return id;
        }

        internal GameObject Resolve(int id) => objects.TryGetValue(id, out var target) ? target : null;
        internal sealed class Snapshot
        {
            internal readonly List<Node> nodes = new List<Node>();
            internal readonly Dictionary<GameObject, MeshState> meshes = new Dictionary<GameObject, MeshState>();
            internal readonly List<ExternalLink> externalLinks = new List<ExternalLink>();
        }

        internal sealed class ExternalLink
        {
            internal Component owner;
            internal Reference reference;
        }

        internal sealed class MeshState
        {
            internal bool active;
            internal string tag;
        }

        internal sealed class Node
        {
            internal int id, parentId, sibling, layer;
            internal HideFlags hideFlags;
            internal string name, tag;
            internal bool active, canRecreate;
            internal Vector3 position, scale;
            internal Quaternion rotation;
            internal readonly List<ComponentState> components = new List<ComponentState>();
        }

        internal sealed class ComponentState
        {
            internal Type type;
            internal string json;
            internal Component prefabSource;
            internal readonly List<Reference> references = new List<Reference>();
        }

        internal sealed class Reference
        {
            internal string path;
            internal Object external;
            internal int nodeId, componentIndex;
            internal Type componentType;
        }

        internal Snapshot Capture(IEnumerable<GameObject> meshes)
        {
            var snapshot = new Snapshot();
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                var target = transform.gameObject;
                var node = new Node
                {
                    id = GetId(target),
                    parentId = GetId(transform.parent == null ? null : transform.parent.gameObject),
                    sibling = transform.GetSiblingIndex(),
                    name = target.name,
                    tag = target.tag,
                    layer = target.layer,
                    hideFlags = target.hideFlags,
                    active = target.activeSelf,
                    canRecreate = IsMenuObject(target),
                    position = transform.localPosition,
                    rotation = transform.localRotation,
                    scale = transform.localScale
                };
                foreach (Component component in target.GetComponents<Component>())
                {
                    if (!IsMenuComponent(component))
                        continue;
                    var state = new ComponentState
                    {
                        type = component.GetType(),
                        json = SerializeSettings(component),
                        prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(component)
                    };
                    var property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath.StartsWith("m_", StringComparison.Ordinal))
                            continue;
                        state.references.Add(CaptureReference(property.propertyPath, property.objectReferenceValue));
                    }

                    node.components.Add(state);
                }

                snapshot.nodes.Add(node);
            }

            foreach (var mesh in meshes.Distinct())
            {
                if (mesh != null)
                    snapshot.meshes[mesh] = new MeshState
                    {
                        active = mesh.activeSelf,
                        tag = mesh.tag
                    };
            }

            CaptureExternalLinks(snapshot);
            return snapshot;
        }

        private static string SerializeSettings(Component component)
        {
            // Native ownership belongs to the new component, not to the deleted instance.
            // Keep user settings while avoiding stale GameObject/script/prefab instance IDs.
            string[] ownership =
            {
                "m_GameObject",
                "m_Script",
                "m_CorrespondingSourceObject",
                "m_PrefabInstance",
                "m_PrefabAsset",
                "m_EditorClassIdentifier",
                "m_Name"
            };
            var fields = SplitJsonFields(EditorJsonUtility.ToJson(component));
            return "{" + string.Join(",", fields.Where(field => !ownership.Any(name => field.TrimStart().StartsWith("\"" + name + "\"", StringComparison.Ordinal)))) + "}";
        }

        internal static List<string> SplitJsonFields(string json)
        {
            var fields = new List<string>();
            int start = json.IndexOf('{') + 1;
            int end = json.LastIndexOf('}');
            int depth = 0;
            bool quoted = false, escaped = false;
            for (int i = start; i < end; i++)
            {
                char character = json[i];
                if (quoted)
                {
                    if (escaped)
                        escaped = false;
                    else if (character == '\\')
                        escaped = true;
                    else if (character == '"')
                        quoted = false;
                    continue;
                }

                if (character == '"')
                    quoted = true;
                else if (character == '{' || character == '[')
                    depth++;
                else if (character == '}' || character == ']')
                    depth--;
                else if (character == ',' && depth == 0)
                {
                    fields.Add(json.Substring(start, i - start));
                    start = i + 1;
                }
            }

            string last = json.Substring(start, end - start);
            if (!string.IsNullOrWhiteSpace(last))
                fields.Add(last);
            return fields;
        }

        private void CaptureExternalLinks(Snapshot snapshot)
        {
            // Presets and custom scripts can refer to a menu component that gets deleted.
            // Remember those links so recreating the menu does not leave a broken reference.
            foreach (var owner in root.transform.root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (owner == null || IsMenuComponent(owner))
                    continue;
                var property = new SerializedObject(owner).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath.StartsWith("m_", StringComparison.Ordinal))
                        continue;
                    var target = property.objectReferenceValue;
                    var menuObject = target as GameObject;
                    if (target is Component component && IsMenuComponent(component))
                        menuObject = component.gameObject;
                    if (menuObject == null || !menuObject.transform.IsChildOf(root.transform))
                        continue;
                    snapshot.externalLinks.Add(new ExternalLink { owner = owner, reference = CaptureReference(property.propertyPath, target) });
                }
            }
        }

        private Reference CaptureReference(string path, Object value)
        {
            var reference = new Reference
            {
                path = path,
                external = value
            };
            var target = value as GameObject;
            if (value is Component component)
                target = component.gameObject;
            if (target == null || !target.transform.IsChildOf(root.transform))
                return reference;
            reference.nodeId = GetId(target);
            reference.external = null;
            if (value is Component internalComponent)
            {
                reference.componentType = internalComponent.GetType();
                reference.componentIndex = Array.IndexOf(target.GetComponents(reference.componentType), internalComponent);
            }

            return reference;
        }

        internal Dictionary<int, Node> ChangedNodes(Snapshot before, Snapshot after)
        {
            var previous = before.nodes.ToDictionary(node => node.id);
            var current = after.nodes.ToDictionary(node => node.id);
            var changed = new HashSet<int>(previous.Keys.Concat(current.Keys));
            changed.RemoveWhere(id => previous.TryGetValue(id, out var a) && current.TryGetValue(id, out var b) && a.parentId == b.parentId && a.sibling == b.sibling && a.name == b.name && a.tag == b.tag && a.active == b.active && a.layer == b.layer && a.hideFlags == b.hideFlags && a.position == b.position && a.rotation == b.rotation && a.scale == b.scale && a.components.Select(component => component.type).SequenceEqual(b.components.Select(component => component.type)) && a.components.Select(component => component.json).SequenceEqual(b.components.Select(component => component.json)));
            return changed.ToDictionary(id => id, id => current.TryGetValue(id, out var node) ? node : null);
        }

        private static bool ComponentChanged(Node before, Node after, Type type)
        {
            var original = before.components.FirstOrDefault(component => component.type == type);
            var applied = after?.components.FirstOrDefault(component => component.type == type);
            return original?.json != applied?.json;
        }

        internal void Restore(Snapshot snapshot, Dictionary<int, Node> changed)
        {
            var wanted = new HashSet<int>(snapshot.nodes.Select(node => node.id));
            // Children first. Objects that acquired other functionality are preserved.
            foreach (var transform in root.GetComponentsInChildren<Transform>(true).Reverse())
            {
                if (transform == null || transform == root.transform || wanted.Contains(GetId(transform.gameObject)) || !changed.ContainsKey(GetId(transform.gameObject)))
                    continue;
                RemoveMenuObject(transform.gameObject);
            }

            foreach (var node in snapshot.nodes)
            {
                if (!changed.TryGetValue(node.id, out var applied))
                    continue;
                var target = Resolve(node.id);
                if (target == null && node.canRecreate)
                {
                    target = new GameObject(node.name);
                    Undo.RegisterCreatedObjectUndo(target, "メニュー履歴を復元");
                    identities[target] = node.id;
                    objects[node.id] = target;
                    applied = null;
                    changed[node.id] = null;
                }

                if (target == null)
                    continue;
                if (target != root && node.canRecreate)
                {
                    var parent = Resolve(node.parentId);
                    bool parentChanged = applied == null || node.parentId != applied.parentId;
                    if (parentChanged)
                        Undo.SetTransformParent(target.transform, parent == null ? root.transform : parent.transform, "メニュー履歴を復元");
                    Undo.RecordObject(target.transform, "メニューの配置を復元");
                    if (parentChanged || node.position != applied.position)
                        target.transform.localPosition = node.position;
                    if (parentChanged || node.rotation != applied.rotation)
                        target.transform.localRotation = node.rotation;
                    if (parentChanged || node.scale != applied.scale)
                        target.transform.localScale = node.scale;
                    if (parentChanged || node.sibling != applied.sibling)
                        target.transform.SetSiblingIndex(node.sibling);
                    Undo.RecordObject(target, "メニューの表示を復元");
                    if (applied == null || node.name != applied.name)
                        target.name = node.name;
                    if (applied == null || node.tag != applied.tag)
                        target.tag = node.tag;
                    if (applied == null || node.layer != applied.layer)
                        target.layer = node.layer;
                    if (applied == null || node.hideFlags != applied.hideFlags)
                        target.hideFlags = node.hideFlags;
                    if (applied == null || node.active != applied.active)
                        target.SetActive(node.active);
                }
                else if (target != root && node.components.Count > 0 && (applied == null || node.sibling != applied.sibling))
                {
                    Undo.RecordObject(target.transform, "メニューの順序を復元");
                    target.transform.SetSiblingIndex(node.sibling);
                }

                foreach (var component in target.GetComponents<Component>())
                {
                    if (IsMenuComponent(component) && ComponentChanged(node, applied, component.GetType()) && !node.components.Any(state => state.type == component.GetType()))
                        Undo.DestroyObjectImmediate(component);
                }

                foreach (var state in node.components)
                {
                    if (!ComponentChanged(node, applied, state.type))
                        continue;
                    if (target.GetComponent(state.type) == null && state.prefabSource != null && PrefabUtility.IsPartOfPrefabInstance(target))
                        PrefabUtility.RevertRemovedComponent(target, state.prefabSource, InteractionMode.UserAction);
                    if (target.GetComponent(state.type) == null)
                        Undo.AddComponent(target, state.type);
                }
            }

            // All objects and components must exist before remapping references between them.
            foreach (var node in snapshot.nodes)
            {
                if (!changed.TryGetValue(node.id, out var applied))
                    continue;
                var target = Resolve(node.id);
                if (target == null)
                    continue;
                foreach (var state in node.components)
                {
                    if (!ComponentChanged(node, applied, state.type))
                        continue;
                    var component = target.GetComponent(state.type);
                    Undo.RegisterCompleteObjectUndo(component, "メニュー設定を復元");
                    EditorJsonUtility.FromJsonOverwrite(state.json, component);
                    var serialized = new SerializedObject(component);
                    foreach (var reference in state.references)
                    {
                        var property = serialized.FindProperty(reference.path);
                        if (property != null)
                            property.objectReferenceValue = ResolveReference(reference);
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                    EditorUtility.SetDirty(component);
                }
            }

            foreach (var pair in snapshot.meshes)
            {
                if (pair.Key == null)
                    continue;
                Undo.RecordObject(pair.Key, "衣装の表示状態を復元");
                pair.Key.SetActive(pair.Value.active);
                pair.Key.tag = pair.Value.tag;
                PrefabUtility.RecordPrefabInstancePropertyModifications(pair.Key);
                EditorUtility.SetDirty(pair.Key);
            }

            foreach (var link in snapshot.externalLinks)
            {
                if (link.owner == null || !changed.ContainsKey(link.reference.nodeId))
                    continue;
                var serialized = new SerializedObject(link.owner);
                var property = serialized.FindProperty(link.reference.path);
                // An explicit new assignment made elsewhere is left alone.
                if (property == null || property.objectReferenceValue != null)
                    continue;
                property.objectReferenceValue = ResolveReference(link.reference);
                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(link.owner);
            }
        }

        private Object ResolveReference(Reference reference)
        {
            if (reference.nodeId == 0)
                return reference.external;
            var target = Resolve(reference.nodeId);
            if (target == null || reference.componentType == null)
                return target;
            var components = target.GetComponents(reference.componentType);
            return reference.componentIndex < components.Length ? components[reference.componentIndex] : null;
        }

        internal static bool IsMenuComponent(Component component) => component is ItemToggler || component is MenuFolder || component is AutoGeneratedNextPageMarker;
        internal static bool IsMenuObject(GameObject target) => target.GetComponents<Component>().Any(IsMenuComponent) && target.GetComponents<Component>().All(component => component is Transform || IsMenuComponent(component));
        internal static void RemoveMenuObject(GameObject target)
        {
            if (IsMenuObject(target) && target.transform.childCount == 0 && !PrefabUtility.IsPartOfPrefabInstance(target))
            {
                Undo.DestroyObjectImmediate(target);
                return;
            }

            foreach (var component in target.GetComponents<Component>())
                if (IsMenuComponent(component))
                    Undo.DestroyObjectImmediate(component);
        }
    }
}
