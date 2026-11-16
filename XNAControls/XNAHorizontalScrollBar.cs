using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rampastring.XNAUI.XNAControls;

/// <summary>
/// A horizontal scroll bar that can be utilized for various other controls.
/// </summary>
/// <remarks>
/// See also the sibling <see cref="XNAScrollBar"/> class.
/// Inherits scroll state and math from <see cref="XNAScrollBarBase"/>;
/// the scroll axis runs along <see cref="XNAControl.Width"/>.
/// </remarks>
public class XNAHorizontalScrollBar : XNAScrollBarBase
{
    /// <summary>
    /// Creates a new scroll bar.
    /// </summary>
    /// <param name="windowManager">The game window manager.</param>
    public XNAHorizontalScrollBar(WindowManager windowManager) : base(windowManager)
    {
        ExclusiveInputCapture = true;
        HandlesDragging = true;

        var scrollLeftTexture = AssetLoader.LoadTexture("hsbLeftArrow.png");

        btnScrollLeft = new XNAButton(WindowManager);
        btnScrollLeft.Name = nameof(btnScrollLeft);
        btnScrollLeft.ClientRectangle = new Rectangle(0, 0, scrollLeftTexture.Width, scrollLeftTexture.Height);
        btnScrollLeft.IdleTexture = scrollLeftTexture;
        if (AssetLoader.AssetExists("hsbLeftArrowHovered.png"))
            btnScrollLeft.HoverTexture = AssetLoader.LoadTexture("hsbLeftArrowHovered.png");

        var scrollRightTexture = AssetLoader.LoadTexture("hsbRightArrow.png");

        btnScrollRight = new XNAButton(WindowManager);
        btnScrollRight.Name = nameof(btnScrollRight);
        btnScrollRight.ClientRectangle = new Rectangle(Width - scrollRightTexture.Width, 0,
            scrollRightTexture.Width, scrollRightTexture.Height);
        btnScrollRight.IdleTexture = scrollRightTexture;
        if (AssetLoader.AssetExists("hsbRightArrowHovered.png"))
            btnScrollRight.HoverTexture = AssetLoader.LoadTexture("hsbRightArrowHovered.png");

        // The arrow buttons cap the track, so their widths serve as the
        // header/footer sizes of the base class.
        HeaderHeight = btnScrollLeft.Width;
        FooterHeight = btnScrollRight.Width;

        ScrolledToBottom += (s, e) => ScrolledToRight?.Invoke(this, EventArgs.Empty);

        ClientRectangleUpdated += XNAScrollBar_ClientRectangleUpdated;
    }

    private void XNAScrollBar_ClientRectangleUpdated(object sender, EventArgs e)
    {
        btnScrollRight.ClientRectangle = new Rectangle(Width - btnScrollRight.Width, 0,
            btnScrollRight.Width, btnScrollRight.Height);
        Refresh();
    }

    /// <summary>
    /// Raised when the scroll bar is scrolled and it reaches its rightmost value.
    /// </summary>
    public event EventHandler ScrolledToRight;

    /// <summary>
    /// The scroll bar's current position, measured from the left edge of the content.
    /// Alias of <see cref="XNAScrollBarBase.ViewTop"/> for horizontal use.
    /// The parent of the scroll-bar has to keep the scrollbar up-to-date when the
    /// view of the parent changes.
    /// </summary>
    public int ViewLeft
    {
        get => ViewTop;
        set => ViewTop = value;
    }

    /// <summary>
    /// Returns the height of the scroll bar.
    /// </summary>
    public int ScrollHeight => btnScrollLeft.IdleTexture.Height;

    private XNAButton btnScrollLeft;

    private XNAButton btnScrollRight;

    private Texture2D background;
    private Texture2D thumbMiddle;
    private Texture2D thumbLeft;
    private Texture2D thumbRight;

    private bool isHeldDown = false;

