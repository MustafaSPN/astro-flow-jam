using System;
using System.Collections.Generic;
using AstroFlowJam.Core;
using NUnit.Framework;
using UnityEngine;

namespace AstroFlowJam.Tests
{
    /// <summary>Tahta mekanikleri: delik, tek eksenli blok, buzlu blok, anahtar – kilit, asteroit, katmanlı blok.</summary>
    public class BoardMechanicTests
    {
        static RocketSpec R(string id, GameColor c, int x, int y, int w = 1, int h = 1) =>
            new(id, c, new Vector2Int(x, y), new Vector2Int(w, h), 20);

        static ExitSpec Gate(GameColor c, Edge e, int start, int width = 2) => new(c, e, start, width);

        [Test]
        public void Hole_BlocksMovement_AndExitPath()
        {
            var holes = new[] { new Vector2Int(0, 3) };
            var board = new BoardModel(6, 6, new[] { R("A", GameColor.Red, 0, 0) }, new[] { Gate(GameColor.Red, Edge.Top, 0) }, holes);
            Assert.IsTrue(board.IsHole(new Vector2Int(0, 3)));
            Assert.AreEqual(2, board.FreeDistance(0, Dir.Up));
            Assert.AreEqual(ExitCheck.Blocked, board.CheckExit(0, Dir.Up));
            Assert.IsFalse(board.IsFreeFor(0, new Vector2Int(0, 3)));
            Assert.Throws<InvalidOperationException>(() => board.Relocate(0, new Vector2Int(0, 3)));
        }

