using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;
using RPV_Tracker.Domains.TimeTracking.Services;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Pages
{
    /// <summary>
    /// Local, per-machine preferences: theme, screenshot cadence, and which monitor(s) to
    /// capture. Every control here applies immediately and is persisted by <see cref="AppSettings"/>.
    /// </summary>
    internal class SettingsPage : UserControl
    {
        private const int TitleHeight = 32;
        private const int SubtitleHeight = 32;
        private const int SectionGap = 24;
        private const int AppearanceHeight = 150;
        private const int IntervalHeight = 168;
        private const int ScheduleHeight = 150;
        private const int MonitorHeight = 208;

        private readonly TimeTrackingService service;

        private readonly Panel scrollHost;
        private readonly Panel content;
        private readonly Label titleLabel;
        private readonly Label subtitleLabel;

        private readonly CardPanel appearanceCard;
        private readonly Label appearanceCaption;
        private readonly SegmentedControl themeControl;

        private readonly CardPanel intervalCard;
        private readonly Label intervalCaption;
        private readonly SegmentedControl intervalControl;
        private readonly Label intervalNote;

        private readonly CardPanel scheduleCard;
        private readonly Label scheduleCaption;
        private readonly ThemedComboBox scheduleStartPicker;
        private readonly Label scheduleToLabel;
        private readonly ThemedComboBox scheduleEndPicker;

        private readonly CardPanel monitorCard;
        private readonly Label monitorCaption;
        private readonly SegmentedControl monitorModeControl;
        private readonly ThemedComboBox monitorPicker;
        private readonly Label monitorNote;

        public SettingsPage(TimeTrackingService trackingService)
        {
            service = trackingService;
            BackColor = RpvTheme.Cream;
            DoubleBuffered = true;

            scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = RpvTheme.Cream };
            content = new Panel { BackColor = RpvTheme.Cream };

            titleLabel = MakeLabel(RpvTheme.FontH1, RpvTheme.HeadingText, "Settings", TitleHeight);
            subtitleLabel = MakeLabel(RpvTheme.FontBody, RpvTheme.Stone,
                "Personal preferences for this computer — theme, screenshot cadence, work schedule, and capture setup.",
                SubtitleHeight);

            // ---- appearance ----
            appearanceCard = new CardPanel();
            Label appearanceHeader = MakeCardHeader("Appearance");
            appearanceHeader.Dock = DockStyle.Top;
            appearanceCaption = MakeCaption("Switch between a light and dark interface.");
            themeControl = new SegmentedControl { Options = new[] { "Light", "Dark" } };
            themeControl.SetSelectedIndexSilently(RpvTheme.IsDarkMode ? 1 : 0);
            themeControl.SelectedIndexChanged += (s, e) =>
            {
                bool dark = themeControl.SelectedIndex == 1;
                AppSettings.DarkMode = dark;
                RpvTheme.ApplyMode(dark);
            };
            appearanceCard.Controls.Add(appearanceCaption);
            appearanceCard.Controls.Add(themeControl);
            appearanceCard.Controls.Add(appearanceHeader);

            // ---- screenshot interval ----
            intervalCard = new CardPanel();
            Label intervalHeader = MakeCardHeader("Screenshot interval");
            intervalHeader.Dock = DockStyle.Top;
            intervalCaption = MakeCaption("How often RPV captures a screenshot and closes an activity interval.");
            intervalControl = new SegmentedControl
            {
                Options = ScreenshotIntervals.Minutes.Select(m => m + " min").ToArray()
            };
            intervalControl.SetSelectedIndexSilently(IndexOfInterval(AppSettings.ScreenshotIntervalMinutes));
            intervalNote = MakeCaption(string.Empty);
            intervalNote.ForeColor = RpvTheme.Warning;
            intervalControl.SelectedIndexChanged += (s, e) =>
            {
                int minutes = ScreenshotIntervals.Minutes[intervalControl.SelectedIndex];
                AppSettings.ScreenshotIntervalMinutes = minutes;
                service.SetIntervalMinutes(minutes);
                UpdateIntervalNote();
            };
            intervalCard.Controls.Add(intervalCaption);
            intervalCard.Controls.Add(intervalControl);
            intervalCard.Controls.Add(intervalNote);
            intervalCard.Controls.Add(intervalHeader);

            // ---- work schedule ----
            scheduleCard = new CardPanel();
            Label scheduleHeader = MakeCardHeader("Work schedule");
            scheduleHeader.Dock = DockStyle.Top;
            scheduleCaption = MakeCaption("Starting a session outside these hours (or on a weekend) offers to flag it as overtime.");

            scheduleStartPicker = MakeTimeField(AppSettings.WorkScheduleStart);
            scheduleStartPicker.SelectedIndexChanged += (s, e) =>
                AppSettings.WorkScheduleStart = TimeSpan.FromMinutes(((TimeChoice)scheduleStartPicker.SelectedItem).Minutes);

            scheduleToLabel = MakeCaption("to");
            scheduleToLabel.TextAlign = ContentAlignment.MiddleCenter;

            scheduleEndPicker = MakeTimeField(AppSettings.WorkScheduleEnd);
            scheduleEndPicker.SelectedIndexChanged += (s, e) =>
                AppSettings.WorkScheduleEnd = TimeSpan.FromMinutes(((TimeChoice)scheduleEndPicker.SelectedItem).Minutes);

            scheduleCard.Controls.Add(scheduleCaption);
            scheduleCard.Controls.Add(scheduleStartPicker);
            scheduleCard.Controls.Add(scheduleToLabel);
            scheduleCard.Controls.Add(scheduleEndPicker);
            scheduleCard.Controls.Add(scheduleHeader);

            // ---- monitor capture ----
            monitorCard = new CardPanel();
            Label monitorHeader = MakeCardHeader("Screenshot capture");
            monitorHeader.Dock = DockStyle.Top;
            monitorCaption = MakeCaption("Capture every connected monitor, or just one.");
            monitorModeControl = new SegmentedControl { Options = new[] { "All monitors", "Single monitor" } };
            monitorModeControl.SetSelectedIndexSilently(AppSettings.CaptureAllMonitors ? 0 : 1);

            monitorPicker = new ThemedComboBox();
            PopulateMonitorPicker();
            monitorPicker.Enabled = !AppSettings.CaptureAllMonitors;
            monitorPicker.SelectedIndexChanged += (s, e) =>
            {
                var choice = monitorPicker.SelectedItem as ScreenChoice;
                if (choice != null)
                {
                    AppSettings.SelectedMonitorId = choice.DeviceName;
                }
            };

            monitorNote = MakeCaption(string.Empty);

            monitorModeControl.SelectedIndexChanged += (s, e) =>
            {
                bool allMonitors = monitorModeControl.SelectedIndex == 0;
                AppSettings.CaptureAllMonitors = allMonitors;
                monitorPicker.Enabled = !allMonitors;
                UpdateMonitorNote();
            };

            monitorCard.Controls.Add(monitorCaption);
            monitorCard.Controls.Add(monitorModeControl);
            monitorCard.Controls.Add(monitorPicker);
            monitorCard.Controls.Add(monitorNote);
            monitorCard.Controls.Add(monitorHeader);

            content.Controls.Add(titleLabel);
            content.Controls.Add(subtitleLabel);
            content.Controls.Add(appearanceCard);
            content.Controls.Add(intervalCard);
            content.Controls.Add(scheduleCard);
            content.Controls.Add(monitorCard);

            scrollHost.Controls.Add(content);
            Controls.Add(scrollHost);

            scrollHost.Resize += (s, e) => LayoutContent();

            UpdateIntervalNote();
            UpdateMonitorNote();
            LayoutContent();
        }

        private void UpdateIntervalNote()
        {
            intervalNote.Text = service.IsTracking
                ? "You're tracking right now — this takes effect on your next session."
                : "Applies the next time you start tracking.";
        }

        private void UpdateMonitorNote()
        {
            monitorNote.Text = monitorModeControl.SelectedIndex == 0
                ? "Every connected monitor is included in each screenshot."
                : "Only the selected monitor below is captured.";
        }

        private void PopulateMonitorPicker()
        {
            monitorPicker.Items.Clear();

            Screen[] screens = Screen.AllScreens;
            int selectIndex = 0;
            for (int i = 0; i < screens.Length; i++)
            {
                Screen screen = screens[i];
                string name = "Monitor " + (i + 1) + " — " + screen.Bounds.Width + "×" + screen.Bounds.Height
                    + (screen.Primary ? " (Primary)" : string.Empty);
                monitorPicker.Items.Add(new ScreenChoice { DeviceName = screen.DeviceName, DisplayName = name });

                if (screen.DeviceName == AppSettings.SelectedMonitorId)
                {
                    selectIndex = i;
                }
            }

            if (monitorPicker.Items.Count > 0)
            {
                monitorPicker.SelectedIndex = selectIndex;
            }
        }

        private static int IndexOfInterval(int minutes)
        {
            int[] options = ScreenshotIntervals.Minutes;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == minutes)
                {
                    return i;
                }
            }
            return 0;
        }

        // --------------------------------------------------------------- layout

        private void LayoutContent()
        {
            int available = scrollHost.ClientSize.Width;
            if (available <= 0)
            {
                return;
            }

            int margin = available < 720 ? RpvTheme.Space4 : RpvTheme.Space6;
            int width = Math.Min(RpvTheme.ContentMaxWidth, available - (margin * 2));
            if (width < 320)
            {
                width = 320;
            }

            content.Left = Math.Max(margin, (available - width) / 2);
            content.Top = RpvTheme.Space5;
            content.Width = width;

            titleLabel.SetBounds(0, 0, width, TitleHeight);
            subtitleLabel.SetBounds(0, TitleHeight + 2, width, SubtitleHeight);

            int y = TitleHeight + 2 + SubtitleHeight + RpvTheme.Space5;
            appearanceCard.SetBounds(0, y, width, AppearanceHeight);
            LayoutAppearanceCard(width);

            y += AppearanceHeight + SectionGap;
            intervalCard.SetBounds(0, y, width, IntervalHeight);
            LayoutIntervalCard(width);

            y += IntervalHeight + SectionGap;
            scheduleCard.SetBounds(0, y, width, ScheduleHeight);
            LayoutScheduleCard(width);

            y += ScheduleHeight + SectionGap;
            monitorCard.SetBounds(0, y, width, MonitorHeight);
            LayoutMonitorCard(width);

            y += MonitorHeight + RpvTheme.Space5;
            content.Height = y;

            scrollHost.AutoScrollMinSize = new Size(0, content.Height);
        }

        // Every card header is Dock=Top inside CardPanel's Padding.Top (20px) + its own
        // 34px height, so it occupies relative Y 20–54. Everything below it in the card
        // must start at or after 54 — anything higher visually collides with the header.
        private const int HeaderBottom = 54;

        private void LayoutAppearanceCard(int width)
        {
            int pad = RpvTheme.Space5;
            appearanceCaption.SetBounds(pad, HeaderBottom + 4, width - (pad * 2), 20);
            themeControl.SetBounds(pad, HeaderBottom + 32, 220, 40);
        }

        private void LayoutIntervalCard(int width)
        {
            int pad = RpvTheme.Space5;
            intervalCaption.SetBounds(pad, HeaderBottom + 4, width - (pad * 2), 20);
            intervalControl.SetBounds(pad, HeaderBottom + 32, Math.Min(420, width - (pad * 2)), 40);
            intervalNote.SetBounds(pad, HeaderBottom + 76, width - (pad * 2), 18);
        }

        private void LayoutScheduleCard(int width)
        {
            int pad = RpvTheme.Space5;
            scheduleCaption.SetBounds(pad, HeaderBottom + 4, width - (pad * 2), 32);
            scheduleStartPicker.SetBounds(pad, HeaderBottom + 40, 130, 26);
            scheduleToLabel.SetBounds(pad + 134, HeaderBottom + 40, 30, 26);
            scheduleEndPicker.SetBounds(pad + 168, HeaderBottom + 40, 130, 26);
        }

        private void LayoutMonitorCard(int width)
        {
            int pad = RpvTheme.Space5;
            monitorCaption.SetBounds(pad, HeaderBottom + 4, width - (pad * 2), 20);
            monitorModeControl.SetBounds(pad, HeaderBottom + 32, 260, 40);
            monitorPicker.SetBounds(pad, HeaderBottom + 80, Math.Min(360, width - (pad * 2)), 28);
            monitorNote.SetBounds(pad, HeaderBottom + 116, width - (pad * 2), 18);
        }

        // --------------------------------------------------------------- helpers

        private static Label MakeLabel(Font font, Color color, string text, int height)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.Cream,
                Font = font,
                ForeColor = color,
                Text = text,
                Height = height,
                UseMnemonic = false,
                TextAlign = ContentAlignment.TopLeft
            };
        }

        private static Label MakeCaption(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Charcoal,
                Text = text,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        /// <summary>A themed dropdown of times of day in 30-minute steps.</summary>
        private static ThemedComboBox MakeTimeField(TimeSpan value)
        {
            var combo = new ThemedComboBox();
            for (int minutes = 0; minutes < 24 * 60; minutes += 30)
            {
                combo.Items.Add(new TimeChoice(minutes));
            }

            int rounded = RoundToHalfHour(value);
            foreach (TimeChoice choice in combo.Items)
            {
                if (choice.Minutes == rounded)
                {
                    combo.SelectedItem = choice;
                    break;
                }
            }

            return combo;
        }

        private static int RoundToHalfHour(TimeSpan value)
        {
            const int day = 24 * 60;
            int minutes = (int)Math.Round(value.TotalMinutes / 30.0) * 30;
            return ((minutes % day) + day) % day;
        }

        private static Label MakeCardHeader(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontH3,
                ForeColor = RpvTheme.HeadingText,
                Height = 34,
                Text = text,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private class ScreenChoice
        {
            public string DeviceName;
            public string DisplayName;

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private class TimeChoice
        {
            public readonly int Minutes;

            public TimeChoice(int minutes)
            {
                Minutes = minutes;
            }

            public override string ToString()
            {
                return DateTime.Today.AddMinutes(Minutes).ToString("h:mm tt");
            }
        }
    }
}
