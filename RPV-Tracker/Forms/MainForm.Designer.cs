using RPV_Tracker.Branding;
using RPV_Tracker.Controls;

namespace RPV_Tracker.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.contentPanel = new System.Windows.Forms.Panel();
            this.navBar = new System.Windows.Forms.Panel();
            this.navLogo = new RpvLogo();
            this.navLinksHost = new System.Windows.Forms.FlowLayoutPanel();
            this.userMonogram = new Monogram();
            this.userNameLabel = new System.Windows.Forms.Label();
            this.signOutLink = new NavLink();
            this.trackingIndicator = new System.Windows.Forms.Label();
            this.navBar.SuspendLayout();
            this.SuspendLayout();
            //
            // navLogo
            //
            this.navLogo.Location = new System.Drawing.Point(24, 14);
            this.navLogo.Name = "navLogo";
            this.navLogo.PointSize = 15F;
            this.navLogo.Size = new System.Drawing.Size(80, 28);
            this.navLogo.TabIndex = 0;
            this.navLogo.WordmarkColor = RpvTheme.White;
            //
            // navLinksHost
            //
            this.navLinksHost.BackColor = RpvTheme.Midnight;
            this.navLinksHost.Location = new System.Drawing.Point(120, 0);
            this.navLinksHost.Name = "navLinksHost";
            this.navLinksHost.Size = new System.Drawing.Size(620, 56);
            this.navLinksHost.TabIndex = 1;
            this.navLinksHost.WrapContents = false;
            //
            // userMonogram
            //
            this.userMonogram.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.userMonogram.Location = new System.Drawing.Point(878, 12);
            this.userMonogram.Name = "userMonogram";
            this.userMonogram.Size = new System.Drawing.Size(32, 32);
            this.userMonogram.TabIndex = 2;
            //
            // userNameLabel
            //
            this.userNameLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.userNameLabel.AutoSize = false;
            this.userNameLabel.BackColor = RpvTheme.Midnight;
            this.userNameLabel.Font = RpvTheme.FontBody;
            this.userNameLabel.ForeColor = RpvTheme.White;
            this.userNameLabel.Location = new System.Drawing.Point(918, 0);
            this.userNameLabel.Name = "userNameLabel";
            this.userNameLabel.Size = new System.Drawing.Size(170, 56);
            this.userNameLabel.TabIndex = 3;
            this.userNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // signOutLink
            //
            this.signOutLink.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.signOutLink.Location = new System.Drawing.Point(1092, 0);
            this.signOutLink.Name = "signOutLink";
            this.signOutLink.Size = new System.Drawing.Size(84, 56);
            this.signOutLink.TabIndex = 4;
            this.signOutLink.Text = "Sign out";
            this.signOutLink.Click += new System.EventHandler(this.signOutLink_Click);
            //
            // trackingIndicator
            //
            this.trackingIndicator.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.trackingIndicator.AutoSize = false;
            this.trackingIndicator.BackColor = RpvTheme.Midnight;
            this.trackingIndicator.Font = RpvTheme.FontCaption;
            this.trackingIndicator.ForeColor = RpvTheme.Ember;
            this.trackingIndicator.Location = new System.Drawing.Point(752, 0);
            this.trackingIndicator.Name = "trackingIndicator";
            this.trackingIndicator.Size = new System.Drawing.Size(116, 56);
            this.trackingIndicator.TabIndex = 5;
            this.trackingIndicator.Text = "● Recording";
            this.trackingIndicator.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.trackingIndicator.Visible = false;
            //
            // navBar
            //
            this.navBar.BackColor = RpvTheme.Midnight;
            this.navBar.Controls.Add(this.navLogo);
            this.navBar.Controls.Add(this.navLinksHost);
            this.navBar.Controls.Add(this.trackingIndicator);
            this.navBar.Controls.Add(this.userMonogram);
            this.navBar.Controls.Add(this.userNameLabel);
            this.navBar.Controls.Add(this.signOutLink);
            this.navBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.navBar.Location = new System.Drawing.Point(0, 0);
            this.navBar.Name = "navBar";
            this.navBar.Size = new System.Drawing.Size(1200, 56);
            this.navBar.TabIndex = 0;
            //
            // contentPanel
            //
            this.contentPanel.BackColor = RpvTheme.Cream;
            this.contentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.contentPanel.Location = new System.Drawing.Point(0, 56);
            this.contentPanel.Name = "contentPanel";
            this.contentPanel.Size = new System.Drawing.Size(1200, 704);
            this.contentPanel.TabIndex = 1;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = RpvTheme.Cream;
            this.ClientSize = new System.Drawing.Size(1200, 760);
            this.Controls.Add(this.contentPanel);
            this.Controls.Add(this.navBar);
            this.MinimumSize = new System.Drawing.Size(940, 620);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "RPV Workforce";
            this.navBar.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel navBar;
        private RpvLogo navLogo;
        private System.Windows.Forms.FlowLayoutPanel navLinksHost;
        private Monogram userMonogram;
        private System.Windows.Forms.Label userNameLabel;
        private NavLink signOutLink;
        private System.Windows.Forms.Label trackingIndicator;
        private System.Windows.Forms.Panel contentPanel;
    }
}
