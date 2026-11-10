using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The whitebox table in the game (2026-11-10): every look of work/whitebox-1110/looks.json builds, one renderer per part
    /// with a mesh filling the part's box and no collider; every enemy kind, the player, the decoy and every weapon in use has its own
    /// look; a character's look stands on the ground with its body inside the prefab's collider; the element states and a weapon's
    /// element move the colours; and the game's copy of the table (Whitebox.Table.cs) was made from the table on disk.</summary>
    [TestFixture]
    public class WhiteboxLooksTest
    {
        static IEnumerable<string> LookIds => Whitebox.LookIds.ToArray();

        static IEnumerable<EnemyKind> Kinds => (EnemyKind[])Enum.GetValues(typeof(EnemyKind));

        [TestCaseSource(nameof(LookIds)), CreateScene]
        public void Build_EveryLookOfTheTable_OneRendererPerPart_FillingItsBox_NoCollider(string id)
        {
            var parent = new GameObject("Parent").transform;
            var root = Whitebox.Build(id, parent);
            var look = root.GetComponent<WhiteboxLook>();
            var parts = Whitebox.PartsOf(id);

            Assert.That(parts.Count, Is.GreaterThan(0), "the table has parts for it");
            Assert.That(look.LookId, Is.EqualTo(id));
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(parts.Count), "one renderer per part");
            Assert.That(root.GetComponentsInChildren<Collider>(), Is.Empty, "no colliders");
            for (int i = 0; i < parts.Count; i++)
            {
                var filter = look.PartRenderer(i).GetComponent<MeshFilter>();
                Assert.That(filter.sharedMesh && filter.sharedMesh.vertexCount > 0, Is.True, $"part {i} has a mesh");
                var t = filter.transform;
                var size = Vector3.Scale(filter.sharedMesh.bounds.size, t.localScale);
                Assert.That(Vector3.Distance(size, parts[i].size), Is.LessThan(.01f), $"part {i} ({parts[i].prim}) fills its box");
                Assert.That(Vector3.Distance(t.localPosition + Vector3.Scale(filter.sharedMesh.bounds.center, t.localScale), parts[i].position),
                    Is.LessThan(.01f), $"part {i} ({parts[i].prim}) sits where the table puts it");
                Assert.That(look.ShownColour(i), Is.EqualTo(parts[i].TableColour), $"part {i} starts in the table's colour");
            }
            Object.Destroy(parent.gameObject);
        }

        [Test, CreateScene]
        public void Build_UnknownId_GivesAPlainCubeAndAWarning()
        {
            var parent = new GameObject("Parent").transform;
            string id = "no such look " + Guid.NewGuid().ToString("N"); // a new id each time: an id warns only once per play
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no look \"no such look "));

            var root = Whitebox.Build(id, parent);

            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(1));
            Object.Destroy(parent.gameObject);
        }

        [Test]
        public void LookIds_EveryEnemyKindThePlayerAndTheDecoy_HaveTheirOwnLook()
        {
            foreach (var kind in Kinds)
                Assert.That(Whitebox.Has(WhiteboxCharacter.LookIdFor(kind)), Is.True, $"{kind} has a look");
            Assert.That(Kinds.Select(WhiteboxCharacter.LookIdFor).Distinct().Count(), Is.EqualTo(Kinds.Count()), "no two kinds share a look");
            Assert.That(Whitebox.Has("player") && Whitebox.Has("decoy"), Is.True, "the player and the decoy");
        }

        [Test]
        public void LookIds_EveryWeaponInUse_HasItsOwnLook_TheRetiredOnesNone()
        {
            var inUse = ((WeaponForm[])Enum.GetValues(typeof(WeaponForm))).Where(f => !WeaponRoster.IsRetired(f)).ToArray();
            foreach (var form in (WeaponForm[])Enum.GetValues(typeof(WeaponForm)))
            {
                string id = V2WeaponPresentation.LookIdFor(form);
                if (WeaponRoster.IsRetired(form)) Assert.That(id, Is.Null, $"{form} is retired");
                else Assert.That(Whitebox.Has(id), Is.True, $"{form} has a look ({id})");
            }
            Assert.That(inUse.Select(V2WeaponPresentation.LookIdFor).Distinct().Count(), Is.EqualTo(inUse.Length), "no two weapons share a look");
        }

        /// <summary>Every character's look on its own prefab's collider: its lowest point on the ground, its body (the largest part) no
        /// wider than the capsule, its top not far over the capsule. Marks and small pieces (a barrel, a tail, a crown) may reach out.</summary>
        [Test]
        public void CharacterLooks_StandOnTheGround_TheirBodyInsideThePrefabsCollider()
        {
#if UNITY_EDITOR
            var characters = Kinds.Select(k => (name: k.ToString(), id: WhiteboxCharacter.LookIdFor(k))).Append((name: "PlayerV2", id: "player"));
            foreach (var (name, id) in characters)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/AnomalyArena/V2/Prefabs/{name}.prefab");
                Assert.That(prefab, Is.Not.Null, $"the {name} prefab");
                var capsule = prefab.GetComponent<CapsuleCollider>();
                Assert.That(capsule, Is.Not.Null, $"{name} has its capsule");
                var parts = Whitebox.PartsOf(id);
                var bounds = Whitebox.LocalBounds(id);
                var body = parts[Whitebox.MainPart(parts)];
                float feet = capsule.center.y - capsule.height * .5f;
                Assert.That(feet, Is.EqualTo(0f).Within(.01f), $"{name}'s feet are at its origin");
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(.01f), $"{id} stands on the ground");
                Assert.That(Mathf.Max(body.size.x, body.size.z) * .5f, Is.LessThanOrEqualTo(capsule.radius * 1.25f), $"{id}'s body is about as wide as {name}'s collider");
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(capsule.height * 1.3f), $"{id} is about as tall as {name}'s collider");
            }
