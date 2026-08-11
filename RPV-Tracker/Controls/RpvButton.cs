using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;

namespace RPV_Tracker.Controls
{
    internal enum RpvButtonVariant
    {
        /// <summary>Terracotta fill. The action colour — use sparingly to preserve its urgency.</summary>
        Primary,

        /// <summary>Outlined Slate. For secondary actions sitting next to a primary.</summary>
        Secondary,

        /// <summary>Steel text only, underlined on hover. For low-emphasis links.</summary>
        Tertiary
    }

    /// <summary>Owner-drawn button implementing the three brand button styles.</summary>
    internal class RpvButton : Button
    {
        private const int CornerRadius = 6;

        private RpvButtonVariant variant = RpvButtonVariant.Primary;
        private bool hovered;
        private bool pressed;

        public RpvButton()
        {
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            UseVisualStyleBackColor = false;
            Font = RpvTheme.FontBodyMedium;
            Cursor = Cursors.Hand;
            Size = new Size(140, RpvTheme.InputHeight);
        }

        public RpvButtonVariant Variant
        {
            get { return variant; }
            set { variant = value; Invalidate(); }
        }

        protected override void OnMouseEnter(System.EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(System.EventArgs e)
        {
            hovered = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(System.EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(RpvTheme.BackdropFor(this));
            RpvTheme.EnableSmoothing(g);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            Color fill = Color.Empty;
            Color border = Color.Empty;
            Color text;

            switch (variant)
            {
                case RpvButtonVariant.Secondary:
                    fill = hovered && Enabled ? RpvTheme.Mist : Color.Empty;
                    border = Enabled ? RpvTheme.Slate : RpvTheme.Stone;
                    text = Enabled ? RpvTheme.Slate : RpvTheme.Stone;
                    break;

                case RpvButtonVariant.Tertiary:
                    text = Enabled ? RpvTheme.Steel : RpvTheme.Stone;
                    break;

                default:
                    if (!Enabled)
                    {
                        fill = RpvTheme.Sand;
                    }
                    else if (pressed || hovered)
                    {
                        fill = RpvTheme.ActionHover;
                    }
                    else
                    {
                        fill = RpvTheme.Terracotta;
                    }
                    text = RpvTheme.OnAccent;
                    break;
            }

            using (var path = RpvTheme.RoundedRect(bounds, CornerRadius))
            {
                if (!fill.IsEmpty)
                {
                    using (var brush = new SolidBrush(fill))
                    {
                        g.FillPath(brush, path);
                    }
                }

                if (!border.IsEmpty)
                {
                    using (var pen = new Pen(border, 1.5f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            TextRenderer.DrawText(g, Text, Font, bounds, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (variant == RpvButtonVariant.Tertiary && hovered && Enabled)
            {
                Size measured = TextRenderer.MeasureText(Text, Font);
                int underlineY = (Height + measured.Height) / 2;
                int x = (Width - measured.Width) / 2;
                using (var pen = new Pen(text))
                {
                    g.DrawLine(pen, x, underlineY, x + measured.Width, underlineY);
                }
            }

            // Keyboard focus needs a visible affordance; the default dotted rectangle
            // clashes with the rounded shape, so draw a soft ring instead.
            if (Focused && ShowFocusCues && Enabled)
            {
                using (var path = RpvTheme.RoundedRect(Rectangle.Inflate(bounds, -3, -3), CornerRadius - 2))
                using (var pen = new Pen(variant == RpvButtonVariant.Primary ? RpvTheme.Sand : RpvTheme.Steel))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }
}
