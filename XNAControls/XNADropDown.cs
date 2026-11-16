using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rampastring.Tools;
using Rampastring.XNAUI.FontManagement;
using System;
using System.Collections.Generic;
using Rampastring.XNAUI.Extensions;

namespace Rampastring.XNAUI.XNAControls;

public enum DropDownState
{
    CLOSED,
    OPENED_DOWN,
    OPENED_UP
}

/// <summary>
/// A drop-down control with optional scrollable item list,
/// scroll bar child control and close-on-click-outside behaviour.
/// </summary>
public class XNADropDown : XNAControl
{
    protected const int DefaultScrollBarWidth = 3;

    /// <summary>Number of rows scrolled per mouse wheel notch, matching the vanilla behaviour.</summary>
    protected const int ScrollWheelItemStep = 3;

    /// <summary>Current scroll offset in item count for scrollable lists.</summary>
    protected int _scrollOffset;

    /// <summary>Explicit cap on the number of visible rows when open. 0 = no explicit cap.</summary>
    protected int _maxVisibleItems;

    /// <summary>True while the user is dragging the scroll bar thumb.</summary>
    protected bool _isDraggingScrollBar;

    /// <summary>When true, the next click is ignored to prevent accidental selection after drag.</summary>
    protected bool _skipNextItemSelection;

    /// <summary>Index of the item currently under the cursor, or -1 if none.</summary>
    protected int _correctHoveredIndex = -1;

    /// <summary>Cached mouse state from the previous frame for delta calculations.</summary>
    protected MouseState _previousMouseState;

    /// <summary>The thin scroll bar child control shown when the list is scrollable.</summary>
    protected XNAScrollBar _dropDownScrollBar;

    /// <summary>
    /// Gets the number of rows the open list can show at once: the smaller of
    /// <see cref="MaxVisibleItems"/> (0 = no explicit cap) and the number of rows
    /// that fit on screen next to the list's closed-state anchor
    /// (<see cref="numFittingItems"/>). Before the list has been laid open,
    /// <see cref="numFittingItems"/> is 0 and the screen cap does not apply.
    /// </summary>
    protected int VisibleLimit
    {
        get
        {
            int cappedRows = _maxVisibleItems > 0 ? _maxVisibleItems : int.MaxValue;
            int fittingRows = numFittingItems > 0 ? numFittingItems : int.MaxValue;

            return Math.Min(cappedRows, fittingRows);
        }
    }

    /// <summary>True when there are more items than fit in <see cref="VisibleLimit"/>
    /// rows, so the open list needs a scroll bar.</summary>
    protected bool _isScrollable => Items.Count > VisibleLimit;

    /// <summary>
    /// Gets or sets the maximum number of visible items when the dropdown is open.
    /// Use 0 to show as many items as fit on screen. A scroll bar appears whenever
    /// the item list is longer than the visible row count (<see cref="VisibleLimit"/>).
    /// </summary>
    public int MaxVisibleItems
    {
        get => _maxVisibleItems;
        set => _maxVisibleItems = Math.Max(0, value);
    }

    /// <summary>
    /// Creates a new drop-down control.
    /// </summary>
    /// <param name="windowManager">The WindowManager associated with this control.</param>
    public XNADropDown(WindowManager windowManager) : base(windowManager)
    {
        ItemHeight = UISettings.ActiveSettings.DropDownDefaultItemHeight.GetValueOrDefault((int)FontManager.GetTextDimensions("Test String @", FontIndex).Y + 1);
        Height = ItemHeight + 2;
        HandlesDragging = true;
    }

    public delegate void SelectedIndexChangedEventHandler(object sender, EventArgs e);
    public event SelectedIndexChangedEventHandler SelectedIndexChanged;

    /// <summary>
    /// Raised when the user re-selects an already selected drop-down item.
    /// </summary>
    public event EventHandler IndexReselected;

    /// <summary>
    /// The index of the top-most visible drop down item.
    /// </summary>
    public int TopIndex { get; set; }

    /// <summary>The height of drop-down items in pixels.</summary>
    public int ItemHeight { get; set; }

    public int? BorderThickness
    {
        get => field ?? UISettings.ActiveSettings.BorderThickness;
        set => field = value;
    }

    /// <summary>The list of items displayed in the drop-down.</summary>
    public List<XNADropDownItem> Items = new List<XNADropDownItem>();

    /// <summary>Gets the current drop-down state (closed, opened down, or opened up).</summary>
    public DropDownState DropDownState { get; private set; }

    private bool _allowDropDown = true;

    /// <summary>
    /// Controls whether the drop-down control can be dropped down.
    /// </summary>
    public bool AllowDropDown
    {
        get => _allowDropDown;
        set
        {
            _allowDropDown = value;
            if (!_allowDropDown && DropDownState != DropDownState.CLOSED)
            {
                CloseDropDown();
            }
        }
    }

    private int _selectedIndex = -1;

