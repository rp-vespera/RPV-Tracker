using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;

namespace RPV_Tracker.Controls
{
    /// <summary>
    /// A dropdown-list ComboBox that paints its closed box and its open list in the app's
    /// own palette. The stock control's BackColor/ForeColor only reach the closed box —
    /// the open dropdown list ignores them and always renders OS white-on-black, which
    /// looks broken dropped onto a dark card. Owner-drawing both fixes that.
    /// </summary>
    internal class ThemedComboBox : ComboBox
    {
        public ThemedComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 24;
            Font = RpvTheme.FontBody;
            BackColor = RpvTheme.CardSurface;
            ForeColor = RpvTheme.Charcoal;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = Enabled ? (selected ? RpvTheme.Terracotta : RpvTheme.CardSurface) : RpvTheme.Mist;
            Color fore = Enabled ? (selected ? RpvTheme.OnAccent : RpvTheme.Charcoal) : RpvTheme.Stone;

            using (var brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            if (e.Index >= 0 && e.Index < Items.Count)
            {
                var textBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, textBounds, fore,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding
                    | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
