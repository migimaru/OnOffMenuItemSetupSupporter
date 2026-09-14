using System;
using System.Collections.Generic;

namespace Migimaru.OnOffMenuItemSetupSupporter.Editor
{
    internal static class MenuPageCalculator
    {
        internal const int MaxMenuItemCount = 8;
        internal const int BackItemCount = 1;


        internal static int GetAvailableItemSlots(
            int childFolderCount,
            int existingItemCount,
            bool reserveNextPage
        )
        {
            if (childFolderCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(childFolderCount)
                );
            }


            if (existingItemCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(existingItemCount)
                );
            }


            int usedSlots =
                BackItemCount
                + childFolderCount
                + existingItemCount;


            if (reserveNextPage)
            {
                usedSlots++;
            }


            return MaxMenuItemCount - usedSlots;
        }


        internal static bool CanCreateNextPage(
            int childFolderCount,
            int existingItemCount
        )
        {
            return GetAvailableItemSlots(
                childFolderCount,
                existingItemCount,
                true
            ) >= 0;
        }


        internal static bool TryCreateNewPagePlan(
            int newItemCount,
            int firstPageChildFolderCount,
            int firstPageExistingItemCount,
            out List<int> pageItemCounts
        )
        {
            pageItemCounts =
                new List<int>();


            if (newItemCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(newItemCount)
                );
            }


            if (firstPageChildFolderCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(firstPageChildFolderCount)
                );
            }


            if (firstPageExistingItemCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(firstPageExistingItemCount)
                );
            }


            if (newItemCount == 0)
            {
                return true;
            }


            int remainingItemCount =
                newItemCount;


            bool isFirstPage =
                true;


            while (remainingItemCount > 0)
            {
                int childFolderCount =
                    isFirstPage
                        ? firstPageChildFolderCount
                        : 0;


                int existingItemCount =
                    isFirstPage
                        ? firstPageExistingItemCount
                        : 0;


                int availableWithoutNextPage =
                    GetAvailableItemSlots(
                        childFolderCount,
                        existingItemCount,
                        false
                    );


                if (availableWithoutNextPage < 0)
                {
                    pageItemCounts.Clear();

                    return false;
                }


                if (
                    remainingItemCount
                    <= availableWithoutNextPage
                )
                {
                    pageItemCounts.Add(
                        remainingItemCount
                    );


                    return true;
                }


                int availableWithNextPage =
                    GetAvailableItemSlots(
                        childFolderCount,
                        existingItemCount,
                        true
                    );


                if (availableWithNextPage < 0)
                {
                    pageItemCounts.Clear();

                    return false;
                }


                int itemCountForThisPage =
                    Math.Min(
                        remainingItemCount,
                        availableWithNextPage
                    );


                pageItemCounts.Add(
                    itemCountForThisPage
                );


                remainingItemCount -=
                    itemCountForThisPage;


                isFirstPage =
                    false;
            }


            return true;
        }
    }
}