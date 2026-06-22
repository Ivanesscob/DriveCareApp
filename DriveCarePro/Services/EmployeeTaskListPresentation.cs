using DriveCarePro.Services.ServiceDocuments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Services
{
    /// <summary>Подсветка и статусы заданий в списке на главной и в дереве.</summary>
    internal static class EmployeeTaskListPresentation
    {
        public const string ReadyToCompleteStatus = "Готово к завершению";
        public const string DelegatedInProgressStatus = "Передано сотруднику";
        public const string DelegateDoneStatus = "Исполнитель завершил — завершите своё";
        public const string DefaultWorkStatus = "В работе";

        public static string NormalizeBaseStatus(string statusFromDb)
        {
            var s = (statusFromDb ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
                return DefaultWorkStatus;
            if (IsModerationLikeStatus(s))
                return DefaultWorkStatus;
            return s;
        }

        public static bool IsModerationLikeStatus(string status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Length == 0)
                return false;
            return s.IndexOf("модерац", StringComparison.OrdinalIgnoreCase) >= 0
                   || s.IndexOf("moderation", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void EnrichTaskForest(IList<TaskTreeNodeVm> roots)
        {
            if (roots == null)
                return;
            AssignLevelNumbers(roots);
            foreach (var root in roots)
                EnrichNode(root);
        }

        /// <summary>Нумерация узлов: 1 → 1.1 → 1.2 → 2.1.1 …</summary>
        public static void AssignLevelNumbers(IList<TaskTreeNodeVm> roots)
        {
            if (roots == null)
                return;

            for (var i = 0; i < roots.Count; i++)
                AssignLevelNumberRecursive(roots[i], (i + 1).ToString());
        }

        private static void AssignLevelNumberRecursive(TaskTreeNodeVm node, string number)
        {
            if (node == null)
                return;

            node.LevelNumber = number;
            for (var i = 0; i < node.Children.Count; i++)
                AssignLevelNumberRecursive(node.Children[i], number + "." + (i + 1));
        }

        /// <summary>Раскрыть ветку и прокрутить к текущему заданию в TreeView.</summary>
        public static void ExpandAndFocusCurrentTask(TreeView tree, Guid currentTaskId)
        {
            if (tree == null || currentTaskId == Guid.Empty)
                return;

            foreach (var item in tree.Items)
            {
                var container = tree.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container != null && ExpandTreeViewItemToTask(container, currentTaskId))
                    return;
            }
        }

        private static bool ExpandTreeViewItemToTask(TreeViewItem item, Guid taskId)
        {
            item.IsExpanded = true;

            if (item.DataContext is TaskTreeNodeVm vm && vm.TaskId == taskId)
            {
                item.IsSelected = true;
                item.BringIntoView();
                return true;
            }

            foreach (var childObj in item.Items)
            {
                var childItem = item.ItemContainerGenerator.ContainerFromItem(childObj) as TreeViewItem;
                if (childItem != null && ExpandTreeViewItemToTask(childItem, taskId))
                    return true;
            }

            return false;
        }

        /// <summary>Раскрыть все уровни дерева, чтобы была видна вся цепочка.</summary>
        public static void ExpandAllTreeNodes(TreeView tree)
        {
            if (tree == null)
                return;

            foreach (var item in tree.Items)
            {
                var container = tree.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container != null)
                    ExpandTreeViewItemFully(container);
            }
        }

        private static void ExpandTreeViewItemFully(TreeViewItem item)
        {
            item.IsExpanded = true;
            item.UpdateLayout();
            foreach (var childObj in item.Items)
            {
                var childItem = item.ItemContainerGenerator.ContainerFromItem(childObj) as TreeViewItem;
                if (childItem != null)
                    ExpandTreeViewItemFully(childItem);
            }
        }

        private static void EnrichNode(TaskTreeNodeVm node)
        {
            foreach (var child in node.Children)
                EnrichNode(child);

            if (!node.IsCurrentEmployeeTask || node.IsCompleted || node.Children.Count == 0)
                return;

            if (!AllDescendantsCompletedInTree(node))
                return;

            node.IsReadyToComplete = true;
            node.StatusDisplay = DelegateDoneStatus;
        }

        public static bool AllDescendantsCompletedInTree(TaskTreeNodeVm node)
        {
            foreach (var child in node.Children)
            {
                if (!child.IsCompleted)
                    return false;
                if (!AllDescendantsCompletedInTree(child))
                    return false;
            }

            return true;
        }

        public static void ApplyFlatRowPresentation(ProHomeDataService.EmployeeTaskRowVm row, bool hasDelegate, bool allDescendantsDone)
        {
            if (row == null)
                return;

            row.StatusDisplay = NormalizeBaseStatus(row.StatusDisplay);

            if (allDescendantsDone && hasDelegate)
            {
                row.IsReadyToComplete = true;
                row.IsPartnerDone = true;
                row.StatusDisplay = DelegateDoneStatus;
                return;
            }

            if (hasDelegate)
            {
                row.IsPartnerDone = false;
                row.StatusDisplay = DelegatedInProgressStatus;
            }
        }
    }
}
