using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Rampastring.XNAUI.XNAControls;

/// <summary>
/// A vertical scroll bar that can be utilized for various other controls.
/// Renders using textures for the thumb (top, middle, bottom parts) and background,
/// and provides up/down arrow buttons.
/// </summary>
/// <remarks>
/// See also the sibling <see cref="XNAHorizontalScrollBar"/> class.
/// Inherits scroll state and math from <see cref="XNAScrollBarBase"/>.
/// </remarks>
public class XNAScrollBar : XNAScrollBarBase
{
    /// <summary>
    /// Creates a new scroll bar.
    /// </summary>
    /// <param name="windowManager">The game window manager.</param>
    public XNAScrollBar(WindowManager windowManager) : base(windowManager)
    {
        ClientRectangleUpdated += XNAScrollBar_ClientRectangleUpdated;
    }

    private void XNAScrollBar_ClientRectangleUpdated(object sender, EventArgs e)
    {
        // The buttons are created in Initialize; thin-mode scroll bars have no buttons.
        if (btnScrollDown == null)
            return;

        btnScrollDown.ClientRectangle = new Rectangle(0,
            Height - btnScrollDown.Height,
            btnScrollDown.Width, btnScrollDown.Height);
        Refresh();
    }

    /// <summary>
    /// Returns the width of the scroll bar's up-arrow texture,
    /// or <see cref="ScrollBarWidth"/> in thin mode.
    /// The texture is loaded lazily so thin-mode scroll bars never touch
    /// the sb*.png assets (and callers may query the width before Initialize).
    /// Overridden to avoid circular dependency with <see cref="XNAControl.Width"/>.
    /// </summary>
    public override int ScrollWidth => Thin ? ScrollBarWidth : UpArrowTexture.Width;

    private Texture2D UpArrowTexture => upArrowTexture ??= AssetLoader.LoadTexture("sbUpArrow.png");

    /// <summary>
    /// Whether the scroll bar renders in a thin style: a solid track and thumb drawn with
    /// <see cref="XNAControl.FillRectangle"/> at <see cref="ScrollBarWidth"/> pixels wide,
    /// without up/down buttons and without the scroll bar's own click and drag handling
    /// (input is then driven by the owning control, e.g. a drop-down list).
    /// Must be set before the control is initialized.
    /// </summary>
    public bool Thin { get; set; }

    /// <summary>
    /// The width of the track and thumb in thin mode, in pixels.
    /// </summary>
    public int ScrollBarWidth { get; set; } = 3;

    /// <summary>
    /// Background color of the track in thin mode.
    /// </summary>
    public Color BarColor { get; set; } = Color.Black;

    /// <summary>
    /// Fill color of the thumb in thin mode.
    /// </summary>
    public Color ThumbColor { get; set; } = Color.White;

    /// <summary>
    /// Border color around the thumb in thin mode.
    /// </summary>
    public Color ThumbBorderColor { get; set; } = Color.Gray;

    private XNAButton btnScrollUp;

    private XNAButton btnScrollDown;

    private Texture2D upArrowTexture;

    private Texture2D background;
    private Texture2D thumbMiddle;
    private Texture2D thumbTop;
    private Texture2D thumbBottom;

    private bool isHeldDown = false;

