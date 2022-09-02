﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Input;

namespace Avalonia.Controls
{
    /// <summary>
    /// A data source for a <see cref="TreeDataGrid"/> which displays a flat grid.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public class FlatTreeDataGridSource<TModel> : ITreeDataGridSource<TModel>, IDisposable
        where TModel: class
    {
        private IEnumerable<TModel> _items;
        private TreeDataGridItemsSourceView<TModel> _itemsView;
        private AnonymousSortableRows<TModel>? _rows;
        private IComparer<TModel>? _comparer;
        private ITreeDataGridSelection? _selection;
        private bool _isSelectionSet;

        public FlatTreeDataGridSource(IEnumerable<TModel> items)
        {
            _items = items;
            _itemsView = TreeDataGridItemsSourceView<TModel>.GetOrCreate(items);
            Columns = new ColumnList<TModel>();
        }

        public ColumnList<TModel> Columns { get; }
        public IRows Rows => _rows ??= CreateRows();
        IColumns ITreeDataGridSource.Columns => Columns;

        public IEnumerable<TModel> Items
        {
            get => _items;
            set
            {
                if (_items != value)
                {
                    _items = value;
                    _itemsView = TreeDataGridItemsSourceView<TModel>.GetOrCreate(value);
                    _rows?.SetItems(_itemsView);
                    if (_selection is object)
                        _selection.Source = value;
                }
            }
        }

        public ITreeDataGridSelection? Selection
        {
            get
            {
                if (_selection == null && !_isSelectionSet)
                    _selection = new TreeDataGridRowSelectionModel<TModel>(this);
                return _selection;
            }
            set
            {
                if (_selection is object)
                    throw new InvalidOperationException("Selection is already initialized.");
                _selection = value;
                _isSelectionSet = true;
            }
        }

        public ITreeDataGridRowSelectionModel<TModel>? RowSelection => Selection as ITreeDataGridRowSelectionModel<TModel>;
        public bool IsHierarchical => false;
        public bool IsSorted => _comparer is not null;

        public event Action? Sorted;

        public void Dispose()
        {
            _rows?.Dispose();
            GC.SuppressFinalize(this);
        }

        void ITreeDataGridSource.DragDropRows(
            ITreeDataGridSource source,
            IEnumerable<IndexPath> indexes,
            IndexPath targetIndex,
            TreeDataGridRowDropPosition position,
            DragDropEffects effects)
        {
            if (!effects.HasAnyFlag(DragDropEffects.Move))
                throw new NotSupportedException("Only move is currently supported for drag/drop.");
            if (IsSorted)
                throw new NotSupportedException("Drag/drop is not supported on sorted data.");
            if (position == TreeDataGridRowDropPosition.Inside)
                throw new ArgumentException("Invalid drop position.", nameof(position));
            if (targetIndex.Count != 1)
                throw new ArgumentException("Invalid target index.", nameof(targetIndex));
            if (_items is not IList<TModel> items)
                throw new InvalidOperationException("Items does not implement IList<T>.");

            if (position == TreeDataGridRowDropPosition.None)
                return;

            var i = targetIndex[0];

            if (position == TreeDataGridRowDropPosition.After && i < items.Count - 1)
                ++i;

            foreach (var src in indexes)
            {
                if (src.Count != 1)
                    throw new ArgumentException($"Invalid source index '{src}'.", nameof(indexes));
                var item = items[src[0]];
                items.RemoveAt(src[0]);
                items.Insert(i++, item);
            }
        }

        bool ITreeDataGridSource.SortBy(IColumn? column, ListSortDirection direction)
        {
            if (column is IColumn<TModel> typedColumn)
            {
                if (!Columns.Contains(typedColumn))
                    return true;

                var comparer = typedColumn.GetComparison(direction);

                if (comparer is not null)
                {
                    _comparer = comparer is not null ? new FuncComparer<TModel>(comparer) : null;
                    _rows?.Sort(_comparer);
                    Sorted?.Invoke();
                    foreach (var c in Columns)
                        c.SortDirection = c == column ? direction : null;
                }
                return true;
            }

            return false;
        }

        private AnonymousSortableRows<TModel> CreateRows()
        {
            return new AnonymousSortableRows<TModel>(_itemsView, _comparer);
        }
    }
}
