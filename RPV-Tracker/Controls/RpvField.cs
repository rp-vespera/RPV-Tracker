using System;
using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;

namespace RPV_Tracker.Controls
{
    /// <summary>
    /// A labelled form input following the brand's input spec: 13px Stone label above,
    /// 6px gap, 40px white field with a 6px radius, Steel focus ring, Danger error ring.
    /// </summary>
    internal class RpvField : UserControl
    {
        private const int LabelHeight = 18;
        private const int LabelGap = 6;
        private const int InputTop = LabelHeight + LabelGap;
        private const int SidePadding = 13;

        private readonly Label captionLabel;
        private readonly TextBox input;

        private string placeholder = string.Empty;
        private bool isPassword;
        private bool showingPlaceholder;
        private bool hasError;
        private bool inputFocused;

        public RpvField()
        {
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);

            BackColor = RpvTheme.Cream;

            captionLabel = new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Location = new Point(0, 0),
                Height = LabelHeight,
                TextAlign = ContentAlignment.MiddleLeft
            };

            input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Charcoal,
                BackColor = RpvTheme.White,
                TabIndex = 0
            };

            input.Enter += (s, e) => { inputFocused = true; HidePlaceholder(); Invalidate(); };
            input.Leave += (s, e) => { inputFocused = false; ShowPlaceholderIfEmpty(); Invalidate(); };
            input.TextChanged += (s, e) => OnValueChanged(EventArgs.Empty);

            Controls.Add(captionLabel);
            Controls.Add(input);

            // Set last: assigning Height raises OnResize, which needs the children to exist.
            Height = InputTop + RpvTheme.InputHeight;
        }

        /// <summary>Raised when the user edits the value. Not raised for placeholder swaps.</summary>
        public event EventHandler ValueChanged;

        public string LabelText
        {
            get { return captionLabel.Text; }
            set { captionLabel.Text = value; }
        }

        public string PlaceholderText
        {
            get { return placeholder; }
            set
            {
                placeholder = value ?? string.Empty;
                if (!input.Focused)
                {
                    ShowPlaceholderIfEmpty();
                }
            }
        }

        public bool IsPassword
        {
            get { return isPassword; }
            set
            {
                isPassword = value;
                input.UseSystemPasswordChar = value && !showingPlaceholder;
            }
        }

        /// <summary>The typed value. Returns empty string while the placeholder is displayed.</summary>
        public string Value
        {
            get { return showingPlaceholder ? string.Empty : input.Text; }
            set
            {
                showingPlaceholder = false;
                input.ForeColor = RpvTheme.Charcoal;
                input.UseSystemPasswordChar = isPassword;
                input.Text = value ?? string.Empty;
                if (!input.Focused)
                {
                    ShowPlaceholderIfEmpty();
                }
            }
        }

        /// <summary>Paints the error border. The message itself belongs to the form, not the field.</summary>
        public bool HasError
        {
            get { return hasError; }
            set { hasError = value; Invalidate(); }
        }

        public new bool Focus()
        {
            return input.Focus();
        }

        private void HidePlaceholder()
        {
            if (!showingPlaceholder)
            {
                return;
            }

            showingPlaceholder = false;
            input.Text = string.Empty;
            input.ForeColor = RpvTheme.Charcoal;
            input.UseSystemPasswordChar = isPassword;
        }

        private void ShowPlaceholderIfEmpty()
        {
            if (input.Text.Length != 0 || placeholder.Length == 0)
            {
                return;
            }

            showingPlaceholder = true;
            // The placeholder must stay legible, so password masking is lifted while it shows.
            input.UseSystemPasswordChar = false;
            input.ForeColor = RpvTheme.Stone;
            input.Text = placeholder;
        }

        private void OnValueChanged(EventArgs e)
        {
            if (showingPlaceholder)
            {
                return;
            }

            EventHandler handler = ValueChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            ShowPlaceholderIfEmpty();
            base.OnLoad(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Resize can fire during base construction and from the designer, both times
            // before the children are in place.
            if (captionLabel == null || input == null)
            {
                return;
            }

            Height = InputTop + RpvTheme.InputHeight;
            captionLabel.Width = Width;
            input.Left = SidePadding;
            input.Width = Math.Max(0, Width - (SidePadding * 2));
            input.Top = InputTop + ((RpvTheme.InputHeight - input.Height) / 2);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            input.Focus();
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            RpvTheme.EnableSmoothing(g);

            var box = new Rectangle(0, InputTop, Width - 1, RpvTheme.InputHeight - 1);

            Color borderColor;
            float borderWidth;
            if (hasError)
            {
                borderColor = RpvTheme.Danger;
                borderWidth = 1.5f;
            }
            else if (inputFocused)
            {
                borderColor = RpvTheme.Steel;
                borderWidth = 1.5f;
            }
            else
            {
                borderColor = RpvTheme.InputBorder;
                borderWidth = 1f;
            }

            using (var path = RpvTheme.RoundedRect(box, 6))
            {
                using (var brush = new SolidBrush(RpvTheme.White))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(borderColor, borderWidth))
                {
                    g.DrawPath(pen, path);
                }
            }

            base.OnPaint(e);
        }
    }
}
