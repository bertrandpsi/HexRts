using System;

namespace HexRts.Logic.HexRts
{
    interface IPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    class Position : IPosition
    {
        public int X { get; set; }
        public int Y { get; set; }

        public Position()
        {
        }

        public Position(IPosition pos)
        {
            X = pos.X;
            Y = pos.Y;
        }

        public Position Add(int x,int y)
        {
            return new Position { X = X + x, Y = Y + y };
        }

        public static Position CellToScreen(IPosition source)
        {
            return new Position { X = source.X * 130 + (source.Y % 2) * 65 + 65, Y = source.Y * 90 + 45 };
        }

        public static Position ScreenToCell(IPosition source)
        {
            var y = source.Y / 90;
            return new Position { X = (source.X - (y % 2) * 65) / 130, Y = y };
        }

        public static double Distance(IPosition posA, IPosition posB)
        {
            var a = posA.X - posB.X;
            var b = posA.Y - posB.Y;
            return Math.Sqrt(a * a + b * b);
        }

        public static bool Same(IPosition posA, IPosition posB) => (posA.X == posB.X && posA.Y == posB.Y);
    }
}