using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class SourceKeysTest
    {
        static readonly Regex SpaceOrEnter = new(
            @"\b(spaceKey|enterKey|numpadEnterKey|Key\.Space|Key\.Enter|Key\.NumpadEnter|KeyCode\.Space|KeyCode\.Return|KeyCode\.KeypadEnter)\b");
        const string WeaponCardKeys = "GameManagerFlow.cs";

        [Test]
        public void GameScripts_ReadNeitherSpaceNorEnter_ExceptTheWeaponCardKeys()
        {
            string root = Path.Combine(Application.dataPath, "AnomalyArena", "Scripts");
            Assume.That(Directory.Exists(root), Is.True, "the game scripts are on disk (editor test run)");
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            var offenders = files.Where(file => Path.GetFileName(file) != WeaponCardKeys)
                .SelectMany(file => File.ReadAllLines(file)
                    .Select((line, i) => (file, i, code: line.Split(new[] { "//" }, 2, System.StringSplitOptions.None)[0]))
                    .Where(entry => SpaceOrEnter.IsMatch(entry.code))
                    .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.i + 1}"))
                .ToArray();
            Assert.That(offenders, Is.Empty, "scripts other than the weapon card's key handling that read Space or Enter");
            var flow = files.Single(file => Path.GetFileName(file) == WeaponCardKeys);
            Assert.That(SpaceOrEnter.IsMatch(File.ReadAllText(flow)), Is.True, "the weapon card's key handling reads them");
        }
    }
}
