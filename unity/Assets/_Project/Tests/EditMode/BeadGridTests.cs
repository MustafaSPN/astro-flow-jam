using System.Collections.Generic;
using AstroFlowJam.Core;
using NUnit.Framework;
using UnityEngine;

namespace AstroFlowJam.Tests
{
    public class BeadGridTests
    {
        // 3×3, üst satırdan başlayarak:
        // R B G
        // Y R B
        // G Y R
        const string Layout = "RBGYRBGYR";

        [Test]
        public void FirstInLine_LooksFromEachSide()
        {
            var grid = new BeadGrid(3, 3, Layout);
            Assert.AreEqual(grid.Index(0, 0), grid.FirstInLine(GridSide.Top, 0));
            Assert.AreEqual(grid.Index(1, 2), grid.FirstInLine(GridSide.Bottom, 1));
            Assert.AreEqual(grid.Index(0, 1), grid.FirstInLine(GridSide.Left, 1));
            Assert.AreEqual(grid.Index(2, 2), grid.FirstInLine(GridSide.Right, 2));
            Assert.AreEqual(-1, grid.FirstInLine(GridSide.Top, 3));
        }

        [Test]
        public void ReservedBead_KeepsBlocking_UntilCleared()
        {
            var grid = new BeadGrid(3, 3, Layout);
            var top = grid.FirstInLine(GridSide.Top, 0);
            Assert.IsTrue(grid.TryReserve(top));
            Assert.IsFalse(grid.TryReserve(top));
            Assert.AreEqual(top, grid.FirstInLine(GridSide.Top, 0), "rezerve boncuk arkasındakini engellemeli");

            grid.Clear(top);
            Assert.AreEqual(grid.Index(0, 1), grid.FirstInLine(GridSide.Top, 0));
            Assert.AreEqual(8, grid.Remaining);
        }

        [Test]
        public void EmptyCells_AreSkipped_AndNotCounted()
        {
            var grid = new BeadGrid(3, 1, ".R.");
            Assert.AreEqual(1, grid.Total);
            Assert.AreEqual(grid.Index(1, 0), grid.FirstInLine(GridSide.Left, 0));
            Assert.AreEqual(1, grid.InitialCounts[GameColor.Red]);
        }

        [Test]
        public void Counts_MatchLayout()
        {
            var counts = new BeadGrid(3, 3, Layout).InitialCounts;
            Assert.AreEqual(3, counts[GameColor.Red]);
            Assert.AreEqual(2, counts[GameColor.Blue]);
            Assert.AreEqual(2, counts[GameColor.Green]);
            Assert.AreEqual(2, counts[GameColor.Yellow]);
        }
    }

    public class CapacityPlannerTests
    {
        static RocketSpec R(string id, GameColor c, int w, int h) => new(id, c, Vector2Int.zero, new Vector2Int(w, h), 0);

        [Test]
        public void BigRocketGetsDoubleShare_AndTotalsMatchExactly()
        {
            var rockets = new List<RocketSpec> { R("B1", GameColor.Blue, 1, 2), R("B2", GameColor.Blue, 1, 1), R("R1", GameColor.Red, 1, 1) };
            CapacityPlanner.Assign(rockets, new Dictionary<GameColor, int> { [GameColor.Blue] = 133, [GameColor.Red] = 43 });
            Assert.AreEqual(89, rockets[0].capacity);
            Assert.AreEqual(44, rockets[1].capacity);
            Assert.AreEqual(43, rockets[2].capacity);
        }

        [Test]
        public void ColorWithBeadsButNoRocket_Throws()
        {
            var rockets = new List<RocketSpec> { R("R1", GameColor.Red, 1, 1) };
            Assert.Throws<System.ArgumentException>(() =>
                CapacityPlanner.Assign(rockets, new Dictionary<GameColor, int> { [GameColor.Red] = 10, [GameColor.Blue] = 5 }));
        }
    }
}
