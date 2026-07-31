using RPV_Tracker.Branding;
using RPV_Tracker.Controls;

namespace RPV_Tracker.Forms
{
    partial class LoginForm
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
            this.formPanel = new System.Windows.Forms.Panel();
            this.brandPanel = new System.Windows.Forms.Panel();
            this.brandLogo = new RpvLogo();
            this.brandWordmark = new System.Windows.Forms.Label();
            this.brandHeadline = new System.Windows.Forms.Label();
            this.taglineRule = new System.Windows.Forms.Panel();
            this.taglineLabel = new System.Windows.Forms.Label();
            this.titleLabel = new System.Windows.Forms.Label();
            this.subtitleLabel = new System.Windows.Forms.Label();
            this.usernameField = new RpvField();
            this.passwordField = new RpvField();
            this.errorLabel = new System.Windows.Forms.Label();
            this.signInButton = new RpvButton();
            this.forgotButton = new RpvButton();
            this.demoHintLabel = new System.Windows.Forms.Label();
            this.brandPanel.SuspendLayout();
            this.formPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // brandLogo
            //
            this.brandLogo.Location = new System.Drawing.Point(48, 52);
            this.brandLogo.Name = "brandLogo";
            this.brandLogo.PointSize = 26F;
            this.brandLogo.Size = new System.Drawing.Size(140, 46);
            this.brandLogo.TabIndex = 0;
            this.brandLogo.WordmarkColor = RpvTheme.White;
            //
            // brandWordmark
            //
            this.brandWordmark.AutoSize = false;
            this.brandWordmark.BackColor = System.Drawing.Color.Transparent;
            this.brandWordmark.Font = RpvTheme.FontCaption;
            this.brandWordmark.ForeColor = RpvTheme.Stone;
            this.brandWordmark.Location = new System.Drawing.Point(50, 100);
            this.brandWordmark.Name = "brandWordmark";
            this.brandWordmark.Size = new System.Drawing.Size(290, 20);
            this.brandWordmark.TabIndex = 1;
            this.brandWordmark.Text = "Renaissance Park & Vespera";
            // Without this the ampersand is swallowed as a mnemonic prefix.
            this.brandWordmark.UseMnemonic = false;
            //
            // brandHeadline
            //
            this.brandHeadline.AutoSize = false;
            this.brandHeadline.BackColor = System.Drawing.Color.Transparent;
            this.brandHeadline.Font = RpvTheme.FontH2;
            this.brandHeadline.ForeColor = RpvTheme.White;
            this.brandHeadline.Location = new System.Drawing.Point(48, 212);
            this.brandHeadline.Name = "brandHeadline";
            this.brandHeadline.Size = new System.Drawing.Size(284, 110);
            this.brandHeadline.TabIndex = 2;
            this.brandHeadline.Text = "Everything your team needs, in one place.";
            //
            // taglineRule
            //
            this.taglineRule.BackColor = RpvTheme.Terracotta;
            this.taglineRule.Location = new System.Drawing.Point(48, 476);
            this.taglineRule.Name = "taglineRule";
            this.taglineRule.Size = new System.Drawing.Size(48, 2);
            this.taglineRule.TabIndex = 3;
            //
            // taglineLabel
            //
            this.taglineLabel.AutoSize = false;
            this.taglineLabel.BackColor = System.Drawing.Color.Transparent;
            this.taglineLabel.Font = RpvTheme.FontCaption;
            this.taglineLabel.ForeColor = RpvTheme.Sand;
            this.taglineLabel.Location = new System.Drawing.Point(48, 490);
            this.taglineLabel.Name = "taglineLabel";
            this.taglineLabel.Size = new System.Drawing.Size(280, 20);
            this.taglineLabel.TabIndex = 4;
            this.taglineLabel.Text = "Forward · Responsible";
            //
            // brandPanel
            //
            this.brandPanel.BackColor = RpvTheme.Midnight;
            this.brandPanel.Controls.Add(this.brandLogo);
            this.brandPanel.Controls.Add(this.brandWordmark);
            this.brandPanel.Controls.Add(this.brandHeadline);
            this.brandPanel.Controls.Add(this.taglineRule);
            this.brandPanel.Controls.Add(this.taglineLabel);
            this.brandPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.brandPanel.Location = new System.Drawing.Point(0, 0);
            this.brandPanel.Name = "brandPanel";
            this.brandPanel.Size = new System.Drawing.Size(380, 580);
            this.brandPanel.TabIndex = 0;
            //
            // titleLabel
            //
            this.titleLabel.AutoSize = false;
            this.titleLabel.BackColor = System.Drawing.Color.Transparent;
            this.titleLabel.Font = RpvTheme.FontH1;
            this.titleLabel.ForeColor = RpvTheme.Midnight;
            this.titleLabel.Location = new System.Drawing.Point(80, 108);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(380, 38);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "Sign in";
            //
            // subtitleLabel
            //
            this.subtitleLabel.AutoSize = false;
            this.subtitleLabel.BackColor = System.Drawing.Color.Transparent;
            this.subtitleLabel.Font = RpvTheme.FontBody;
            this.subtitleLabel.ForeColor = RpvTheme.Stone;
            this.subtitleLabel.Location = new System.Drawing.Point(80, 150);
            this.subtitleLabel.Name = "subtitleLabel";
            this.subtitleLabel.Size = new System.Drawing.Size(380, 22);
            this.subtitleLabel.TabIndex = 1;
            this.subtitleLabel.Text = "Use your RPV workforce account.";
            //
            // usernameField
            //
            this.usernameField.BackColor = RpvTheme.Cream;
            this.usernameField.LabelText = "Username";
            this.usernameField.Location = new System.Drawing.Point(80, 202);
            this.usernameField.Name = "usernameField";
            this.usernameField.PlaceholderText = "your.username";
            this.usernameField.Size = new System.Drawing.Size(380, 64);
            this.usernameField.TabIndex = 2;
            //
            // passwordField
            //
            this.passwordField.BackColor = RpvTheme.Cream;
            this.passwordField.IsPassword = true;
            this.passwordField.LabelText = "Password";
            this.passwordField.Location = new System.Drawing.Point(80, 282);
            this.passwordField.Name = "passwordField";
            this.passwordField.PlaceholderText = "Enter your password";
            this.passwordField.Size = new System.Drawing.Size(380, 64);
            this.passwordField.TabIndex = 3;
            //
            // errorLabel
            //
            this.errorLabel.AutoSize = false;
            this.errorLabel.BackColor = System.Drawing.Color.Transparent;
            this.errorLabel.Font = RpvTheme.FontCaption;
            this.errorLabel.ForeColor = RpvTheme.Danger;
            this.errorLabel.Location = new System.Drawing.Point(80, 354);
            this.errorLabel.Name = "errorLabel";
            this.errorLabel.Size = new System.Drawing.Size(380, 36);
            this.errorLabel.TabIndex = 4;
            this.errorLabel.Visible = false;
            //
            // signInButton
            //
            this.signInButton.Location = new System.Drawing.Point(80, 396);
            this.signInButton.Name = "signInButton";
            this.signInButton.Size = new System.Drawing.Size(380, 44);
            this.signInButton.TabIndex = 5;
            this.signInButton.Text = "Sign in";
            this.signInButton.Variant = RpvButtonVariant.Primary;
            this.signInButton.Click += new System.EventHandler(this.signInButton_Click);
            //
            // forgotButton
            //
            this.forgotButton.Location = new System.Drawing.Point(80, 450);
            this.forgotButton.Name = "forgotButton";
            this.forgotButton.Size = new System.Drawing.Size(380, 32);
            this.forgotButton.TabIndex = 6;
            this.forgotButton.Text = "Forgot your password?";
            this.forgotButton.Variant = RpvButtonVariant.Tertiary;
            this.forgotButton.Click += new System.EventHandler(this.forgotButton_Click);
            //
            // demoHintLabel
            //
            this.demoHintLabel.AutoSize = false;
            this.demoHintLabel.BackColor = System.Drawing.Color.Transparent;
            this.demoHintLabel.Font = RpvTheme.FontCaption;
            this.demoHintLabel.ForeColor = RpvTheme.Stone;
            this.demoHintLabel.Location = new System.Drawing.Point(80, 502);
            this.demoHintLabel.Name = "demoHintLabel";
            this.demoHintLabel.Size = new System.Drawing.Size(380, 20);
            this.demoHintLabel.TabIndex = 7;
            this.demoHintLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.demoHintLabel.Visible = false;
            //
            // formPanel
            //
            this.formPanel.BackColor = RpvTheme.Cream;
            this.formPanel.Controls.Add(this.titleLabel);
            this.formPanel.Controls.Add(this.subtitleLabel);
            this.formPanel.Controls.Add(this.usernameField);
            this.formPanel.Controls.Add(this.passwordField);
            this.formPanel.Controls.Add(this.errorLabel);
            this.formPanel.Controls.Add(this.signInButton);
            this.formPanel.Controls.Add(this.forgotButton);
            this.formPanel.Controls.Add(this.demoHintLabel);
            this.formPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formPanel.Location = new System.Drawing.Point(380, 0);
            this.formPanel.Name = "formPanel";
            this.formPanel.Size = new System.Drawing.Size(540, 580);
            this.formPanel.TabIndex = 1;
            //
            // LoginForm
            //
            this.AcceptButton = this.signInButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = RpvTheme.Cream;
            this.ClientSize = new System.Drawing.Size(920, 580);
            this.Controls.Add(this.formPanel);
            this.Controls.Add(this.brandPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Sign in · RPV Workforce";
            this.brandPanel.ResumeLayout(false);
            this.formPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel brandPanel;
        private RpvLogo brandLogo;
        private System.Windows.Forms.Label brandWordmark;
        private System.Windows.Forms.Label brandHeadline;
        private System.Windows.Forms.Panel taglineRule;
        private System.Windows.Forms.Label taglineLabel;
        private System.Windows.Forms.Panel formPanel;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Label subtitleLabel;
        private RpvField usernameField;
        private RpvField passwordField;
        private System.Windows.Forms.Label errorLabel;
        private RpvButton signInButton;
        private RpvButton forgotButton;
        private System.Windows.Forms.Label demoHintLabel;
    }
}
