using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AstroFlowJam.Tests
{
    public class LevelPicturesTests
    {
        const string Known = "L01_Hidden";

        [Test]
        public void Acquire_ReturnsSameTextureForEveryUser()
        {
            var a = LevelPictures.Acquire(Known);
            var b = LevelPictures.Acquire(Known);
            Assert.IsNotNull(a);
            Assert.AreSame(a, b);
            LevelPictures.Release(Known);
            LevelPictures.Release(Known);
        }

        [Test]
        public void UnknownOrEmptyName_IsHarmless()
        {
            Assert.IsNull(LevelPictures.Acquire(""));
            Assert.IsNull(LevelPictures.Acquire("yok_boyle_resim"));
            Assert.DoesNotThrow(() => LevelPictures.Release("yok_boyle_resim"));
        }

        [Test]
        public void EveryDatabaseLevel_PointsToAnExistingPicture()
        {
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/_Project/Data/LevelDatabase.asset");
            var missing = database.levels
                .Where(l => string.IsNullOrEmpty(l.pictureName) || !Resources.Load<Texture2D>("LevelPictures/" + l.pictureName))
                .Select(l => l.levelId).ToList();
            CollectionAssert.IsEmpty(missing);
        }
    }
}