        [Test]
        public void Hole_UnderRocket_IsRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new BoardModel(6, 6, new[] { R("A", GameColor.Red, 1, 1) }, Array.Empty<ExitSpec>(), new[] { new Vector2Int(1, 1) }));
        }

        [Test]
        public void OneWay_MovesAndExitsOnlyAlongItsAxis()
        {
            var a = R("A", GameColor.Red, 2, 2);
            a.axis = MoveAxis.Horizontal;
            var board = new BoardModel(6, 6, new[] { a }, new[] { Gate(GameColor.Red, Edge.Top, 2), Gate(GameColor.Red, Edge.Right, 2) });
            Assert.AreEqual(0, board.FreeDistance(0, Dir.Up));
            Assert.AreEqual(3, board.FreeDistance(0, Dir.Right));
            Assert.AreEqual(ExitCheck.Blocked, board.CheckExit(0, Dir.Up));
            Assert.AreEqual(ExitCheck.Ok, board.CheckExit(0, Dir.Right));
        }

        [Test]
        public void Ice_FreezesUntilEnoughBlocksExit()
        {
            var iced = R("Ice", GameColor.Blue, 3, 0);
            iced.ice = 2;
            var board = new BoardModel(6, 6, new[] { R("A", GameColor.Red, 0, 0), R("B", GameColor.Red, 0, 2), iced },
                new[] { Gate(GameColor.Red, Edge.Left, 0, 3), Gate(GameColor.Blue, Edge.Right, 0) });
            var changed = new List<int>();
            board.FrozenChanged += changed.Add;

            Assert.IsTrue(board.IsFrozen(2));
            Assert.AreEqual(0, board.FreeDistance(2, Dir.Right));
            Assert.AreEqual(ExitCheck.Blocked, board.CheckExit(2, Dir.Right));

            board.RemoveForExit(0, Dir.Left);
            Assert.AreEqual(1, board.IceLeft(2));
            Assert.IsTrue(board.IsFrozen(2));
            board.RemoveForExit(1, Dir.Left);
            Assert.AreEqual(0, board.IceLeft(2));
            Assert.IsFalse(board.IsFrozen(2));
            Assert.AreEqual(ExitCheck.Ok, board.CheckExit(2, Dir.Right));
            CollectionAssert.AreEqual(new[] { 2, 2 }, changed);
        }

        [Test]
        public void Lock_OpensWhenKeyOfSameGroupExits()
        {
            var key = R("Key", GameColor.Red, 0, 0);
            key.keyGroup = 1;
            var other = R("Other", GameColor.Red, 0, 2);
            var lockedA = R("LockA", GameColor.Blue, 5, 0);
            lockedA.lockGroup = 1;
            var lockedB = R("LockB", GameColor.Blue, 5, 3);
            lockedB.lockGroup = 2;
            var board = new BoardModel(6, 6, new[] { key, other, lockedA, lockedB },
                new[] { Gate(GameColor.Red, Edge.Left, 0, 3), Gate(GameColor.Blue, Edge.Right, 0, 4) });

            Assert.IsTrue(board.IsLocked(2));
            board.RemoveForExit(1, Dir.Left);          // anahtarsız blok kilidi açmaz
            Assert.IsTrue(board.IsLocked(2));
            board.RemoveForExit(0, Dir.Left);          // 1. grubun anahtarı
            Assert.IsFalse(board.IsLocked(2));
            Assert.IsTrue(board.IsLocked(3));          // başka grubun kilidi kapalı kalır
            Assert.AreEqual(ExitCheck.Ok, board.CheckExit(2, Dir.Right));
        }

        [Test]
        public void Asteroid_FixedBlocksMovement_CountedBreaksAfterExits()
        {
            // Kalıcı L (pivot 2,2) ve sayacı 1 olan tek hücre (0,3): A yukarı çıkamaz, B çıkınca tek hücre kırılır.
            var rocks = new[]
            {
                new AsteroidSpec { shape = AsteroidShape.L, origin = new Vector2Int(2, 2) },
                new AsteroidSpec { shape = AsteroidShape.Single, origin = new Vector2Int(0, 3), count = 1 },
            };
            var board = new BoardModel(6, 6, new[] { R("A", GameColor.Red, 0, 0), R("B", GameColor.Blue, 5, 0) },
                new[] { Gate(GameColor.Red, Edge.Top, 0), Gate(GameColor.Blue, Edge.Right, 0) }, null, rocks);
            Assert.IsTrue(board.IsAsteroid(new Vector2Int(2, 4)));
            Assert.IsTrue(board.IsAsteroid(new Vector2Int(3, 2)));
            Assert.AreEqual(2, board.FreeDistance(0, Dir.Up));
            Assert.AreEqual(ExitCheck.Blocked, board.CheckExit(0, Dir.Up));
            Assert.IsFalse(board.TryGetRocketAt(new Vector2Int(0, 3), out _));

            var changed = -1;
            board.AsteroidChanged += k => changed = k;
            board.RemoveForExit(1, Dir.Right);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(-1, board.AsteroidLeft(1));
            Assert.IsFalse(board.IsAsteroid(new Vector2Int(0, 3)));
            Assert.AreEqual(0, board.AsteroidLeft(0));                 // kalıcı asteroit yerinde
            Assert.AreEqual(ExitCheck.Ok, board.CheckExit(0, Dir.Up));
        }

        [Test]
        public void Solver_WaitsForCountedAsteroid()
        {
            // Tek sütunluk tahta: kırmızı yukarı ancak asteroit kırılınca çıkar; mavi sağdan çıkıp sayacı bitirir.
            var rocks = new[] { new AsteroidSpec { shape = AsteroidShape.Single, origin = new Vector2Int(0, 3), count = 1 } };
            var rockets = new[] { R("A", GameColor.Red, 0, 0), R("B", GameColor.Blue, 0, 5) };
            var exits = new[] { Gate(GameColor.Red, Edge.Top, 0, 1), Gate(GameColor.Blue, Edge.Right, 5, 1) };
            Assert.IsTrue(BoardSolver.CanClearBoard(1, 6, rockets, exits, asteroids: rocks));
            var fixedRock = new[] { new AsteroidSpec { shape = AsteroidShape.Single, origin = new Vector2Int(0, 3) } };
            Assert.IsFalse(BoardSolver.CanClearBoard(1, 6, rockets, exits, asteroids: fixedRock));
        }
    
        [Test]
        public void Layered_OuterExits_BlockStaysWithInnerColor()
        {
            var layered = R("L", GameColor.Red, 0, 0);
            layered.layered = true;
            layered.innerColor = GameColor.Blue;
            layered.innerCapacity = 7;
            var iced = R("I", GameColor.Green, 3, 3);
            iced.ice = 2;
            var board = new BoardModel(6, 6, new[] { layered, iced },
                new[] { Gate(GameColor.Red, Edge.Left, 0), Gate(GameColor.Blue, Edge.Bottom, 0), Gate(GameColor.Green, Edge.Top, 3) });

            Assert.AreEqual(ExitCheck.WrongColor, board.CheckExit(0, Dir.Down));   // önce dış renk
            board.RemoveForExit(0, Dir.Left);
            Assert.IsTrue(board.IsOnBoard(0));
            var inner = board.GetRocket(0);
            Assert.AreEqual(GameColor.Blue, inner.color);
            Assert.AreEqual(7, inner.capacity);
            Assert.IsFalse(inner.layered);
            Assert.AreEqual(1, board.IceLeft(1));                                  // katman çıkışı da sayaçları azaltır
            Assert.IsTrue(board.TryGetRocketAt(new Vector2Int(0, 0), out _));
            Assert.AreEqual(ExitCheck.Ok, board.CheckExit(0, Dir.Down));
            board.RemoveForExit(0, Dir.Down);
            Assert.IsFalse(board.IsOnBoard(0));
            Assert.AreEqual(0, board.IceLeft(1));
        }

        [Test]
        public void BoosterRemoval_EachBlockCountsAsAnExit()
        {
            // UFO üç kırmızı bloğu alır: buz sayacı (3) ve sayaçlı asteroit (3) üç kez azalır; ikisi de sıfıra iner.
            var iced = R("I", GameColor.Blue, 5, 5);
            iced.ice = 3;
            var rocks = new[] { new AsteroidSpec { shape = AsteroidShape.Single, origin = new Vector2Int(3, 3), count = 3 } };
            var board = new BoardModel(6, 6, new[] { R("A", GameColor.Red, 0, 0), R("B", GameColor.Red, 2, 0), R("C", GameColor.Red, 4, 0), iced },
                new[] { Gate(GameColor.Red, Edge.Left, 0), Gate(GameColor.Blue, Edge.Right, 4) }, null, rocks);
            for (var i = 0; i < 3; i++) board.RemoveByBooster(i);
            Assert.AreEqual(0, board.IceLeft(3));
            Assert.IsFalse(board.IsFrozen(3));
            Assert.AreEqual(-1, board.AsteroidLeft(0));
            Assert.IsFalse(board.IsAsteroid(new Vector2Int(3, 3)));
        }

        [Test]
        public void Solver_PeelsLayers()
        {
            var layered = R("L", GameColor.Red, 0, 0);
            layered.layered = true;
            layered.innerColor = GameColor.Blue;
            layered.innerCapacity = 5;
            var exits = new[] { Gate(GameColor.Red, Edge.Left, 0), Gate(GameColor.Blue, Edge.Right, 0) };
            Assert.IsTrue(BoardSolver.CanClearBoard(6, 6, new[] { layered }, exits));
            Assert.IsFalse(BoardSolver.CanClearBoard(6, 6, new[] { layered }, new[] { Gate(GameColor.Blue, Edge.Right, 0) }));
        }
    }
}
