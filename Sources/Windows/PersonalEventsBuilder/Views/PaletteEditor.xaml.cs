using System;
using System.Globalization;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

using Windows.UI;

namespace PersonalEventsBuilder.Views {
    public sealed partial class PaletteEditor : UserControl {
        public event EventHandler? CloseRequested;
        private const string DefaultPalette = "232323,333333,373737,D9B287,37,8,20";
        private const string SettingsKey = "WorkspacePalette";
        private readonly Color[] colors = new Color[4];
        private bool updating;

        public PaletteEditor() {
            this.InitializeComponent();
            this.Role.SelectedIndex = 0;
            if (!this.LoadPalette(Helpers.UserSettings.Read(SettingsKey, DefaultPalette))) {
                this.LoadPalette(DefaultPalette);
            }
        }

        private bool LoadPalette(string value) {
            var parts = value.Split(',');
            if (parts.Length != 7) {
                return false;
            }
            for (int i = 0; i < this.colors.Length; i++) {
                if (parts[i].Length != 6 || !uint.TryParse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb)) {
                    return false;
                }
                this.colors[i] = Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            }
            if (!int.TryParse(parts[4], out int warmth) || warmth is < 0 or > 100 ||
                !int.TryParse(parts[5], out int difference) || difference is < 0 or > 24 ||
                !int.TryParse(parts[6], out int lines) || lines is < 0 or > 55) {
                return false;
            }
            this.updating = true;
            this.Warmth.Value = warmth;
            this.Difference.Value = difference;
            this.Lines.Value = lines;
            this.Picker.Color = this.colors[this.Role.SelectedIndex];
            this.updating = false;
            this.Apply();
            return true;
        }

        internal void FocusCloseButton() => this.CloseButton.Focus(FocusState.Keyboard);

        internal void Save() {
            string[] hex = Array.ConvertAll(this.colors, color => $"{color.R:X2}{color.G:X2}{color.B:X2}");
            Helpers.UserSettings.Write(SettingsKey, $"{string.Join(",", hex)},{(int)this.Warmth.Value},{(int)this.Difference.Value},{(int)this.Lines.Value}");
        }

        private void OnRoleChanged(object sender, SelectionChangedEventArgs args) {
            if (this.Role.SelectedIndex >= 0) {
                this.updating = true;
                this.Picker.Color = this.colors[this.Role.SelectedIndex];
                this.updating = false;
            }
        }

        private void OnColorChanged(ColorPicker sender, ColorChangedEventArgs args) {
            if (this.updating || this.Role.SelectedIndex < 0) {
                return;
            }
            this.colors[this.Role.SelectedIndex] = args.NewColor;
            this.Apply();
        }

        private void OnToneChanged(object sender, RangeBaseValueChangedEventArgs args) {
            if (!this.updating && this.Lines != null && this.Role.SelectedIndex >= 0) {
                this.Apply();
            }
        }

        private void Apply() {
            void Set(string key, Color color) => ((SolidColorBrush)Application.Current.Resources[key]).Color = color;
            Color Tone(int role, double delta) {
                byte Channel(byte channel, double weight) =>
                    (byte)Math.Clamp(Math.Floor(channel + delta + weight * this.Warmth.Value + 0.5), 0, 255);
                var color = this.colors[role];
                return Color.FromArgb(255, Channel(color.R, 0.13), Channel(color.G, 0.045), Channel(color.B, -0.055));
            }
            Set("WorkspaceBackgroundBrush", Tone(0, 0));
            Set("PanelBrush", Tone(0, 5));
            Set("WorkspaceDeepBrush", Tone(0, -8));
            Set("GridLineBrush", Tone(0, 5 + this.Lines.Value));
            Set("ZoneSplitterBrush", Tone(0, 11));
            Set("ZoneSplitterHoverBrush", Tone(0, 34));
            Set("PrimaryFieldBrush", Tone(1, 0));
            Set("LowerFieldBrush", Tone(1, -this.Difference.Value));
            Set("SideFieldBrush", Tone(1, this.Difference.Value));
            Set("CommandBrush", Tone(2, 0));
            Set("CommandHoverBrush", Tone(2, 10));
            Set("WorkspaceAccentBrush", this.colors[3]);
        }

        private void OnReset(object sender, RoutedEventArgs args) => this.LoadPalette(DefaultPalette);

        private void OnClose(object sender, RoutedEventArgs args) => this.CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}