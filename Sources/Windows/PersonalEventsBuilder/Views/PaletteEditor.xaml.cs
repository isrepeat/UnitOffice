using System;
using System.Linq;
using System.Collections.Generic;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace PersonalEventsBuilder.Views {
    public sealed partial class PaletteEditor : UserControl {
        public event EventHandler? CloseRequested;
        private const string DefaultPalette = "v2,warm,0,17,18";
        private const string SettingsKey = "WorkspacePalette";
        private const string ProfileFolder = "PaletteProfiles";
        private const string ActiveKey = "ActivePaletteProfile";
        private bool updating = true;

        public PaletteEditor() {
            this.InitializeComponent();
            var names = Helpers.UserSettings.Names(ProfileFolder);
            if (names.Length == 0) {
                string initial = Localization.UiStrings.Get("Tr_PaletteDefaultName");
                if (!this.LoadPalette(Helpers.UserSettings.Read(SettingsKey, DefaultPalette))) {
                    this.LoadPalette(DefaultPalette);
                }
                Helpers.UserSettings.Write(ProfileFolder, initial, this.SerializePalette());
                names = [initial];
            }
            foreach (var name in names.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)) {
                this.Profiles.Items.Add(name);
            }
            var selected = Helpers.UserSettings.Read(ActiveKey, names[0]);
            this.Profiles.SelectedItem = this.Profiles.Items.Contains(selected) ? selected : names[0];
        }

        internal void FocusCloseButton() => this.CloseButton.Focus(FocusState.Keyboard);

        private bool LoadPalette(string value) {
            var parts = value.Split(',');
            bool legacy = parts.Length is 7 or 16;
            if (!legacy && (parts.Length != 5 || parts[0] != "v2")) {
                return false;
            }
            string scheme = legacy ? "warm" : parts[1];
            var item = this.Scheme.Items.Cast<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, scheme));
            string brightnessValue = legacy ? (parts.Length == 16 ? parts[7] : "0") : parts[2];
            if (item == null || !int.TryParse(brightnessValue, out int brightness) || brightness is < -20 or > 35 ||
                !int.TryParse(parts[legacy ? 5 : 3], out int difference) || difference is < 0 or > 24 ||
                !int.TryParse(parts[legacy ? 6 : 4], out int lines) || lines is < 0 or > 55) {
                return false;
            }
            this.updating = true;
            this.Scheme.SelectedItem = item;
            this.Brightness.Value = brightness;
            this.Difference.Value = difference;
            this.Lines.Value = lines;
            this.updating = false;
            this.Apply();
            return true;
        }

        private string SerializePalette() {
            return $"v2,{((ComboBoxItem)this.Scheme.SelectedItem).Tag},{(int)this.Brightness.Value},{(int)this.Difference.Value},{(int)this.Lines.Value}";
        }

        private void OnSchemeChanged(object sender, SelectionChangedEventArgs args) {
            if (!this.updating && this.Scheme.SelectedItem != null) {
                this.Apply();
                this.SaveCurrent();
            }
        }

        private void SaveCurrent() {
            if (this.Profiles.SelectedItem is string name) {
                Helpers.UserSettings.Write(ProfileFolder, name, this.SerializePalette());
            }
        }

        private void OnProfileChanged(object sender, SelectionChangedEventArgs args) {
            if (this.Profiles.SelectedItem is string name) {
                if (!this.LoadPalette(Helpers.UserSettings.Read(ProfileFolder, name, DefaultPalette))) {
                    this.LoadPalette(DefaultPalette);
                }
                Helpers.UserSettings.Write(ActiveKey, name);
            }
        }

        private void OnNameChanged(object sender, TextChangedEventArgs args) {
            string name = this.ProfileName.Text.Trim();
            this.CreateButton.IsEnabled = name.Length > 0 && !this.Profiles.Items.Cast<string>().Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        private void OnCreate(object sender, RoutedEventArgs args) {
            string name = this.ProfileName.Text.Trim();
            Helpers.UserSettings.Write(ProfileFolder, name, this.SerializePalette());
            this.Profiles.Items.Add(name);
            this.Profiles.SelectedItem = name;
            this.ProfileName.Text = "";
        }

        private void OnToneChanged(object sender, RangeBaseValueChangedEventArgs args) {
            if (!this.updating) {
                this.Apply((Slider)sender);
                this.SaveCurrent();
            }
        }

        private void Apply(Slider? changed = null) {
            var names = new List<string>();
            var values = new List<string>();
            void Set(string key, Color color, params Slider[] parameters) {
                var brush = (SolidColorBrush)Application.Current.Resources[key];
                if (changed == null || parameters.Contains(changed)) {
                    names.Add(key);
                    values.Add(color.A == 255 ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
                }
                brush.Color = color;
            }
            var (surface, field, command, accent, foreground) = ((ComboBoxItem)this.Scheme.SelectedItem).Tag switch {
                "graphite" => (0x222222, 0x323232, 0x373737, 0xC4C4C4, 0xDADADA),
                "slate" => (0x202429, 0x303439, 0x35393E, 0xA8C3D7, 0xD7DFE5),
                _ => (0x28241F, 0x38332E, 0x3E3934, 0xD9B287, 0xD7D5D2)
            };
            Color Shade(int rgb, int offset = 0) {
                byte Channel(int shift) => (byte)Math.Clamp(((rgb >> shift) & 255) + offset, 0, 255);
                return Color.FromArgb(255, Channel(16), Channel(8), Channel(0));
            }
            Color Tone(int offset) => Shade(surface, (int)this.Brightness.Value + offset);
            Set("WorkspaceBackgroundBrush", Tone(0), this.Brightness);
            Set("PanelBrush", Tone(5), this.Brightness);
            Set("WorkspaceDeepBrush", Tone(-8), this.Brightness);
            Set("GridLineBrush", Tone(5 + (int)this.Lines.Value), this.Brightness, this.Lines);
            Set("ZoneSplitterBrush", Tone(11), this.Brightness);
            Set("ZoneSplitterHoverBrush", Tone(34), this.Brightness);
            Set("PrimaryFieldBrush", Shade(field, (int)this.Brightness.Value), this.Brightness);
            Set("LowerFieldBrush", Shade(field, (int)this.Brightness.Value - (int)this.Difference.Value), this.Brightness, this.Difference);
            Set("SideFieldBrush", Shade(field, (int)this.Brightness.Value + (int)this.Difference.Value), this.Brightness, this.Difference);
            Set("CommandBrush", Shade(command, (int)this.Brightness.Value), this.Brightness);
            Set("WorkspaceAccentBrush", Shade(accent));
            Set("WorkspaceTextBrush", Shade(foreground));
            Set("CommandTextBrush", Shade(accent));
            if (names.Count > 0) {
                this.BrushNames.Text = string.Join(Environment.NewLine, names);
                this.BrushValues.Text = string.Join(Environment.NewLine, values);
            }
        }

        private void OnReset(object sender, RoutedEventArgs args) {
            this.LoadPalette(DefaultPalette);
            this.SaveCurrent();
        }

        private void OnClose(object sender, RoutedEventArgs args) => this.CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}