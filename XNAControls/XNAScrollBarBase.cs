using Microsoft.Xna.Framework;
using System;

namespace Rampastring.XNAUI.XNAControls;

/// <summary>
/// Abstract base class for scroll bars, containing shared scroll state, math and event handling.
/// Provides thumb position calculation, track click handling and scroll-by-step logic.
/// Subclasses supply rendering and optional button/input handling.
/// </summary>
/// <remarks>
/// The scrollbar uses <see cref="Length"/> and <see cref="DisplayedPixelCount"/> to calculate
/// thumb size and scrollable range. Call <see cref="Refresh()"/> after changing these values.
/// </remarks>
public abstract class XNAScrollBarBase : XNAControl
{
    protected XNAScrollBarBase(WindowManager windowManager) : base(windowManager)
    {
    }

    /// <summary>
    /// Added to the scroll range during track-click calculation so the thumb can reach
    /// the very bottom of the track despite integer truncation.
    /// </summary>
    private const float ScrollRoundingFactor = 0.99f;

    /// <summary>
    /// Minimum thumb size in pixels along the scroll axis.
    /// </summary>
    private const int MIN_THUMB_SIZE = 10;

    /// <summary>
    /// Fired when the scroll position changes via track click, button click or step scroll.
    /// </summary>
    public event EventHandler Scrolled;

    /// <summary>
    /// Fired when the scroll position reaches the very bottom of the track.
    /// </summary>
    public event EventHandler ScrolledToBottom;

    /// <summary>
    /// Total scrollable content length in pixels along the scroll axis
    /// (e.g. sum of all item heights in a vertical list).
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Number of pixels the parent control can display at once.
    /// </summary>
    public int DisplayedPixelCount { get; set; }

    /// <summary>
    /// Current scroll offset in pixels from the leading edge of the content
    /// (the top; aliased as <c>ViewLeft</c> on horizontal scroll bars).
    /// </summary>
    public int ViewTop { get; set; }

    /// <summary>
    /// How many pixels to scroll per button click or arrow key press.
    /// </summary>
    public int ScrollStep { get; set; } = 10;

    /// <summary>
    /// Gets the width used for layout calculations. Defaults to <see cref="XNAControl.Width"/>,
    /// can be overridden by subclasses that use a fixed width.
    /// </summary>
    public virtual int ScrollWidth => Width;

    /// <summary>Height of the thumb in pixels.</summary>
    protected int thumbHeight;

    /// <summary>Number of pixels the thumb can travel between top and bottom extremes.</summary>
    protected int scrollablePixels;

    /// <summary>Y coordinate (in control-local space) of the top of the clickable track area.</summary>
    protected int buttonMinY;

    /// <summary>Y coordinate of the bottom of the clickable track area.</summary>
    protected int buttonMaxY;

    /// <summary>Current Y position of the thumb's top edge.</summary>
    protected int buttonY;

    /// <summary>
    /// Fixed size reserved at the leading edge of the scroll axis for an
    /// up-button or other header element (the top for vertical bars, the left
    /// for horizontal ones). Set to 0 for scroll bars without buttons.
    /// </summary>
    protected int HeaderHeight { get; set; }

    /// <summary>
    /// Fixed size reserved at the trailing edge of the scroll axis for a
    /// down-button or other footer element (the bottom for vertical bars, the
    /// right for horizontal ones). Set to 0 for scroll bars without buttons.
    /// </summary>
    protected int FooterHeight { get; set; }

    /// <summary>
    /// Returns true when the content is longer than the display area and a thumb should be drawn.
    /// </summary>
    public bool IsDrawn() => scrollablePixels > 0;

    /// <summary>
    /// Length of the scroll bar along its scroll axis:
    /// <see cref="XNAControl.Height"/> for vertical bars (the default),
    /// <see cref="XNAControl.Width"/> for horizontal ones.
    /// </summary>
    protected virtual int AxisLength => Height;

    /// <summary>
    /// Returns the cursor coordinate along the scroll axis:
    /// Y for vertical bars (the default), X for horizontal ones.
    /// </summary>
    protected virtual int AxisCoordinate(Point cursorPoint) => cursorPoint.Y;

