using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;
using RPV_Tracker.Domains.Auth.Models;

namespace RPV_Tracker.Forms
{
    /// <summary>
    /// The account card that drops from the nav bar when the signed-in name is clicked:
    /// who you are, and the ids the backend knows you by.
    /// </summary>
    /// <remarks>
    /// The three ids are shown separately on purpose. An account id, an employee id, and a
    /// business-partner id are different numbers for the same person (1, 3723 and 3363 here),
    /// and endpoints disagree about which one they want — so seeing all three at once is what
    /// makes a "wrong id" bug obvious rather than mysterious. Values copy on click for exactly
    /// that reason.
    ///
    /// Built as a borderless top-level window rather than a panel inside the shell so it can
    /// overhang the nav bar, float above page content, and close on click-away.
    /// </remarks>
    internal sealed class ProfilePopup : Form
    {
        // Sized so a full work email fits on one line at FontBodyMedium — the longest value
        // on the card by a wide margin, and the one most likely to be read rather than copied.
        private const int PopupWidth = 396;
        private const int CaptionWidth = 118;
        private const int Pad = RpvTheme.Space5;
        private const int RowHeight = 28;
        private const int HeaderHeight = 44;

        private readonly Label hintLabel;
        private readonly Timer hintResetTimer;

        public ProfilePopup(AuthenticatedUser user)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = RpvTheme.CardSurface;
            Font = RpvTheme.FontBody;
            KeyPreview = true;

            var monogram = new Monogram
            {
                Initials = user.Initials,
                Bounds = new Rectangle(Pad, Pad, 40, 40)
            };

            var nameLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontH3,
                ForeColor = RpvTheme.HeadingText,
                Text = string.IsNullOrWhiteSpace(user.FullName) ? "Signed in" : user.FullName,
                Bounds = new Rectangle(Pad + 52, Pad, PopupWidth - Pad - 52 - Pad, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                AutoEllipsis = true
            };

            var positionLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Steel,
                Text = string.IsNullOrWhiteSpace(user.Role) ? "Employee" : user.Role,
                Bounds = new Rectangle(Pad + 52, Pad + 22, PopupWidth - Pad - 52 - Pad, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                AutoEllipsis = true
            };

            Controls.Add(monogram);
            Controls.Add(nameLabel);
            Controls.Add(positionLabel);

            int y = Pad + HeaderHeight + RpvTheme.Space3;
            Controls.Add(MakeDivider(y));
            y += RpvTheme.Space3;

            var rows = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Position", user.Role),
                new KeyValuePair<string, string>("Employee ID", user.EmployeeId),
                new KeyValuePair<string, string>("S_BPartner ID", user.BusinessPartnerId),
                new KeyValuePair<string, string>("Employee no.", user.EmployeeNo),
                new KeyValuePair<string, string>("Account ID", user.Id),
                new KeyValuePair<string, string>("Username", user.Username),
                new KeyValuePair<string, string>("Email", user.Email)
            };

            foreach (KeyValuePair<string, string> row in rows)
            {
                AddRow(row.Key, row.Value, y);
                y += RowHeight;
            }

            y += RpvTheme.Space2;
            Controls.Add(MakeDivider(y));
            y += RpvTheme.Space3;

            hintLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Text = "Click a value to copy it.",
                Bounds = new Rectangle(Pad, y, PopupWidth - (Pad * 2), 18),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            Controls.Add(hintLabel);

            ClientSize = new Size(PopupWidth, y + 18 + Pad);

            // Rounded to match CardPanel, so the popup reads as the same family of surface.
            using (var path = RpvTheme.RoundedRect(new Rectangle(0, 0, PopupWidth, ClientSize.Height), RpvTheme.RadiusMd))
            {
                Region = new Region(path);
            }

            hintResetTimer = new Timer { Interval = 1600 };
            hintResetTimer.Tick += (s, e) =>
            {
                hintResetTimer.Stop();
                if (!IsDisposed)
                {
                    hintLabel.ForeColor = RpvTheme.Stone;
                    hintLabel.Text = "Click a value to copy it.";
                }
            };
        }

        private Label MakeDivider(int y)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.Border,
                Bounds = new Rectangle(Pad, y, PopupWidth - (Pad * 2), 1)
            };
        }

        private void AddRow(string caption, string value, int y)
        {
            bool missing = string.IsNullOrWhiteSpace(value);
            string shown = missing ? "—" : value;

            var captionLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Stone,
                Text = caption,
                Bounds = new Rectangle(Pad, y, CaptionWidth, RowHeight),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };

            var valueLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = missing ? RpvTheme.Stone : RpvTheme.Charcoal,
                Text = shown,
                Bounds = new Rectangle(Pad + CaptionWidth, y, PopupWidth - Pad - CaptionWidth - Pad, RowHeight),
                TextAlign = ContentAlignment.MiddleRight,
                UseMnemonic = false,
                AutoEllipsis = true
            };

            if (!missing)
            {
                valueLabel.Cursor = Cursors.Hand;
                valueLabel.Click += (s, e) => Copy(caption, value);
            }

            Controls.Add(captionLabel);
            Controls.Add(valueLabel);
        }

        private void Copy(string caption, string value)
        {
            try
            {
                Clipboard.SetText(value);
                hintLabel.ForeColor = RpvTheme.Success;
                hintLabel.Text = "Copied " + caption + " — " + value;
            }
            catch (Exception)
            {
                // The clipboard is a shared OS resource another process can hold open. Losing
                // a copy is not worth an error dialog on top of a read-only profile card.
                hintLabel.ForeColor = RpvTheme.Warning;
                hintLabel.Text = "Couldn't copy — the clipboard was busy.";
            }

            hintResetTimer.Stop();
            hintResetTimer.Start();
        }

        /// <summary>
        /// Drops the card below <paramref name="anchor"/>, nudged back on screen if the shell
        /// sits near a screen edge.
        /// </summary>
        public void ShowBelow(Form owner, Control anchor)
        {
            Point origin = anchor.PointToScreen(new Point(0, anchor.Height));
            Rectangle screen = Screen.FromControl(anchor).WorkingArea;

            int left = Math.Min(origin.X, screen.Right - Width - RpvTheme.Space2);
            left = Math.Max(screen.Left + RpvTheme.Space2, left);

            int top = origin.Y + RpvTheme.Space1;
            if (top + Height > screen.Bottom)
            {
                top = Math.Max(screen.Top, screen.Bottom - Height - RpvTheme.Space2);
            }

            Location = new Point(left, top);
            Show(owner);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // The window has no chrome of its own — without this it floats as an edgeless
            // slab over page content in both themes.
            using (var pen = new Pen(RpvTheme.Border))
            {
                e.Graphics.DrawPath(pen,
                    RpvTheme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), RpvTheme.RadiusMd));
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && hintResetTimer != null)
            {
                hintResetTimer.Stop();
                hintResetTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
