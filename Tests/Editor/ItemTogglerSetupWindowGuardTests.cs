using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using jp.lilxyzw.lilycalinventory.runtime;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor.Tests
{
    public sealed class ItemTogglerSetupWindowGuardTests
    {
        private readonly List<GameObject> createdObjects =
            new List<GameObject>();


        [TearDown]
        public void TearDown()
        {
            foreach (
                GameObject createdObject
                in createdObjects
            )
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(
                        createdObject
                    );
                }
            }


            createdObjects.Clear();
        }


        [Test]
        public void 通常のMenuFolderは登録先にできる()
        {
            GameObject menuFolderObject =
                CreateGameObject(
                    "Accessory"
                );


            menuFolderObject
                .AddComponent<MenuFolder>();


            Assert.IsTrue(
                ItemTogglerSetupWindow
                    .IsSelectableMenuFolder(
                        menuFolderObject
                    )
            );
        }


        [Test]
        public void EditorOnlyのMenuFolderは登録先にできない()
        {
            GameObject menuFolderObject =
                CreateGameObject(
                    "Accessory"
                );


            menuFolderObject
                .AddComponent<MenuFolder>();


            menuFolderObject.tag =
                "EditorOnly";


            Assert.IsFalse(
                ItemTogglerSetupWindow
                    .IsSelectableMenuFolder(
                        menuFolderObject
                    )
            );
        }


        [Test]
        public void 親がEditorOnlyなら子MenuFolderも登録先にできない()
        {
            GameObject parent =
                CreateGameObject(
                    "Parent"
                );


            parent.tag =
                "EditorOnly";


            GameObject menuFolderObject =
                CreateGameObject(
                    "Accessory"
                );


            menuFolderObject.transform.SetParent(
                parent.transform
            );


            menuFolderObject
                .AddComponent<MenuFolder>();


            Assert.IsFalse(
                ItemTogglerSetupWindow
                    .IsSelectableMenuFolder(
                        menuFolderObject
                    )
            );
        }


        [Test]
        public void 親Rootと子Rootに同じMeshが含まれても一度だけ列挙される()
        {
            GameObject parentRoot =
                CreateGameObject(
                    "ParentRoot"
                );


            GameObject childRoot =
                CreateGameObject(
                    "ChildRoot"
                );


            childRoot.transform.SetParent(
                parentRoot.transform
            );


            GameObject meshObject =
                CreateGameObject(
                    "Mesh"
                );


            meshObject.transform.SetParent(
                childRoot.transform
            );


            meshObject.AddComponent
                <SkinnedMeshRenderer>();


            List<GameObject> roots =
                new List<GameObject>
                {
                    parentRoot,
                    childRoot
                };


            List<List<GameObject>> result =
                ItemTogglerSetupWindow
                    .BuildUniqueMeshLists(
                        roots
                    );


            Assert.AreEqual(
                2,
                result.Count
            );


            Assert.AreEqual(
                1,
                result[0].Count
            );


            Assert.AreEqual(
                meshObject,
                result[0][0]
            );


            Assert.AreEqual(
                0,
                result[1].Count
            );
        }


        [Test]
        public void 選択0件なら追加ボタンは無効()
        {
            bool result =
                ItemTogglerSetupWindow
                    .CanEnableAddButton(
                        0,
                        true,
                        true
                    );


            Assert.IsFalse(
                result
            );
        }


        [Test]
        public void 選択1件なら追加ボタンは有効()
        {
            bool result =
                ItemTogglerSetupWindow
                    .CanEnableAddButton(
                        1,
                        true,
                        true
                    );


            Assert.IsTrue(
                result
            );
        }


        [Test]
        public void 選択があっても8項目調整不能なら追加ボタンは無効()
        {
            bool result =
                ItemTogglerSetupWindow
                    .CanEnableAddButton(
                        1,
                        true,
                        false
                    );


            Assert.IsFalse(
                result
            );
        }


        [Test]
        public void 自動調整OFFなら選択があれば追加可能()
        {
            bool result =
                ItemTogglerSetupWindow
                    .CanEnableAddButton(
                        1,
                        false,
                        false
                    );


            Assert.IsTrue(
                result
            );
        }


        private GameObject CreateGameObject(
            string name
        )
        {
            GameObject gameObject =
                new GameObject(
                    name
                );


            createdObjects.Add(
                gameObject
            );


            return gameObject;
        }
    }
}