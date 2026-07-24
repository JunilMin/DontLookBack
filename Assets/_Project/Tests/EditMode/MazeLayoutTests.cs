using System.Collections.Generic;
using DontLookBack.ObservationMaze;
using NUnit.Framework;

namespace DontLookBack.Tests.EditMode
{
    public sealed class MazeLayoutTests
    {
        [Test]
        public void PlayerStartsAtMazeCenter()
        {
            var maze = new MazeLayout(31, 31, 42);

            Assert.That(maze.Start, Is.EqualTo(new MazeCell(15, 15)));
        }

        [Test]
        public void ExitIsOnOuterBoundaryAndFarFromStart()
        {
            var maze = new MazeLayout(31, 31, 42);

            Assert.That(maze.IsBoundary(maze.Exit), Is.True);
            Assert.That(maze.Solution.Count, Is.GreaterThan(100));
        }

        [Test]
        public void SolutionUsesOnlyOpenPassages()
        {
            var maze = new MazeLayout(31, 31, 42);

            for (var index = 0; index < maze.Solution.Count - 1; index++)
            {
                var current = maze.Solution[index];
                var next = maze.Solution[index + 1];
                var direction = DirectionBetween(current, next);
                Assert.That(maze.CanMove(current, direction), Is.True,
                    $"Expected an open passage between ({current.X},{current.Y}) and ({next.X},{next.Y}).");
            }
        }

        [Test]
        public void EveryCellCanBeReachedFromCenter()
        {
            var maze = new MazeLayout(31, 31, 42);
            var visited = new HashSet<MazeCell> { maze.Start };
            var queue = new Queue<MazeCell>();
            queue.Enqueue(maze.Start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (var direction = 0; direction < 4; direction++)
                {
                    if (!maze.CanMove(current, direction)) continue;
                    var next = maze.Step(current, direction);
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }

            Assert.That(visited.Count, Is.EqualTo(maze.Width * maze.Height));
        }

        private static int DirectionBetween(MazeCell from, MazeCell to)
        {
            if (to.Y > from.Y) return 0;
            if (to.X > from.X) return 1;
            if (to.Y < from.Y) return 2;
            return 3;
        }
    }
}
