using System.Collections.Generic;
using NUnit.Framework;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor.Tests
{
    public sealed class MenuPageCalculatorTests
    {
        [Test]
        public void 空ページに7個なら1ページに収まる()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    7,
                    0,
                    0,
                    out List<int> pages
                );


            Assert.IsTrue(result);

            CollectionAssert.AreEqual(
                new[]
                {
                    7
                },
                pages
            );
        }


        [Test]
        public void 空ページに8個ならNextPageが必要()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    8,
                    0,
                    0,
                    out List<int> pages
                );


            Assert.IsTrue(result);

            CollectionAssert.AreEqual(
                new[]
                {
                    6,
                    2
                },
                pages
            );
        }


        [Test]
        public void 空ページに14個なら3ページになる()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    14,
                    0,
                    0,
                    out List<int> pages
                );


            Assert.IsTrue(result);

            CollectionAssert.AreEqual(
                new[]
                {
                    6,
                    6,
                    2
                },
                pages
            );
        }


        [Test]
        public void 子MenuFolderが2個で5個登録なら1ページに収まる()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    5,
                    2,
                    0,
                    out List<int> pages
                );


            Assert.IsTrue(result);

            CollectionAssert.AreEqual(
                new[]
                {
                    5
                },
                pages
            );
        }


        [Test]
        public void 子MenuFolderが2個で6個登録ならNextPageが必要()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    6,
                    2,
                    0,
                    out List<int> pages
                );


            Assert.IsTrue(result);

            CollectionAssert.AreEqual(
                new[]
                {
                    4,
                    2
                },
                pages
            );
        }


        [Test]
        public void 子MenuFolderが6個で1個登録なら1ページに収まる()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    1,
                    6,
                    0,
                    out List<int> pages
                );


            Assert.IsTrue(result);

            CollectionAssert.AreEqual(
                new[]
                {
                    1
                },
                pages
            );
        }


        [Test]
        public void 子MenuFolderが6個で2個登録ならItemなしでNextPageを作れる()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    2,
                    6,
                    0,
                    out List<int> pages
                );


            Assert.IsTrue(result);

            CollectionAssert.AreEqual(
                new[]
                {
                    0,
                    2
                },
                pages
            );
        }


        [Test]
        public void 子MenuFolderが7個ある場合はNextPageを作れない()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    1,
                    7,
                    0,
                    out List<int> pages
                );


            Assert.IsFalse(result);

            Assert.AreEqual(
                0,
                pages.Count
            );
        }


        [Test]
        public void 既存Itemが6個で新規1個ならそのページに収まる()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    1,
                    0,
                    6,
                    out List<int> pages
                );


            Assert.IsTrue(result);

            CollectionAssert.AreEqual(
                new[]
                {
                    1
                },
                pages
            );
        }


        [Test]
        public void 既存Itemが6個で新規2個ならItemなしでNextPageを作る()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    2,
                    0,
                    6,
                    out List<int> pages
                );


            Assert.IsTrue(result);

            CollectionAssert.AreEqual(
                new[]
                {
                    0,
                    2
                },
                pages
            );
        }


        [Test]
        public void 既存Itemが7個ある場合はNextPageを作れない()
        {
            bool result =
                MenuPageCalculator.TryCreateNewPagePlan(
                    1,
                    0,
                    7,
                    out List<int> pages
                );


            Assert.IsFalse(result);

            Assert.AreEqual(
                0,
                pages.Count
            );
        }


        [Test]
        public void NextPageを予約した場合も合計8枠を超えない()
        {
            int available =
                MenuPageCalculator.GetAvailableItemSlots(
                    2,
                    3,
                    true
                );


            /*
             * Back        1
             * MenuFolder  2
             * Existing    3
             * NextPage    1
             * NewItem     1
             * ----------------
             * 合計        8
             */
            Assert.AreEqual(
                1,
                available
            );
        }


        [Test]
        public void 子MenuFolderが6個ならNextPageだけ置くことができる()
        {
            bool canCreate =
                MenuPageCalculator.CanCreateNextPage(
                    6,
                    0
                );


            Assert.IsTrue(
                canCreate
            );
        }


        [Test]
        public void 子MenuFolderが7個ならNextPageを置けない()
        {
            bool canCreate =
                MenuPageCalculator.CanCreateNextPage(
                    7,
                    0
                );


            Assert.IsFalse(
                canCreate
            );
        }
    }
}