using System;

namespace UDK.WinUI.Grid {
    public readonly record struct GridArea(int Column, int Row, int Width, int Height) {
        public int Right => this.Column + this.Width;
        public int Bottom => this.Row + this.Height;
        public bool IsEmpty => this.Width <= 0 || this.Height <= 0;
        public bool Contains(GridAddress address) => address.Column >= this.Column && address.Column < this.Right
            && address.Row >= this.Row && address.Row < this.Bottom;

        public GridAddress Closest(GridAddress address) => new(
            Math.Clamp(address.Column, this.Column, this.Right - 1),
            Math.Clamp(address.Row, this.Row, this.Bottom - 1)
        );

        public GridArea Intersect(GridArea other) {
            int left = Math.Max(this.Column, other.Column);
            int top = Math.Max(this.Row, other.Row);
            return new(left, top, Math.Min(this.Right, other.Right) - left, Math.Min(this.Bottom, other.Bottom) - top);
        }
    }
}