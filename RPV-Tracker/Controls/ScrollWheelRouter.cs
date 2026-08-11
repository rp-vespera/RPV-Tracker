using System;
using System.Drawing;
using System.Windows.Forms;

namespace RPV_Tracker.Controls
{
    /// <summary>
    /// Makes the mouse wheel scroll whatever AutoScroll panel is under the cursor — not just
    /// whichever control happens to hold keyboard focus, which is where Windows actually
    /// delivers WM_MOUSEWHEEL by default. A page built almost entirely out of non-focusable
    /// Labels and CardPanels means the "focused" control is essentially never the one under
    /// the mouse, so without this the wheel does nothing over most of the page.
    /// </summary>
    /// <remarks>
    /// Installed once, application-wide, as an <see cref="IMessageFilter"/> — every page
    /// gets working wheel-scroll for free, including ones added later, with no per-page
    /// wiring. On each wheel message it hit-tests the actual cursor position down to the
    /// deepest control there, then walks back up looking for the nearest AutoScroll
    /// ancestor (so scrolling over a nested list, like the task list inside a card, scrolls
    /// that list rather than the whole page) and moves it directly.
    /// </remarks>
    internal sealed class ScrollWheelRouter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        private static bool installed;

        public static void Install()
        {
            if (installed)
            {
                return;
            }
            installed = true;
            Application.AddMessageFilter(new ScrollWheelRouter());
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL)
            {
                return false;
            }

            Control target = Control.FromHandle(m.HWnd);
            Form host = target == null ? null : target.FindForm();
            if (host == null)
            {
                return false;
            }

            Point screenPoint = Cursor.Position;
            Control hit = DeepestControlAt(host, screenPoint);
            int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);

            for (Control candidate = hit; candidate != null; candidate = candidate.Parent)
            {
                var scrollable = candidate as ScrollableControl;
                if (scrollable == null || !scrollable.AutoScroll)
                {
                    continue;
                }

                int maxScroll = Math.Max(0, scrollable.DisplayRectangle.Height - scrollable.ClientSize.Height);
                if (maxScroll == 0)
                {
                    continue;
                }

                int current = -scrollable.AutoScrollPosition.Y;
                int next = Math.Max(0, Math.Min(maxScroll, current - delta));
                scrollable.AutoScrollPosition = new Point(0, next);
                return true;
            }

            return false;
        }

        private static Control DeepestControlAt(Control root, Point screenPoint)
        {
            Control current = root;
            while (true)
            {
                Point clientPoint = current.PointToClient(screenPoint);
                Control next = null;

                foreach (Control child in current.Controls)
                {
                    if (child.Visible && child.Bounds.Contains(clientPoint))
                    {
                        next = child;
                        break;
                    }
                }

                if (next == null)
                {
                    return current;
                }
                current = next;
            }
        }
    }
}
