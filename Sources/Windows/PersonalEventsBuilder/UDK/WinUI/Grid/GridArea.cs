using System;

namespace UDK.WinUI.Grid {
    public readonly struct GridArea : System.IEquatable<GridArea> {
        public int Column { get; }
        public int Row { get; }
        public int Width { get; }
        public int Height { get; }

        public GridArea(int column, int row, int width, int height) {
            this.Column = column;
            this.Row = row;
            this.Width = width;
            this.Height = height;
        }

        public bool Equals(GridArea other) => this.Column == other.Column && this.Row == other.Row && this.Width == other.Width && this.Height == other.Height;
        public override bool Equals(object? obj) => obj is GridArea other && this.Equals(other);
        public override int GetHashCode() => System.HashCode.Combine(this.Column, this.Row, this.Width, this.Height);

        public static bool operator ==(GridArea left, GridArea right) => left.Equals(right);
        public static bool operator !=(GridArea left, GridArea right) => !left.Equals(right);

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