#else
            Assert.Ignore("Reads the prefabs in the editor only.");
#endif
        }

        [Test]
        public void StatusTint_EachState_MovesTheColourTowardItsElement_AlphaKept()
        {
            var own = new Color(.5f, .5f, .5f, .8f);

            var none = WhiteboxCharacter.StatusTint(own, default, 1f);
            var frozen = WhiteboxCharacter.StatusTint(own, new WhiteboxCharacter.StatusView { frozen = true }, 1f);
            var burning = WhiteboxCharacter.StatusTint(own, new WhiteboxCharacter.StatusView { fire = 5f }, 1f);
            var poisoned = WhiteboxCharacter.StatusTint(own, new WhiteboxCharacter.StatusView { poison = 3f }, 1f);
            var blinkOn = WhiteboxCharacter.StatusTint(own, new WhiteboxCharacter.StatusView { paralysed = true }, 0f);
            var blinkOff = WhiteboxCharacter.StatusTint(own, new WhiteboxCharacter.StatusView { paralysed = true }, .125f);

            Assert.That(none, Is.EqualTo(own), "no state leaves the colour");
            Assert.That(Nearer(frozen, own, Whitebox.Colour("ice")), Is.True, "frozen: toward the icy blue");
            Assert.That(Nearer(burning, own, Whitebox.Colour("fire")), Is.True, "burning: toward orange");
            Assert.That(Nearer(poisoned, own, Whitebox.Colour("poison")), Is.True, "poisoned: toward green");
            Assert.That(Nearer(blinkOn, own, Whitebox.Colour("shock")), Is.True, "paralysed: toward yellow");
            Assert.That(Distance(blinkOn, blinkOff), Is.GreaterThan(.05f), "paralysed: it blinks");
            Assert.That(new[] { frozen.a, burning.a, poisoned.a, blinkOn.a }, Is.All.EqualTo(own.a), "the alpha is kept");
        }

        [Test, CreateScene]
        public void PaintElement_TheFrontEndTakesTheElementColour_TheRestKeepsTheTable_NoneGivesItBack()
        {
            var parent = new GameObject("Parent").transform;
            var look = Whitebox.Build(V2WeaponPresentation.LookIdFor(WeaponForm.Rifle), parent).GetComponent<WhiteboxLook>();
            int barrel = 0, stock = 1; // the table's rifle: a long barrel reaching furthest forward, a stock behind the grip
            Assume.That(look.Part(barrel).Bounds.max.z, Is.GreaterThan(look.Part(stock).Bounds.max.z), "the barrel is the front end");

            V2WeaponPresentation.PaintElement(look, ElementKind.Frost);
            var front = look.ShownColour(barrel);
            var back = look.ShownColour(stock);
            V2WeaponPresentation.PaintElement(look, ElementKind.None);

            Assert.That(front, Is.EqualTo(ElementCombat.ColorFor(ElementKind.Frost)), "the front end in the element's colour");
            Assert.That(back, Is.EqualTo(look.TableColour(stock)), "the rest in the table's colour");
            Assert.That(look.ShownColour(barrel), Is.EqualTo(look.TableColour(barrel)), "no element: the table's colour again");
            Object.Destroy(parent.gameObject);
        }

        /// <summary>Whitebox.Table.cs is made from looks.json by make_whitebox_cs.py; a table changed without running the script fails
        /// here. The table lives in work/, which a copy of the project made only for testing may not have.</summary>
        [Test]
        public void TableSha256_MatchesTheTableOnDisk()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "work", "whitebox-1110", "looks.json"));
            if (!File.Exists(path)) Assert.Ignore("No whitebox table at " + path);
            var bytes = File.ReadAllBytes(path);
            var text = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n");
            string hash;
            using (var sha = SHA256.Create())
                hash = string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));

            Assert.That(hash, Is.EqualTo(Whitebox.TableSha256), "run work/whitebox-1110/make_whitebox_cs.py after changing the table");
        }

        /// <summary>The table seen in 3D, not only from the side: the repaired ship's fuel tank hangs outside the hull, and every shadow's
        /// eye sits on its body's front at its height (the side view had both where only a side view shows them).</summary>
        [Test]
        public void Table_FuelTankOutsideTheHull_ShadowEyesOnTheirBodies()
        {
            var ship = Whitebox.PartsOf("ship");
            var hull = ship[Whitebox.MainPart(ship)].Bounds;
            var tank = ship.First(p => p.colour == "core").Bounds;
            Assert.That(tank.max.x > hull.max.x || tank.min.x < hull.min.x, Is.True, "the fuel tank reaches out of the hull's side");

            foreach (var id in new[] { "shadow.a", "shadow.b", "shadow.c" })
            {
                var parts = Whitebox.PartsOf(id);
                var body = parts.First(p => p.colour == "shadow");
                var eye = parts.First(p => p.colour == "eye");
                float front = Whitebox.FrontAt(body, eye.position.y);
                Assert.That(eye.Bounds.min.z, Is.LessThanOrEqualTo(front + .001f), $"{id}: the eye touches the body");
                Assert.That(eye.Bounds.max.z, Is.GreaterThan(front), $"{id}: the eye shows in front of it");
            }
        }

        [Test, CreateScene]
        public void ShowParts_TheCratesLockHidden_ForGood_TheRestShown()
        {
            var parent = new GameObject("Parent").transform;
            var look = Whitebox.Build("crate", parent).GetComponent<WhiteboxLook>();

            int hidden = look.ShowParts("lock", false);
            look.Visible = false;
            look.Visible = true;

            Assert.That(hidden, Is.EqualTo(1), "the crate has one lock");
            for (int i = 0; i < look.PartCount; i++)
                Assert.That(look.PartRenderer(i).enabled, Is.EqualTo(look.PartColourKey(i) != "lock"), $"part {i} ({look.PartColourKey(i)})");
            Object.Destroy(parent.gameObject);
        }

        /// <summary>The whitebox base's walls are made from its look by the model walls' rules (V2Campaign.AddModelWalls over
        /// BaseFootprint.Outlines): the outline is the table's 10 x 10 base and its door, no larger.</summary>
        [Test, CreateScene]
        public void BaseFootprint_OfTheWhiteboxBase_IsWhatIsSeen()
        {
            var parent = new GameObject("Base").transform;
            var body = Whitebox.Build("base.body", parent);
            body.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var heights = new[] { .4f, Query.CastHeight, 1.4f, 2f, 2.6f };

            var outlines = BaseFootprint.Outlines(body.gameObject, parent, heights, 1, 32);

            Assert.That(outlines, Has.Count.EqualTo(1), "the meshes are readable and give one outline");
            var box = BaseFootprint.Bounds(outlines[0]);
            var seen = Whitebox.LocalBounds("base.body");
            Assert.That(box.width, Is.EqualTo(seen.size.x).Within(.05f), "as wide as the base");
            Assert.That(box.yMin, Is.EqualTo(-seen.max.z).Within(.05f), "out to the door on the south");
            Assert.That(box.yMax, Is.EqualTo(-seen.min.z).Within(.05f), "and to the back wall on the north");
            Object.Destroy(parent.gameObject);
        }

        static float Distance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        static bool Nearer(Color tinted, Color own, Color target) => Distance(tinted, target) < Distance(own, target) - .05f;
    }
}
