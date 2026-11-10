using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// 2026-11-06 V6 enemy models: renders every enemy prefab the way the game frames it (pitch 72 degrees and field of view 40, as CameraRig
    /// and the arena scenes), at its runtime size (V2ModelPresentation: body radius over the reference radius, times 1.35, or 1.1 for the
    /// Tyrant), in the first frame of its idle clip, with two rings on the ground: the gameplay radius (EnemyDefinition.radius, white) and the
    /// prefab's capsule collider (cyan). Per kind it writes four pictures:
    ///   &lt;Kind&gt;.png               as the game shows it at rest;
    ///   &lt;Kind&gt;-windup.png        with the wind-up tint V2ModelPresentation writes into _EmissionColor, on the materials as they are;
    ///   &lt;Kind&gt;-windup-glow.png   the same on throw-away copies of the materials with _EMISSION switched on (what "-bodyGlow on" changes);
    ///   &lt;Kind&gt;-flash-glow.png    the hit flash on those copies.
    /// Nothing is saved but the PNGs: the scene is a new unsaved one, and the prefab instances and material copies are destroyed.
    ///   -executeMethod AnomalyArena.EditorTools.CaptureEnemyModels.CaptureBatch -tag before|after [-kinds Skitter,Tyrant] [-out folder]
    /// </summary>
    public static class CaptureEnemyModels
    {
        const string Tag = "[V6Capture] ";
        const string PrefabFolder = "Assets/AnomalyArena/V2/Prefabs/";
        const string DefinitionFolder = "Assets/AnomalyArena/V2/Data/Enemies/";
        const string DefaultOut = "work/art-plans/v2-1104/models/_captures";
        const int Size = 1024;
        const float Pitch = 72f, FieldOfView = 40f;
        static readonly string[] Kinds = { "Grunt", "Brute", "Skitter", "Spitter", "Rammer", "Bloater", "Warden", "Splitter", "Spire", "Stomper", "Shade", "Tyrant" };
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static void CaptureBatch()
        {
            int code = 0;
            try { Capture(Arg("-tag") ?? "capture", Arg("-kinds"), Arg("-out")); }
            catch (Exception e) { Debug.LogError(Tag + e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static void Capture(string tag, string kinds, string outRoot) => Capture(tag, kinds, outRoot, -1f, false);

        /// <param name="brightness">above 0: render every model at this V2ModelPresentation.modelBrightness instead of its own value.</param>
        /// <param name="gameLight">light the pictures like the arena: the scene's own directional light when one is loaded (the open
        /// arena scene in the editor), else one with the arena's colour and intensity; the biome's flat ambient and a near-black floor.</param>
        public static void Capture(string tag, string kinds, string outRoot, float brightness, bool gameLight)
        {
            Brightness = brightness;
            GameLight = gameLight;
            var list = string.IsNullOrWhiteSpace(kinds) ? Kinds : kinds.Split(',').Select(k => k.Trim()).Where(k => k.Length > 0).ToArray();
            string folder = Path.Combine(string.IsNullOrWhiteSpace(outRoot) ? DefaultOut : outRoot, tag);
            Directory.CreateDirectory(folder);
            bool batch = Application.isBatchMode;
            var previous = SceneManager.GetActiveScene();
            Scene scene;
            if (batch) scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else
            {
                // In an open editor: an extra empty scene, far from the arena and closed again without saving, so the
                // scene that is open there is not touched (its lights may still add a little to these pictures).
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
            }
            Origin = batch ? Vector3.zero : new Vector3(20000f, 0f, 20000f);
            try { CaptureIn(tag, list, folder); }
            finally
            {
                if (!batch)
                {
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        static Vector3 Origin;
        static float Brightness = -1f;
        static bool GameLight;
        /// <summary>Optional lighting trial on top of gameLight: the scene light's colour (alpha unused) and intensity, and the ambient,
        /// used for the pictures only; the scene light gets its own values back afterwards and nothing is saved.</summary>
        public static Color? TrialLightColor, TrialAmbient;
        public static float TrialLightIntensity = -1f;
        // The arena as saved in ArenaV2.unity (2026-11-06): one directional light, cold blue at 1.2, and the biome's flat ambient.
        static readonly Color ArenaLight = new Color(.72f, .84f, 1f), ArenaAmbient = new Color(.19f, .24f, .35f), ArenaFloor = new Color(.085f, .09f, .105f);

        static void CaptureIn(string tag, string[] list, string folder)
        {
            Light scene = null;
            if (GameLight)
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (light.type == LightType.Directional && light.enabled) { scene = light; break; }
            if (!scene)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                sun.intensity = GameLight ? 1.2f : 1.25f;
                sun.color = GameLight ? ArenaLight : new Color(1f, .96f, .9f);
                sun.shadows = LightShadows.Soft;
                if (GameLight) scene = sun;
            }
            Color keptColor = scene ? scene.color : Color.white;
            float keptIntensity = scene ? scene.intensity : 1f;
            if (GameLight && scene && TrialLightColor.HasValue) scene.color = TrialLightColor.Value;
            if (GameLight && scene && TrialLightIntensity > 0f) scene.intensity = TrialLightIntensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = GameLight ? TrialAmbient ?? ArenaAmbient : new Color(.42f, .44f, .48f);
            try { CaptureLit(tag, list, folder); }
            finally
            {
                if (scene) { scene.color = keptColor; scene.intensity = keptIntensity; }
            }
        }

        static void CaptureLit(string tag, string[] list, string folder)
        {

            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Ground";
            ground.transform.SetPositionAndRotation(Origin, Quaternion.Euler(90f, 0f, 0f));
            ground.transform.localScale = Vector3.one * 80f;
            var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMaterial.SetColor(BaseColorId, GameLight ? ArenaFloor : new Color(.27f, .28f, .31f));
            groundMaterial.SetFloat("_Smoothness", 0f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.fieldOfView = FieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.13f, .14f, .16f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 600f;
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();

            int failed = 0;
            foreach (var kind in list)
            {
                try { CaptureKind(kind, tag, folder, camera, target); }
                catch (Exception e) { failed++; Debug.LogError($"{Tag}{kind}: {e}"); }
            }
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(groundMaterial);
            Debug.Log($"{Tag}{tag}: {list.Length - failed} of {list.Length} kinds written to {Path.GetFullPath(folder)}");
            if (failed > 0) throw new Exception($"{failed} kind(s) failed");
        }

        static void CaptureKind(string kind, string tag, string folder, Camera camera, RenderTexture target)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + kind + ".prefab");
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionFolder + kind + ".asset");
            if (!prefab || !definition) throw new Exception($"missing {PrefabFolder + kind}.prefab or {DefinitionFolder + kind}.asset");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var made = new List<Object>();
            try
            {
                instance.transform.SetPositionAndRotation(Origin, Quaternion.identity);
                var presentation = instance.GetComponent<V2ModelPresentation>();
                if (!presentation || !presentation.modelRoot) throw new Exception("no V2ModelPresentation with a model root");
                var model = presentation.modelRoot;
                // Only the 3D model: whitebox shapes and other art the arena switches off stay hidden.
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    if (!renderer.transform.IsChildOf(model)) renderer.enabled = false;
                float runtime = (presentation.scaleWithBodyRadius ? definition.radius / Mathf.Max(.01f, presentation.referenceBodyRadius) : 1f) *
                                (presentation.presentationScale > 0f ? presentation.presentationScale : kind == "Tyrant" ? 1.1f : 1.35f);
                model.localPosition = presentation.authoredPosition;
                model.localScale = presentation.authoredScale * runtime;
                if (presentation.idleClip && presentation.animator) presentation.idleClip.SampleAnimation(presentation.animator.gameObject, 0f);

                var capsule = instance.GetComponent<CapsuleCollider>();
                made.Add(Ring("Gameplay radius", Origin, definition.radius, new Color(1f, 1f, 1f), .035f, made));
                if (capsule)
                {
                    var centre = instance.transform.TransformPoint(capsule.center);
                    float scale = Mathf.Max(instance.transform.lossyScale.x, instance.transform.lossyScale.z);
                    made.Add(Ring("Capsule collider", new Vector3(centre.x, 0f, centre.z), capsule.radius * scale, new Color(.2f, .9f, 1f), .045f, made));
                }

                // The same framing before and after, so the two pictures of a kind compare directly.
                float half = Mathf.Max(1.8f, definition.radius * 4.5f);
                var look = Origin + new Vector3(0f, Mathf.Min(1.2f, definition.radius), 0f);
                camera.transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
                camera.transform.position = look - camera.transform.forward * (half / Mathf.Tan(FieldOfView * .5f * Mathf.Deg2Rad));

                var renderers = presentation.modelRenderers != null && presentation.modelRenderers.Length > 0
                    ? presentation.modelRenderers.Where(r => r).ToArray()
                    : model.GetComponentsInChildren<Renderer>(true);
                // As V2ModelPresentation does in play: the base colour is its runtime tint (white) times the model brightness.
                float bright = Brightness > 0f ? Brightness : presentation.modelBrightness;
                var block = new MaterialPropertyBlock();
                foreach (var renderer in renderers)
                {
                    renderer.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, new Color(bright, bright, bright, 1f));
                    renderer.SetPropertyBlock(block);
                }
                SetEmission(renderers, presentation.idleEmission);
                Render(camera, target, Path.Combine(folder, kind + ".png"));
                SetEmission(renderers, presentation.idleEmission + presentation.windupEmission);
                Render(camera, target, Path.Combine(folder, kind + "-windup.png"));

                var originals = renderers.Select(r => r.sharedMaterials).ToArray();
                bool keyword = renderers.SelectMany(r => r.sharedMaterials).Any(m => m && m.IsKeywordEnabled("_EMISSION"));
                foreach (var renderer in renderers)
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                    {
                        if (!m) return m;
                        var copy = new Material(m);
                        copy.EnableKeyword("_EMISSION");
                        copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        made.Add(copy);
                        return copy;
                    }).ToArray();
                SetEmission(renderers, presentation.idleEmission + presentation.windupEmission);
                Render(camera, target, Path.Combine(folder, kind + "-windup-glow.png"));
                SetEmission(renderers, presentation.idleEmission + presentation.flashEmission);
                Render(camera, target, Path.Combine(folder, kind + "-flash-glow.png"));
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = originals[i];

                var size = Bounds(renderers);
                Debug.Log($"{Tag}{kind} ({tag}): model {model.name}, runtime scale x{runtime:0.###} on authored {presentation.authoredScale}, " +
                          $"visible size {size.size.x:0.##} x {size.size.y:0.##} x {size.size.z:0.##} m at ({size.center.x:0.##}, {size.min.y:0.##}, {size.center.z:0.##}), " +
                          $"gameplay radius {definition.radius:0.##}, capsule {(capsule ? capsule.radius.ToString("0.##") : "none")}, materials _EMISSION {(keyword ? "on" : "off")}");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                foreach (var thing in made) if (thing) Object.DestroyImmediate(thing);
            }
        }

        static Bounds Bounds(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static void SetEmission(Renderer[] renderers, Color colour)
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetColor(EmissionId, colour);
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>A flat ring on the ground, drawn unlit so it reads the same under any light.</summary>
        static GameObject Ring(string name, Vector3 centre, float radius, Color colour, float width, List<Object> made)
        {
            const int segments = 128;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * (radius - width * .5f);
                vertices[i * 2 + 1] = direction * (radius + width * .5f);
                int next = (i + 1) % segments, t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next * 2; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1; triangles[t + 4] = next * 2; triangles[t + 5] = next * 2 + 1;
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor(BaseColorId, colour);
            made.Add(mesh);
            made.Add(material);
            var ring = new GameObject(name);
            ring.transform.position = centre + Vector3.up * .03f;
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ring;
        }

        static void Render(Camera camera, RenderTexture target, string path)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else
            {
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            picture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
