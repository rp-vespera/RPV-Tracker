using System;
using System.Windows.Forms;
using RPV_Tracker.Domains.Auth.Models;
using RPV_Tracker.Domains.Auth.Services;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Forms
{
    /// <summary>
    /// Sign-in screen. On success the result is pushed into <see cref="AppSession"/> and
    /// the form closes with <see cref="DialogResult.OK"/>.
    /// </summary>
    public partial class LoginForm : Form
    {
        private bool signingIn;

        public LoginForm()
        {
            InitializeComponent();

            if (RpvConfig.DemoMode)
            {
                demoHintLabel.Text = "Demo mode — sign in with demo / demo";
                demoHintLabel.Visible = true;
            }

            usernameField.ValueChanged += ClearError;
            passwordField.ValueChanged += ClearError;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            usernameField.Focus();
        }

        private void ClearError(object sender, EventArgs e)
        {
            if (!errorLabel.Visible)
            {
                return;
            }

            errorLabel.Visible = false;
            usernameField.HasError = false;
            passwordField.HasError = false;
        }

        private async void signInButton_Click(object sender, EventArgs e)
        {
            await SignInAsync();
        }

        private void forgotButton_Click(object sender, EventArgs e)
        {
            // Password resets run through the web client's OTP flow; there is no desktop
            // equivalent yet, so point people at the channel that can actually help.
            MessageBox.Show(this,
                "Password resets are handled by your HR administrator, or through the RPV web portal.",
                "Reset your password",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async System.Threading.Tasks.Task SignInAsync()
        {
            if (signingIn)
            {
                return;
            }

            SetBusy(true);

            try
            {
                LoginResult result = await AuthService.LoginAsync(usernameField.Value, passwordField.Value);
                AppSession.Start(result);
                DialogResult = DialogResult.OK;
            }
            catch (ApiException ex)
            {
                ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                ShowError("Something went wrong while signing in: " + ex.Message);
            }
            finally
            {
                // The form is already closing on success, so only restore state if it isn't.
                if (DialogResult != DialogResult.OK)
                {
                    SetBusy(false);
                }
            }
        }

        private void SetBusy(bool busy)
        {
            signingIn = busy;
            signInButton.Enabled = !busy;
            signInButton.Text = busy ? "Signing in…" : "Sign in";
            usernameField.Enabled = !busy;
            passwordField.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void ShowError(string message)
        {
            errorLabel.Text = message;
            errorLabel.Visible = true;
            usernameField.HasError = true;
            passwordField.HasError = true;
            passwordField.Focus();
        }
    }
}