    /// <summary>
    /// Recalculates thumb size and track bounds from <see cref="Length"/>,
    /// <see cref="DisplayedPixelCount"/>, <see cref="HeaderHeight"/> and <see cref="FooterHeight"/>.
    /// Call after any of those values change.
    /// </summary>
    public void Refresh()
    {
        int trackLength = AxisLength - HeaderHeight - FooterHeight;
        int nonDisplayedLines = Length - DisplayedPixelCount;

        if (nonDisplayedLines <= 0)
        {
            thumbHeight = trackLength;
            scrollablePixels = 0;
            OnRefreshNoScroll();
        }
        else
        {
            thumbHeight = Math.Max(trackLength - (int)(trackLength * nonDisplayedLines / (double)Length),
                MIN_THUMB_SIZE);
            scrollablePixels = trackLength - thumbHeight;
            OnRefreshWithScroll();
        }

        buttonMinY = HeaderHeight + thumbHeight / 2;
        buttonMaxY = AxisLength - FooterHeight - (thumbHeight / 2);

        RefreshButtonY();
    }

    /// <summary>
    /// Called by <see cref="Refresh()"/> when all content fits without scrolling.
    /// Override to disable buttons or hide the thumb.
    /// </summary>
    protected virtual void OnRefreshNoScroll() { }

    /// <summary>
    /// Called by <see cref="Refresh()"/> when scrolling is needed.
    /// Override to enable buttons or show the thumb.
    /// </summary>
    protected virtual void OnRefreshWithScroll() { }

    /// <summary>
    /// Updates the thumb position from a new <paramref name="viewTop"/> value.
    /// </summary>
    public void RefreshButtonY(int viewTop)
    {
        ViewTop = viewTop;
        RefreshButtonY();
    }

    /// <summary>
    /// Recalculates <see cref="buttonY"/> from the current <see cref="ViewTop"/>.
    /// Clamped so the thumb never leaves the track bounds.
    /// </summary>
    public void RefreshButtonY()
    {
        int nonDisplayedLines = Length - DisplayedPixelCount;

        if (nonDisplayedLines <= 0)
        {
            buttonY = HeaderHeight;
            return;
        }

        buttonY = buttonMinY + (int)(((ViewTop / (double)nonDisplayedLines) * scrollablePixels) - thumbHeight / 2);
        buttonY = Math.Max(HeaderHeight, Math.Min(buttonY, AxisLength - FooterHeight - thumbHeight));
    }

    /// <summary>
    /// Handles a mouse click on the track area. Sets <see cref="ViewTop"/> to match
    /// the clicked position, clamped to valid bounds. Fires <see cref="Scrolled"/>.
    /// </summary>
    /// <param name="cursorPoint">Cursor position in control-local coordinates.</param>
    public void HandleTrackClick(Point cursorPoint)
    {
        if (!IsDrawn())
            return;

        int coordinate = AxisCoordinate(cursorPoint);

        if (coordinate < HeaderHeight
            || coordinate > AxisLength - FooterHeight)
        {
            return;
        }

        if (coordinate <= buttonMinY || DisplayedPixelCount >= Length)
        {
            ViewTop = 0;
            RefreshButtonY();
            Scrolled?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (coordinate >= buttonMaxY)
        {
            ViewTop = Length - DisplayedPixelCount;
            RefreshButtonY();
            Scrolled?.Invoke(this, EventArgs.Empty);
            ScrolledToBottom?.Invoke(this, EventArgs.Empty);
            return;
        }

        double difference = buttonMaxY - buttonMinY;
        double location = coordinate - buttonMinY;
        int nonDisplayedLines = Length - DisplayedPixelCount;

        ViewTop = (int)(location / difference * (nonDisplayedLines + ScrollRoundingFactor));
        ViewTop = Math.Max(0, Math.Min(ViewTop, nonDisplayedLines));
        RefreshButtonY();

        Scrolled?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Scrolls up by <see cref="ScrollStep"/> pixels. Clamps to 0.
    /// </summary>
    protected void ScrollUp()
    {
        if (ViewTop > 0)
        {
            ViewTop -= ScrollStep;
            if (ViewTop < 0)
                ViewTop = 0;
        }

        RefreshButtonY();
        Scrolled?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Scrolls down by <see cref="ScrollStep"/> pixels. Clamps to maximum.
    /// </summary>
    protected void ScrollDown()
    {
        int nonDisplayedLines = Length - DisplayedPixelCount;

        if (ViewTop < nonDisplayedLines)
            ViewTop = Math.Min(ViewTop + ScrollStep, nonDisplayedLines);

        RefreshButtonY();
        Scrolled?.Invoke(this, EventArgs.Empty);
    }
}
