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
    public sealed partial class ItemTogglerEditWindow : EditorWindow
    {
        private sealed class MenuFolderEntry
        {
            public GameObject gameObject;
            public string displayName;
        }

        private sealed class ExistingGroup
        {
            public ItemToggler toggler;
            public readonly List<EditMeshEntry> entries = new List<EditMeshEntry>();
            public GameObject logicalMenuFolder;
            public bool maintainMultiple = true;
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
            public GameObject outfitRoot;
            public ExistingGroup group;
            public int objectIndex = -1;
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

        private sealed class WorkspaceViewState
        {
            public GameObject sourceMenuFolder;
            public ItemTogglerSetupMode setupMode;
            public bool includeChildMenuFolders;
            public bool overwriteMode;
            public bool skipEditorOnly;
            public bool autoAdjust;
            public bool foldersAtTop;
            public readonly List<GameObject> outfitRoots = new List<GameObject>();
            public readonly List<int> entryOrder = new List<int>();
            public readonly Dictionary<int, int> meshOutfits = new Dictionary<int, int>();
            public readonly HashSet<int> omittedMeshes = new HashSet<int>();
            public Vector2 scrollPosition;
            public float displayColumnWidth;
            public float editorOnlyColumnWidth;
            public float meshNameColumnWidth;
            public float menuFolderColumnWidth;
            public float doNotRegisterColumnWidth;
            public readonly Dictionary<string, int> selectedFolders = new Dictionary<string, int>();
            public readonly Dictionary<string, int> snapshotFolders = new Dictionary<string, int>();
            public readonly Dictionary<int, bool> groupMaintain = new Dictionary<int, bool>();
            public readonly Dictionary<int, bool> snapshotActive = new Dictionary<int, bool>();
            public readonly Dictionary<int, string> snapshotTag = new Dictionary<int, string>();
            public readonly Dictionary<int, bool> activeValues = new Dictionary<int, bool>();
            public readonly Dictionary<int, string> tagValues = new Dictionary<int, string>();
        }

        [SerializeField]
        private GameObject sourceMenuFolder;
        [SerializeField]
        private ItemTogglerSetupMode newItemSetupMode;
        [SerializeField]
        private bool includeChildMenuFolders;
        private readonly List<MenuFolderEntry> menuFolders = new List<MenuFolderEntry>();
        private readonly List<ExistingGroup> existingGroups = new List<ExistingGroup>();
        private readonly List<EditMeshEntry> displayEntries = new List<EditMeshEntry>();
        private readonly List<string> statusMessages = new List<string>();
        private Dictionary<GameObject, List<ItemToggler>> allTargetRegistrations = new Dictionary<GameObject, List<ItemToggler>>();
        private Vector2 scrollPosition;
        private bool initialized;
        private bool closeAccepted;
        [SerializeField]
        private float displayColumnWidth = 45f;
        [SerializeField]
        private float editorOnlyColumnWidth = 75f;
        [SerializeField]
        private float meshNameColumnWidth = 180f;
        [SerializeField]
        private float menuFolderColumnWidth = 110f;
        [SerializeField]
        private float doNotRegisterColumnWidth = 80f;
        public static void Open(GameObject sourceMenuFolder, ItemTogglerSetupMode newItemSetupMode)
        {
            OpenWorkspace(sourceMenuFolder, newItemSetupMode, false);
        }

        private void RequestClose()
        {
            ResetChanges();
            closeAccepted = true;
            Close();
        }

        private void OnDestroy()
        {
            if (!closeAccepted && initialized)
                RestoreImmediateChangesToSnapshot();
            checkpoints.Clear();
        }
    }
}
