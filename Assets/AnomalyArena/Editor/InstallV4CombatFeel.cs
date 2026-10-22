using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AnomalyArena.EditorTools
{

    public static class InstallV4CombatFeel
    {
        static readonly string[] Scenes =
        {
            "Assets/AnomalyArena/Scenes/ArenaV2.unity", "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity",
            "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity"
        };

        const string TextureFolder = "Assets/AnomalyArena/Art/V4/VFX/Textures";
        const string PoolName = "V4 Combat VFX";
        const int PoolSize = 160;

        [Serializable]
        sealed class Entry
        {
            public string scene;
            public int blackHoles, blackHolesWithV4Layers, poolQuads, hitSparks, armoryChoices, edgeOutlines;
            public bool poolBound;
        }

        [Serializable]
        sealed class Receipt
        {
            public string savedAtUtc, rollbackFolder;
            public int texturesImported;
            public float[] mkDamage;
            public string[] offeredStatCards;
            public float[] bulwarkShieldShare;
            public Entry[] scenes;
            public bool playRun, testsRun;
        }

        [MenuItem("Anomaly Arena/V4/Install Combat Feel")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play before installing V4 combat feel.");
            var current = SceneManager.GetActiveScene();
            string returnTo = current.path;
            if (current.isDirty)
                throw new InvalidOperationException("The open scene has unsaved edits; save or discard them first.");
            var scenes = Scenes.Where(File.Exists).Distinct().ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No V2 scene found.");
            string backup = "work/rollback/v4-combat-feel-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            foreach (var path in scenes) File.Copy(path, backup + "/" + Path.GetFileName(path), false);

            int imported = ImportTextures();
            var receipt = new Receipt
                { savedAtUtc = DateTime.UtcNow.ToString("o"), rollbackFolder = backup, texturesImported = imported };
            WriteData(receipt, backup);

            var reports = new List<Entry>();
            foreach (var path in scenes)
            {
                var scene = SceneManager.GetActiveScene().path == path
                    ? SceneManager.GetActiveScene()
                    : EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                reports.Add(InstallScene(scene));
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Save failed: " + path);
            }

            AssetDatabase.SaveAssets();
            receipt.scenes = reports.ToArray();
            Directory.CreateDirectory("work/authoring-receipts");
            File.WriteAllText("work/authoring-receipts/v4-combat-feel.json", JsonUtility.ToJson(receipt, true));
            if (!string.IsNullOrEmpty(returnTo) && SceneManager.GetActiveScene().path != returnTo &&
                File.Exists(returnTo))
                EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            Debug.Log("[V4CombatFeel] " + JsonUtility.ToJson(receipt));
        }

        static int ImportTextures()
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = texture ? Mathf.Max(texture.width, 1) : 256;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
                count++;
            }

            return count;
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidOperationException("Missing asset: " + path);
            return asset;
        }

        static Texture2D Tex(string id) => Load<Texture2D>(TextureFolder + "/" + id + ".png");
        static Sprite SpriteOf(string id) => Load<Sprite>(TextureFolder + "/" + id + ".png");

        static Texture[] Frames(string prefix, int count) =>
            Enumerable.Range(0, count).Select(i => (Texture)Tex(prefix + i)).ToArray();

        static void WriteData(Receipt receipt, string backup)
        {
            const string progressionPath = "Assets/AnomalyArena/Data/ProgressionRules.asset";
            const string statusPath = "Assets/AnomalyArena/Data/StatusRules.asset";
            const string rosterPath = "Assets/AnomalyArena/V2/Data/WeaponRoster.asset";
            const string bulwarkPath = "Assets/AnomalyArena/Data/Skills/Bulwark.asset";
            foreach (var path in new[] { progressionPath, statusPath, rosterPath, bulwarkPath })
                File.Copy(path, backup + "/" + Path.GetFileName(path), true);

            var rules = Load<ProgressionRules>(progressionPath);
            var defaults = ScriptableObject.CreateInstance<ProgressionRules>();
            Undo.RecordObject(rules, "V4 stat cards");
            rules.statCards = defaults.statCards;
            var caps = rules.startingStats.caps;
            if (caps != null && caps.Length > (int)StatId.AttackSpeed) caps[(int)StatId.AttackSpeed] = 2f;
            rules.startingStats.critChance = 0f;
            rules.startingStats.critCap = .6f;
            rules.startingStats.critMultiplier = 2f;
            rules.startingStats.armor = 0f;
            rules.startingStats.armorConstant = 100f;
            rules.startingStats.armorCap = 300f;
            EditorUtility.SetDirty(rules);
            UnityEngine.Object.DestroyImmediate(defaults);
            receipt.offeredStatCards = rules.statCards.Where(card => !card.retired)
                .Select(card => card.stat + (card.bonus != StatBonus.None ? "+" + card.bonus : "")).ToArray();

            var status = Load<StatusRules>(statusPath);
            Undo.RecordObject(status, "V4 element rules");
            status.enemyFireStacks = 2f;
            EditorUtility.SetDirty(status);

            var roster = Load<WeaponRoster>(rosterPath);
            Undo.RecordObject(roster, "V4 Mk table");
            roster.mkDamage = new[] { 1f, 1.4f, 1.65f, 1.9f, 2.1f, 2.3f };
            roster.mkWaveSpacing = 3;
            foreach (var definition in roster.definitions)
                definition.retired = WeaponRoster.IsRetired(definition.form);
            EditorUtility.SetDirty(roster);
            receipt.mkDamage = roster.mkDamage;

            var bulwark = Load<SkillDefinition>(bulwarkPath);
            Undo.RecordObject(bulwark, "V4 Bulwark shield share");
            bulwark.strength = new[] { .2f, .3f, .3f };
            bulwark.shieldScalesWithMaxHp = true;
            EditorUtility.SetDirty(bulwark);
            receipt.bulwarkShieldShare = bulwark.strength;
            foreach (var guid in AssetDatabase.FindAssets("t:SkillDefinition",
                         new[] { "Assets/AnomalyArena/Data/Skills" }))
                EditorUtility.SetDirty(
                    AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            AssetDatabase.SaveAssets();
        }

        static Entry InstallScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var entry = new Entry { scene = scene.path };

            var pool = roots.SelectMany(root => root.GetComponentsInChildren<FxPool>(true)).FirstOrDefault();
            if (!pool || pool.items == null || pool.items.Length == 0 || !pool.items[0])
                throw new InvalidOperationException("Saved FxPool missing in " + scene.path);
            Undo.RecordObject(pool, "V4 hit sparks");
            pool.hitSparks = Frames("hit-spark-", 3);
            pool.hitCrit = Tex("hit-crit");
            pool.hitRing = Tex("hit-ring");
            pool.muzzleFlash = Tex("muzzle-flash");
            EditorUtility.SetDirty(pool);
            entry.hitSparks = pool.hitSparks.Length;

            foreach (var hole in roots.SelectMany(root => root.GetComponentsInChildren<V2BlackHolePresentation>(true)))
            {
                entry.blackHoles++;
                if (!hole.core || !hole.gravity) continue;
                Undo.RecordObject(hole, "V4 black hole layers");
                hole.warningRing = Layer(hole, hole.warningRing, "V4 Warning Ring", "hole-warning-ring");
                hole.inwardStreaks = Layer(hole, hole.inwardStreaks, "V4 Inward Streaks", "hole-inward-streaks");
                hole.coreGlow = Layer(hole, hole.coreGlow, "V4 Core Glow", "hole-core-glow");
                hole.rimGlow = Layer(hole, hole.rimGlow, "V4 Rim Glow", "hole-rim-glow");
                int order = hole.gravity.sortingOrder;
                var stack = new[]
                {
                    hole.gravity, hole.warningRing, hole.inwardStreaks, hole.coreGlow, hole.core, hole.swirl,
                    hole.rimGlow, hole.portal
                };
                for (int i = 0; i < stack.Length; i++)
                    if (stack[i])
                    {
                        stack[i].sortingOrder = order + i;
                        EditorUtility.SetDirty(stack[i]);
                    }

                EditorUtility.SetDirty(hole);
                entry.blackHolesWithV4Layers++;
            }

            entry.armoryChoices = EnsureThreeArmoryChoices(roots);

            var gm = roots.SelectMany(root => root.GetComponentsInChildren<GameManager>(true))
                .First();
            var vfx = roots.SelectMany(root => root.GetComponentsInChildren<V4CombatVfx>(true)).FirstOrDefault();
            if (!vfx)
            {
                var host = new GameObject(PoolName);
                Undo.RegisterCreatedObjectUndo(host, "V4 combat VFX pool");
                host.transform.SetParent(gm.transform, false);
                vfx = host.AddComponent<V4CombatVfx>();
            }

            Undo.RecordObject(vfx, "V4 combat VFX");
            var material = pool.items[0].sharedMaterial;
            var quads = new List<Renderer>();
            if (vfx.items != null) quads.AddRange(vfx.items.Where(item => item));
            while (quads.Count < PoolSize)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Quad " + quads.Count.ToString("000");
                UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(vfx.transform, false);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.enabled = false;
                quads.Add(renderer);
            }

            foreach (var quad in quads)
            {
                quad.sharedMaterial = material;
                EditorUtility.SetDirty(quad);
            }

            vfx.items = quads.ToArray();
            vfx.fireFrames = Frames("status-fire-flame-", 6);
            vfx.poisonFrames = Frames("status-poison-bubble-", 4);
            vfx.poisonMist = Tex("status-poison-mist");
            vfx.frostShell = Tex("status-frost-shell");
            vfx.frostRing = Tex("status-frost-ring");
            vfx.frostShard = Tex("status-frost-shard");
            vfx.lightningFrames = Frames("status-lightning-arc-", 4);
            vfx.lightningJolt = Tex("status-lightning-jolt");
            vfx.slowRing = Tex("status-slow-ring");
            vfx.auraOvercharge = Tex("aura-overcharge");
            vfx.auraRadiance = Tex("aura-radiance");
            vfx.auraTimeSlip = Tex("aura-timeslip");
            vfx.auraShield = Tex("aura-shield-hex");
            vfx.auraVoid = Tex("aura-void");
            vfx.strikeRocket = Tex("strike-rocket");
            vfx.strikeTarget = Tex("strike-target");
            vfx.strikeFlash = Tex("strike-flash");
            vfx.poisonSpike = Tex("enemy-poison-spike");
            vfx.prismBeam = Tex("elite-prism-beam");
            entry.edgeOutlines = EnsureEdgeOutlines(roots);
            EditorUtility.SetDirty(vfx);
            entry.poolQuads = vfx.items.Length;
            entry.poolBound = vfx.fireFrames.All(t => t) && vfx.lightningFrames.All(t => t) && vfx.auraShield;
            return entry;
        }

        static int EnsureEdgeOutlines(GameObject[] roots)
        {
            int count = 0;
            foreach (var labels in roots.SelectMany(root => root.GetComponentsInChildren<V2WorldLabels>(true)))
            {
                if (labels.edgeLabels == null) continue;
                foreach (var text in labels.edgeLabels)
                {
                    if (!text) continue;
                    var outline = text.GetComponent<UnityEngine.UI.Outline>();
                    if (!outline) outline = Undo.AddComponent<UnityEngine.UI.Outline>(text.gameObject);
                    outline.effectColor = new Color(.12f, .02f, .2f, .95f);
                    outline.effectDistance = new Vector2(2f, -2f);
                    EditorUtility.SetDirty(outline);
                    count++;
                }
            }

            return count;
        }

        static int EnsureThreeArmoryChoices(GameObject[] roots)
        {
            var screens = roots.SelectMany(root => root.GetComponentsInChildren<V2UiScreen>(true)).ToArray();
            var armory = screens.FirstOrDefault(screen => screen.state == ArenaUiState.Armory);
            var draft = screens.FirstOrDefault(screen => screen.state == ArenaUiState.Draft);
            if (!armory || armory.choices == null || armory.choices.Length == 0)
                throw new InvalidOperationException("Saved armory screen missing.");
            if (!draft || draft.choices == null || draft.choices.Length < RunWeapons.ArmoryOfferCount)
                throw new InvalidOperationException("Saved draft screen needs three cards for the layout.");
            Undo.RecordObject(armory, "V4 three armory cards");
            var choices = armory.choices.Where(choice => choice).ToList();
            while (choices.Count < RunWeapons.ArmoryOfferCount)
            {
                var source = choices[choices.Count - 1];
                var clone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
                clone.name = "Choice" + (choices.Count + 1);
                Undo.RegisterCreatedObjectUndo(clone, "V4 armory card");
                foreach (var button in clone.GetComponentsInChildren<ArenaUiButton>(true))
                {
                    button.index = choices.Count;
                    EditorUtility.SetDirty(button);
                }

                choices.Add(clone.GetComponent<V2UiChoice>());
            }

            for (int i = 0; i < choices.Count && i < draft.choices.Length; i++)
                CopyLayout(draft.choices[i].transform, choices[i].transform);
            armory.choices = choices.ToArray();
            EditorUtility.SetDirty(armory);
            return armory.choices.Length;
        }

        static void CopyLayout(Transform from, Transform to)
        {
            if (from is RectTransform a && to is RectTransform b)
            {
                b.anchorMin = a.anchorMin;
                b.anchorMax = a.anchorMax;
                b.pivot = a.pivot;
                b.anchoredPosition = a.anchoredPosition;
                b.sizeDelta = a.sizeDelta;
                EditorUtility.SetDirty(b);
            }

            foreach (Transform child in to)
            {
                var match = from.Find(child.name);
                if (match) CopyLayout(match, child);
            }
        }

        static SpriteRenderer Layer(V2BlackHolePresentation hole, SpriteRenderer existing, string name, string sprite)
        {
            var renderer = existing;
            if (!renderer)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, name);
                go.transform.SetParent(hole.core.transform.parent, false);
                go.layer = hole.core.gameObject.layer;
                renderer = go.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = SpriteOf(sprite);
            renderer.sharedMaterial = hole.core.sharedMaterial;
            renderer.sortingLayerID = hole.core.sortingLayerID;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
            return renderer;
        }
    }
}