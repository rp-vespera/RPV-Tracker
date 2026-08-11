using System;
using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;

namespace RPV_Tracker.Pages
{
    /// <summary>
    /// Empty state for sections that are routed but not built yet. Follows the brand's
    /// empty-state pattern: Stone text and a single Terracotta call to action.
    /// </summary>
    internal class PlaceholderPage : UserControl
    {
        private readonly Label titleLabel;
        private readonly Label bodyLabel;
        private readonly RpvButton actionButton;

        public PlaceholderPage(string sectionTitle)
        {
            BackColor = RpvTheme.Cream;
            DoubleBuffered = true;

            titleLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.Cream,
                Font = RpvTheme.FontH2,
                ForeColor = RpvTheme.HeadingText,
                Height = 32,
                Text = sectionTitle,
                TextAlign = ContentAlignment.MiddleCenter
            };

            bodyLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.Cream,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Stone,
                Height = 24,
                Text = "This section isn't built yet.",
                TextAlign = ContentAlignment.MiddleCenter
            };

            actionButton = new RpvButton
            {
                Text = "Back to dashboard",
                Variant = RpvButtonVariant.Primary,
                Size = new Size(200, RpvTheme.InputHeight)
            };
            actionButton.Click += (s, e) => OnBackRequested(EventArgs.Empty);

            Controls.Add(titleLabel);
            Controls.Add(bodyLabel);
            Controls.Add(actionButton);
        }

        /// <summary>Raised when the empty-state CTA is pressed.</summary>
        public event EventHandler BackRequested;

        protected virtual void OnBackRequested(EventArgs e)
        {
            EventHandler handler = BackRequested;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            int centreY = (ClientSize.Height / 2) - 60;
            titleLabel.SetBounds(0, centreY, ClientSize.Width, 32);
            bodyLabel.SetBounds(0, centreY + 36, ClientSize.Width, 24);
            actionButton.SetBounds((ClientSize.Width - actionButton.Width) / 2,
                centreY + 76, actionButton.Width, actionButton.Height);
        }
    }
}
