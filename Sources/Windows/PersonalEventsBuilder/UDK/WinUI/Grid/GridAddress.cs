namespace UDK.WinUI.Grid {
    public readonly struct GridAddress : System.IEquatable<GridAddress> {
        public int Column { get; }
        public int Row { get; }

        public GridAddress(int column, int row) {
            this.Column = column;
            this.Row = row;
        }

        public bool Equals(GridAddress other) => this.Column == other.Column && this.Row == other.Row;
        public override bool Equals(object? obj) => obj is GridAddress other && this.Equals(other);
        public override int GetHashCode() => System.HashCode.Combine(this.Column, this.Row);

        public static bool operator ==(GridAddress left, GridAddress right) => left.Equals(right);
        public static bool operator !=(GridAddress left, GridAddress right) => !left.Equals(right);
    }
}