using System;
using System.Collections.Generic;

namespace DontLookBack.ObservationMaze
{
    public readonly struct MazeCell : IEquatable<MazeCell>
    {
        public MazeCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(MazeCell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is MazeCell other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(MazeCell left, MazeCell right) => left.Equals(right);
        public static bool operator !=(MazeCell left, MazeCell right) => !left.Equals(right);
    }

    public sealed class MazeLayout
    {
        private static readonly int[] DeltaX = { 0, 1, 0, -1 };
        private static readonly int[] DeltaY = { 1, 0, -1, 0 };
        private readonly bool[,,] passages;

        public MazeLayout(int width, int height, int seed)
        {
            if (width < 5 || height < 5)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "A playable maze must be at least 5 by 5 cells.");
            }

            Width = width;
            Height = height;
            Start = new MazeCell(width / 2, height / 2);
            passages = new bool[width, height, 4];
            Generate(seed);
            Exit = FindFarthestBoundaryCell();
            Solution = BuildPath(Start, Exit);
        }

        public int Width { get; }
        public int Height { get; }
        public MazeCell Start { get; }
        public MazeCell Exit { get; }
        public IReadOnlyList<MazeCell> Solution { get; }

        public bool IsInside(MazeCell cell) =>
            cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

        public bool IsBoundary(MazeCell cell) =>
            cell.X == 0 || cell.Y == 0 || cell.X == Width - 1 || cell.Y == Height - 1;

        public bool CanMove(MazeCell from, int direction) =>
            IsInside(from) && direction >= 0 && direction < 4 && passages[from.X, from.Y, direction];

        public MazeCell Step(MazeCell from, int direction) =>
            new(from.X + DeltaX[direction], from.Y + DeltaY[direction]);

        private void Generate(int seed)
        {
            var random = new Random(seed);
            var visited = new bool[Width, Height];
            var stack = new Stack<MazeCell>();
            stack.Push(Start);
            visited[Start.X, Start.Y] = true;

            while (stack.Count > 0)
            {
                var current = stack.Peek();
                var candidates = new List<int>(4);
                for (var direction = 0; direction < 4; direction++)
                {
                    var next = Step(current, direction);
                    if (IsInside(next) && !visited[next.X, next.Y])
                    {
                        candidates.Add(direction);
                    }
                }

                if (candidates.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                var chosen = candidates[random.Next(candidates.Count)];
                var destination = Step(current, chosen);
                passages[current.X, current.Y, chosen] = true;
                passages[destination.X, destination.Y, (chosen + 2) % 4] = true;
                visited[destination.X, destination.Y] = true;
                stack.Push(destination);
            }
        }

        private MazeCell FindFarthestBoundaryCell()
        {
            var distances = DistancesFrom(Start, out _);
            var best = Start;
            var bestDistance = -1;

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    var cell = new MazeCell(x, y);
                    if (IsBoundary(cell) && distances[x, y] > bestDistance)
                    {
                        best = cell;
                        bestDistance = distances[x, y];
                    }
                }
            }

            return best;
        }

        private IReadOnlyList<MazeCell> BuildPath(MazeCell from, MazeCell to)
        {
            DistancesFrom(from, out var parents);
            var path = new List<MazeCell> { to };
            var current = to;
            while (current != from)
            {
                current = parents[current.X, current.Y];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        private int[,] DistancesFrom(MazeCell origin, out MazeCell[,] parents)
        {
            var distances = new int[Width, Height];
            parents = new MazeCell[Width, Height];
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    distances[x, y] = -1;
                }
            }

            var queue = new Queue<MazeCell>();
            queue.Enqueue(origin);
            distances[origin.X, origin.Y] = 0;
            parents[origin.X, origin.Y] = origin;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (var direction = 0; direction < 4; direction++)
                {
                    if (!CanMove(current, direction)) continue;
                    var next = Step(current, direction);
                    if (distances[next.X, next.Y] >= 0) continue;
                    distances[next.X, next.Y] = distances[current.X, current.Y] + 1;
                    parents[next.X, next.Y] = current;
                    queue.Enqueue(next);
                }
            }

            return distances;
        }
    }
}
