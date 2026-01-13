using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MarketDataCollector.Uwp.Controls;

/// <summary>
/// A reusable data table component with built-in search, sort, pagination,
/// and empty state handling.
/// </summary>
public sealed partial class DataTable : UserControl
{
    #region Events

    public event EventHandler<string>? SearchChanged;
    public event EventHandler<int>? SortChanged;
    public event EventHandler? RefreshRequested;
    public event EventHandler<IList<object>>? SelectionChanged;
    public event EventHandler? EmptyActionClicked;
    public event EventHandler<int>? PageChanged;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(DataTable),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(DataTable),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(DataTable),
            new PropertyMetadata(null, OnItemTemplateChanged));

    public static readonly DependencyProperty ShowSearchProperty =
        DependencyProperty.Register(nameof(ShowSearch), typeof(bool), typeof(DataTable),
            new PropertyMetadata(true));

    public static readonly DependencyProperty SearchPlaceholderProperty =
        DependencyProperty.Register(nameof(SearchPlaceholder), typeof(string), typeof(DataTable),
            new PropertyMetadata("Search..."));

    public static readonly DependencyProperty ShowSortProperty =
        DependencyProperty.Register(nameof(ShowSort), typeof(bool), typeof(DataTable),
            new PropertyMetadata(false));

    public static readonly DependencyProperty SortOptionsProperty =
        DependencyProperty.Register(nameof(SortOptions), typeof(IList<string>), typeof(DataTable),
            new PropertyMetadata(null, OnSortOptionsChanged));

    public static readonly DependencyProperty ShowRefreshProperty =
        DependencyProperty.Register(nameof(ShowRefresh), typeof(bool), typeof(DataTable),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowPaginationProperty =
        DependencyProperty.Register(nameof(ShowPagination), typeof(bool), typeof(DataTable),
            new PropertyMetadata(false));

    public static readonly DependencyProperty PageSizeProperty =
        DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(DataTable),
            new PropertyMetadata(20));

    public static readonly DependencyProperty TotalItemsProperty =
        DependencyProperty.Register(nameof(TotalItems), typeof(int), typeof(DataTable),
            new PropertyMetadata(0));

    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(DataTable),
            new PropertyMetadata(1));

    public static readonly DependencyProperty MaxTableHeightProperty =
        DependencyProperty.Register(nameof(MaxTableHeight), typeof(double), typeof(DataTable),
            new PropertyMetadata(400.0));

    public static readonly DependencyProperty EmptyMessageProperty =
        DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(DataTable),
            new PropertyMetadata("No data available"));

    public static readonly DependencyProperty EmptyActionTextProperty =
        DependencyProperty.Register(nameof(EmptyActionText), typeof(string), typeof(DataTable),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SelectionModeProperty =
        DependencyProperty.Register(nameof(SelectionMode), typeof(ListViewSelectionMode), typeof(DataTable),
            new PropertyMetadata(ListViewSelectionMode.None));

    public static readonly DependencyProperty ShowCountBadgeProperty =
        DependencyProperty.Register(nameof(ShowCountBadge), typeof(bool), typeof(DataTable),
            new PropertyMetadata(true));

    #endregion

    #region Properties

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public bool ShowSearch
    {
        get => (bool)GetValue(ShowSearchProperty);
        set => SetValue(ShowSearchProperty, value);
    }

    public string SearchPlaceholder
    {
        get => (string)GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    public bool ShowSort
    {
        get => (bool)GetValue(ShowSortProperty);
        set => SetValue(ShowSortProperty, value);
    }

    public IList<string>? SortOptions
    {
        get => (IList<string>?)GetValue(SortOptionsProperty);
        set => SetValue(SortOptionsProperty, value);
    }

    public bool ShowRefresh
    {
        get => (bool)GetValue(ShowRefreshProperty);
        set => SetValue(ShowRefreshProperty, value);
    }

    public bool ShowPagination
    {
        get => (bool)GetValue(ShowPaginationProperty);
        set => SetValue(ShowPaginationProperty, value);
    }

    public int PageSize
    {
        get => (int)GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    public int TotalItems
    {
        get => (int)GetValue(TotalItemsProperty);
        set => SetValue(TotalItemsProperty, value);
    }

    public int CurrentPage
    {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public double MaxTableHeight
    {
        get => (double)GetValue(MaxTableHeightProperty);
        set => SetValue(MaxTableHeightProperty, value);
    }

    public string EmptyMessage
    {
        get => (string)GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    public string EmptyActionText
    {
        get => (string)GetValue(EmptyActionTextProperty);
        set => SetValue(EmptyActionTextProperty, value);
    }

    public ListViewSelectionMode SelectionMode
    {
        get => (ListViewSelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public bool ShowCountBadge
    {
        get => (bool)GetValue(ShowCountBadgeProperty);
        set => SetValue(ShowCountBadgeProperty, value);
    }

    // Computed properties
    public Visibility TitleVisibility => string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility CountVisibility => ShowCountBadge ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SearchVisibility => ShowSearch ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SortVisibility => ShowSort ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RefreshVisibility => ShowRefresh ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PaginationVisibility => ShowPagination ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyActionVisibility => string.IsNullOrEmpty(EmptyActionText) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility FooterVisibility => ShowPagination || SelectionMode != ListViewSelectionMode.None
        ? Visibility.Visible : Visibility.Collapsed;

    public string ItemCountDisplay => $"{GetItemCount()} items";
    public string PageInfoDisplay => $"Page {CurrentPage} of {TotalPages}";
    public string SelectionInfoDisplay => DataListView?.SelectedItems.Count > 0
        ? $"{DataListView.SelectedItems.Count} selected" : string.Empty;

    public int TotalPages => PageSize > 0 ? Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize)) : 1;
    public bool CanGoPrev => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;

    #endregion

    public DataTable()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateEmptyState();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DataTable table)
        {
            table.DataListView.ItemsSource = e.NewValue as IEnumerable;
            table.UpdateEmptyState();
        }
    }

    private static void OnItemTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DataTable table)
        {
            table.DataListView.ItemTemplate = e.NewValue as DataTemplate;
        }
    }

    private static void OnSortOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DataTable table && e.NewValue is IList<string> options)
        {
            table.SortCombo.Items.Clear();
            foreach (var option in options)
            {
                table.SortCombo.Items.Add(new ComboBoxItem { Content = option });
            }
        }
    }

    private void UpdateEmptyState()
    {
        var hasItems = GetItemCount() > 0;
        DataListView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
    }

    private int GetItemCount()
    {
        if (ItemsSource == null) return 0;

        if (ItemsSource is ICollection collection)
        {
            return collection.Count;
        }

        var count = 0;
        foreach (var _ in ItemsSource)
        {
            count++;
        }
        return count;
    }

    #region Event Handlers

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            SearchChanged?.Invoke(this, sender.Text);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        SearchChanged?.Invoke(this, args.QueryText);
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SortChanged?.Invoke(this, SortCombo.SelectedIndex);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DataListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedItems = new List<object>();
        foreach (var item in DataListView.SelectedItems)
        {
            selectedItems.Add(item);
        }
        SelectionChanged?.Invoke(this, selectedItems);
    }

    private void EmptyActionButton_Click(object sender, RoutedEventArgs e)
    {
        EmptyActionClicked?.Invoke(this, EventArgs.Empty);
    }

    private void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (CanGoPrev)
        {
            CurrentPage--;
            PageChanged?.Invoke(this, CurrentPage);
        }
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (CanGoNext)
        {
            CurrentPage++;
            PageChanged?.Invoke(this, CurrentPage);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        DataListView.SelectedItems.Clear();
    }

    /// <summary>
    /// Gets the currently selected items.
    /// </summary>
    public IList<object> GetSelectedItems()
    {
        var items = new List<object>();
        foreach (var item in DataListView.SelectedItems)
        {
            items.Add(item);
        }
        return items;
    }

    /// <summary>
    /// Scrolls to the specified item.
    /// </summary>
    public void ScrollToItem(object item)
    {
        DataListView.ScrollIntoView(item);
    }

    /// <summary>
    /// Clears the search text.
    /// </summary>
    public void ClearSearch()
    {
        SearchBox.Text = string.Empty;
    }

    #endregion
}
