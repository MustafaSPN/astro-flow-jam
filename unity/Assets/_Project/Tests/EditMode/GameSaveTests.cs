using AstroFlowJam;
using NUnit.Framework;
using UnityEngine;

namespace AstroFlowJam.Tests
{
    public class GameSaveTests
    {
        const string Key = "afj_test_save";

        [SetUp, TearDown]
        public void Clean() => GameSave.DeleteKey(Key);

        [Test]
        public void MissingKey_ReturnsEachCallersFallback()
        {
            Assert.AreEqual(3, GameSave.GetInt(Key, 3));
            Assert.AreEqual(7, GameSave.GetInt(Key, 7));
        }

        [Test]
        public void SetInt_IsReadBackAndWrittenToPrefs()
        {
            Assert.AreEqual(0, GameSave.GetInt(Key));      // önce "yok" olarak önbelleğe girer
            GameSave.SetInt(Key, 5);
            Assert.AreEqual(5, GameSave.GetInt(Key, 1));
            Assert.AreEqual(5, PlayerPrefs.GetInt(Key));
        }

        [Test]
        public void DeleteKey_RevertsToFallback()
        {
            GameSave.SetString(Key, "x");
            GameSave.DeleteKey(Key);
            Assert.AreEqual("y", GameSave.GetString(Key, "y"));
            Assert.IsFalse(PlayerPrefs.HasKey(Key));
        }

        [Test]
        public void GameSettings_ToggleFlipsAndAnnounces()
        {
            var changed = 0;
            void Count() => changed++;
            GameSettings.Changed += Count;
            try
            {
                Assert.IsTrue(GameSettings.IsOn(Key));         // kayıt yokken açık
                GameSettings.Toggle(Key);
                Assert.IsFalse(GameSettings.IsOn(Key));
                GameSettings.Toggle(Key);
                Assert.IsTrue(GameSettings.IsOn(Key));
                Assert.AreEqual(2, changed);
            }
            finally { GameSettings.Changed -= Count; }
        }

        [Test]
        public void NumberText_MatchesToString()
        {
            foreach (var n in new[] { -1, 0, 7, 1023, 1024, 50000 })
                Assert.AreEqual(n.ToString(), NumberText.Of(n));
        }
    }
}
