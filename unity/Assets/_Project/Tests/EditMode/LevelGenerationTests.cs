using System.Collections.Generic;
using AstroFlowJam.Core;
using NUnit.Framework;
using UnityEngine;

namespace AstroFlowJam.Tests
{
    public class LevelGenerationTests
    {
        static LevelGenerator.Recipe Recipe() => new()
        {
            id = "T", boardWidth = 4, boardHeight = 5, colors = new[] { GameColor.Blue, GameColor.Yellow },
            small = new[] { 1, 1 }, big = new[] { 1, 0 }, shape = LevelGenerator.Shape.Circle, seed = 1, candidates = 10,
        };

        [Test]
        public void Simulator_SolvesSingleRocketNextToItsDoor()
        {
            // 20 kırmızı boncuk, kapıya bitişik tek kırmızı roket.
            var input = new LevelSimulator.Input
            {
                boardWidth = 3, boardHeight = 3,
                rockets = new List<RocketSpec> { new("R1", GameColor.Red, new Vector2Int(0, 2), Vector2Int.one, 20) },
                exits = new List<ExitSpec> { new(GameColor.Red, Edge.Top, 0, 2) },
                beadColumns = 5, beadRows = 4, beadLayout = new string('R', 20),
            };
            var result = LevelSimulator.Run(input);
            Assert.IsTrue(result.solved, result.ToString());
            Assert.AreEqual(1f, result.usefulAtStart);
        }

        [Test]
        public void Generator_BalancesAmmoWithBeads_AndIsSolvable()
        {
            var level = LevelGenerator.Generate(Recipe());
            Assert.IsNotNull(level);
            var grid = new BeadGrid(level.beadColumns, level.beadRows, level.beadLayout);
            var ammo = new Dictionary<GameColor, int>();
            foreach (var r in level.rockets) ammo[r.color] = (ammo.TryGetValue(r.color, out var a) ? a : 0) + r.capacity;
            foreach (var kv in grid.InitialCounts) Assert.AreEqual(kv.Value, ammo[kv.Key], kv.Key.ToString());
            Assert.IsTrue(level.result.solved, level.result.ToString());
        }

        [Test]
        public void Generator_OuterLayerIsFirstColor()
        {
            var level = LevelGenerator.Generate(Recipe());
            var grid = new BeadGrid(level.beadColumns, level.beadRows, level.beadLayout);
            var outer = 0;
            var total = 0;
            for (var c = 0; c < grid.Columns; c++)
            {
                var b = grid.FirstInLine(GridSide.Top, c);
                if (b < 0) continue;
                total++;
                if (grid.ColorAt(b) == GameColor.Blue) outer++;
            }
            Assert.Greater(outer, total / 2, "Dış katman ağırlıkla ilk renk olmalı");
        }
    }
}
