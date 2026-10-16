using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnomalyArena.EditorTools
{
                 
                                                                                                                            
                                                                                                                      
                  
    public static class InstallV4Interface
    {
        static readonly string[] Scenes =
        {
            "Assets/AnomalyArena/Scenes/ArenaV2.unity", "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity",
            "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity"
        };

        const string TextureFolder = "Assets/AnomalyArena/Art/V4/UI/Textures";
        const string IntroName = "WeaponIntroScreen";
        const string FxName = "CardPickFx";
        const int SparkPool = 24;
                                                                                                                       
        static readonly Vector2 SmallSlot = new(146f, 92f);
        const float Edge = 24f, Gap = 8f, Bottom = 22f;

        [Serializable]
        sealed class Entry
        {
            public string scene;
            public int slotsLaidOut, introTexts, sparks;
            public bool staminaSet, introBound, fxBound;
        }

        [Serializable]
        sealed class Receipt
        {
            public string savedAtUtc, rollbackFolder;
            public int texturesImported;
            public float sprintDuration, staminaCap;
            public Entry[] scenes;
        }

        [MenuItem("Anomaly Arena/V4/Install Interface")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play before installing the V4 interface.");
            var current = SceneManager.GetActiveScene();
            string returnTo = current.path;
            if (current.isDirty)
                throw new InvalidOperationException("The open scene has unsaved edits; save or discard them first.");
            var scenes = Scenes.Where(File.Exists).Distinct().ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No V2 scene found.");
            string backup = "work/rollback/v4-interface-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            foreach (var path in scenes) File.Copy(path, backup + "/" + Path.GetFileName(path), false);

            AssetDatabase.Refresh();
            var receipt = new Receipt
                { savedAtUtc = DateTime.UtcNow.ToString("o"), rollbackFolder = backup, texturesImported = ImportTextures() };
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
            File.WriteAllText("work/authoring-receipts/v4-interface.json", JsonUtility.ToJson(receipt, true));
            if (!string.IsNullOrEmpty(returnTo) && SceneManager.GetActiveScene().path != returnTo && File.Exists(returnTo))
                EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            Debug.Log("[V4Interface] " + JsonUtility.ToJson(receipt));
        }

        static int ImportTextures()
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.sRGBTexture = true;
                                                                                          
                importer.spriteBorder = Path.GetFileNameWithoutExtension(path) == "card-pick-frame"
                    ? new Vector4(64f, 64f, 64f, 64f)
                    : Vector4.zero;
                importer.SaveAndReimport();
                count++;
            }

            return count;
        }

        static Sprite SpriteOf(string id)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/" + id + ".png");
            if (!sprite) throw new InvalidOperationException("Missing sprite: " + id);
            return sprite;
        }

        static void WriteData(Receipt receipt, string backup)
        {
            const string progressionPath = "Assets/AnomalyArena/Data/ProgressionRules.asset";
            File.Copy(progressionPath, backup + "/" + Path.GetFileName(progressionPath), true);
            var rules = AssetDatabase.LoadAssetAtPath<ProgressionRules>(progressionPath);
            if (!rules) throw new InvalidOperationException("Missing asset: " + progressionPath);
            Undo.RecordObject(rules, "V4 sprint");
                                                                                                              
            rules.startingStats.sprintDuration = 1f;
            var caps = rules.startingStats.caps;
            if (caps != null && caps.Length > (int)StatId.Stamina) caps[(int)StatId.Stamina] = 1.75f;
            EditorUtility.SetDirty(rules);
            AssetDatabase.SaveAssets();
            receipt.sprintDuration = rules.startingStats.sprintDuration;
            receipt.staminaCap = caps != null && caps.Length > (int)StatId.Stamina ? caps[(int)StatId.Stamina] : -1f;
        }

        static Entry InstallScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var entry = new Entry { scene = scene.path };
            foreach (var player in roots.SelectMany(root => root.GetComponentsInChildren<PlayerController>(true)))
            {
                Undo.RecordObject(player, "V4 sprint");
                player.stamina.sprintDuration = 1f;
                player.stamina.minToStart = .3f;
                EditorUtility.SetDirty(player);
                entry.staminaSet = true;
            }

            var hud = roots.SelectMany(root => root.GetComponentsInChildren<V2Hud>(true)).FirstOrDefault();
            if (!hud) throw new InvalidOperationException("Saved V2Hud missing in " + scene.path);
            Undo.RecordObject(hud, "V4 interface");
            entry.slotsLaidOut = LayOutBottomRow(hud);
            entry.introTexts = BuildWeaponIntro(hud);
            entry.introBound = hud.screens.Any(screen => screen && screen.state == ArenaUiState.WeaponIntro && screen.body && screen.icon);
            entry.sparks = BuildCardPickFx(hud);
            entry.fxBound = hud.cardPickFx && hud.cardPickFx.frame && hud.cardPickFx.ghostHolder;
            EditorUtility.SetDirty(hud);
            return entry;
        }

                                                                                                          

        static int LayOutBottomRow(V2Hud hud)
        {
            int count = 0;
            for (int i = 0; i < hud.autoSlots.Length; i++)
            {
                if (!hud.autoSlots[i]) continue;
                var rect = (RectTransform)hud.autoSlots[i].transform;
                Undo.RecordObject(rect, "V4 slot layout");
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(Edge + i * (SmallSlot.x + Gap), Bottom);
                LayOutSlot(hud.autoSlots[i], SmallSlot, true);
                count++;
            }

            int skills = hud.skillSlots.Length;
            for (int i = 0; i < skills; i++)
            {
                if (!hud.skillSlots[i]) continue;
                var rect = (RectTransform)hud.skillSlots[i].transform;
                Undo.RecordObject(rect, "V4 slot layout");
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-(Edge + (skills - 1 - i) * (SmallSlot.x + Gap)), Bottom);
                LayOutSlot(hud.skillSlots[i], SmallSlot, true);
                count++;
            }

            foreach (var slot in hud.handSlots)
            {
                if (!slot) continue;
                LayOutSlot(slot, ((RectTransform)slot.transform).sizeDelta, false);
                count++;
            }

            return count;
        }

        static void LayOutSlot(V2UiSlot slot, Vector2 size, bool small)
        {
            const float pad = 10f, top = 6f, titleTop = 29f;
            var rect = (RectTransform)slot.transform;
            Undo.RecordObject(rect, "V4 slot layout");
            rect.sizeDelta = size;
            EditorUtility.SetDirty(rect);
            float w = size.x, h = size.y;
                                                                                            
            float glyphRight = w - pad;
            Place(slot.weaponIcon, glyphRight - 30f, top + 1f, 30f, 18f);
            Place(slot.contentIcon, glyphRight - 22f, top, 22f, 22f);
            float elementX = glyphRight - 30f - 4f - 20f;
            Place(slot.icon, elementX, top, 20f, 20f);
            var keyTransform = slot.transform.Find("SlotKey");
            var key = keyTransform ? keyTransform.GetComponent<Text>() : null;
            Place(key, pad, top, elementX - 4f - pad, 19f);
            Fit(key, 8, 12, TextAnchor.UpperLeft);
                                                                                                           
            float detailHeight = small ? 15f : 17f, detailTop = h - 8f - detailHeight;
            Place(slot.title, pad, titleTop, w - pad * 2f, detailTop - titleTop - 2f);
            Fit(slot.title, small ? 9 : 11, small ? 13 : 16, TextAnchor.UpperLeft);
            Place(slot.detail, pad, detailTop, w - pad * 2f, detailHeight);
            Fit(slot.detail, 8, small ? 11 : 12, TextAnchor.MiddleLeft);
            var bar = slot.cooldown ? slot.cooldown.transform.parent as RectTransform : null;
            if (bar && bar != rect)
            {
                Place(bar, 6f, h - 6f, w - 12f, 3f);
                Place(slot.cooldown, 0f, 0f, w - 12f, 3f);
            }
        }

        static void Place(Component part, float left, float top, float width, float height)
        {
            if (!part || !(part.transform is RectTransform rect)) return;
            Undo.RecordObject(rect, "V4 layout");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            EditorUtility.SetDirty(rect);
        }

        static void Fit(Text text, int min, int max, TextAnchor anchor)
        {
            if (!text) return;
            Undo.RecordObject(text, "V4 text fit");
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = min;
            text.resizeTextMaxSize = max;
            text.fontSize = max;
            EditorUtility.SetDirty(text);
        }

                                                                                                                      

        static int BuildWeaponIntro(V2Hud hud)
        {
            var screens = hud.screens.Where(screen => screen).ToList();
            var intro = screens.FirstOrDefault(screen => screen.state == ArenaUiState.WeaponIntro);
            if (!intro)
            {
                var paused = screens.FirstOrDefault(screen => screen.state == ArenaUiState.Paused);
                if (!paused) throw new InvalidOperationException("Saved pause screen missing; it is the weapon card's template.");
                var copy = UnityEngine.Object.Instantiate(paused.gameObject, paused.transform.parent);
                copy.name = IntroName;
                Undo.RegisterCreatedObjectUndo(copy, "V4 weapon card");
                copy.transform.SetSiblingIndex(paused.transform.GetSiblingIndex() + 1);
                copy.SetActive(false);
                intro = copy.GetComponent<V2UiScreen>();
                screens.Add(intro);
            }

            Undo.RecordObject(intro, "V4 weapon card");
            intro.state = ArenaUiState.WeaponIntro;
            intro.choices = new V2UiChoice[0];
            intro.reroll = null;
            intro.rerollLabel = null;
            intro.startNext = null;
            var content = intro.transform.Find("Content");
            if (!content) throw new InvalidOperationException("Weapon card has no Content panel.");

            var clock = content.Find("BuildClock");
            if (clock) Undo.DestroyObjectImmediate(clock.gameObject);
            intro.clock = null;
            foreach (var name in new[] { "RestartRun", "QuitToTitle" })
            {
                var extra = content.Find(name);
                if (extra) Undo.DestroyObjectImmediate(extra.gameObject);
            }

            var heading = Named<Text>(content, "Heading");
            DropLocalization(heading);
            Place(heading, 184f, 34f, 872f, 66f);
            Fit(heading, 22, 34, TextAnchor.MiddleLeft);
            intro.heading = heading;

            var icon = Named<Image>(content, "WeaponIcon");
            if (!icon)
            {
                var go = new GameObject("WeaponIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "V4 weapon card icon");
                go.transform.SetParent(content, false);
                icon = go.GetComponent<Image>();
            }

            Place(icon, 44f, 30f, 120f, 72f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            EditorUtility.SetDirty(icon);
            intro.icon = icon;

            var body = Named<Text>(content, "Explanation");
            Place(body, 44f, 128f, 1012f, 372f);
            Fit(body, 14, 20, TextAnchor.UpperLeft);
            body.supportRichText = true;
            body.lineSpacing = 1.1f;
            intro.body = body;

            var hint = Named<Text>(content, "ContinueHint", "LoadoutSummary");
            if (!heading || !body || !hint) throw new InvalidOperationException("Weapon card is missing its heading, body or hint text.");
            hint.name = "ContinueHint";
            Place(hint, 44f, 612f, 1012f, 36f);
            Fit(hint, 12, 16, TextAnchor.MiddleCenter);
            hint.color = new Color(.62f, .70f, .80f, 1f);
            intro.summary = hint;

            var buttonTransform = content.Find("ContinueButton");
            if (!buttonTransform) buttonTransform = content.Find("ResumeRun");
            if (!buttonTransform) throw new InvalidOperationException("Weapon card has no button to reuse.");
            buttonTransform.name = "ContinueButton";
            Place(buttonTransform, 330f, 524f, 440f, 65f);
            var action = buttonTransform.GetComponent<ArenaUiButton>();
            Undo.RecordObject(action, "V4 weapon card");
            action.hud = hud;
            action.command = ArenaUiCommand.DismissWeaponIntro;
            action.index = -1;
            EditorUtility.SetDirty(action);
            var label = buttonTransform.GetComponentInChildren<Text>(true);
            DropLocalization(label);
            intro.continueLabel = label;

                                                                                        
            var root = intro.gameObject;
            var backdrop = root.GetComponent<Image>();
            var anywhere = root.GetComponent<Button>();
            if (!anywhere) anywhere = Undo.AddComponent<Button>(root);
            anywhere.transition = Selectable.Transition.None;
            anywhere.targetGraphic = backdrop;
            anywhere.navigation = new Navigation { mode = Navigation.Mode.None };
            var anywhereAction = root.GetComponent<ArenaUiButton>();
            if (!anywhereAction) anywhereAction = Undo.AddComponent<ArenaUiButton>(root);
            anywhereAction.hud = hud;
            anywhereAction.command = ArenaUiCommand.DismissWeaponIntro;
            anywhereAction.index = -1;
            if (anywhere.onClick.GetPersistentEventCount() == 0)
                UnityEventTools.AddPersistentListener(anywhere.onClick, anywhereAction.Invoke);
            EditorUtility.SetDirty(anywhere);
            EditorUtility.SetDirty(anywhereAction);
            EditorUtility.SetDirty(intro);

            var texts = new[] { heading, body, hint, label };
            var fonts = (hud.fontLabels ?? new Text[0]).Where(text => text).ToList();
            foreach (var text in texts) if (!fonts.Contains(text)) fonts.Add(text);
            hud.fontLabels = fonts.ToArray();
            hud.screens = screens.ToArray();
            return texts.Length;
        }

        static T Named<T>(Transform parent, params string[] names) where T : Component
        {
            foreach (var name in names)
            {
                var child = parent.Find(name);
                if (child && child.GetComponent<T>()) return child.GetComponent<T>();
            }

            return null;
        }

                                                                                                                           
        static void DropLocalization(Component part)
        {
            if (!part) return;
            foreach (var behaviour in part.GetComponents<MonoBehaviour>())
                if (behaviour && behaviour.GetType().Name == "LocalizeStringEvent")
                    Undo.DestroyObjectImmediate(behaviour);
        }

                                                                             

        static int BuildCardPickFx(V2Hud hud)
        {
            var fx = hud.cardPickFx ? hud.cardPickFx : hud.GetComponentInChildren<V2CardPickFx>(true);
            if (!fx)
            {
                var go = new GameObject(FxName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "V4 card-pick flourish");
                go.transform.SetParent(hud.transform, false);
                fx = go.AddComponent<V2CardPickFx>();
            }

            Undo.RecordObject(fx, "V4 card-pick flourish");
            var root = (RectTransform)fx.transform;
            Stretch(root);
            root.SetAsLastSibling();
            fx.screenTint = Layer(root, "ScreenTint", null);
            Stretch(fx.screenTint.rectTransform);
            fx.rays = Layer(root, "Rays", SpriteOf("card-pick-rays"));
            fx.ring = Layer(root, "Ring", SpriteOf("card-pick-ring"));
            fx.frame = Layer(root, "Frame", SpriteOf("card-pick-frame"));
            fx.frame.type = Image.Type.Sliced;
            fx.frame.fillCenter = false;
            var spark = SpriteOf("card-pick-spark");
            fx.sparks = Enumerable.Range(0, SparkPool).Select(i => Layer(root, "Spark " + i.ToString("00"), spark)).ToArray();
            var holder = root.Find("GhostHolder") as RectTransform;
            if (!holder)
            {
                var go = new GameObject("GhostHolder", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "V4 card-pick ghost holder");
                go.transform.SetParent(root, false);
                holder = (RectTransform)go.transform;
            }

            Stretch(holder);
            holder.gameObject.SetActive(false);
            holder.SetAsLastSibling();
            fx.ghostHolder = holder;
            int order = 0;
            foreach (var image in new[] { fx.screenTint, fx.rays, fx.ring, fx.frame }.Concat(fx.sparks))
                image.transform.SetSiblingIndex(order++);
            EditorUtility.SetDirty(fx);
            hud.cardPickFx = fx;
            return fx.sparks.Length;
        }

        static Image Layer(RectTransform root, string name, Sprite sprite)
        {
            var child = root.Find(name);
            Image image;
            if (child && child.GetComponent<Image>()) image = child.GetComponent<Image>();
            else
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "V4 card-pick layer");
                go.transform.SetParent(root, false);
                image = go.GetComponent<Image>();
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(64f, 64f);
            }

            Undo.RecordObject(image, "V4 card-pick layer");
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = Color.white;
            image.enabled = false;
            EditorUtility.SetDirty(image);
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            Undo.RecordObject(rect, "V4 layout");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(rect);
        }
    }
}
