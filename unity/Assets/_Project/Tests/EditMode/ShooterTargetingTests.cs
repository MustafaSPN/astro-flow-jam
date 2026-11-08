using AstroFlowJam.Core;
using NUnit.Framework;
using UnityEngine;

namespace AstroFlowJam.Tests
{
    public class ShooterTargetingTests
    {
        // 4×4, üst satırdan başlayarak:
        // R B R R
        // R R B R
        // . . . .
        // . . . .
        const string Layout = "RBRRRRBR........";

        static BeadGrid Grid() => new(4, 4, Layout);

        [Test]
        public void SideOf_UsesDominantAxisFromGridCenter()
        {
            var grid = Grid();
            Assert.AreEqual(GridSide.Top, ShooterTargeting.SideOf(grid, new Vector2(2.5f, -1f)));
            Assert.AreEqual(GridSide.Bottom, ShooterTargeting.SideOf(grid, new Vector2(1.5f, 5f)));
            Assert.AreEqual(GridSide.Left, ShooterTargeting.SideOf(grid, new Vector2(-1f, 1.5f)));
            Assert.AreEqual(GridSide.Right, ShooterTargeting.SideOf(grid, new Vector2(5f, 2.5f)));
        }

        [Test]
        public void Pick_TakesFirstSolidOnly_NeverLooksBehindAnotherColor()
        {
            var grid = Grid();
            // Sütun 1'e üstten bakan kırmızı: ilk dolu hücre mavi, arkasındaki kırmızı alınamaz.
            Assert.AreEqual(-1, ShooterTargeting.Pick(grid, new Vector2(1.5f, -1f), GameColor.Red, 0f));
            Assert.AreEqual(grid.Index(1, 0), ShooterTargeting.Pick(grid, new Vector2(1.5f, -1f), GameColor.Blue, 0f));
        }

        [Test]
        public void CanHitFromOrbit_SeesEveryEdgeLine_ButNeverBehindAnotherColor()
        {
            // Ortadaki yeşil dört yandan kırmızıyla çevrili: yeşil vuramaz, kırmızı vurur.
            var grid = new BeadGrid(3, 3, "RRRRGRRRR");
            Assert.IsFalse(ShooterTargeting.CanHitFromOrbit(grid, GameColor.Green));
            Assert.IsTrue(ShooterTargeting.CanHitFromOrbit(grid, GameColor.Red));
            // Üst kenardaki kırmızı açılınca yeşil 1. sütundan görünür.
            grid.TryReserve(grid.Index(1, 0));
            grid.Clear(grid.Index(1, 0));
            Assert.IsTrue(ShooterTargeting.CanHitFromOrbit(grid, GameColor.Green));
        }

        [Test]
        public void Pick_WithPass_HitsOnlyTheFrontBeadOfALinePerPass()
        {
            var grid = Grid();
            var pass = new PassLines();
            var shooter = new Vector2(0.5f, -1f);
            var front = ShooterTargeting.Pick(grid, shooter, GameColor.Red, 0f, pass);
            Assert.AreEqual(grid.Index(0, 0), front);
            grid.TryReserve(front);
            grid.Clear(front);
            // Öndeki vuruldu, arkasındaki (0,1) kırmızı ama aynı geçişte vurulamaz.
            Assert.AreEqual(-1, ShooterTargeting.Pick(grid, shooter, GameColor.Red, 0f, pass));
            // Yeni geçişte vurulur.
            pass.Reset();
            Assert.AreEqual(grid.Index(0, 1), ShooterTargeting.Pick(grid, shooter, GameColor.Red, 0f, pass));
        }

        [Test]
        public void Pick_ReservedFirstSolid_BlocksItsLine()
        {
            var grid = Grid();
            grid.TryReserve(grid.Index(0, 0));
            Assert.AreEqual(-1, ShooterTargeting.Pick(grid, new Vector2(0.5f, -1f), GameColor.Red, 0f));
            grid.Clear(grid.Index(0, 0));
            Assert.AreEqual(grid.Index(0, 1), ShooterTargeting.Pick(grid, new Vector2(0.5f, -1f), GameColor.Red, 0f));
        }

        [Test]
        public void Pick_UsesAlignedNeighbourLine_WithinTolerance()
        {
            var grid = Grid();
            // Sütun 1 ile 2 arasının biraz soluna bakan kırmızı: kendi sütunu (1) mavi, tolerans içindeki sütun 2 kırmızı.
            var shooter = new Vector2(1.9f, -1f);
            Assert.AreEqual(-1, ShooterTargeting.Pick(grid, shooter, GameColor.Red, 0.3f));
            Assert.AreEqual(grid.Index(2, 0), ShooterTargeting.Pick(grid, shooter, GameColor.Red, 0.7f));
        }

        [Test]
        public void Pick_PrefersNearestCandidate()
        {
            var grid = Grid();
            // Sol kenardan satır 0 ve 1'e hizalı (ikisi de kırmızı ile başlar); atıcı satır 1'e daha yakın.
            var shooter = new Vector2(-1f, 1.2f);
            Assert.AreEqual(grid.Index(0, 1), ShooterTargeting.Pick(grid, shooter, GameColor.Red, 1f));
        }

        [Test]
        public void AlignedLines_SortedByCloseness()
        {
            CollectionAssert.AreEqual(new[] { 1, 2 }, ShooterTargeting.AlignedLines(1.9f, 4, 0.7f));
            CollectionAssert.AreEqual(new[] { 1 }, ShooterTargeting.AlignedLines(1.5f, 4, 0.2f));
        }
    }
}
