using System;
using AstroFlowJam.Core;
using NUnit.Framework;
using UnityEngine;

namespace AstroFlowJam.Tests
{
    public class BoardModelTests
    {
        static RocketSpec R(string id, GameColor c, int x, int y, int w = 1, int h = 1) =>
            new(id, c, new Vector2Int(x, y), new Vector2Int(w, h), w * h > 1 ? 40 : 20);

        static BoardModel Board(RocketSpec[] rockets, params ExitSpec[] exits) => new(6, 6, rockets, exits);

        [Test]
        public void FreeDistance_StopsAtEdgeAndOtherRockets()
        {
            var board = Board(new[] { R("A", GameColor.Red, 0, 0), R("B", GameColor.Blue, 0, 3) });
            Assert.AreEqual(2, board.FreeDistance(0, Dir.Up));
            Assert.AreEqual(5, board.FreeDistance(0, Dir.Right));
            Assert.AreEqual(0, board.FreeDistance(0, Dir.Down));
            Assert.AreEqual(2, board.FreeDistance(1, Dir.Up));
        }

        [Test]
        public void FreeDistance_UsesWholeShapeForBigRockets()
        {
            // 2×1 roketin sağ hücresinin üstü dolu: yukarı hiç gidemez.
            var board = Board(new[] { R("Big", GameColor.Red, 0, 0, 2, 1), R("Block", GameColor.Blue, 1, 1) });
            Assert.AreEqual(0, board.FreeDistance(0, Dir.Up));
        }

        [Test]
        public void Move_CannotJumpOverPieces()
        {
            var board = Board(new[] { R("A", GameColor.Red, 0, 0), R("B", GameColor.Blue, 2, 0) });
            Assert.Throws<InvalidOperationException>(() => board.Move(0, Dir.Right, 3));
            board.Move(0, Dir.Right, 1);
            Assert.AreEqual(new Vector2Int(1, 0), board.GetRocket(0).origin);
            Assert.IsTrue(board.TryGetRocketAt(new Vector2Int(1, 0), out var idx));
            Assert.AreEqual(0, idx);
            Assert.IsFalse(board.TryGetRocketAt(new Vector2Int(0, 0), out _));
        }

        [Test]
        public void Relocate_MovesToFreeCells_AndRejectsOccupied()
        {
            var board = Board(new[] { R("A", GameColor.Red, 0, 0, 2, 1), R("B", GameColor.Blue, 3, 3) });
            board.Relocate(0, new Vector2Int(2, 2));
            Assert.IsTrue(board.TryGetRocketAt(new Vector2Int(3, 2), out var idx));
            Assert.AreEqual(0, idx);
            Assert.IsFalse(board.TryGetRocketAt(new Vector2Int(0, 0), out _));
            Assert.Throws<InvalidOperationException>(() => board.Relocate(0, new Vector2Int(2, 3)));
            Assert.Throws<InvalidOperationException>(() => board.Relocate(0, new Vector2Int(5, 0)));
        }

        [Test]
        public void Relocate_CanOverlapItsOwnPreviousCells()
        {
            var board = Board(new[] { R("A", GameColor.Red, 0, 0, 2, 1) });
            board.Relocate(0, new Vector2Int(1, 0));
            Assert.IsTrue(board.TryGetRocketAt(new Vector2Int(2, 0), out _));
            Assert.IsFalse(board.TryGetRocketAt(new Vector2Int(0, 0), out _));
        }

        [Test]
        public void Constructor_RejectsOverlapAndOutOfBounds()
        {
            Assert.Throws<ArgumentException>(() => Board(new[] { R("A", GameColor.Red, 0, 0, 2, 1), R("B", GameColor.Blue, 1, 0) }));
            Assert.Throws<ArgumentException>(() => Board(new[] { R("A", GameColor.Red, 5, 5, 1, 2) }));
        }

        [Test]
        public void Exit_MatchingColorGate_IsOk()
        {
            var board = Board(new[] { R("A", GameColor.Red, 0, 2) }, new ExitSpec(GameColor.Red, Edge.Top, 0, 1));
            Assert.AreEqual(ExitCheck.Ok, board.CheckExit(0, Dir.Up));
        }

        [Test]
        public void Exit_WrongColor_IsRejected()
        {
            var board = Board(new[] { R("A", GameColor.Red, 0, 2) }, new ExitSpec(GameColor.Blue, Edge.Top, 0, 1));
            Assert.AreEqual(ExitCheck.WrongColor, board.CheckExit(0, Dir.Up));
        }

        [Test]
        public void Exit_NoGate_IsWall()
        {
            var board = Board(new[] { R("A", GameColor.Red, 0, 2) }, new ExitSpec(GameColor.Red, Edge.Top, 3, 1));
            Assert.AreEqual(ExitCheck.NoGate, board.CheckExit(0, Dir.Up));
        }

        [Test]
        public void Exit_BlockedPath_IsRejected()
        {
            var board = Board(new[] { R("A", GameColor.Red, 0, 0), R("B", GameColor.Blue, 0, 4) },
                new ExitSpec(GameColor.Red, Edge.Top, 0, 1));
            Assert.AreEqual(ExitCheck.Blocked, board.CheckExit(0, Dir.Up));
        }