    /// <summary>
    /// Gets or sets the currently selected item index.
    /// Fires <see cref="SelectedIndexChanged"/> or <see cref="IndexReselected"/> on set,
    /// and keeps the selected item in view while the list is open.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int oldSelectedIndex = _selectedIndex;

            _selectedIndex = value;

            if (value != oldSelectedIndex)
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            else
                IndexReselected?.Invoke(this, EventArgs.Empty);

            // Keep the selected item in view while the list is open.
            if (DropDownState != DropDownState.CLOSED)
                EnsureSelectedIndexVisible(resetOnNoSelection: false);
        }
    }

    /// <summary>Gets the currently selected item, or null if none is selected.</summary>
    public XNADropDownItem SelectedItem
    {
        get
        {
            if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
                return null;

            return Items[SelectedIndex];
        }
    }

    public int FontIndex { get; set; }

    private Color? _borderColor;

    public Color BorderColor
    {
        get => _borderColor ?? UISettings.ActiveSettings.PanelBorderColor;
        set => _borderColor = value;
    }

    private Color? _focusColor;

    public Color FocusColor
    {
        get => _focusColor ?? UISettings.ActiveSettings.FocusColor;
        set => _focusColor = value;
    }

    private Color? _backColor;

    public Color BackColor
    {
        get => _backColor ?? UISettings.ActiveSettings.DropDownBackgroundColor;
        set => _backColor = value;
    }

    private Color? _listBackgroundColor;

    /// <summary>
    /// The background color of the drop down's list of selectable elements when the drop down is open.
    /// </summary>
    public Color ListBackgroundColor
    {
        get => _listBackgroundColor ?? UISettings.ActiveSettings.BackgroundColor;
        set => _listBackgroundColor = value;
    }

    private Color? _textColor;

    public Color TextColor
    {
        get => _textColor ?? UISettings.ActiveSettings.AltColor;
        set => _textColor = value;
    }

    private Color? _disabledItemColor;

    public Color DisabledItemColor
    {
        get => _disabledItemColor ?? UISettings.ActiveSettings.DisabledItemColor;
        set => _disabledItemColor = value;
    }

    /// <summary>
    /// If set, the drop-down is opened upwards rather than downwards.
    /// </summary>
    public bool OpenUp { get; set; }

    private bool _showEllipsisOnOverflow = false;

    /// <summary>
    /// When the selected item's text is too wide for the closed control, it is always
    /// cut off at the last fully fitting character. When enabled, an ellipsis ("...") is
    /// appended to indicate truncation.
    /// </summary>
    public bool ShowEllipsisOnOverflow
    {
        get => _showEllipsisOnOverflow;
        set
        {
            _showEllipsisOnOverflow = value;
            InvalidateDisplayTextCache();
        }
    }

    public Texture2D DropDownTexture { get; set; }
    public Texture2D DropDownOpenTexture { get; set; }

    public EnhancedSoundEffect ClickSoundEffect { get; set; }

    private bool clickedAfterOpen = false;
    private int numFittingItems = 0;

    /// <summary>
    /// Screen Y coordinate of the control's top edge in closed state, captured when the
    /// list is opened. Used to recompute how many items fit above the control when the
    /// list is open (<see cref="DropDownState.OPENED_UP"/>), including re-runs after
    /// <see cref="Items"/> changes while the list is open.
    /// </summary>
    private int openTop;

    /// <summary>
    /// Screen Y coordinate of the control's bottom edge in closed state, captured when
    /// the list is opened. Used to recompute how many items fit below the control when
    /// the list is open (<see cref="DropDownState.OPENED_DOWN"/>), including re-runs after
    /// <see cref="Items"/> changes while the list is open.
    /// </summary>
    private int openBottom;

    /// <summary>
    /// <see cref="Items"/>.Count snapshot taken when the list is opened. A difference from
    /// the current count means the item list was mutated while the list is open, which
    /// requires the open layout to be re-applied.
    /// </summary>
    private int openItemCount;

    /// <summary>
    /// The width of the open dropdown list. Computed in <see cref="OpenDropDown"/> as the
    /// maximum of <see cref="XNAControl.Width"/> and the widest item's content width, so
    /// wide items remain fully visible when the list is expanded.
    /// </summary>
    private int expandedListWidth = 0;

    /// <summary>
    /// Stores the control width used when the dropdown is closed so it can be restored
    /// after the expanded list has temporarily widened the control. Null when no closed
    /// width has been captured yet, so a legitimate <see cref="XNAControl.Width"/> of 0
    /// is preserved instead of being treated as "not captured".
    /// </summary>
    private int? closedWidth = null;

    /// <summary>
    /// Ensures the framework-visible control width matches the actual visible dropdown
    /// width while the list is open, and restores the original width when the list closes.
    /// </summary>
    private void SyncWidthToDropDownState()
    {
        if (DropDownState != DropDownState.CLOSED)
        {
            if (closedWidth == null)
                closedWidth = Width;

            int openWidth = Math.Max(closedWidth.Value, expandedListWidth);
            if (Width != openWidth)
                Width = openWidth;
        }
        else
        {
            if (closedWidth != null && Width != closedWidth.Value)
                Width = closedWidth.Value;
            closedWidth = null;
            expandedListWidth = 0;
        }
    }

    /// <summary>
    /// Current visible width of the control, accounting for whether the dropdown list
    /// is open and may be wider than the closed-state <see cref="XNAControl.Width"/>.
    /// </summary>
    private int CurrentDisplayWidth
    {
        get
        {
            SyncWidthToDropDownState();
            return Width;
        }
    }

    // Cache for the truncated display text produced by GetDisplayTextForSelectedItem.
    // That method is called every frame from DrawSelectedItem; without this cache it
    // would re-run the MeasureString truncation on each draw.
    private (string cachedDisplayText, int cachedSelectedIndex, int cachedWidth, int cachedFontIndex, string cachedItemText) displayTextCache = (null, -1, 0, 0, null);

    #region AddItem methods

    /// <summary>
    /// Adds an item into the drop-down.
    /// </summary>
    /// <param name="item">The item.</param>
    public void AddItem(XNADropDownItem item)
    {
        Items.Add(item);
    }

    /// <summary>
    /// Generates and adds an item with the specified text into the drop-down.
    /// </summary>
    /// <param name="text">The text of the item.</param>
    public void AddItem(string text)
    {
        var item = new XNADropDownItem();
        item.Text = text;

        Items.Add(item);
    }

    /// <summary>
    /// Generates and adds an item with the specified text and texture
    /// into the drop-down.
    /// </summary>
    /// <param name="text">The text of the item.</param>
    /// <param name="texture">The item's texture.</param>
    public void AddItem(string text, Texture2D texture)
    {
        var item = new XNADropDownItem();
        item.Text = text;
        item.Texture = texture;

        Items.Add(item);
    }

    /// <summary>
    /// Generates and adds an item with the specified text
    /// and text color into the drop-down control.
    /// </summary>
    /// <param name="text">The text of the item.</param>
    /// <param name="color">The color of the item's text.</param>
    public void AddItem(string text, Color color)
    {
        var item = new XNADropDownItem();
        item.Text = text;
        item.TextColor = color;

        Items.Add(item);
    }

    #endregion

    /// <summary>
    /// Initializes the drop-down textures, creates the scroll bar child control,
    /// and subscribes to input events.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        DropDownTexture = AssetLoader.LoadTexture("comboBoxArrow.png");
        DropDownOpenTexture = AssetLoader.LoadTexture("openedComboBoxArrow.png");

        Height = DropDownTexture.Height;

        _dropDownScrollBar = new XNAScrollBar(WindowManager)
        {
            Thin = true,
            ScrollBarWidth = DefaultScrollBarWidth,
            BarColor = BackColor,
            ThumbColor = FocusColor,
            ThumbBorderColor = BorderColor
        };
        _dropDownScrollBar.Scrolled += ScrollBar_Scrolled;

        _previousMouseState = Mouse.GetState();
        ClientRectangleUpdated += (s, e) => InvalidateDisplayTextCache();
    }

    /// <summary>
    /// Syncs _scrollOffset when the user clicks on the scroll bar track.
    /// </summary>
    private void ScrollBar_Scrolled(object sender, EventArgs e)
    {
        _scrollOffset = _dropDownScrollBar.ViewTop;
    }

    protected override void ParseControlINIAttribute(IniFile iniFile, string key, string value)
    {
        switch (key)
        {
            case "OpenUp":
                OpenUp = Conversions.BooleanFromString(value, OpenUp);
                return;
            case "DropDownTexture":
                DropDownTexture = AssetLoader.LoadTextureUncached(value);
                return;
            case "DropDownOpenTexture":
                DropDownOpenTexture = AssetLoader.LoadTextureUncached(value);
                return;
            case "ItemHeight":
                ItemHeight = Conversions.IntFromString(value, ItemHeight);
                return;
            case "MaxVisibleItems":
                MaxVisibleItems = Conversions.IntFromString(value, MaxVisibleItems);
                return;
            case "ClickSoundEffect":
                ClickSoundEffect = new EnhancedSoundEffect(value);
                return;
            case "FontIndex":
                FontIndex = Conversions.IntFromString(value, FontIndex);
                return;
            case "BorderColor":
                BorderColor = AssetLoader.GetRGBAColorFromString(value);
                return;
            case "FocusColor":
                FocusColor = AssetLoader.GetRGBAColorFromString(value);
                return;
            case "BackColor":
                BackColor = AssetLoader.GetRGBAColorFromString(value);
                return;
            case "DisabledItemColor":
                DisabledItemColor = AssetLoader.GetColorFromString(value);
                return;
            case "ShowEllipsisOnOverflow":
                ShowEllipsisOnOverflow = Conversions.BooleanFromString(value, false);
                return;
        }

        if (key.StartsWith("Option", StringComparison.InvariantCulture))
        {
            AddItem(value);
            return;
        }

        base.ParseControlINIAttribute(iniFile, key, value);
    }

    /// <summary>
    /// Gets the text color of a drop-down item.
    /// </summary>
    /// <param name="item">The item.</param>
    protected Color GetItemTextColor(XNADropDownItem item) => item.TextColor ?? TextColor;

    /// <summary>
    /// Per-frame update: tracks hover index, handles scroll bar dragging,
    /// and closes the drop-down when the user clicks outside its bounds.
    /// </summary>
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (DropDownState == DropDownState.CLOSED)
        {
            _correctHoveredIndex = -1;
            _previousMouseState = Mouse.GetState();

            return;
        }

        SyncOpenLayoutIfNeeded();

        Point cursorPoint = GetCursorPoint();
        MouseState mouseState = Mouse.GetState();

        if (_isDraggingScrollBar)
            _correctHoveredIndex = -1;
        else
            _correctHoveredIndex = GetHoveredIndexWithScroll(cursorPoint);

        // Scroll bar drag handling
        if (_isScrollable)
        {
            Rectangle sbBounds = new Rectangle(
                Width - _dropDownScrollBar.ScrollWidth,
                DropDownState == DropDownState.OPENED_DOWN ? DropDownTexture.Height + 1 : 1,
                _dropDownScrollBar.ScrollWidth,
                Height - DropDownTexture.Height - 2);

            if (mouseState.LeftButton == ButtonState.Pressed && sbBounds.Contains(cursorPoint))
            {
                _isDraggingScrollBar = true;
            }

            if (_isDraggingScrollBar)
            {
                // Sync happens via the scroll bar's Scrolled event (ScrollBar_Scrolled).
                Point sbLocal = new Point(cursorPoint.X - sbBounds.X, cursorPoint.Y - sbBounds.Y);
                _dropDownScrollBar.HandleTrackClick(new Point(sbLocal.X + _dropDownScrollBar.ScrollWidth / 2, sbLocal.Y));
            }

            if (mouseState.LeftButton == ButtonState.Released && _isDraggingScrollBar)
            {
                // Only arm the skip flag when the release happened on the control:
                // a release outside generates no click event, so an armed flag would
                // swallow the user's next item selection instead.
                bool releasedOnControl = cursorPoint.X >= 0 && cursorPoint.X <= CurrentDisplayWidth &&
                    cursorPoint.Y >= 0 && cursorPoint.Y <= Height;

                _skipNextItemSelection = releasedOnControl;
                _isDraggingScrollBar = false;
            }
        }

        // Close the dropdown when the user clicks outside its bounds while it is open.
        if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
        {
            if (cursorPoint.X < 0 || cursorPoint.X > CurrentDisplayWidth || cursorPoint.Y < 0 || cursorPoint.Y > Height)
            {
                CloseDropDown();
                _previousMouseState = mouseState;
                return;
            }
        }

        _previousMouseState = mouseState;
    }

    /// <summary>
    /// Handles left mouse button press: opens the drop-down list and detaches it
    /// from the parent panel so it can overlay other controls.
    /// </summary>
    public override void OnMouseLeftDown(InputEventArgs inputEventArgs)
    {
        base.OnMouseLeftDown(inputEventArgs);

        if (!AllowDropDown)
            return;

        inputEventArgs.Handled = true;

        if (DropDownState != DropDownState.CLOSED)
            return;

        ClickSoundEffect?.Play();

        clickedAfterOpen = false;

        OpenDropDown();

        Detach();
    }

    /// <summary>
    /// Opens the drop-down list: captures the closed-state screen anchors, calculates
    /// height, positions the scroll bar child when scrollable, adds it as a child
    /// control, and scrolls the selected item into view.
    /// </summary>
    public virtual void OpenDropDown()
    {
        TopIndex = 0;
        _scrollOffset = 0;

        Rectangle closedRectangle = GetWindowRectangle();

        openTop = closedRectangle.Top;
        openBottom = closedRectangle.Bottom;
        openItemCount = Items.Count;

        DropDownState = OpenUp ? DropDownState.OPENED_UP : DropDownState.OPENED_DOWN;

        ApplyOpenLayout(isOpening: true);

        // Scroll the selected item into view, or back to the top when nothing is selected.
        EnsureSelectedIndexVisible(resetOnNoSelection: true);
    }

    /// <summary>
    /// Applies the open list's layout: expanded width, how many items fit on screen
    /// (<see cref="numFittingItems"/>), control position/height, scroll offset clamping
    /// and the scroll bar child. Relies on the closed-state screen anchors captured in
    /// <see cref="OpenDropDown"/>, so it is safe to re-run when <see cref="Items"/> is
    /// mutated while the list is open.
    /// </summary>
    /// <param name="isOpening">True when called straight from <see cref="OpenDropDown"/>,
    /// false when re-syncing after a mid-open change of <see cref="Items"/>.</param>
    private void ApplyOpenLayout(bool isOpening)
    {
        expandedListWidth = closedWidth ?? Width;
        foreach (var item in Items)
        {
            int itemWidth = 4; //padding
            if (item.Texture != null)
                itemWidth += item.Texture.Width + 1;
            if (item.Text != null)
                itemWidth += (int)Math.Ceiling(Renderer.MeasureString(item.Text, FontIndex).X);

            if (itemWidth > expandedListWidth)
                expandedListWidth = itemWidth;
        }

        int visibleItemCount;

        if (DropDownState == DropDownState.OPENED_DOWN)
        {
            int availableHeight = WindowManager.RenderResolutionY - (openBottom + 1);
            numFittingItems = Math.Max(1, availableHeight / (ItemHeight * GetTotalScalingRecursive()));

            visibleItemCount = Math.Min(VisibleLimit, Items.Count);

            Height = DropDownTexture.Height + 2 + ItemHeight * visibleItemCount;
        }
        else
        {
            int availableHeight = openTop - 1;
            numFittingItems = Math.Max(1, availableHeight / (ItemHeight * GetTotalScalingRecursive()));

            visibleItemCount = Math.Min(VisibleLimit, Items.Count);

            int newHeight = DropDownTexture.Height + 1 + ItemHeight * visibleItemCount;

            if (isOpening)
                Y -= 1 + ItemHeight * visibleItemCount;
            else
                Y -= newHeight - Height;

            Height = newHeight;
        }

        // Keep the scroll offsets inside the valid item range in case Items was
        // mutated while the list was open.
        int maxScrollOffset = Math.Max(0, Items.Count - visibleItemCount);

        _scrollOffset = Math.Min(Math.Max(_scrollOffset, 0), maxScrollOffset);
        TopIndex = Math.Min(Math.Max(TopIndex, 0), maxScrollOffset);

        SyncWidthToDropDownState();

        // Position and configure the scroll bar child when the list is scrollable.
        if (_isScrollable)
        {
            int sbWidth = _dropDownScrollBar.ScrollWidth;
            int listHeight = Height - DropDownTexture.Height - 2;
            int sbY = DropDownState == DropDownState.OPENED_DOWN ? DropDownTexture.Height + 1 : 1;

            _dropDownScrollBar.ClientRectangle = new Rectangle(Width - sbWidth, sbY, sbWidth, listHeight);
            _dropDownScrollBar.Length = Items.Count;
            _dropDownScrollBar.DisplayedPixelCount = visibleItemCount;
            _dropDownScrollBar.ViewTop = _scrollOffset;
            _dropDownScrollBar.Refresh();

            if (_dropDownScrollBar.Parent == null)
                AddChild(_dropDownScrollBar);
        }
        else if (_dropDownScrollBar.Parent != null)
        {
            _isDraggingScrollBar = false;
            RemoveChild(_dropDownScrollBar);
        }
    }

    /// <summary>
    /// Re-applies the open list layout when <see cref="Items"/> has gained or lost
    /// entries while the list is open, so the height, scroll offsets and scroll bar
    /// always match the current item list.
    /// </summary>
    private void SyncOpenLayoutIfNeeded()
    {
        if (DropDownState == DropDownState.CLOSED || Items.Count == openItemCount)
            return;

        ApplyOpenLayout(isOpening: false);
        openItemCount = Items.Count;
    }

    /// <summary>
    /// Handles left click when the drop-down is open: selects items, handles
    /// scroll bar area clicks, and closes the list when clicking outside.
    /// </summary>
    public override void OnLeftClick(InputEventArgs inputEventArgs)
    {
        base.OnLeftClick(inputEventArgs);
        inputEventArgs.Handled = true;

        if (DropDownState == DropDownState.CLOSED)
            return;

        if (_skipNextItemSelection)
        {
            _skipNextItemSelection = false;
            return;
        }

        Point cursorPoint = GetCursorPoint();

        if (_isScrollable)
        {
            Rectangle itemsRect = DropDownState == DropDownState.OPENED_DOWN
                ? new Rectangle(0, DropDownTexture.Height, CurrentDisplayWidth - _dropDownScrollBar.ScrollWidth, Height - DropDownTexture.Height)
                : new Rectangle(0, 0, CurrentDisplayWidth - _dropDownScrollBar.ScrollWidth, Height - DropDownTexture.Height);

            if (!_isDraggingScrollBar && itemsRect.Contains(cursorPoint))
            {
                int relativeY = cursorPoint.Y - itemsRect.Y - 1;
                int visibleIndex = relativeY / ItemHeight;

                // Border pixels of the list area (relativeY < 0, or below the last visible row)
                // fall through to the header/outside handling below.
                if (relativeY >= 0 && visibleIndex < VisibleLimit)
                {
                    int clickedIndex = visibleIndex + _scrollOffset;

                    if (clickedIndex < Items.Count && Items[clickedIndex].Selectable)
                    {
                        SelectedIndex = clickedIndex;
                        ClickSoundEffect?.Play();
                        CloseDropDown();
                    }

                    // The click landed on a row: resolve it here; the legacy path
                    // below is reserved for header and outside clicks.
                    return;
                }
            }

            Rectangle sbBounds = new Rectangle(
                Width - _dropDownScrollBar.ScrollWidth,
                DropDownState == DropDownState.OPENED_DOWN ? DropDownTexture.Height + 1 : 1,
                _dropDownScrollBar.ScrollWidth,
                Height - DropDownTexture.Height - 2);

            if (sbBounds.Contains(cursorPoint))
                return;

            bool onHeader = DropDownState == DropDownState.OPENED_DOWN
                ? cursorPoint.Y < DropDownTexture.Height + 1
                : cursorPoint.Y > Height - DropDownTexture.Height - 1;

            bool outsideControl = cursorPoint.X < 0 || cursorPoint.X > CurrentDisplayWidth
                || cursorPoint.Y < 0 || cursorPoint.Y > Height;

            // Inside the list area but not on a row (list borders, gaps next to the scroll bar):
            // ignore the click instead of falling through to the legacy path below.
            if (!onHeader && !outsideControl)
                return;
        }

        int itemIndexOnCursor = GetItemIndexOnCursor();

        if (itemIndexOnCursor == -1 && !clickedAfterOpen)
        {
            clickedAfterOpen = true;
            return;
        }

        if (itemIndexOnCursor > -1)
        {
            if (Items[itemIndexOnCursor].Selectable)
                SelectedIndex = itemIndexOnCursor;
            else
                return;
        }

        ClickSoundEffect?.Play();

        CloseDropDown();
    }

    /// <summary>
    /// Closes the drop-down list: resets height, re-attaches to parent,
    /// clears hover/drag state, and removes the scroll bar child.
    /// </summary>
    protected virtual void CloseDropDown()
    {
        if (DropDownState == DropDownState.OPENED_UP)
        {
            Y = Bottom - DropDownTexture.Height;
        }

        Height = DropDownTexture.Height;
        DropDownState = DropDownState.CLOSED;
        SyncWidthToDropDownState();
        Attach();
        _isDraggingScrollBar = false;
        _skipNextItemSelection = false;
        _correctHoveredIndex = -1;

        if (_dropDownScrollBar.Parent != null)
            RemoveChild(_dropDownScrollBar);
    }

    /// <summary>
    /// Handles scroll wheel input. When closed, cycles through items.
    /// When open, scrolls the item list and syncs the scroll bar thumb.
    /// While the list is open, the window manager only delivers wheel events
    /// when the cursor is over this control, so no outside-suppression is needed here.
    /// </summary>
    public override void OnMouseScrolled(InputEventArgs inputEventArgs)
    {
        if (!AllowDropDown)
            return;

        if (DropDownState == DropDownState.CLOSED)
        {
            if (Cursor.ScrollWheelValue < 0)
            {
                if (SelectedIndex >= Items.Count - 1)
                    return;

                inputEventArgs.Handled = true;

                if (Items[SelectedIndex + 1].Selectable)
                    SelectedIndex++;
            }

            if (Cursor.ScrollWheelValue > 0)
            {
                if (SelectedIndex < 1)
                    return;

                inputEventArgs.Handled = true;

                if (Items[SelectedIndex - 1].Selectable)
                    SelectedIndex--;
            }
        }
        else if (_isScrollable)
        {
            bool scrolled = false;

            if (Cursor.ScrollWheelValue < 0)
            {
                int maxScrollOffset = Math.Max(0, Items.Count - VisibleLimit);
                if (_scrollOffset < maxScrollOffset)
                {
                    inputEventArgs.Handled = true;
                    _scrollOffset = Math.Min(_scrollOffset + ScrollWheelItemStep, maxScrollOffset);
                    scrolled = true;
                }
            }
            else if (Cursor.ScrollWheelValue > 0)
            {
                if (_scrollOffset > 0)
                {
                    inputEventArgs.Handled = true;
                    _scrollOffset = Math.Max(0, _scrollOffset - ScrollWheelItemStep);
                    scrolled = true;
                }
            }

            if (scrolled)
            {
                _dropDownScrollBar.ViewTop = _scrollOffset;
                _dropDownScrollBar.RefreshButtonY();
            }
        }

        base.OnMouseScrolled(inputEventArgs);
    }

    /// <summary>
    /// Returns the index of the item that the cursor currently points to,
    /// or -1 if the cursor is in the header area, or -2 if outside the control.
    /// </summary>
    private int GetItemIndexOnCursor()
    {
        Point p = GetCursorPoint();

        if (p.X < 0 || p.X > CurrentDisplayWidth ||
            p.Y > Height ||
            p.Y < 0)
        {
            return -2;
        }

        int topIndex = _isScrollable ? _scrollOffset : TopIndex;
        int itemIndex;

        if (DropDownState == DropDownState.OPENED_DOWN)
        {
            if (p.Y < DropDownTexture.Height + 1)
                return -1;

            int y = p.Y - DropDownTexture.Height - 1;
            itemIndex = topIndex + (y / ItemHeight);
        }
        else // if (DropDownState == DropDownState.OPENED_UP)
        {
            if (p.Y > ClientRectangle.Height - DropDownTexture.Height - 1)
                return -1;

            itemIndex = topIndex + (p.Y - 1) / ItemHeight;
        }

        if (itemIndex < Items.Count && itemIndex > -1)
        {
            return itemIndex;
        }

        return -1;
    }

    /// <summary>
    /// Invalidates the cached display text.
    /// </summary>
    private void InvalidateDisplayTextCache()
    {
        displayTextCache = (null, -1, 0, 0, null);
    }

    /// <summary>
    /// Gets the display text for the selected item, fitted to the control's bounds so
    /// it does not visually overrun the control. Appends an ellipsis to indicate
    /// truncation when <see cref="ShowEllipsisOnOverflow"/> is enabled.
    /// </summary>
    private string GetDisplayTextForSelectedItem(XNADropDownItem item, int textX)
    {
        (string cachedDisplayText, int cachedSelectedIndex, int cachedWidth, int cachedFontIndex, string cachedItemText) = displayTextCache;

        if (cachedDisplayText != null &&
            cachedSelectedIndex == SelectedIndex &&
            cachedWidth == Width &&
            cachedFontIndex == FontIndex &&
            cachedItemText == item.Text)
        {
            return cachedDisplayText;
        }

        string displayText = item.Text;

        if (item.Text != null)
        {
            int availableWidth = ShowEllipsisOnOverflow ? (Width - textX - DropDownTexture.Width - 2) : (Width - textX - 2);

            if (availableWidth <= 0)
            {
                displayText = string.Empty;
            }
            else
            {
                Vector2 textSize = Renderer.MeasureString(item.Text, FontIndex);

                if (textSize.X > availableWidth)
                {
                    const string ellipsis = "...";
                    float ellipsisWidth = ShowEllipsisOnOverflow
                        ? Renderer.MeasureString(ellipsis, FontIndex).X
                        : 0f;
                    float maxWidth = availableWidth - ellipsisWidth;

                    if (maxWidth <= 0)
                    {
                        displayText = string.Empty;
                    }
                    else
                    {
                        int bestFit = 0;
                        int low = 0;
                        int high = item.Text.Length;

                        while (low <= high)
                        {
                            int mid = low + ((high - low) / 2);
                            string test = item.Text.SubstringSurrogateAware(0, mid);
                            float currentWidth = Renderer.MeasureString(test, FontIndex).X;

                            if (currentWidth <= maxWidth)
                            {
                                bestFit = mid;
                                low = mid + 1;
                            }
                            else
                            {
                                high = mid - 1;
                            }
                        }

                        displayText = item.Text.SubstringSurrogateAware(0, bestFit);
                        if (ShowEllipsisOnOverflow)
                            displayText += ellipsis;
                    }
                }
            }
        }

        displayTextCache = (displayText, SelectedIndex, Width, FontIndex, item.Text);

        return displayText;
    }

    /// <summary>
    /// Draws the drop-down.
    /// </summary>
    public override void Draw(GameTime gameTime)
    {
        SyncOpenLayoutIfNeeded();

        Rectangle dropDownRect;
        if (DropDownState == DropDownState.CLOSED)
            dropDownRect = new Rectangle(0, 0, Width, Height);
        else if (DropDownState == DropDownState.OPENED_DOWN)
            dropDownRect = new Rectangle(0, 0, Width, DropDownTexture.Height);
        else
            dropDownRect = new Rectangle(0, Height - DropDownTexture.Height, Width, DropDownTexture.Height);

        FillRectangle(new Rectangle(dropDownRect.X + 1, dropDownRect.Y + 1,
            dropDownRect.Width - 2, dropDownRect.Height - 2), BackColor);
        DrawRectangle(dropDownRect, BorderColor, BorderThickness.Value);

        if (SelectedIndex > -1 && SelectedIndex < Items.Count)
        {
            XNADropDownItem item = Items[SelectedIndex];

            int textX = 3;
            if (item.Texture != null)
            {
                DrawTexture(item.Texture,
                    new Rectangle(1, dropDownRect.Y + 2,
                    item.Texture.Width, item.Texture.Height), Color.White);
                textX += item.Texture.Width + 1;
            }

            if (item.Text != null)
            {
                string displayText = GetDisplayTextForSelectedItem(item, textX);
                int textY = dropDownRect.Y + Renderer.GetSingleLineTextYPadding(FontIndex, dropDownRect.Height);
                DrawStringWithShadow(displayText, FontIndex, new Vector2(textX, textY), GetItemTextColor(item));
            }
        }

        if (AllowDropDown)
        {
            var ddRectangle = new Rectangle(Width - DropDownTexture.Width,
                dropDownRect.Y, DropDownTexture.Width, DropDownTexture.Height);

            if (DropDownState != DropDownState.CLOSED)
            {
                DrawTexture(DropDownOpenTexture,
                    ddRectangle, RemapColor);

                Rectangle listRectangle;

                if (DropDownState == DropDownState.OPENED_DOWN)
                    listRectangle = new Rectangle(0, DropDownTexture.Height, CurrentDisplayWidth, Height - DropDownTexture.Height);
                else
                    listRectangle = new Rectangle(0, 0, CurrentDisplayWidth, Height - DropDownTexture.Height);

                DrawRectangle(listRectangle, BorderColor);

                int topIndex = _isScrollable ? _scrollOffset : TopIndex;
                int visibleCount = Math.Min(VisibleLimit, Items.Count - topIndex);

                for (int i = 0; i < visibleCount; i++)
                {
                    int y = listRectangle.Y + 1 + i * ItemHeight;
                    DrawItem(topIndex + i, y);
                }
            }
            else
            {
                DrawTexture(DropDownTexture, ddRectangle, RemapColor);
            }
        }

        base.Draw(gameTime);
    }

    /// <summary>
    /// Draws a single drop-down item with the hovered and selected rows highlighted.
    /// This can be overridden in derived classes to customize the drawing code.
    /// </summary>
    /// <param name="index">The index of the item to be drawn.</param>
    /// <param name="y">The Y coordinate of the item's top border.</param>
    protected virtual void DrawItem(int index, int y)
    {
        if (_isScrollable)
        {
            int visibleIndex = index - _scrollOffset;
            if (visibleIndex < 0 || visibleIndex >= VisibleLimit)
                return;

            int baseY = DropDownState == DropDownState.OPENED_DOWN
                ? DropDownTexture.Height + 1
                : 1;
            y = baseY + visibleIndex * ItemHeight;
        }

        XNADropDownItem item = Items[index];

        int itemWidth = CurrentDisplayWidth - (_isScrollable ? _dropDownScrollBar.ScrollWidth + 1 : 2);

        // Both the hovered and the selected row are filled with the focus color.
        if (_correctHoveredIndex == index || SelectedIndex == index)
            FillRectangle(new Rectangle(1, y, itemWidth, ItemHeight), FocusColor);
        else
            FillRectangle(new Rectangle(1, y, itemWidth, ItemHeight), ListBackgroundColor);

        int textX = 2;
        if (item.Texture != null)
        {
            DrawTexture(item.Texture, new Rectangle(1, y + 1, item.Texture.Width, item.Texture.Height), Color.White);
            textX += item.Texture.Width + 1;
        }

        Color textColor;

        if (item.Selectable)
            textColor = GetItemTextColor(item);
        else
            textColor = DisabledItemColor;

        if (item.Text != null)
        {
            int textY = y + Renderer.GetTextYPadding(item.Text, FontIndex, ItemHeight);
            DrawStringWithShadow(item.Text, FontIndex, new Vector2(textX, textY), textColor);
        }
    }

    /// <summary>
    /// Ensures the currently selected item is visible in the scrollable list.
    /// Adjusts <see cref="_scrollOffset"/> if needed and syncs the scroll bar thumb.
    /// </summary>
    /// <param name="resetOnNoSelection">If true and no item is selected, resets scroll offset to 0.</param>
    protected void EnsureSelectedIndexVisible(bool resetOnNoSelection = false)
    {
        if (!_isScrollable || SelectedIndex < 0)
        {
            if (resetOnNoSelection)
                _scrollOffset = 0;
            return;
        }

        if (SelectedIndex < _scrollOffset)
            _scrollOffset = SelectedIndex;
        else if (SelectedIndex >= _scrollOffset + VisibleLimit)
            _scrollOffset = SelectedIndex - VisibleLimit + 1;

        // Keep the offset inside the item range even when SelectedIndex went stale,
        // e.g. because the item list shrank while the drop-down was closed.
        _scrollOffset = Math.Min(Math.Max(_scrollOffset, 0), Math.Max(0, Items.Count - VisibleLimit));

        _dropDownScrollBar.ViewTop = _scrollOffset;
        _dropDownScrollBar.RefreshButtonY();
    }

    /// <summary>
    /// Returns the index of the item that the cursor currently points to,
    /// or -1 if the cursor is outside the item area or over a non-selectable item.
    /// </summary>
    protected int GetHoveredIndexWithScroll(Point cursorPoint)
    {
        if (DropDownState == DropDownState.CLOSED)
            return -1;

        // The item area excludes the header row and the list border; when the list
        // is scrollable it also excludes the scroll bar strip on the right.
        int areaWidth = _isScrollable
            ? CurrentDisplayWidth - _dropDownScrollBar.ScrollWidth
            : Width;
        int areaY = DropDownState == DropDownState.OPENED_DOWN ? DropDownTexture.Height + 1 : 1;

        Rectangle itemsArea = new Rectangle(0, areaY, areaWidth, Height - DropDownTexture.Height - 2);

        if (!itemsArea.Contains(cursorPoint))
            return -1;

        int visibleIndex = (cursorPoint.Y - itemsArea.Y) / ItemHeight;
        int visibleRowCount = _isScrollable ? VisibleLimit : Items.Count;

        if (visibleIndex < 0 || visibleIndex >= visibleRowCount)
            return -1;

        int index = visibleIndex + (_isScrollable ? _scrollOffset : 0);

        if (index < Items.Count && Items[index].Selectable)
            return index;

        return -1;
    }
}
