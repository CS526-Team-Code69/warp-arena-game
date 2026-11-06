using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AnomalyArena.EditorTools
{
                 
                                                                                                                                             
                                                                                                                                       
       
                                                                                                                                           
                                                                                                                                                     
                                                                                                                       
                                                                                                                                                    
                                                                                                                                                   
                                                                                                                                         
                                                                                                                                                          
                                                                                                                                              
                                                                                                                                                          
                                                                                                                                                                
                                                                                                                                                         
       
                                                                                                                                  
                                                                                                                                                           
                                                                                                                                                  
                                                                                                                                                    
                                                                                                                                              
                                                                                                                                               
                                                                                                                                           
       
                                                                                       
                                                                                                                                                           
                                                                                   
                                                                                               
                                                                                                                  
                  
    public static class InstallV6EnemyModels
    {
        const string Tag = "[V6Install] ";
        const string PrefabFolder = "Assets/AnomalyArena/V2/Prefabs/";
        const string V6SourceFolder = "Assets/AnomalyArena/Art/V6/Enemies/";
        const string V6ModelFolder = "Assets/AnomalyArena/Art/V6/EnemyModels/Generated/";
        const string SceneFolder = "Assets/AnomalyArena/Scenes";
        const string PrefabSearchFolder = "Assets/AnomalyArena";
                                                                                           
        const string DefaultSourceRoot = "work/art-plans/v2-1104/models";
        const string OldSuffix = "_V3", NewSuffix = "_V6";
        const string RigGeneric = "blender-generic", RigHumanoid = "rigged-humanoid", RigNone = "none";
        const string LitShader = "Universal Render Pipeline/Lit";
                                                                                                                                               
        const int TriangleWarning = 50000;
                                                                                                                
        const float FacingAspect = 1.3f;
                                                                                                                             
        const int MapSize = 2048;
        static readonly string[] Kinds = { "Grunt", "Brute", "Skitter", "Spitter", "Rammer", "Bloater", "Warden", "Splitter", "Spire", "Stomper", "Shade", "Tyrant" };
                                                                                                                                   
        const string PlayerKind = "PlayerV2";
        const string PlayerSourceFolder = "Assets/AnomalyArena/Art/V6/Player/";
        const string PlayerModelFolder = "Assets/AnomalyArena/Art/V6/PlayerModels/Generated/";
                                                                                                                                          
        static readonly string[] DefaultLoops = { "Idle", "Run", "Walk", "Sprint" };
                                                                                                                         
        static readonly string[] GaitClips = { "Run", "Walk", "Sprint" };
        const string RunSpeedParameter = "RunSpeed";
                                                                                            
        const float PlayerPresentationScale = 1.3f;
        const string PlayerStashFile = "explorer-presentation.json";
                                                                                                                                          
        static readonly string[] ClipNames = { "Idle", "Run", "Attack" };
        static readonly string[] ClipFields = { "idleClip", "runClip", "attackClip" };
                                                                                                                    
        static readonly string[] PresentationFields = { "modelRoot", "animator", "idleClip", "runClip", "attackClip", "modelRenderers", "authoredScale", "authoredPosition" };
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        enum Fit { Balanced, Height, Footprint }

        sealed class Options
        {
            public string SourceRoot;
            public Fit DefaultFit = Fit.Balanced;
            public readonly Dictionary<string, Fit> FitByKind = new Dictionary<string, Fit>(StringComparer.OrdinalIgnoreCase);
            public bool? BodyGlow;                                                                                           
            public Fit FitFor(string kind) => FitByKind.TryGetValue(kind, out var fit) ? fit : DefaultFit;
        }

                                                                                                                 
        [Serializable]
        sealed class Manifest
        {
            public string kind, source_task_id, rig, fbx_axis_note, notes;
            public int triangles;
            public float height;
            public float[] bounds_min, bounds_max;
            public string[] clips;
                                                                                                                        
            public string model_file;
            public string[] clip_files;
                                                                                                                              
            public string[] extra_clips, loop_clips;
        }

        sealed class Job
        {
            public string Kind, Rig, SourceDir, AssetDir, ModelDir, PrefabPath, ModelFile;
            public Manifest Manifest;
            public string[] Textures;
            public string[] ClipFiles;                                                                                    
            public string[] Clips = ClipNames;                                                          
            public bool Player => Kind == PlayerKind;
            public bool Loops(string clip) => Manifest?.loop_clips != null && Manifest.loop_clips.Length > 0 ? Manifest.loop_clips.Contains(clip) : DefaultLoops.Contains(clip);
        }

                                                                                                                                                
        [Serializable]
        sealed class PlayerStash
        {
            public float runReferenceSpeed;
            public bool legsFollowMovement, playerAttackMotion;
            public Color illustratedTint;                                                                
        }

                                                                                                                                                                
        sealed class OldModel
        {
            public string ControllerPath;
            public Material Material;
            public string MotionNode = "";
        }

                                   

        [MenuItem("Anomaly Arena/V6/Install Enemy Models")]
        static void InstallMenu() => Install();

        [MenuItem("Anomaly Arena/V6/Revert Enemy Models To V3")]
        static void RevertMenu() => Revert();

        [MenuItem("Anomaly Arena/V6/Body Glow On (V6 Materials)")]
        static void BodyGlowOnMenu() => SetBodyGlow(true);

        [MenuItem("Anomaly Arena/V6/Body Glow Off (V6 Materials)")]
        static void BodyGlowOffMenu() => SetBodyGlow(false);

                                                                                                                                               
        public static void InstallBatch() => Install(Arg("-kinds"), Arg("-fit"), Arg("-bodyGlow"), Arg("-source"));

                                                                                                                 
        public static void RevertBatch() => Revert(Arg("-kinds"));

                                                                                                                                           
        public static void BodyGlowBatch()
        {
            string value = Arg("-bodyGlow");
            Require(!string.IsNullOrWhiteSpace(value), "BodyGlowBatch needs -bodyGlow on or -bodyGlow off");
            SetBodyGlow(ParseSwitch(value, "-bodyGlow"), Arg("-kinds"));
        }

                                                                                                                                         
                                                                      
                                                                                                                                                     
                                                                                                          
                                                                                                                                                                    
                                                                                                                                    
        public static void Install(string kinds = null, string fit = null, string bodyGlow = null, string source = null)
        {
            var list = ParseKinds(kinds);
            var options = new Options
            {
                SourceRoot = ResolveSourceRoot(source),
                BodyGlow = string.IsNullOrWhiteSpace(bodyGlow) ? (bool?)null : ParseSwitch(bodyGlow, "-bodyGlow")
            };
            ParseFit(fit, options);
            var scenes = new SceneOverrides();
            Run("install", list, kind => InstallKind(kind, options, scenes));
        }

                                                                                                                                                     
        public static void Revert(string kinds = null) => Run("revert", ParseKinds(kinds), RevertKind);

                     
                                                                                                                                                     
                                                                                                                                                  
                                                                                                   
                      
        public static void SetBodyGlow(bool on, string kinds = null) => Run(on ? "body glow on" : "body glow off", ParseKinds(kinds), kind =>
        {
            string path = (kind == PlayerKind ? PlayerModelFolder + kind : V6ModelFolder + "Enemy_" + kind) + "/PBR_0.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) return "skipped: no V6 material yet (" + path + ")";
            ApplyBodyGlow(material, on);
            EditorUtility.SetDirty(material);
            return $"{path}: _EMISSION {(on ? "on" : "off")}";
        });

                                                                                                                                                                               
        static void Run(string action, List<string> kinds, Func<string, string> step)
        {
            Require(!EditorApplication.isPlaying, "stop Play first");
            Require(!EditorApplication.isCompiling, "wait until the scripts have compiled");
            Log($"{action}: {string.Join(", ", kinds)}");
            var done = new List<string>();
            var failed = new List<string>();
            try
            {
                for (int i = 0; i < kinds.Count; i++)
                {
                    string kind = kinds[i];
                    if (!Application.isBatchMode) EditorUtility.DisplayProgressBar("V6 enemy models", $"{action}: {kind}", (float)i / kinds.Count);
                    try
                    {
                        string result = step(kind);
                        done.Add($"{kind}: {result}");
                        Log($"{kind}: {result}");
                    }
                    catch (Exception e)
                    {
                        failed.Add($"{kind}: {e.Message}");
                        Debug.LogError($"{Tag}{kind} FAILED: {e}");
                    }
                }
            }
            finally
            {
                if (!Application.isBatchMode) EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }
            Log($"{action} finished: {done.Count} done or skipped, {failed.Count} failed.\n  " + string.Join("\n  ", done.Concat(failed.Select(f => "FAILED " + f))));
            if (failed.Count > 0) throw Fail($"{action} failed for {failed.Count} kind(s): {string.Join(" | ", failed)}");
        }

                              

        static string InstallKind(string kind, Options options, SceneOverrides scenes)
        {
            string prefabPath = PrefabFolder + kind + ".prefab";
            Require(File.Exists(prefabPath), $"missing {prefabPath}");
            var job = ReadManifest(kind, options.SourceRoot);
            if (job.Rig == RigNone)
                return "skipped: the manifest's rig is \"none\" (this humanoid has no rig yet); nothing changed";
            job.PrefabPath = prefabPath;
            var old = ReadOldModel(prefabPath, kind);
            CopySources(job);
            var clips = job.Rig == RigGeneric ? ImportGeneric(job) : ImportHumanoid(job, old.MotionNode);
            var material = BuildMaterial(job, old.Material, options.BodyGlow);
            var controller = BuildController(job, clips, old.ControllerPath);
            AssetDatabase.SaveAssets();
            var model = BuildModelPrefab(job, material, controller, clips[0], out Bounds unit);
                                                                                                                            
            var fit = job.Player && !options.FitByKind.ContainsKey(kind) ? Fit.Height : options.FitFor(kind);
            string fitted = SwapIntoEnemyPrefab(job, model, unit, clips, fit, out string baseName);
            FollowInVariants(prefabPath, kind, baseName, installed: true);
            scenes.Warn(prefabPath, kind);
            return "installed, " + fitted;
        }

        static Job ReadManifest(string kind, string sourceRoot)
        {
            string dir = Path.Combine(sourceRoot, kind).Replace('\\', '/');
            string file = dir + "/manifest.json";
            Require(File.Exists(file), $"missing {file}: the Blender step has not delivered {kind}");
            Manifest manifest;
            try { manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(file)); }
            catch (Exception e) { throw Fail($"{file} is not readable JSON: {e.Message}"); }
            Require(manifest != null, $"{file} is empty");
            Require(string.Equals(manifest.kind, kind, StringComparison.OrdinalIgnoreCase), $"{file} names the kind '{manifest.kind}', not {kind}");
            string rig = (manifest.rig ?? "").Trim().ToLowerInvariant();
            Require(rig == RigGeneric || rig == RigHumanoid || rig == RigNone, $"{file}: unknown rig '{manifest.rig}' (expected {RigGeneric}, {RigHumanoid} or {RigNone})");
            Log($"{kind}: manifest rig {rig}, {manifest.triangles:N0} triangles, height {manifest.height:0.###}, clips [{Join(manifest.clips)}], source task {manifest.source_task_id}");
            if (!string.IsNullOrWhiteSpace(manifest.fbx_axis_note)) Log($"{kind}: axis note: {manifest.fbx_axis_note}");
            if (!string.IsNullOrWhiteSpace(manifest.notes)) Log($"{kind}: notes: {manifest.notes}");
            bool player = kind == PlayerKind;
            var job = new Job
            {
                Kind = kind, Rig = rig, Manifest = manifest, SourceDir = dir,
                AssetDir = (player ? PlayerSourceFolder : V6SourceFolder) + kind + "/",
                ModelDir = player ? PlayerModelFolder + kind + "/" : V6ModelFolder + "Enemy_" + kind + "/"
            };
            if (rig == RigNone) return job;
            Require(manifest.clips != null && ClipNames.All(c => manifest.clips.Contains(c)), $"{file}: clips [{Join(manifest.clips)}] must include {string.Join(", ", ClipNames)}");
            if (manifest.extra_clips != null && manifest.extra_clips.Length > 0)
            {
                Require(rig == RigGeneric, $"{file}: extra_clips need rig {RigGeneric} (one FBX holding every take)");
                Require(manifest.extra_clips.All(c => !string.IsNullOrWhiteSpace(c) && !ClipNames.Contains(c)), $"{file}: extra_clips [{Join(manifest.extra_clips)}] repeat a main clip or are empty");
                job.Clips = ClipNames.Concat(manifest.extra_clips.Select(c => c.Trim())).Distinct().ToArray();
                Log($"{kind}: extra clips [{Join(manifest.extra_clips)}], looping [{string.Join(", ", job.Clips.Where(job.Loops))}]");
            }
            if (player) Require(job.Clips.Contains("Run"), $"{file}: the player needs a Run clip");
            if (manifest.triangles > TriangleWarning)
                Warn($"{kind}: {manifest.triangles:N0} triangles; the web build pays for every one (T-014), the V3 enemies were about 2.6k and the web slim cuts models to 20k");
            job.Textures = new[] { "_BaseColor", "_Normal", "_Metallic", "_Roughness" }.Select(s => kind + s + ".png").ToArray();
            if (rig == RigGeneric)
            {
                job.ModelFile = SafeRelative(manifest.model_file, file) ?? kind + ".fbx";
                return job;
            }
                                                                                                                                                             
            job.ModelFile = SafeRelative(manifest.model_file, file) ?? FirstExisting(dir, kind + ".fbx", kind + "-rigged.fbx");
            job.ClipFiles = new string[ClipNames.Length];
            for (int i = 0; i < ClipNames.Length; i++)
            {
                int listed = Array.IndexOf(manifest.clips, ClipNames[i]);
                string named = manifest.clip_files != null && listed >= 0 && listed < manifest.clip_files.Length ? SafeRelative(manifest.clip_files[listed], file) : null;
                job.ClipFiles[i] = named ?? FirstExisting(dir, $"Animations/{ClipNames[i]}.fbx", ClipNames[i] == "Attack" ? "Animations/Action.fbx" : null);
            }
            return job;
        }

        static OldModel ReadOldModel(string prefabPath, string kind)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(asset, $"cannot load {prefabPath}");
            var presentation = asset.GetComponent<V2ModelPresentation>();
            Require(presentation, $"{prefabPath} has no {nameof(V2ModelPresentation)}");
            var shown = presentation.modelRoot;
            Require(shown && shown.parent, $"{prefabPath}: {nameof(V2ModelPresentation)}.modelRoot is empty");
            var oldRoot = IsNew(shown) ? shown.parent.Find(BaseNameOf(shown) + OldSuffix) : shown;
            Require(oldRoot, $"{prefabPath}: {shown.name} is shown but {BaseNameOf(shown)}{OldSuffix} is missing");
            var animator = oldRoot.GetComponentInChildren<Animator>(true);
            Require(animator && animator.runtimeAnimatorController is AnimatorController, $"{prefabPath}: the V3 model {oldRoot.name} has no Animator controller");
            var info = new OldModel { ControllerPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) };
            var renderer = ModelRenderers(oldRoot).FirstOrDefault();
            info.Material = renderer ? renderer.sharedMaterial : null;
            var idle = ClipsOf(animator, prefabPath)[0];
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(idle)) is ModelImporter clipImporter) info.MotionNode = clipImporter.motionNodeName ?? "";
            Log($"{kind}: V3 model {oldRoot.name}: controller {info.ControllerPath}, material {(info.Material ? AssetDatabase.GetAssetPath(info.Material) : "none")}, clip root node '{info.MotionNode}'");
            return info;
        }

        static void CopySources(Job job)
        {
            EnsureFolder(job.AssetDir);
                                                                                                                                 
                                                                                                                 
            var files = new List<string> { job.ModelFile };
            files.AddRange(job.Textures);
            if (job.ClipFiles != null) files.AddRange(job.ClipFiles);
                                                                                                                 
            foreach (string relative in files.Distinct()) CopyOne(job, relative);
        }

        static void CopyOne(Job job, string relative)
        {
            string from = job.SourceDir + "/" + relative;
            Require(File.Exists(from), $"{job.Kind}: missing {from}");
            string to = job.AssetDir + relative;
            EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
            bool same = File.Exists(to) && SameBytes(from, to);
            if (!same) File.Copy(from, to, true);
            if (!same || !AssetImporter.GetAtPath(to)) AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
            Log($"{job.Kind}: {(same ? "unchanged" : "copied")} {to} ({new FileInfo(to).Length / 1048576.0:0.0} MB)");
        }

                                   

                                                                                                                                    
        static AnimationClip[] ImportGeneric(Job job)
        {
            string path = job.AssetDir + job.ModelFile;
            var importer = ModelImporterAt(path);
                                                                                                                                             
            ApplyModelSettings(importer, null, "", importAnimation: true);
            importer.SaveAndReimport();                                                
            var takes = importer.defaultClipAnimations ?? Array.Empty<ModelImporterClipAnimation>();
            Log($"{job.Kind}: {path} takes [{string.Join(", ", takes.Select(t => $"{t.takeName} {t.firstFrame:0}-{t.lastFrame:0}"))}]");
            importer.clipAnimations = job.Clips.Select(name => ClipFrom(FindTake(takes, name, path, allowSingle: false), name, job.Loops(name))).ToArray();
            importer.SaveAndReimport();
            RequireAvatar(path, job.Kind);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return job.Clips.Select(name =>
            {
                var clip = LoadClip(path, name, job.Kind, job.Loops(name));
                CheckBindings(clip, model, job.Kind);
                return clip;
            }).ToArray();
        }

                     
                                                                                                                                                    
                                                                                                                                                          
                      
        static AnimationClip[] ImportHumanoid(Job job, string v3MotionNode)
        {
            string modelPath = job.AssetDir + job.ModelFile;
            var rig = ModelImporterAt(modelPath);
            ApplyModelSettings(rig, null, v3MotionNode, importAnimation: false);
            rig.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            string motionNode = v3MotionNode;
            if (motionNode.Length > 0 && !model.transform.Find(motionNode))
            {
                Warn($"{job.Kind}: the new rig has no {motionNode} (the V3 clips' root node); clips import without one");
                motionNode = "";
                rig.motionNodeName = "";
                rig.SaveAndReimport();
            }
            var avatar = RequireAvatar(modelPath, job.Kind);
            var clips = new AnimationClip[ClipNames.Length];
            for (int i = 0; i < ClipNames.Length; i++)
            {
                string path = job.AssetDir + job.ClipFiles[i];
                var importer = ModelImporterAt(path);
                ApplyModelSettings(importer, avatar, motionNode, importAnimation: true);
                importer.SaveAndReimport();
                var takes = importer.defaultClipAnimations ?? Array.Empty<ModelImporterClipAnimation>();
                Log($"{job.Kind}: {path} takes [{string.Join(", ", takes.Select(t => t.takeName))}]");
                importer.clipAnimations = new[] { ClipFrom(FindTake(takes, ClipNames[i], path, allowSingle: true), ClipNames[i], job.Loops(ClipNames[i])) };
                importer.SaveAndReimport();
                clips[i] = LoadClip(path, ClipNames[i], job.Kind, job.Loops(ClipNames[i]));
                CheckBindings(clips[i], model, job.Kind);
            }
            return clips;
        }

        static void ApplyModelSettings(ModelImporter importer, Avatar copyFrom, string motionNode, bool importAnimation)
        {
                                                                                                                                                 
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
                                                                                                                                        
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            if (copyFrom)
            {
                importer.sourceAvatar = copyFrom;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            }
            else importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName = motionNode ?? "";
            importer.importAnimation = importAnimation;
                                                                                                             
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = false;
            importer.optimizeGameObjects = false;
        }

                                                                                                                                                                                          
        static ModelImporterClipAnimation FindTake(ModelImporterClipAnimation[] takes, string clip, string where, bool allowSingle)
        {
            Require(takes != null && takes.Length > 0, $"{where} has no animation takes");
            var matches = takes.Where(t => string.Equals(t.takeName, clip, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
                matches = takes.Where(t => t.takeName.Split('|').Any(part => string.Equals(part.Trim(), clip, StringComparison.OrdinalIgnoreCase))).ToList();
            Require(matches.Count <= 1, $"{where}: several takes match {clip}: {string.Join(", ", matches.Select(t => t.takeName))}");
            if (matches.Count == 1) return matches[0];
            if (allowSingle && takes.Length == 1) return takes[0];
            throw Fail($"{where}: no take for {clip}; the takes are [{string.Join(", ", takes.Select(t => t.takeName))}]");
        }

                                                                                                                                                                
                                                                                                                        
        static ModelImporterClipAnimation ClipFrom(ModelImporterClipAnimation take, string name, bool loop)
        {
            take.name = name;
            take.loopTime = loop;
            take.loopPose = loop;
            take.cycleOffset = 0f;
            take.mirror = false;
            take.lockRootRotation = false;
            take.lockRootHeightY = true;
            take.lockRootPositionXZ = false;
            take.keepOriginalOrientation = true;
            take.keepOriginalPositionY = true;
            take.keepOriginalPositionXZ = true;
            take.wrapMode = WrapMode.Default;
            return take;
        }

        static AnimationClip LoadClip(string path, string name, string kind, bool loop)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
            Require(clip, $"{kind}: {path} has no clip {name} after import");
            int curves = AnimationUtility.GetCurveBindings(clip).Length;
            Require(curves > 0, $"{kind}: clip {name} in {path} has no curves");
            if (Mathf.Abs(clip.frameRate - 30f) > .01f) Warn($"{kind}: clip {name} runs at {clip.frameRate:0.##} fps; the contract says 30");
            if (clip.isLooping != loop) Warn($"{kind}: clip {name} loop={clip.isLooping}, expected {loop}");
            Log($"{kind}: clip {name}: {clip.length:0.00} s at {clip.frameRate:0.##} fps, loop {clip.isLooping}, {curves} curves");
            return clip;
        }

                                                                                                                                                       
        static void CheckBindings(AnimationClip clip, GameObject model, string kind)
        {
            var paths = AnimationUtility.GetCurveBindings(clip).Select(b => b.path).Distinct().ToList();
            var missing = paths.Where(p => p.Length > 0 && !model.transform.Find(p)).ToList();
            if (missing.Count == 0) return;
            Require(missing.Count < paths.Count, $"{kind}: none of clip {clip.name}'s paths exist in {model.name}, for example {missing[0]}");
            Warn($"{kind}: clip {clip.name} animates {missing.Count} of {paths.Count} paths {model.name} lacks, for example {string.Join(", ", missing.Take(3))}");
        }

        static Avatar RequireAvatar(string path, string kind)
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            Require(avatar && avatar.isValid, $"{kind}: {path} produced no valid Generic avatar (is the mesh skinned to an armature?)");
            return avatar;
        }

                                                         

        static Material BuildMaterial(Job job, Material v3Template, bool? bodyGlow)
        {
            EnsureFolder(job.ModelDir);
            var baseMap = ImportTexture(job.AssetDir + job.Kind + "_BaseColor.png", srgb: true);
            var normalMap = ImportTexture(job.AssetDir + job.Kind + "_Normal.png", srgb: false, normal: true);
            var metallicSmoothness = BuildMetallicSmoothness(job);
            string path = job.ModelDir + "PBR_0.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                                                                                                                                                     
                                                                                                                                                      
                if (v3Template && v3Template.shader && v3Template.shader.name == LitShader) material = new Material(v3Template) { name = "PBR_0" };
                else
                {
                    var shader = Shader.Find(LitShader);
                    Require(shader, $"shader {LitShader} not found");
                    material = new Material(shader) { name = "PBR_0" };
                    material.SetFloat("_WorkflowMode", 1f);
                    material.SetColor(EmissionColor, Color.black);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    Warn($"{job.Kind}: no URP Lit V3 material to copy; built a plain URP Lit one");
                }
                AssetDatabase.CreateAsset(material, path);
                Log($"{job.Kind}: created {path} from {(v3Template ? AssetDatabase.GetAssetPath(v3Template) : LitShader)}");
            }
            Require(material.shader && material.shader.name == LitShader, $"{path} uses {(material.shader ? material.shader.name : "no shader")}, not {LitShader}");
            SetTexture(material, "_BaseMap", baseMap);
            SetTexture(material, "_MainTex", baseMap);
            SetTexture(material, "_BumpMap", normalMap);
            SetTexture(material, "_MetallicGlossMap", metallicSmoothness);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
                                                                                                                
            if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", null);
            Require(material.HasProperty(EmissionColor) && material.HasProperty("_BaseColor"), $"{path} lacks _EmissionColor or _BaseColor, which V2ModelPresentation drives");
            if (bodyGlow.HasValue) ApplyBodyGlow(material, bodyGlow.Value);
            EditorUtility.SetDirty(material);
            Log($"{job.Kind}: {path}: base {baseMap.width}px, normal {normalMap.width}px, metallic-smoothness {metallicSmoothness.width}px, _EMISSION {(material.IsKeywordEnabled("_EMISSION") ? "on" : "off")}");
            return material;
        }

        static void ApplyBodyGlow(Material material, bool on)
        {
            material.SetColor(EmissionColor, Color.black);
            if (on)
            {
                material.EnableKeyword("_EMISSION");
                                                                                                                                                     
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        static void SetTexture(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property)) material.SetTexture(property, texture);
        }

        static Texture2D ImportTexture(string path, bool srgb, bool normal = false, bool alphaFromInput = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer, $"{path} did not import as a texture");
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            var alpha = alphaFromInput ? TextureImporterAlphaSource.FromInput : importer.alphaSource;
            if (importer.textureType != type || importer.sRGBTexture != srgb || !importer.mipmapEnabled || importer.maxTextureSize != MapSize
                || importer.alphaSource != alpha || importer.alphaIsTransparency)
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = MapSize;
                importer.alphaSource = alpha;
                importer.alphaIsTransparency = false;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(texture, $"{path} did not import");
            if (texture.width != MapSize || texture.height != MapSize) Warn($"{path} is {texture.width}x{texture.height}; the contract says {MapSize}");
            return texture;
        }

                                                                                                                                               
        static Texture2D BuildMetallicSmoothness(Job job)
        {
            string metallicPath = job.AssetDir + job.Kind + "_Metallic.png", roughnessPath = job.AssetDir + job.Kind + "_Roughness.png";
            string path = job.ModelDir + "MetallicSmoothness_0.png";
            bool fresh = File.Exists(path) && File.GetLastWriteTimeUtc(path) >= File.GetLastWriteTimeUtc(metallicPath) && File.GetLastWriteTimeUtc(path) >= File.GetLastWriteTimeUtc(roughnessPath);
            if (!fresh)
            {
                var metallic = ReadPixels(metallicPath, out int width, out int height);
                var roughness = ReadPixels(roughnessPath, out int roughWidth, out int roughHeight);
                Require(width == roughWidth && height == roughHeight, $"{job.Kind}: metallic {width}x{height} and roughness {roughWidth}x{roughHeight} differ in size");
                var packed = new Color32[metallic.Length];
                for (int i = 0; i < packed.Length; i++) packed[i] = new Color32(metallic[i].r, 0, 0, (byte)(255 - roughness[i].r));
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                try
                {
                    texture.SetPixels32(packed);
                    texture.Apply(false);
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Log($"{job.Kind}: wrote {path} ({width}x{height}) from {metallicPath} and {roughnessPath}");
            }
            return ImportTexture(path, srgb: false, alphaFromInput: true);
        }

                                                                                                                            
        static Color32[] ReadPixels(string path, out int width, out int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Require(texture.LoadImage(File.ReadAllBytes(path), false), $"{path} is not a readable PNG");
                width = texture.width;
                height = texture.height;
                return texture.GetPixels32();
            }
            finally { Object.DestroyImmediate(texture); }
        }

                                                                                                                                                    
        static AnimatorController BuildController(Job job, AnimationClip[] clips, string v3Path)
        {
            if (job.Player) return BuildPlayerController(job, clips);
            string path = job.ModelDir + "MovementCombat.controller";
            if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(path))
            {
                Require(AssetDatabase.LoadAssetAtPath<AnimatorController>(v3Path), $"{job.Kind}: missing V3 controller {v3Path}");
                Require(AssetDatabase.CopyAsset(v3Path, path), $"{job.Kind}: could not copy {v3Path} to {path}");
                Log($"{job.Kind}: copied {v3Path} to {path}");
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Require(controller && controller.layers.Length > 0, $"{job.Kind}: {path} has no layers");
                                                                                                              
            Require(controller.layers[0].name == "Base Layer", $"{job.Kind}: {path} layer 0 is '{controller.layers[0].name}', not Base Layer");
            var top = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToList();
            Require(ClipNames.All(top.Contains), $"{job.Kind}: {path} Base Layer states are [{string.Join(", ", top)}]; it needs {string.Join(", ", ClipNames)} at the top");
            var byName = ClipNames.Select((name, i) => (name, clip: clips[i])).ToDictionary(p => p.name, p => p.clip);
            int assigned = 0;
            foreach (var layer in controller.layers) assigned += AssignMotions(layer.stateMachine, byName, job.Kind, path);
            EditorUtility.SetDirty(controller);
            Log($"{job.Kind}: {path}: {assigned} states play the V6 clips, {controller.parameters.Length} parameters as V3");
            return controller;
        }

                     
                                                                                                                                                     
                                                                                                                                                   
                                                                                                                                
                      
        static AnimatorController BuildPlayerController(Job job, AnimationClip[] clips)
        {
            EnsureFolder(job.ModelDir);
            string path = job.ModelDir + "MovementCombat.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                Log($"{job.Kind}: created {path}");
            }
            Require(controller && controller.layers.Length > 0 && controller.layers[0].name == "Base Layer", $"{job.Kind}: {path} has no Base Layer first");
            var parameters = controller.parameters;
            int speed = Array.FindIndex(parameters, p => p.name == RunSpeedParameter);
            if (speed < 0) controller.AddParameter(new AnimatorControllerParameter { name = RunSpeedParameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            else
            {
                Require(parameters[speed].type == AnimatorControllerParameterType.Float, $"{job.Kind}: {path} has a {RunSpeedParameter} that is not a float");
                parameters[speed].defaultFloat = 1f;
                controller.parameters = parameters;
            }
            var machine = controller.layers[0].stateMachine;
            for (int i = 0; i < job.Clips.Length; i++)
            {
                string name = job.Clips[i];
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s && s.name == name) ?? machine.AddState(name, new Vector3(300f, 60f * i, 0f));
                Require(!(state.motion is BlendTree), $"{job.Kind}: {path} state {name} holds a blend tree");
                state.motion = clips[i];
                state.speed = 1f;
                bool gait = GaitClips.Contains(name);
                state.speedParameterActive = gait;
                state.speedParameter = gait ? RunSpeedParameter : "";
                EditorUtility.SetDirty(state);
            }
            foreach (var stale in machine.states.Select(s => s.state).Where(s => s && !job.Clips.Contains(s.name)).ToList())
            {
                Log($"{job.Kind}: {path}: removed state {stale.name}, which has no clip now");
                machine.RemoveState(stale);
            }
            machine.defaultState = machine.states.Select(s => s.state).First(s => s.name == "Idle");
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            Log($"{job.Kind}: {path}: states [{string.Join(", ", job.Clips)}], {RunSpeedParameter} drives [{string.Join(", ", job.Clips.Where(GaitClips.Contains))}]");
            return controller;
        }

        static int AssignMotions(AnimatorStateMachine machine, Dictionary<string, AnimationClip> clips, string kind, string path)
        {
            int assigned = 0;
            foreach (var child in machine.states)
            {
                var state = child.state;
                Require(!(state.motion is BlendTree), $"{kind}: {path} state {state.name} holds a blend tree; extend the installer for it");
                if (clips.TryGetValue(state.name, out var clip))
                {
                    state.motion = clip;
                    EditorUtility.SetDirty(state);
                    assigned++;
                }
                else if (state.motion) throw Fail($"{kind}: {path} state {state.name} plays {state.motion.name}, which has no V6 clip");
            }
            foreach (var child in machine.stateMachines) assigned += AssignMotions(child.stateMachine, clips, kind, path);
            return assigned;
        }

                     
                                                                                                                                                   
                                                                                                                                                  
                                                                                                                                                
                                                                                                                                        
                      
        static GameObject BuildModelPrefab(Job job, Material material, AnimatorController controller, AnimationClip idle, out Bounds unit)
        {
            string path = job.ModelDir + "Model.prefab";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(job.AssetDir + job.ModelFile);
            Require(fbx, $"{job.Kind}: cannot load {job.AssetDir + job.ModelFile}");
            Bounds raw = MeasureFbx(fbx, idle, job.Kind + " new model");
                                                                                                                                           
                                                                                                                                               
                                                                                                                                            
                                                                                                                               
            var offset = job.Rig == RigGeneric ? Vector3.zero : new Vector3(-raw.center.x, -raw.min.y, -raw.center.z);
            unit = new Bounds(raw.center + offset, raw.size);
            if (job.Manifest.height > 0f)
            {
                float ratio = raw.size.y / job.Manifest.height;
                string note = $"{job.Kind}: idle-pose height in Unity {raw.size.y:0.###}, manifest height {job.Manifest.height:0.###}";
                if (ratio < .67f || ratio > 1.5f) Warn(note + ": units or the up axis differ from the Blender export");
                else Log(note);
            }

                                                                                                                                                  
                                                                                                            
            bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var scene = existed ? default : EditorSceneManager.NewPreviewScene();
            GameObject root;
            if (existed) root = PrefabUtility.LoadPrefabContents(path);
            else
            {
                root = new GameObject("Model");
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            try
            {
                GameObject instance = null;
                foreach (var child in root.transform.Cast<Transform>().ToList())
                {
                    if (!instance && PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject) && PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == fbx) instance = child.gameObject;
                    else Object.DestroyImmediate(child.gameObject);
                }
                if (!instance) instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                instance.transform.localPosition = fbx.transform.localPosition + offset;
                instance.transform.localRotation = fbx.transform.localRotation;
                instance.transform.localScale = fbx.transform.localScale;
                var animator = instance.GetComponent<Animator>();
                Require(animator, $"{job.Kind}: {fbx.name} has no Animator on its root (did the Generic rig import?)");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                var renderers = ModelRenderers(instance.transform);
                Require(renderers.Length > 0, $"{job.Kind}: {fbx.name} has no mesh renderer");
                Require(renderers.OfType<SkinnedMeshRenderer>().Any(r => r.bones.Length > 0), $"{job.Kind}: {fbx.name} has no skinned mesh; rig {job.Rig} expects one");
                long triangles = 0;
                foreach (var renderer in renderers)
                {
                    var mesh = MeshOf(renderer);
                    int slots = Mathf.Max(1, renderer.sharedMaterials.Length, mesh ? mesh.subMeshCount : 0);
                    renderer.sharedMaterials = Enumerable.Repeat(material, slots).ToArray();
                    if (renderer is SkinnedMeshRenderer skinned) skinned.quality = SkinQuality.Bone4;                                        
                    if (mesh) for (int s = 0; s < mesh.subMeshCount; s++) triangles += mesh.GetIndexCount(s) / 3;
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                Require(success && saved, $"{job.Kind}: could not save {path}");
                Log($"{job.Kind}: {(existed ? "updated" : "created")} {path}: {renderers.Length} renderer(s), {triangles:N0} triangles, " +
                    $"FBX moved by ({offset.x:0.###}, {offset.y:0.###}, {offset.z:0.###}) to stand on the root; idle size {Size(unit.size)}");
                if (triangles > TriangleWarning) Warn($"{job.Kind}: the imported mesh has {triangles:N0} triangles (T-014)");
                return saved;
            }
            finally
            {
                if (existed) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(scene);
            }
        }

                                                                                                                                                                             
        static Bounds MeasureFbx(GameObject fbx, AnimationClip idle, string what)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var space = new GameObject("Probe");
                SceneManager.MoveGameObjectToScene(space, scene);
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(fbx, space.transform);
                return MeasurePose(probe, space.transform, idle, what);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

                                       

        static string SwapIntoEnemyPrefab(Job job, GameObject modelPrefab, Bounds unit, AnimationClip[] clips, Fit fit, out string baseName)
        {
            var root = PrefabUtility.LoadPrefabContents(job.PrefabPath);
            try
            {
                var presentation = root.GetComponent<V2ModelPresentation>();
                Require(presentation, $"{job.PrefabPath} has no {nameof(V2ModelPresentation)}");
                var shown = presentation.modelRoot;
                Require(shown && shown.parent, $"{job.PrefabPath}: {nameof(V2ModelPresentation)}.modelRoot is empty");
                Transform parent = shown.parent, oldRoot, previous;
                if (IsNew(shown))
                {
                                                                                                                              
                    baseName = BaseNameOf(shown);
                    oldRoot = parent.Find(baseName + OldSuffix);
                    Require(oldRoot, $"{job.PrefabPath}: {shown.name} is shown but {baseName}{OldSuffix} is missing");
                    previous = shown;
                }
                else
                {
                    baseName = shown.name;
                    oldRoot = shown;
                    Require(!parent.Find(baseName + OldSuffix), $"{job.PrefabPath}: {baseName} is shown but {baseName}{OldSuffix} exists too; sort it out by hand");
                    previous = parent.Find(baseName + NewSuffix);
                                                                                                                                             
                    oldRoot.localScale = presentation.authoredScale;
                    oldRoot.localPosition = presentation.authoredPosition;
                    if (job.Player) WritePlayerStash(job, presentation);
                }
                var oldAnimator = oldRoot.GetComponentInChildren<Animator>(true);
                Require(oldAnimator, $"{job.PrefabPath}: the V3 model {oldRoot.name} has no Animator");
                var oldClips = ClipsOf(oldAnimator, $"{job.Kind} V3 model");
                if (!IsNew(shown) && (presentation.idleClip != oldClips[0] || presentation.runClip != oldClips[1] || presentation.attackClip != oldClips[2]))
                    Warn($"{job.Kind}: the presentation's clips differ from what the V3 controller plays; Revert will restore the controller's clips");
                var oldRenderers = ModelRenderers(oldRoot);
                Require(oldRenderers.Length > 0, $"{job.PrefabPath}: the V3 model {oldRoot.name} has no renderer to measure");
                if (presentation.rotationOffsetEuler != Vector3.zero)
                    Warn($"{job.Kind}: rotationOffsetEuler is {presentation.rotationOffsetEuler}; the sizes below are measured without it");

                                                                                                                     
                Bounds before = MeasureClone(oldRoot, oldClips[0], $"{job.Kind} V3 model");
                float oldHeight = before.size.y, newHeight = unit.size.y;
                float oldFoot = Mathf.Sqrt(before.size.x * before.size.z), newFoot = Mathf.Sqrt(unit.size.x * unit.size.z);
                Require(oldHeight > 1e-4f && oldFoot > 1e-4f && newHeight > 1e-4f && newFoot > 1e-4f, $"{job.Kind}: a model has no size (old {Size(before.size)}, new {Size(unit.size)})");
                float byHeight = oldHeight / newHeight, byFoot = oldFoot / newFoot;
                float scale = fit switch { Fit.Height => byHeight, Fit.Footprint => byFoot, _ => Mathf.Sqrt(byHeight * byFoot) };
                                                                                                                                                          
                                                                                                                                             
                var position = job.Player ? Vector3.zero : new Vector3(before.center.x, 0f, before.center.z);
                Log($"{job.Kind}: old model {Size(before.size)}, bottom at {before.min.y:0.###}, footprint centre ({before.center.x:0.###}, {before.center.z:0.###}); new model at scale 1 {Size(unit.size)}");
                string fitted = $"fit {fit.ToString().ToLowerInvariant()}: height x{byHeight:0.####}, footprint x{byFoot:0.####}, scale {scale:0.####}; " +
                                $"new {Size(unit.size * scale)}, height {Percent(scale * newHeight / oldHeight)}, footprint {Percent(scale * newFoot / oldFoot)} against the old model";
                Log($"{job.Kind}: {fitted}");
                WarnFacing(job, before, unit, presentation.rotationOffsetEuler);
                var capsule = root.GetComponent<CapsuleCollider>();
                Log($"{job.Kind}: collider {(capsule ? $"capsule radius {capsule.radius:0.###}, height {capsule.height:0.###}" : "none on the root")} and body radius {presentation.referenceBodyRadius:0.###} (reference) are not changed; " +
                    $"kept tuning: rotation offset {presentation.rotationOffsetEuler}, run reference speed {presentation.runReferenceSpeed:0.##}, attack contact {presentation.attackContactNormalized:0.##} of {clips[2].length:0.00} s");

                                                                                                                                               
                                                                                                                    
                var replaced = previous ? ModelRenderers(previous) : Array.Empty<Renderer>();
                bool reuse = previous && PrefabUtility.IsAnyPrefabInstanceRoot(previous.gameObject) && PrefabUtility.GetCorrespondingObjectFromSource(previous.gameObject) == modelPrefab;
                oldRoot.name = baseName + OldSuffix;
                oldRoot.gameObject.SetActive(false);
                var model = reuse ? previous.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, parent);
                if (reuse) model.SetActive(true);
                model.name = baseName + NewSuffix;
                model.transform.SetSiblingIndex(oldRoot.GetSiblingIndex() + 1);
                model.transform.localPosition = position;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * scale;
                SetLayer(model, oldRoot.gameObject.layer);
                var newRenderers = ModelRenderers(model.transform);
                presentation.modelRoot = model.transform;
                presentation.animator = model.GetComponentInChildren<Animator>(true);
                presentation.idleClip = clips[0];
                presentation.runClip = clips[1];
                presentation.attackClip = clips[2];
                presentation.modelRenderers = newRenderers;
                presentation.authoredScale = model.transform.localScale;
                presentation.authoredPosition = position;
                Require(presentation.HasAssignedModel, $"{job.Kind}: the presentation is incomplete after the swap");
                if (job.Player) fitted += "; " + SetUpPlayer(job, root, presentation, model.transform, clips);
                ReplaceSurfaces(root, oldRenderers.Concat(replaced).ToArray(), newRenderers, job.Kind);
                if (previous && !reuse)
                {
                    RequireNoReferencesInto(root, previous, model.transform, job.Kind);
                    string stale = previous.name;
                    Object.DestroyImmediate(previous.gameObject);
                    Log($"{job.Kind}: removed an earlier {stale} that was not an instance of {AssetDatabase.GetAssetPath(modelPrefab)}");
                }
                else if (reuse) Log($"{job.Kind}: kept the earlier {model.name} instance and refitted it");
                RequireNoReferencesInto(root, oldRoot, model.transform, job.Kind);
                SavePrefab(root, job.PrefabPath);
                Log($"{job.Kind}: saved {job.PrefabPath}: {oldRoot.name} hidden, {model.name} shown ({newRenderers.Length} renderer(s))");
                return fitted;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void WarnFacing(Job job, Bounds before, Bounds unit, Vector3 rotationOffset)
        {
            float oldAspect = before.size.z / Mathf.Max(1e-4f, before.size.x), newAspect = unit.size.z / Mathf.Max(1e-4f, unit.size.x);
            bool turned = oldAspect > FacingAspect && newAspect < 1f / FacingAspect || oldAspect < 1f / FacingAspect && newAspect > FacingAspect;
            if (!turned) return;
            Warn($"{job.Kind}: the old model is longer along {(oldAspect > 1f ? "Z (forward)" : "X (sideways)")}, the new one along {(newAspect > 1f ? "Z (forward)" : "X (sideways)")}: " +
                 $"check its facing in Play; the rotation offset {rotationOffset} is kept. Axis note: {job.Manifest.fbx_axis_note}");
        }

                                      

                     
                                                                                                                                     
                                                                                                                                             
                      
        static string SetUpPlayer(Job job, GameObject prefabRoot, V2ModelPresentation presentation, Transform model, AnimationClip[] clips)
        {
            var body = prefabRoot.GetComponent<Combatant>();
            Require(body, $"{job.PrefabPath} has no {nameof(Combatant)}");
                                                                                                                                  
            float size = (presentation.scaleWithBodyRadius ? body.radius / Mathf.Max(.01f, presentation.referenceBodyRadius) : 1f)
                         * (presentation.presentationScale > 0f ? presentation.presentationScale : PlayerPresentationScale);
            var byName = new Dictionary<string, AnimationClip>();
            for (int i = 0; i < job.Clips.Length && i < clips.Length; i++) byName[job.Clips[i]] = clips[i];
            float Measure(string name) => byName.TryGetValue(name, out var clip) ? MeasureGroundSpeed(model, clip, size, $"{job.Kind} {name}") : 0f;
            float run = Measure("Run"), walk = Measure("Walk"), sprint = Measure("Sprint");
            presentation.legsFollowMovement = true;
            presentation.playerAttackMotion = false;
                                                                                                                                    
            presentation.illustratedTint = Color.white;
            const float Least = .3f;                                                                                        
            if (run > Least) presentation.runReferenceSpeed = run;
            else Warn($"{job.Kind}: could not measure the Run clip's foot speed; runReferenceSpeed stays {presentation.runReferenceSpeed:0.##}");
            if (walk > Least) presentation.walkReferenceSpeed = walk;
            if (sprint > Least) presentation.sprintReferenceSpeed = sprint;
                                                                                                                            
            if (walk > Least && run > Least) presentation.walkBelowSpeed = Mathf.Sqrt(walk * run);
            return $"legs follow movement, no attack motion; planted-foot speeds at size x{size:0.##}: run {run:0.##}, walk {walk:0.##}, sprint {sprint:0.##} m/s, " +
                   $"walk below {presentation.walkBelowSpeed:0.##} m/s";
        }

                     
                                                                                                                                                   
                                                                                                                                         
                      
        static float MeasureGroundSpeed(Transform modelRoot, AnimationClip clip, float size, string what)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var space = new GameObject("Measure");
                SceneManager.MoveGameObjectToScene(space, scene);
                var copy = Object.Instantiate(modelRoot.gameObject, space.transform, false);
                copy.SetActive(true);
                var animator = copy.GetComponentInChildren<Animator>(true);
                var feet = copy.GetComponentsInChildren<Transform>(true).Where(t => BoneIs(t, "LeftFoot") || BoneIs(t, "RightFoot")).ToArray();
                if (!animator || feet.Length < 2) { Warn($"{what}: no Animator or no LeftFoot and RightFoot bones to measure"); return 0f; }
                var target = animator.transform;
                Vector3 position = target.localPosition, scale = target.localScale;
                Quaternion rotation = target.localRotation;
                int steps = Mathf.Max(16, Mathf.RoundToInt(clip.length * 120f));
                float dt = clip.length / steps;
                var heights = feet.Select(_ => new float[steps + 1]).ToArray();
                var flats = feet.Select(_ => new Vector3[steps + 1]).ToArray();
                for (int i = 0; i <= steps; i++)
                {
                    clip.SampleAnimation(animator.gameObject, i * dt);
                    target.localPosition = position; target.localRotation = rotation; target.localScale = scale;
                    for (int f = 0; f < feet.Length; f++)
                    {
                        Vector3 p = space.transform.InverseTransformPoint(feet[f].position) * size;
                        heights[f][i] = p.y;
                        flats[f][i] = new Vector3(p.x, 0f, p.z);
                    }
                }
                var speeds = new List<float>();
                for (int f = 0; f < feet.Length; f++)
                {
                    float low = heights[f].Min(), high = heights[f].Max();
                    float planted = low + Mathf.Max(.01f * size, (high - low) * .08f);
                    for (int i = 0; i < steps; i++)
                        if (heights[f][i] <= planted && heights[f][i + 1] <= planted)
                            speeds.Add((flats[f][i + 1] - flats[f][i]).magnitude / dt);
                }
                if (speeds.Count < 4) { Warn($"{what}: only {speeds.Count} planted-foot samples"); return 0f; }
                speeds.Sort();
                float median = speeds[speeds.Count / 2];
                Log($"{what}: planted-foot speed {median:0.###} m/s (median of {speeds.Count} samples, {clip.length:0.00} s clip, size x{size:0.##})");
                return median;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static bool BoneIs(Transform bone, string name)
        {
            string own = bone.name;
            int colon = own.LastIndexOf(':');
            return string.Equals(colon >= 0 ? own.Substring(colon + 1) : own, name, StringComparison.OrdinalIgnoreCase);
        }

        static string PlayerStashPath(string sourceRoot) => Path.Combine(sourceRoot, PlayerKind, PlayerStashFile).Replace('\\', '/');

                                                                                                                                                    
        static void WritePlayerStash(Job job, V2ModelPresentation presentation)
        {
            string path = job.SourceDir + "/" + PlayerStashFile;
            var stash = new PlayerStash
            {
                runReferenceSpeed = presentation.runReferenceSpeed, legsFollowMovement = presentation.legsFollowMovement,
                playerAttackMotion = presentation.playerAttackMotion, illustratedTint = presentation.illustratedTint
            };
            File.WriteAllText(path, JsonUtility.ToJson(stash, true));
            Log($"{job.Kind}: kept the explorer's run reference speed {stash.runReferenceSpeed:0.##} in {path} for Revert");
        }

                             

        static string RevertKind(string kind)
        {
            string prefabPath = PrefabFolder + kind + ".prefab";
            Require(File.Exists(prefabPath), $"missing {prefabPath}");
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            string baseName;
            try
            {
                var presentation = root.GetComponent<V2ModelPresentation>();
                Require(presentation, $"{prefabPath} has no {nameof(V2ModelPresentation)}");
                var shown = presentation.modelRoot;
                Require(shown && shown.parent, $"{prefabPath}: {nameof(V2ModelPresentation)}.modelRoot is empty");
                if (!IsNew(shown))
                {
                    Require(!shown.parent.Find(shown.name + OldSuffix), $"{prefabPath}: {shown.name} is shown but {shown.name}{OldSuffix} exists too; sort it out by hand");
                    return $"skipped: already shows its V3 model {shown.name}";
                }
                baseName = BaseNameOf(shown);
                var oldRoot = shown.parent.Find(baseName + OldSuffix);
                Require(oldRoot, $"{prefabPath}: {shown.name} is shown but {baseName}{OldSuffix} is missing, so there is nothing to go back to");
                var animator = oldRoot.GetComponentInChildren<Animator>(true);
                Require(animator, $"{prefabPath}: {oldRoot.name} has no Animator");
                var clips = ClipsOf(animator, $"{kind} V3 model");
                var oldRenderers = ModelRenderers(oldRoot);
                Require(oldRenderers.Length > 0, $"{prefabPath}: {oldRoot.name} has no renderer");
                var newRenderers = ModelRenderers(shown);

                oldRoot.name = baseName;
                oldRoot.gameObject.SetActive(true);
                presentation.modelRoot = oldRoot;
                presentation.animator = animator;
                presentation.idleClip = clips[0];
                presentation.runClip = clips[1];
                presentation.attackClip = clips[2];
                presentation.modelRenderers = oldRenderers;
                                                                                                
                presentation.authoredScale = oldRoot.localScale;
                presentation.authoredPosition = oldRoot.localPosition;
                if (kind == PlayerKind)
                {
                                                                                                                                      
                    string stashPath = PlayerStashPath(ResolveSourceRoot(null));
                    var stash = File.Exists(stashPath) ? JsonUtility.FromJson<PlayerStash>(File.ReadAllText(stashPath)) : null;
                    presentation.legsFollowMovement = stash != null && stash.legsFollowMovement;
                    presentation.playerAttackMotion = stash != null && stash.playerAttackMotion;
                    if (stash != null && stash.runReferenceSpeed > 0f) presentation.runReferenceSpeed = stash.runReferenceSpeed;
                    else Warn($"{kind}: no {stashPath}; legsFollowMovement is off, runReferenceSpeed stays {presentation.runReferenceSpeed:0.##}");
                                                                                                    
                    presentation.illustratedTint = stash != null && stash.illustratedTint.a > 0f ? stash.illustratedTint : new Color(.62f, .82f, 1f);
                }
                ReplaceSurfaces(root, newRenderers, oldRenderers, kind);
                RequireNoReferencesInto(root, shown, oldRoot, kind);
                Object.DestroyImmediate(shown.gameObject);
                SavePrefab(root, prefabPath);
                Log($"{kind}: saved {prefabPath}: {baseName} shown again (scale {presentation.authoredScale.x:0.####}), {baseName}{NewSuffix} removed");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            FollowInVariants(prefabPath, kind, baseName, installed: false);
            return "reverted to the V3 model";
        }

                                                                                           

        static void FollowInVariants(string basePath, string kind, string baseName, bool installed)
        {
            foreach (string path in VariantsOf(basePath))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var presentation = root.GetComponent<V2ModelPresentation>();
                    if (!presentation) { Log($"{kind}: variant {path} has no {nameof(V2ModelPresentation)}, left alone"); continue; }
                    var inheritedNew = FindByName(root.transform, baseName + NewSuffix);
                    var inheritedOld = FindByName(root.transform, installed ? baseName + OldSuffix : baseName);
                    var shown = presentation.modelRoot;
                    bool ownModel = shown && shown != inheritedNew && shown != inheritedOld;
                    bool changed = false;
                    if (installed)
                    {
                        Require(inheritedNew, $"variant {path} lacks the inherited {baseName}{NewSuffix}");
                        if (!ownModel)
                        {
                                                                                                                              
                            changed |= RevertOverrides(presentation, PresentationFields, path, kind);
                            if (inheritedOld && inheritedOld.gameObject.activeSelf) { inheritedOld.gameObject.SetActive(false); changed = true; }
                            Require(presentation.modelRoot == inheritedNew, $"variant {path} does not show the inherited {baseName}{NewSuffix} after its overrides were reverted");
                        }
                        else
                        {
                                                                                                                                                      
                            if (inheritedNew.gameObject.activeSelf) { inheritedNew.gameObject.SetActive(false); changed = true; }
                            var own = ClipsOf(presentation.animator, $"variant {path}");
                            if (presentation.idleClip != own[0] || presentation.runClip != own[1] || presentation.attackClip != own[2])
                            {
                                presentation.idleClip = own[0];
                                presentation.runClip = own[1];
                                presentation.attackClip = own[2];
                                changed = true;
                            }
                        }
                    }
                    else if (ownModel)
                    {
                                                                                                             
                        var basePresentation = AssetDatabase.LoadAssetAtPath<GameObject>(basePath).GetComponent<V2ModelPresentation>();
                        var baseClips = new[] { basePresentation.idleClip, basePresentation.runClip, basePresentation.attackClip };
                        var ownClips = new[] { presentation.idleClip, presentation.runClip, presentation.attackClip };
                        var same = ClipFields.Where((field, i) => ownClips[i] == baseClips[i]).ToArray();
                        changed |= RevertOverrides(presentation, same, path, kind, quiet: true);
                    }
                    if (!changed) { Log($"{kind}: variant {path} needs no change"); continue; }
                    PrefabUtility.RemoveUnusedOverrides(new[] { root }, InteractionMode.AutomatedAction);
                    SavePrefab(root, path);
                    string what = !ownModel ? "follows the base model" : installed ? $"own model kept, the base's {baseName}{NewSuffix} hidden, clips from its own Animator"
                        : "own model kept, clip values equal to the base again";
                    Log($"{kind}: saved variant {path} ({what})");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        static bool RevertOverrides(V2ModelPresentation presentation, IEnumerable<string> fields, string path, string kind, bool quiet = false)
        {
            using var serialized = new SerializedObject(presentation);
            bool any = false;
            foreach (string field in fields)
            {
                serialized.Update();
                var property = serialized.FindProperty(field);
                if (property == null || !property.prefabOverride) continue;
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                if (!quiet) Warn($"{kind}: variant {path} had its own {field}; reverted so it follows the base");
                any = true;
            }
            return any;
        }

                                                                                                                 
        static List<string> VariantsOf(string basePath)
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabSearchFolder }).Select(g => AssetDatabase.GUIDToAssetPath(g)).Where(File.Exists).Distinct().ToList();
            var found = new List<string>();
            var frontier = new Queue<string>();
            frontier.Enqueue(basePath);
            while (frontier.Count > 0)
            {
                string source = frontier.Dequeue();
                                                                                                                             
                string marker = "m_SourcePrefab: {fileID: 100100000, guid: " + AssetDatabase.AssetPathToGUID(source);
                foreach (string path in prefabs)
                {
                    if (path == basePath || found.Contains(path) || !File.ReadAllText(path).Contains(marker)) continue;
                                                                                                               
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var parent = asset && PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant ? PrefabUtility.GetCorrespondingObjectFromSource(asset) : null;
                    if (!parent || AssetDatabase.GetAssetPath(parent) != source) continue;
                    found.Add(path);
                    frontier.Enqueue(path);
                }
            }
            return found;
        }

                                     

                                                                                                                                   
        static AnimationClip[] ClipsOf(Animator animator, string what)
        {
            var controller = animator ? animator.runtimeAnimatorController as AnimatorController : null;
            Require(controller && controller.layers.Length > 0, $"{what}: no Animator controller");
            var states = controller.layers[0].stateMachine.states;
            return ClipNames.Select(name =>
            {
                var state = states.Select(s => s.state).FirstOrDefault(s => s && s.name == name);
                Require(state && state.motion is AnimationClip, $"{what}: {AssetDatabase.GetAssetPath(controller)} has no {name} state with a clip");
                return (AnimationClip)state.motion;
            }).ToArray();
        }

                                                                                                                                                  
        static Bounds MeasureClone(Transform modelRoot, AnimationClip idle, string what)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var space = new GameObject("Measure");
                SceneManager.MoveGameObjectToScene(space, scene);
                var copy = Object.Instantiate(modelRoot.gameObject, space.transform, false);                                                            
                copy.SetActive(true);
                return MeasurePose(copy, space.transform, idle, what);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static Bounds MeasurePose(GameObject model, Transform space, AnimationClip idle, string what)
        {
            var animator = model.GetComponentInChildren<Animator>(true);
            if (idle && animator)
            {
                var target = animator.transform;
                Vector3 position = target.localPosition, scale = target.localScale;
                Quaternion rotation = target.localRotation;
                idle.SampleAnimation(animator.gameObject, 0f);
                                                                                                                       
                target.localPosition = position;
                target.localRotation = rotation;
                target.localScale = scale;
            }
            Require(MeasureBounds(model, space, out var bounds), $"{what}: no mesh to measure");
            return bounds;
        }

                                                                                                                                       
        static bool MeasureBounds(GameObject root, Transform space, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            var baked = new Mesh();
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh;
                    if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh)
                    {
                                                                                                                                         
                                                                                                                                           
                        skinned.BakeMesh(baked, true);
                        mesh = baked;
                    }
                    else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh) mesh = filter.sharedMesh;
                    else continue;
                    var toSpace = space.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    foreach (var vertex in mesh.vertices)
                    {
                        var point = toSpace.MultiplyPoint3x4(vertex);
                        if (any) bounds.Encapsulate(point);
                        else { bounds = new Bounds(point, Vector3.zero); any = true; }
                    }
                }
            }
            finally { Object.DestroyImmediate(baked); }
            return any;
        }

        static Mesh MeshOf(Renderer renderer) => renderer switch
        {
            SkinnedMeshRenderer skinned => skinned.sharedMesh,
            MeshRenderer _ when renderer.TryGetComponent(out MeshFilter filter) => filter.sharedMesh,
            _ => null
        };

        static Renderer[] ModelRenderers(Transform root) =>
            root.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).ToArray();

                                                                                                                                  
        static void ReplaceSurfaces(GameObject root, Renderer[] from, Renderer[] to, string kind)
        {
            foreach (var feedback in root.GetComponentsInChildren<V2EnemyThreatFeedback>(true))
            {
                if (feedback.surfaces == null) continue;
                var kept = new List<Renderer>();
                int insertAt = -1;
                foreach (var surface in feedback.surfaces)
                {
                    if (surface && from.Contains(surface)) { if (insertAt < 0) insertAt = kept.Count; continue; }
                    kept.Add(surface);
                }
                if (insertAt < 0) continue;
                kept.InsertRange(insertAt, to.Where(r => !kept.Contains(r)));
                feedback.surfaces = kept.ToArray();
                Log($"{kind}: {nameof(V2EnemyThreatFeedback)}.surfaces now [{string.Join(", ", kept.Select(r => r ? r.name : "none"))}]");
            }
        }

                                                                                                                                                                          
        static void RequireNoReferencesInto(GameObject root, Transform leaving, Transform staying, string kind)
        {
            var found = new List<string>();
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behaviour || behaviour.transform.IsChildOf(leaving) || behaviour.transform.IsChildOf(staying)) continue;
                using var serialized = new SerializedObject(behaviour);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var target = property.objectReferenceValue;
                    if (!target) continue;
                    var transform = target is Component component ? component.transform : target is GameObject gameObject ? gameObject.transform : null;
                    if (transform && transform.IsChildOf(leaving)) found.Add($"{behaviour.GetType().Name}.{property.propertyPath} -> {target.name}");
                }
            }
            Require(found.Count == 0, $"{kind}: these fields still point into {leaving.name}: {string.Join("; ", found)}; extend {nameof(InstallV6EnemyModels)} for them");
        }

        static Transform FindByName(Transform root, string name)
        {
            var matches = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToList();
            if (matches.Count > 1) Warn($"{root.name} has {matches.Count} objects named {name}; using the first");
            return matches.FirstOrDefault();
        }

        static bool IsNew(Transform modelRoot) => modelRoot.name.EndsWith(NewSuffix, StringComparison.Ordinal);

        static string BaseNameOf(Transform modelRoot) => IsNew(modelRoot) ? modelRoot.name.Substring(0, modelRoot.name.Length - NewSuffix.Length) : modelRoot.name;

        static void SetLayer(GameObject root, int layer)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.gameObject.layer != layer) transform.gameObject.layer = layer;
        }

        static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Require(success, $"could not save {path}");
        }

        static ModelImporter ModelImporterAt(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Require(importer, $"{path} did not import as a model");
            return importer;
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static bool SameBytes(string a, string b)
        {
            if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
            using var sha = SHA256.Create();
            using var first = File.OpenRead(a);
            using var second = File.OpenRead(b);
            return sha.ComputeHash(first).SequenceEqual(sha.ComputeHash(second));
        }

                                                                                                                                   
        static string SafeRelative(string value, string manifestFile)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string relative = value.Trim().Replace('\\', '/');
            Require(!Path.IsPathRooted(relative) && !relative.Split('/').Contains(".."), $"{manifestFile}: '{value}' must be a path inside the kind's folder");
            return relative;
        }

        static string FirstExisting(string dir, params string[] candidates)
        {
            var named = candidates.Where(c => c != null).ToList();
            return named.FirstOrDefault(c => File.Exists(dir + "/" + c)) ?? named[0];
        }

        static string ResolveSourceRoot(string source)
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            string root = string.IsNullOrWhiteSpace(source) ? Path.Combine(project, DefaultSourceRoot)
                : Path.IsPathRooted(source) ? source : Path.Combine(project, source);
            root = Path.GetFullPath(root).Replace('\\', '/');
            Require(Directory.Exists(root), $"the Blender output folder {root} does not exist");
            return root;
        }

        static List<string> ParseKinds(string list)
        {
            if (string.IsNullOrWhiteSpace(list)) return Kinds.ToList();
            var result = new List<string>();
            foreach (string part in list.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string kind = Kinds.Append(PlayerKind).FirstOrDefault(k => string.Equals(k, part, StringComparison.OrdinalIgnoreCase));
                Require(kind != null, $"unknown kind '{part}'; the V6 kinds are {string.Join(", ", Kinds)} and {PlayerKind}");
                if (!result.Contains(kind)) result.Add(kind);
            }
            return result;
        }

        static void ParseFit(string spec, Options options)
        {
            if (string.IsNullOrWhiteSpace(spec)) return;
            foreach (string part in spec.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = part.Split('=');
                if (bits.Length == 1) { options.DefaultFit = ParseFitMode(bits[0]); continue; }
                Require(bits.Length == 2, $"-fit: '{part}' is not kind=mode");
                string kind = Kinds.Append(PlayerKind).FirstOrDefault(k => string.Equals(k, bits[0].Trim(), StringComparison.OrdinalIgnoreCase));
                Require(kind != null, $"-fit: unknown kind '{bits[0]}'");
                options.FitByKind[kind] = ParseFitMode(bits[1]);
            }
        }

        static Fit ParseFitMode(string value) => value.Trim().ToLowerInvariant() switch
        {
            "balanced" => Fit.Balanced,
            "height" => Fit.Height,
            "footprint" => Fit.Footprint,
            _ => throw Fail($"-fit takes balanced, height or footprint, not '{value}'")
        };

        static bool ParseSwitch(string value, string name) => value.Trim().ToLowerInvariant() switch
        {
            "on" or "true" or "1" or "yes" => true,
            "off" or "false" or "0" or "no" => false,
            _ => throw Fail($"{name} takes on or off, not '{value}'")
        };

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static string Join(string[] values) => values == null ? "" : string.Join(", ", values);

        static string Size(Vector3 size) => $"{size.x:0.###} x {size.y:0.###} x {size.z:0.###} (W x H x D)";

        static string Percent(float ratio) => $"{(ratio - 1f) * 100f:+0;-0;0}%";

        static void Log(string message) => Debug.Log(Tag + message);

        static void Warn(string message) => Debug.LogWarning(Tag + "WARNING " + message);

        static InvalidOperationException Fail(string message) => new InvalidOperationException(Tag + message);

        static void Require(bool condition, string message)
        {
            if (!condition) throw Fail(message);
        }

                                                                                                                                                            
        sealed class SceneOverrides
        {
            Dictionary<string, string> texts;

            public void Warn(string prefabPath, string kind)
            {
                texts ??= Directory.GetFiles(SceneFolder, "*.unity").ToDictionary(p => p.Replace('\\', '/'), p => File.ReadAllText(p));
                string guid = AssetDatabase.AssetPathToGUID(prefabPath);
                var pattern = new Regex(@"target: \{fileID: -?\d+, guid: " + guid + @", type: 3\}\s+propertyPath: (" + string.Join("|", PresentationFields) + @")\b");
                foreach (var scene in texts)
                {
                    int count = pattern.Matches(scene.Value).Count;
                    if (count > 0)
                        InstallV6EnemyModels.Warn($"{kind}: {scene.Key} overrides {count} model values of {kind} instances (authoredScale, authoredPosition ...); " +
                                                  "those instances keep them and show the new model at the old model's numbers. The scene was not changed.");
                }
            }
        }
    }
}
