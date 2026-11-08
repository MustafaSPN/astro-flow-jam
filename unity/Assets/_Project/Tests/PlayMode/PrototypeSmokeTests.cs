using System.Collections;
using AstroFlowJam.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AstroFlowJam.Tests
{
    /// <summary>
    /// Prototip sahnesinde uçtan uca akış: sanal fareyle sürükleme (fizikli hareket ve hücreye oturma), kapı eşiğinde
    /// çıkış, kapı animasyonu, banda zıplama ve atışla boncuk temizlenmesi. Testler ilk bölümdeki uygun roketi kendisi bulur.
    /// </summary>
    public class PrototypeSmokeTests
    {
        Mouse mouse;
        LevelController controller;
        Camera cam;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Batch modda uygulamanın odağı yoktur; varsayılan ayar odaksız cihaz girdisini yok sayar.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Prototype");
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<LevelController>();
            cam = Camera.main;
            Assert.IsNotNull(controller);
            Assert.IsNotNull(controller.Beads, "İlk bölüm boncuklu olmalı");
        }

        [TearDown]
        public void TearDown() => InputSystem.RemoveDevice(mouse);

        [UnityTest]
        public IEnumerator Drag_MovesRocketWithPhysics_AndSnapsToCell()
        {
            Assert.IsTrue(FindSlide(out var index, out var dir), "Bir hücre kaydırılabilen ve kapıdan çıkmayan roket olmalı");
            var from = controller.Board.GetRocket(index).origin;
            var to = from + dir.ToVector();

            yield return Press(CellCenter(from.x, from.y));
            // Parmak 0,4 sn'de bir hücre kayar, sonra 0,3 sn bekler (fizik adımları gerçek zamanla işlesin).
            for (var i = 1; i <= 20; i++)
            {
                yield return Move(Vector2.Lerp(CellCenter(from.x, from.y), CellCenter(to.x, to.y), i / 20f));
                yield return new WaitForSeconds(0.02f);
            }
            yield return new WaitForSeconds(0.3f);
            yield return Release(CellCenter(to.x, to.y));

            Assert.AreEqual(to, controller.Board.GetRocket(index).origin);
        }

        [UnityTest]
        public IEnumerator RocketAtDoorstep_ExitsWhileHeld_RidesBelt_AndClearsBeads()
        {
            Assert.IsTrue(FindReadyExit(out var index, out var dir), "Başta yolu kapıya açık bir roket olmalı");
            var before = controller.Beads.Remaining;
            var from = controller.Board.GetRocket(index).origin;
            var to = from + dir.ToVector() * controller.Board.DistanceToEdge(index, dir);

            // Roketi kapı eşiğine kadar sürükle (zaten eşikteyse yerinde basılı tut).
            yield return Press(CellCenter(from.x, from.y));
            for (var i = 1; i <= 20 && controller.Board.IsOnBoard(index); i++)
            {
                yield return Move(Vector2.Lerp(CellCenter(from.x, from.y), CellCenter(to.x, to.y), i / 20f));
                yield return new WaitForSeconds(0.02f);
            }
            yield return null;
            Assert.IsFalse(controller.Board.IsOnBoard(index), "Kapı eşiğindeki uygun roket basılı tutarken çıkmalı");
            yield return Release(CellCenter(to.x, to.y));

            yield return new WaitForSeconds(2.5f);
            Assert.Less(controller.Beads.Remaining, before, "Bantta dönen roket boncuk temizlemeli");
        }

        bool FindReadyExit(out int index, out Dir dir)
        {
            var board = controller.Board;
            dir = Dir.Up;
            for (index = 0; index < board.RocketCount; index++)
                foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
                    if (board.CheckExit(index, d) == ExitCheck.Ok) { dir = d; return true; }
            index = -1;
            return false;
        }

        static bool CanExitNow(BoardModel board, int index)
        {
            foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
                if (board.CheckExit(index, d) == ExitCheck.Ok) return true;
            return false;
        }

        // Bir hücre kaydırıldığında hiçbir kapının önüne gelmeyen roket ve yön.
        bool FindSlide(out int index, out Dir dir)
        {
            var board = controller.Board;
            for (index = 0; index < board.RocketCount; index++)
                foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
                {
                    // Dokunulduğu an kapıdan çıkacak roket sürükleme testine uymaz.
                    if (CanExitNow(board, index)) break;
                    if (board.FreeDistance(index, d) < 1) continue;
                    var rockets = new System.Collections.Generic.List<RocketSpec>();
                    for (var i = 0; i < board.RocketCount; i++) rockets.Add(board.GetRocket(i));
                    var moved = rockets[index];
                    moved.origin += d.ToVector();
                    rockets[index] = moved;
                    var probe = new BoardModel(board.Width, board.Height, rockets, board.Exits);
                    var exits = false;
                    foreach (Dir e in System.Enum.GetValues(typeof(Dir))) exits |= probe.CheckExit(index, e) != ExitCheck.NoGate && probe.DistanceToEdge(index, e) == 0;
                    if (exits) continue;
                    dir = d;
                    return true;
                }
            dir = Dir.Up;
            return false;
        }

        Vector2 CellCenter(int x, int y) => cam.WorldToScreenPoint(controller.BoardView.CellToWorld(new Vector2(x + 0.5f, y + 0.5f)));

        IEnumerator Press(Vector2 screen)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
            yield return null;
        }

        IEnumerator Move(Vector2 screen)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
            yield return null;
        }

        IEnumerator Release(Vector2 screen)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
            yield return null;
        }
    }
}