    public override void Initialize()
    {
        // The drop-down list adds and removes this scroll bar as a child control
        // every time it opens and closes, so initialization can run more than once.
        if (Initialized)
            return;

        base.Initialize();

        // Thin-mode scroll bars have no buttons or textures of their own;
        // input and layout are driven by the owning control (e.g. a drop-down list).
        if (Thin)
            return;

        ExclusiveInputCapture = true;
        HandlesDragging = true;

        var scrollUpTexture = UpArrowTexture;

        btnScrollUp = new XNAButton(WindowManager);
        btnScrollUp.Name = nameof(btnScrollUp);
        btnScrollUp.ClientRectangle = new Rectangle(0, 0, scrollUpTexture.Width, scrollUpTexture.Height);
        btnScrollUp.IdleTexture = scrollUpTexture;
        if (AssetLoader.AssetExists("sbUpArrowHovered.png"))
            btnScrollUp.HoverTexture = AssetLoader.LoadTexture("sbUpArrowHovered.png");

        var scrollDownTexture = AssetLoader.LoadTexture("sbDownArrow.png");

        btnScrollDown = new XNAButton(WindowManager);
        btnScrollDown.Name = nameof(btnScrollDown);
        btnScrollDown.ClientRectangle = new Rectangle(0, Height - scrollDownTexture.Height,
            scrollDownTexture.Width, scrollDownTexture.Height);
        btnScrollDown.IdleTexture = scrollDownTexture;
        if (AssetLoader.AssetExists("sbDownArrowHovered.png"))
            btnScrollDown.HoverTexture = AssetLoader.LoadTexture("sbDownArrowHovered.png");

        AddChild(btnScrollUp);
        AddChild(btnScrollDown);

        btnScrollUp.LeftClick += (s, e) => ScrollUp();
        btnScrollDown.LeftClick += (s, e) => ScrollDown();

        background = AssetLoader.LoadTexture("sbBackground.png");
        thumbMiddle = AssetLoader.LoadTexture("sbMiddle.png");
        thumbTop = AssetLoader.LoadTexture("sbThumbTop.png");
        thumbBottom = AssetLoader.LoadTexture("sbThumbBottom.png");

        HeaderHeight = btnScrollUp.Height;
        FooterHeight = btnScrollDown.Height;

        // Recalculate with the now-known header/footer sizes; the owner may have
        // changed Length/DisplayedPixelCount (and triggered Refresh) before initialization.
        Refresh();
    }

    public override void Kill()
    {
        // Thin-mode scroll bars have no buttons. These textures are cached,
        // don't allow the buttons to dispose them.
        if (btnScrollDown != null)
        {
            btnScrollDown.IdleTexture = null;
            btnScrollDown.HoverTexture = null;
            btnScrollUp.IdleTexture = null;
            btnScrollUp.HoverTexture = null;
        }

        base.Kill();
    }

    /// <inheritdoc/>
    protected override void OnRefreshNoScroll()
    {
        // The buttons only exist on non-thin scroll bars, and only after Initialize;
        // Refresh can be triggered earlier via ClientRectangleUpdated.
        if (btnScrollUp == null)
            return;

        btnScrollUp.Disable();
        btnScrollDown.Disable();
    }

    /// <inheritdoc/>
    protected override void OnRefreshWithScroll()
    {
        if (btnScrollUp == null)
            return;

        btnScrollUp.Enable();
        btnScrollDown.Enable();
    }

    /// <inheritdoc/>
    public override void OnLeftClick(InputEventArgs inputEventArgs)
    {
        if (Thin)
        {
            // Thin mode has no click handling of its own; the owning control
            // (e.g. the drop-down list) decides what a click on the bar means.
            base.OnLeftClick(inputEventArgs);
        }
        else if (IsDrawn())
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

        // Thin mode has no thumb dragging of its own (see OnLeftClick).
        if (Thin)
            return;

        // Only start dragging if the left button was pressed on this scroll bar;
        // otherwise a drag that started on another control (e.g. an open drop-down)
        // would move the thumb as soon as the cursor crosses the scroll bar.
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
    /// (top cap, stretched middle, bottom cap).
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    public override void Draw(GameTime gameTime)
    {
        if (scrollablePixels > 0)
        {
            if (Thin)
            {
                FillRectangle(new Rectangle(0, 0, ScrollBarWidth, Height), BarColor);

                Rectangle thumbRect = new Rectangle(0, buttonY, ScrollBarWidth, thumbHeight);
                FillRectangle(thumbRect, ThumbColor);
                DrawRectangle(thumbRect, ThumbBorderColor, 1);
            }
            else
            {
                DrawTexture(background, new Rectangle(0, 0, Width, Height), Color.White);

                DrawTexture(thumbTop, new Rectangle(0, buttonY, ScrollWidth, thumbTop.Height), RemapColor);
                DrawTexture(thumbBottom, new Rectangle(0,
                    buttonY + thumbHeight - thumbBottom.Height, ScrollWidth, thumbBottom.Height), Color.White);
                DrawTexture(thumbMiddle, new Rectangle(0,
                    buttonY + thumbTop.Height, ScrollWidth, thumbHeight - thumbTop.Height - thumbBottom.Height), Color.White);
            }
        }

        base.Draw(gameTime);
    }
}