        [Test]
        public void Exit_WideRocket_NeedsWideGateOnTop_ButNarrowGateOnSide()
        {
            // GDD: 2×1 roket üst kapıdan çıkarken iki hücre açıklık ister; sağ kapıdan çıkarken bir hücre yeterlidir.
            var narrowTop = Board(new[] { R("A", GameColor.Red, 0, 0, 2, 1) }, new ExitSpec(GameColor.Red, Edge.Top, 0, 1));
            Assert.AreEqual(ExitCheck.TooNarrow, narrowTop.CheckExit(0, Dir.Up));

            var wideTop = Board(new[] { R("A", GameColor.Red, 0, 0, 2, 1) }, new ExitSpec(GameColor.Red, Edge.Top, 0, 2));
            Assert.AreEqual(ExitCheck.Ok, wideTop.CheckExit(0, Dir.Up));

            var side = Board(new[] { R("A", GameColor.Red, 0, 0, 2, 1) }, new ExitSpec(GameColor.Red, Edge.Right, 0, 1));
            Assert.AreEqual(ExitCheck.Ok, side.CheckExit(0, Dir.Right));
        }

        [Test]
        public void RemoveForExit_FreesCells()
        {
            var board = Board(new[] { R("A", GameColor.Red, 0, 0, 1, 2) }, new ExitSpec(GameColor.Red, Edge.Bottom, 0, 1));
            board.RemoveForExit(0, Dir.Down);
            Assert.IsFalse(board.IsOnBoard(0));
            Assert.AreEqual(0, board.RemainingCount);
            Assert.IsFalse(board.TryGetRocketAt(new Vector2Int(0, 1), out _));
        }

        [Test]
        public void OrbitUnits_SmallIsOne_BigIsTwo()
        {
            Assert.AreEqual(1, R("S", GameColor.Red, 0, 0).OrbitUnits);
            Assert.AreEqual(2, R("V", GameColor.Red, 0, 0, 1, 2).OrbitUnits);
            Assert.AreEqual(2, R("H", GameColor.Red, 0, 0, 2, 1).OrbitUnits);
        }
    }

    public class BoardSolverTests
    {
        [Test]
        public void Solver_DetectsUnclearableBoard()
        {
            // Mavi roketin hiçbir kenarda kapısı yok.
            var rockets = new[] { new RocketSpec("B", GameColor.Blue, new Vector2Int(2, 2), Vector2Int.one, 20) };
            var exits = new[] { new ExitSpec(GameColor.Red, Edge.Top, 0, 6) };
            Assert.IsFalse(BoardSolver.CanClearBoard(6, 6, rockets, exits));
        }

        [Test]
        public void Solver_FindsSolutionThatNeedsAMoveFirst()
        {
            // Kırmızının yolu mavi tarafından kapalı; mavi önce yana çekilmeli.
            var rockets = new[]
            {
                new RocketSpec("R", GameColor.Red, new Vector2Int(0, 0), Vector2Int.one, 20),
                new RocketSpec("B", GameColor.Blue, new Vector2Int(0, 3), Vector2Int.one, 20),
            };
            var exits = new[] { new ExitSpec(GameColor.Red, Edge.Top, 0, 1), new ExitSpec(GameColor.Blue, Edge.Bottom, 3, 1) };
            Assert.IsTrue(BoardSolver.CanClearBoard(6, 6, rockets, exits));
        }
    }

    public class BlockerTests
    {
        [Test]
        public void Blocker_CanMove_ButNeverExits()
        {
            var blocker = RocketSpec.Blocker("X", new Vector2Int(0, 4), Vector2Int.one);
            var board = new BoardModel(6, 6, new[] { blocker }, new[] { new ExitSpec(GameColor.Red, Edge.Top, 0, 1) });
            Assert.AreEqual(ExitCheck.NoGate, board.CheckExit(0, Dir.Up));
            board.Move(0, Dir.Right, 2);
            Assert.AreEqual(new Vector2Int(2, 4), board.GetRocket(0).origin);
            Assert.AreEqual(0, board.RemainingCount, "engeller kalan roket sayısına girmez");
        }

        [Test]
        public void Solver_ClearsRockets_WhileBlockersStay()
        {
            // Engel kırmızının yolunu kapatıyor; çözüm engeli yana çekmek.
            var rockets = new[]
            {
                new RocketSpec("R", GameColor.Red, new Vector2Int(0, 0), Vector2Int.one, 20),
                RocketSpec.Blocker("X", new Vector2Int(0, 3), Vector2Int.one),
            };
            var exits = new[] { new ExitSpec(GameColor.Red, Edge.Top, 0, 1) };
            Assert.IsTrue(BoardSolver.CanClearBoard(6, 6, rockets, exits));
        }

        [Test]
        public void CapacityPlanner_IgnoresBlockers()
        {
            var rockets = new System.Collections.Generic.List<RocketSpec>
            {
                new("R", GameColor.Red, Vector2Int.zero, Vector2Int.one, 0),
                RocketSpec.Blocker("X", new Vector2Int(1, 0), Vector2Int.one),
            };
            CapacityPlanner.Assign(rockets, new System.Collections.Generic.Dictionary<GameColor, int> { [GameColor.Red] = 30 });
            Assert.AreEqual(30, rockets[0].capacity);
            Assert.AreEqual(0, rockets[1].capacity);
        }
    }

    public class OrbitModelTests
    {
        [Test]
        public void Reserve_RespectsUnits()
        {
            var orbit = new OrbitModel(5);
            Assert.IsTrue(orbit.TryReserve(2));
            Assert.IsTrue(orbit.TryReserve(2));
            Assert.AreEqual(4, orbit.UsedUnits);
            // GDD: 4/5 doluyken büyük roket çıkamaz, küçük çıkabilir.
            Assert.IsFalse(orbit.TryReserve(2));
            Assert.IsTrue(orbit.TryReserve(1));
            Assert.IsFalse(orbit.TryReserve(1));
            orbit.Release(2);
            Assert.IsTrue(orbit.CanReserve(2));
        }

        [Test]
        public void Release_MoreThanUsed_Throws()
        {
            var orbit = new OrbitModel(5);
            orbit.TryReserve(1);
            Assert.Throws<InvalidOperationException>(() => orbit.Release(2));
        }
    }
}