    public override void Initialize()
    {
        base.Initialize();

        AddChild(btnScrollLeft);
        AddChild(btnScrollRight);

        btnScrollLeft.LeftClick += (s, e) => ScrollUp();
        btnScrollRight.LeftClick += (s, e) => ScrollDown();

        background = AssetLoader.LoadTexture("hsbBackground.png");
        thumbMiddle = AssetLoader.LoadTexture("hsbMiddle.png");
        thumbLeft = AssetLoader.LoadTexture("hsbThumbLeft.png");
        thumbRight = AssetLoader.LoadTexture("hsbThumbRight.png");
    }

    public override void Kill()
    {
        // These textures are cached, don't allow the buttons to dispose them
        btnScrollRight.IdleTexture = null;
        btnScrollRight.HoverTexture = null;
        btnScrollLeft.IdleTexture = null;
        btnScrollLeft.HoverTexture = null;

        base.Kill();
    }

    /// <inheritdoc/>
    protected override void OnRefreshNoScroll()
    {
        btnScrollRight.Disable();
        btnScrollLeft.Disable();
    }

    /// <inheritdoc/>
    protected override void OnRefreshWithScroll()
    {
        btnScrollRight.Enable();
        btnScrollLeft.Enable();
    }

    /// <inheritdoc/>
    protected override int AxisLength => Width;

    /// <inheritdoc/>
    protected override int AxisCoordinate(Point cursorPoint) => cursorPoint.X;

    /// <summary>
    /// Updates the horizontal position of the scroll bar's thumb
    /// from a new <paramref name="viewLeft"/> value.
    /// </summary>
    public void RefreshButtonX(int viewLeft) => RefreshButtonY(viewLeft);

    /// <summary>
    /// Updates the horizontal position of the scroll bar's thumb
    /// from the current <see cref="ViewLeft"/>.
    /// </summary>
    public void RefreshButtonX() => RefreshButtonY();

    /// <inheritdoc/>
    public override void OnLeftClick(InputEventArgs inputEventArgs)
    {
        if (IsDrawn())
        {
            inputEventArgs.Handled = true;
            base.OnLeftClick(inputEventArgs);

            HandleTrackClick(GetCursorPoint());
        }
    }

    /// <inheritdoc/>
    public override void OnMouseMove()
    {
        base.OnMouseMove();

        // Only start dragging if the left button was pressed on this scroll bar;
        // otherwise a drag that started on another control (e.g. an open drop-down)
        // would scroll as soon as the cursor crosses the scroll bar.
        if (Cursor.LeftDown && IsLeftPressedOn)
        {
            HandleTrackClick(GetCursorPoint());
            isHeldDown = true;
            WindowManager.SelectedControl = this;
        }
    }

    /// <summary>
    /// Updates the scroll bar's logic each frame.
    /// Makes it possible to drag the scrollbar thumb even if the cursor
    /// leaves the scroll bar's surface.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (isHeldDown)
        {
            if (!Cursor.LeftDown)
            {
                isHeldDown = false;
                WindowManager.SelectedControl = null;
            }
            else
            {
                HandleTrackClick(GetCursorPoint());
            }
        }
    }

    /// <summary>
    /// Draws the scroll bar: background track, then the three-part thumb
    /// (left cap, stretched middle, right cap).
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    public override void Draw(GameTime gameTime)
    {
        if (scrollablePixels > 0)
        {
            DrawTexture(background, new Rectangle(0, 0, Width, Height), Color.White);

            // The base class tracks the thumb position and size along the scroll
            // axis in buttonY/thumbHeight regardless of orientation.
            int thumbX = buttonY;
            int thumbSize = thumbHeight;

            DrawTexture(thumbLeft, new Rectangle(thumbX, 0, thumbLeft.Width, ScrollHeight), RemapColor);
            DrawTexture(thumbRight, new Rectangle(thumbX + thumbSize - thumbRight.Width,
                0, thumbRight.Width, ScrollHeight), Color.White);
            DrawTexture(thumbMiddle, new Rectangle(thumbX + thumbLeft.Width,
                0, thumbSize - thumbLeft.Width - thumbRight.Width, ScrollHeight), Color.White);
        }

        base.Draw(gameTime);
    }
}
