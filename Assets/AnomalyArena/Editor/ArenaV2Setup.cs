using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena.EditorTools
{
    public static class ArenaV2Setup
    {
        const string Root = "Assets/AnomalyArena/V2";
        public const string ScenePath = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        public static void Install()
        {
            foreach (string folder in new[] { Root, Root + "/Data", Root + "/Data/Enemies", Root + "/Prefabs", Root + "/Materials" }) EnsureFolder(folder);
            if (File.Exists(ScenePath)) { Debug.LogError("[ArenaV2] Already exists. Use explicit component edits; this installer will not overwrite an iterated scene."); return; }
            ProgressionDataSetup.CreateAssets();
            var lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/AnomalyArena/Materials/Base.mat");
            var transparent = AssetDatabase.LoadAssetAtPath<Material>("Assets/AnomalyArena/Materials/FxTransparent.mat");
            var floorMaterial = MakeMaterial("BattlefieldFloor", lit, new Color(0.13f, 0.19f, 0.25f));
            var wallMaterial = MakeMaterial("HullWall", lit, new Color(0.22f, 0.32f, 0.42f));
            var boundaryMaterial = MakeMaterial("Boundary", lit, new Color(0.12f, 0.72f, 0.88f));
            var holeMaterial = MakeMaterial("VoidSquare", lit, new Color(0.015f, 0.006f, 0.03f));
            var gravityMaterial = MakeMaterial("GravityZone", transparent, new Color(0.55f, 0.2f, 0.85f, 0.25f));
            var portalMaterial = MakeMaterial("Portal", transparent, new Color(0.7f, 0.45f, 1f, 0.9f));
            var telegraphMaterial = MakeMaterial("Telegraph", transparent, new Color(1f, 0.24f, 0.27f, 0.35f));
            var windupMaterial = MakeMaterial("Windup", lit, new Color(1f, 0.75f, 0.15f));
            var map = LoadOrCreate<MapLayout>(Root + "/Data/InterstellarMap.asset", asset =>
            {
                asset.walls = new[] {
                    new Rect(-43, -5, 8, 2), new Rect(-20, -15, 2, 8), new Rect(-20, 7, 2, 8),
                    new Rect(-8, -8, 6, 2), new Rect(4, 6, 6, 2), new Rect(17, -15, 2, 8),
                    new Rect(17, 7, 2, 8), new Rect(35, 3, 8, 2), new Rect(-8, 30, 8, 2), new Rect(3, -32, 8, 2)
                };
                asset.holeAnchors = new[] { new Vector2(-32,-20), new Vector2(-32,20), new Vector2(0,-22), new Vector2(0,22), new Vector2(32,-20), new Vector2(32,20) };
            });
            var biome = LoadOrCreate<Biome>(Root + "/Data/InterstellarBattlefield.asset", asset => { asset.layout = map; asset.floor = floorMaterial; asset.wall = wallMaterial; });
            if (biome.displayName == "Interstellar Battlefield") { biome.displayName = "Wreck Field"; biome.mixTilt = new[] { -10,0,0,0,10,0,0,0,0,0,0 }; EditorUtility.SetDirty(biome); }
            var dustFloor = MakeMaterial("DustFloor", lit, new Color(0.42f,0.32f,0.22f));
            var dustWall = MakeMaterial("DustWall", lit, new Color(0.53f,0.39f,0.25f));
            var riftFloor = MakeMaterial("RiftFloor", lit, new Color(0.18f,0.1f,0.28f));
            var riftWall = MakeMaterial("RiftWall", lit, new Color(0.31f,0.19f,0.47f));
            var dustMap = LoadOrCreate<MapLayout>(Root + "/Data/DustFlatsMap.asset", asset =>
            {
                asset.walls = new[] { new Rect(-42,-6,14,2), new Rect(-10,9,20,2), new Rect(28,4,14,2), new Rect(-8,-10,16,2) };
                asset.holeAnchors = (Vector2[])map.holeAnchors.Clone();
            });
            var riftMap = LoadOrCreate<MapLayout>(Root + "/Data/RiftZoneMap.asset", asset =>
            {
                asset.walls = new[] { new Rect(-42,-5,8,2),new Rect(-8,-4,6,2),new Rect(2,4,6,2),new Rect(34,3,8,2),new Rect(-14,26,6,2),new Rect(8,-28,6,2) };
                asset.holeAnchors = new[] { new Vector2(-26,-15),new Vector2(-26,15),new Vector2(26,-15),new Vector2(26,15) };
            });
            var dust = LoadOrCreate<Biome>(Root + "/Data/DustFlats.asset", asset => { asset.displayName="Dust Flats"; asset.layout=dustMap; asset.floor=dustFloor; asset.wall=dustWall; asset.ambient=new Color(0.3f,0.24f,0.18f); });
            var rift = LoadOrCreate<Biome>(Root + "/Data/RiftZone.asset", asset => { asset.displayName="Rift Zone"; asset.layout=riftMap; asset.floor=riftFloor; asset.wall=riftWall; asset.ambient=new Color(0.2f,0.12f,0.3f); asset.mixTilt=new[]{0,-10,10,0,0,0,0,0,0,0,0}; });
            var wavesData = LoadOrCreate<WaveSet>(Root + "/Data/Waves15.asset", InitWaves);
            var statusRules = FindAsset<StatusRules>();
            if (!statusRules) statusRules = LoadOrCreate<StatusRules>(Root + "/Data/StatusRules.asset", _ => { });
            var definitions = new EnemyDefinition[13];
            var enemyPrefabs = new Enemy[13];
            for (int i = 0; i < definitions.Length; i++)
            {
                int type = i;
                definitions[i] = LoadOrCreate<EnemyDefinition>($"{Root}/Data/Enemies/{(EnemyKind)i}.asset", asset => InitEnemy(asset, type));
                enemyPrefabs[i] = CreateEnemyPrefab(definitions[i], lit, windupMaterial, telegraphMaterial, statusRules);
            }
            var playerPrefab = CreatePlayerPrefab(lit, statusRules);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = ObjectFactory.CreateGameObject("World").AddComponent<WorldMap>();
            world.layout = map;
            world.biome = biome;
            world.biomes = new[] { biome,dust,rift };
            var geometry = ObjectFactory.CreateGameObject("ArenaV2").transform;
            world.floorRenderer = Primitive("Floor", PrimitiveType.Cube, geometry, new Vector3(0,-0.2f,0), new Vector3(100,0.4f,70), floorMaterial, true).GetComponent<Renderer>();
            var boundary = ObjectFactory.CreateGameObject("Boundary").transform;
            boundary.SetParent(geometry, false);
            int wallLayer = LayerMask.NameToLayer("Wall");
            Primitive("North", PrimitiveType.Cube, boundary, new Vector3(0,1.5f,35.5f), new Vector3(102,3,1), boundaryMaterial, true).layer = wallLayer;
            Primitive("South", PrimitiveType.Cube, boundary, new Vector3(0,1.5f,-35.5f), new Vector3(102,3,1), boundaryMaterial, true).layer = wallLayer;
            Primitive("East", PrimitiveType.Cube, boundary, new Vector3(50.5f,1.5f,0), new Vector3(1,3,70), boundaryMaterial, true).layer = wallLayer;
            Primitive("West", PrimitiveType.Cube, boundary, new Vector3(-50.5f,1.5f,0), new Vector3(1,3,70), boundaryMaterial, true).layer = wallLayer;
            var walls = ObjectFactory.CreateGameObject("Walls").transform;
            walls.SetParent(geometry, false);
            for (int i = 0; i < map.walls.Length; i++)
            {
                var rect = map.walls[i];
                Primitive("Hull_" + i, PrimitiveType.Cube, walls, new Vector3(rect.center.x,1.5f,rect.center.y), new Vector3(rect.width,3,rect.height), wallMaterial, true).layer = wallLayer;
            }
            var dustWalls = CreateWallGroup("DustWalls", geometry, dustMap, dustWall, wallLayer);
            var riftWalls = CreateWallGroup("RiftWalls", geometry, riftMap, riftWall, wallLayer);
            dustWalls.gameObject.SetActive(false); riftWalls.gameObject.SetActive(false);
            world.biomeWallGroups = new[] { walls,dustWalls,riftWalls };
            world.holes = new[] { CreateHole("BlackHoleLarge", world.transform, new Vector3(-32,0,-20), 7, holeMaterial, gravityMaterial, portalMaterial), CreateHole("BlackHoleSmall", world.transform, new Vector3(32,0,20), 3, holeMaterial, gravityMaterial, portalMaterial) };
            world.temporaryHole = CreateHole("PreparedPocketVoid", world.transform, new Vector3(0,-20,0), 3, holeMaterial, gravityMaterial, portalMaterial);
            world.temporaryHole.gravityCells = 1f;
            world.temporaryHole.gravityVisual.localScale = new Vector3(5f,0.025f,5f);
            world.temporaryHole.gameObject.SetActive(false);
            world.trialTemporaryHole = CreateHole("TrialPocketVoid", world.transform, new Vector3(0,-20,130), 3, holeMaterial, gravityMaterial, portalMaterial);
            world.trialTemporaryHole.gravityCells = 1f;
            world.trialTemporaryHole.gravityVisual.localScale = new Vector3(5f,0.025f,5f);
            world.trialTemporaryHole.gameObject.SetActive(false);
            world.temporaryWalls = new TemporaryWall[8];
            var skillsRoot = ObjectFactory.CreateGameObject("PreparedSkillObjects").transform;
            skillsRoot.SetParent(world.transform, false);
            for (int i = 0; i < world.temporaryWalls.Length; i++)
            {
                var go = Primitive("Bulwark_" + i, PrimitiveType.Cube, skillsRoot, Vector3.down * 20, new Vector3(4,2,1), wallMaterial, true);
                go.layer = wallLayer;
                world.temporaryWalls[i] = go.AddComponent<TemporaryWall>();
                go.SetActive(false);
            }
            world.decoy = Primitive("Decoy", PrimitiveType.Capsule, skillsRoot, Vector3.down * 20, new Vector3(1,1,1), boundaryMaterial, false).transform;
            world.decoy.gameObject.SetActive(false);
            world.trialDecoy = Primitive("TrialDecoy", PrimitiveType.Capsule, skillsRoot, new Vector3(0,-20,130), Vector3.one, boundaryMaterial, false).transform;
            world.trialDecoy.gameObject.SetActive(false);
            var player = ((GameObject)PrefabUtility.InstantiatePrefab(playerPrefab)).GetComponent<PlayerController>();
            player.name = "Player";
            player.transform.position = Vector3.zero;
            var gm = ObjectFactory.CreateGameObject("GameManager").AddComponent<GameManager>();
            var waves = ObjectFactory.CreateGameObject("WaveManager").AddComponent<WaveManager>();
            var spawner = ObjectFactory.CreateGameObject("WeaponSpawner").AddComponent<WeaponSpawner>();
            var hud = ObjectFactory.CreateGameObject("HUD").AddComponent<Hud>();
            var camGo = ObjectFactory.CreateGameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 250f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.025f, 0.035f, 0.07f);
            cam.transform.position = new Vector3(0,46,-15);
            cam.transform.rotation = Quaternion.Euler(74,0,0);
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<CameraRig>();
            rig.target = player.transform;
            rig.camera = cam;
            var lightGo = ObjectFactory.CreateGameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(0.72f,0.84f,1f);
            lightGo.transform.rotation = Quaternion.Euler(55,-30,0);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = biome.ambient;
            gm.player = player; gm.waves = waves; gm.weapons = spawner; gm.hud = hud; gm.cam = cam;
            gm.litMaterial = lit; gm.fxMaterial = transparent; gm.spawnMarkerMaterial = telegraphMaterial;
            gm.art = null;                                                                                   
            waves.v2 = true; waves.waveSet = wavesData; waves.roster = definitions;
            waves.smallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Grunt.prefab").GetComponent<Enemy>();
            waves.largePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Brute.prefab").GetComponent<Enemy>();
            spawner.defs = MakeStarterDefinitions();
            var poolRoot = ObjectFactory.CreateGameObject("PreparedEnemyPool").transform;
            int[] capacities = { 48,24,48,20,16,20,12,16,2,12,2 };
            var pooled = new List<Enemy>();
            for (int type = 0; type < capacities.Length; type++)
            {
                var group = ObjectFactory.CreateGameObject(((EnemyKind)type).ToString()).transform;
                group.SetParent(poolRoot, false);
                for (int index = 0; index < capacities[type]; index++)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/{(EnemyKind)type}.prefab");
                    var enemy = ((GameObject)PrefabUtility.InstantiatePrefab(source)).GetComponent<Enemy>();
                    enemy.name = $"{(EnemyKind)type}_{index:D2}";
                    enemy.transform.SetParent(group, false);
                    enemy.transform.position = Vector3.down * 20f;
                    enemy.gameObject.SetActive(false);
                    pooled.Add(enemy);
                }
            }
            waves.preparedEnemies = pooled.ToArray();
            var markerRoot = ObjectFactory.CreateGameObject("PreparedSpawnMarkers").transform;
            waves.preparedMarkers = new WaveSpawnMarker[48];
            for (int i = 0; i < waves.preparedMarkers.Length; i++)
            {
                var go = ObjectFactory.CreateGameObject("SpawnWarning_" + i);
                go.transform.SetParent(markerRoot, false);
                var marker = go.AddComponent<WaveSpawnMarker>();
                var cross = Primitive("WarningVisual", PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(1.2f,0.015f,1.2f), telegraphMaterial, false);
                marker.visual = cross.GetComponent<Renderer>();
                waves.preparedMarkers[i] = marker;
                go.SetActive(false);
            }
            var boltRoot = ObjectFactory.CreateGameObject("PreparedEnemyBolts").transform;
            waves.preparedBolts = new EnemyBolt[32];
            for (int i = 0; i < waves.preparedBolts.Length; i++)
            {
                var go = Primitive("Spit_" + i, PrimitiveType.Sphere, boltRoot, Vector3.down * 20, Vector3.one * 0.4f, portalMaterial, false);
                waves.preparedBolts[i] = go.AddComponent<EnemyBolt>();
                go.SetActive(false);
            }
            waves.tyrant = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Tyrant.prefab"))).GetComponent<Enemy>();
            waves.tyrant.name = "PreparedTyrant";
            waves.tyrant.gameObject.SetActive(false);
            world.trialPlatform = Primitive("VoidTrialPlatform", PrimitiveType.Cylinder, world.transform, new Vector3(0,-0.3f,130), new Vector3(24,0.3f,24), floorMaterial, true).transform;
            world.hollow = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Hollow.prefab"))).GetComponent<Enemy>();
            world.hollow.name = "PreparedHollow";
            world.hollow.transform.position = new Vector3(0,0,130);
            world.hollow.gameObject.SetActive(false);
            world.trialPlatform.gameObject.SetActive(false);
            var progression = player.gameObject.AddComponent<RunProgression>();
            progression.Rules = FindAsset<ProgressionRules>();
            var skills = player.gameObject.AddComponent<SkillLoadout>(); skills.Catalog = FindAsset<SkillCatalog>();
            var runWeapons = player.gameObject.AddComponent<RunWeapons>();
            runWeapons.roster = LoadOrCreate<WeaponRoster>(Root + "/Data/WeaponRoster.asset", roster => roster.SetDefaults());
            var attackPool = ObjectFactory.CreateGameObject("PreparedAttackPool").AddComponent<AttackPool>();
            attackPool.actors = new AttackActor[256];
            for (int i = 0; i < attackPool.actors.Length; i++)
            {
                var go = Primitive("Attack_" + i, PrimitiveType.Sphere, attackPool.transform, Vector3.down * 20, Vector3.one * 0.25f, boundaryMaterial, false);
                var actor = go.AddComponent<AttackActor>(); actor.visual = go.GetComponent<Renderer>();
                attackPool.actors[i] = actor; go.SetActive(false);
            }
            var orbs = ObjectFactory.CreateGameObject("PreparedOrbPool").AddComponent<OrbPool>();
            orbs.player = player; orbs.progression = progression;
            orbs.xpMaterial = MakeMaterial("XpOrb", lit, new Color(0.24f,0.82f,1f));
            orbs.hpMaterial = MakeMaterial("HpOrb", lit, new Color(0.28f,1f,0.4f));
            orbs.pool = new ExperienceOrb[256];
            for (int i = 0; i < orbs.pool.Length; i++)
            {
                var go = Primitive("XP-HP Orb " + i, PrimitiveType.Sphere, orbs.transform, Vector3.down * 20, Vector3.one * 0.3f, orbs.xpMaterial, true);
                go.isStatic = false; go.GetComponent<Collider>().isTrigger = true;
                var orb = go.AddComponent<ExperienceOrb>(); orb.visual = go.GetComponent<Renderer>();
                orbs.pool[i] = orb; go.SetActive(false);
            }
            gm.runWeapons = runWeapons;
            gm.progression = progression;
            gm.skills = skills;
            gm.orbs = orbs;
            var fxPool = ObjectFactory.CreateGameObject("PreparedFxQuads").AddComponent<FxPool>();
            fxPool.items = new Renderer[256];
            for (int i = 0; i < fxPool.items.Length; i++)
            {
                var go = Primitive("FxQuad_" + i, PrimitiveType.Quad, fxPool.transform, Vector3.down * 20, Vector3.one, transparent, false);
                fxPool.items[i] = go.GetComponent<Renderer>(); go.SetActive(false);
            }
            fxPool.ring = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AnomalyArena/Art/V2/Textures/fx-ring.png");
            fxPool.spark = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AnomalyArena/Art/V2/Textures/fx-spark.png");
            fxPool.sector = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AnomalyArena/Art/V2/Textures/fx-sector.png");
            WireExistingReferences(gm);
            EditorUtility.SetDirty(gm);
            EditorUtility.SetDirty(waves);
            EditorUtility.SetDirty(world);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) { Debug.LogError("[ArenaV2] Scene save failed."); return; }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(ArenaSetup.ScenePath, true) };
            Debug.Log($"[ArenaV2] Saved {ScenePath}; {pooled.Count} inspectable enemy slots, 13 prefab kinds, 15 wave definitions, two black holes.");
        }

        static void WireExistingReferences(GameManager gm)
        {
            var serialized = new SerializedObject(gm);
            var v2 = serialized.FindProperty("useV2"); if (v2 != null) v2.boolValue = true;
            foreach (var property in new[] { "progression", "runProgression" }) { var p = serialized.FindProperty(property); if (p != null) p.objectReferenceValue = gm.player.GetComponent<RunProgression>(); }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static WeaponSpawner.WeaponDef[] MakeStarterDefinitions()
        {
            string effects = "Assets/AnomalyArena/Effects/";
            return new[] {
                new WeaponSpawner.WeaponDef { type=WeaponType.Gun,prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnomalyArena/Prefabs/Gun.prefab").GetComponent<Weapon>(),uses=6,effectA=AssetDatabase.LoadAssetAtPath<WeaponEffect>(effects+"GunChargedShot.asset"),effectB=AssetDatabase.LoadAssetAtPath<WeaponEffect>(effects+"GunReverseShot.asset") },
                new WeaponSpawner.WeaponDef { type=WeaponType.Knife,prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnomalyArena/Prefabs/Knife.prefab").GetComponent<Weapon>(),uses=4,effectA=AssetDatabase.LoadAssetAtPath<WeaponEffect>(effects+"KnifeThrow.asset"),effectB=AssetDatabase.LoadAssetAtPath<WeaponEffect>(effects+"KnifeHook.asset") },
                new WeaponSpawner.WeaponDef { type=WeaponType.Missile,prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnomalyArena/Prefabs/Missile.prefab").GetComponent<Weapon>(),uses=2,effectA=AssetDatabase.LoadAssetAtPath<WeaponEffect>(effects+"MissileStrike.asset"),effectB=AssetDatabase.LoadAssetAtPath<WeaponEffect>(effects+"MissileSelfLaunch.asset") }
            };
        }
        static Enemy CreateEnemyPrefab(EnemyDefinition definition, Material lit, Material windup, Material telegraph, StatusRules statusRules)
        {
            string path = $"{Root}/Prefabs/{definition.kind}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing.GetComponent<Enemy>();
            var go = PrefabUtility.LoadPrefabContents("Assets/AnomalyArena/Prefabs/EnemySmall.prefab");
            go.name = definition.kind.ToString();
            var enemy = go.GetComponent<Enemy>();
            enemy.definition = definition; enemy.pooled = true;
            enemy.maxHp = definition.hp; enemy.moveSpeed = definition.speed; enemy.radius = definition.radius;
            enemy.attackDamage = definition.damage; enemy.windupTime = definition.windup; enemy.attackInterval = definition.interval; enemy.attackRange = definition.reach; enemy.attackKnockback = definition.knockback; enemy.knockbackScale = definition.knockbackScale;
            enemy.normalMaterial = MakeMaterial("Enemy_" + definition.kind, definition.kind == EnemyKind.Shade ? AssetDatabase.LoadAssetAtPath<Material>("Assets/AnomalyArena/Materials/FxTransparent.mat") : lit, definition.colour);
            enemy.windupMaterial = windup;
            enemy.bodyRenderer.sharedMaterial = enemy.normalMaterial;
            float height = definition.radius * 4f;
            var capsule = go.GetComponent<CapsuleCollider>(); capsule.radius = definition.radius; capsule.height = height; capsule.center = Vector3.up * height * 0.5f;
            enemy.bodyRenderer.transform.localPosition = Vector3.up * height * 0.5f;
            enemy.bodyRenderer.transform.localScale = new Vector3(definition.radius * 2, height * 0.5f, definition.radius * 2);
            enemy.telegraph = Primitive("Telegraph", PrimitiveType.Cylinder, go.transform, new Vector3(0,0.03f,0), new Vector3(2,0.025f,2), telegraph, false).transform;
            enemy.telegraph.gameObject.SetActive(false);
            var status = go.GetComponent<ElementStatus>() ?? go.AddComponent<ElementStatus>(); status.Rules = statusRules;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            PrefabUtility.UnloadPrefabContents(go);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Enemy>();
        }
        static GameObject CreatePlayerPrefab(Material lit, StatusRules rules)
        {
            string path = Root + "/Prefabs/PlayerV2.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing;
            var go = PrefabUtility.LoadPrefabContents("Assets/AnomalyArena/Prefabs/Player.prefab");
            go.name = "PlayerV2";
            var player = go.GetComponent<PlayerController>();
            player.bodyRenderer.sharedMaterial = MakeMaterial("Explorer", lit, new Color(0.3f,0.75f,0.9f));
            var status = go.GetComponent<ElementStatus>() ?? go.AddComponent<ElementStatus>(); status.Rules = rules;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            PrefabUtility.UnloadPrefabContents(go);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        static BlackHole CreateHole(string name, Transform parent, Vector3 at, float size, Material kill, Material gravity, Material portal)
        {
            var go = ObjectFactory.CreateGameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = at;
            var hole = go.AddComponent<BlackHole>(); hole.size = size;
            hole.gravityVisual = Primitive("GravityVisual", PrimitiveType.Cube, go.transform, Vector3.up * 0.015f, new Vector3(size+4,0.025f,size+4), gravity, false).transform;
            hole.killVisual = Primitive("KillVisual", PrimitiveType.Cube, go.transform, Vector3.up * 0.045f, new Vector3(size,0.05f,size), kill, false).transform;
            hole.portalVisual = Primitive("PortalVisual", PrimitiveType.Cube, go.transform, Vector3.up * 0.045f, new Vector3(1,0.07f,1), portal, false).transform;
            hole.portalVisual.gameObject.SetActive(false);
            return hole;
        }
        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material material, bool collision)
        {
            var go = GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=at; go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            else go.isStatic=true;
            return go;
        }
        static Transform CreateWallGroup(string name, Transform parent, MapLayout map, Material material, int layer)
        {
            var group = ObjectFactory.CreateGameObject(name).transform;
            group.SetParent(parent,false);
            for (int i=0;i<map.walls.Length;i++)
            {
                var rect=map.walls[i];
                Primitive("Hull_"+i,PrimitiveType.Cube,group,new Vector3(rect.center.x,1.5f,rect.center.y),new Vector3(rect.width,3f,rect.height),material,true).layer=layer;
            }
            return group;
        }
        static Material MakeMaterial(string name, Material source, Color colour)
        {
            string path=$"{Root}/Materials/{name}.mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path); if(existing) return existing;
            var material=source ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=colour; AssetDatabase.CreateAsset(material,path); return material;
        }
        static T LoadOrCreate<T>(string path, Action<T> initialise) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path); if(asset) return asset;
            asset=ScriptableObject.CreateInstance<T>(); initialise(asset); AssetDatabase.CreateAsset(asset,path); return asset;
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static T FindAsset<T>() where T:UnityEngine.Object
        {
            var ids=AssetDatabase.FindAssets("t:"+typeof(T).Name,new[]{"Assets/AnomalyArena"}); return ids.Length>0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(ids[0])) : null;
        }
        static void InitWaves(WaveSet data)
        {
            int[][] bands={ new[]{90,10,0,0,0,0,0,0,0,0,0},new[]{55,10,25,0,0,10,0,0,0,0,0},new[]{32,15,20,15,0,10,5,0,0,0,3},new[]{22,8,18,15,10,10,5,5,0,3,4},new[]{12,10,18,13,10,10,5,10,0,7,5} };
            data.waves=new WaveSet.Definition[15];
            for(int i=0;i<15;i++) data.waves[i]=new WaveSet.Definition{duration=i<5?30:i<10?40:50,aliveTarget=10+2*i,hpMultiplier=1+0.12f*i,buildTime=20+5*(i/2),eliteCount=i<5?0:i<10?1:i==14?3:2,weights=(int[])bands[i<2?0:i<5?1:i<8?2:i<11?3:4].Clone()};
        }
        static void InitEnemy(EnemyDefinition data,int index)
        {
            data.kind=(EnemyKind)index;data.displayName=data.kind.ToString();
            float[] hp={10,30,5,12,25,14,20,18,40,30,6,300,4000};
            float[] speed={5.4f,3.6f,7.5f,4,4,3.5f,4.5f,4.5f,0,3.2f,8,0,3.5f};
            float[] radius={0.5f,1,0.3f,0.5f,0.7f,0.6f,0.5f,0.5f,0.8f,1,0.35f,1.8f,2.2f};
            float[] damage={5,15,3,6,12,12,0,5,8,6,30,5,15};
            float[] windup={0.5f,0.8f,0.3f,0.8f,0.8f,0.4f,0.5f,0.5f,0.8f,1,0.6f,0.8f,1};
            float[] interval={1,2,0.8f,1.5f,3,1,4,1,3,4,1.5f,3,3};
            int[] xp={1,4,1,2,3,2,3,1,5,4,3,0,0};
            data.hp=hp[index];data.speed=speed[index];data.radius=radius[index];data.damage=damage[index];data.windup=windup[index];data.interval=interval[index];data.xp=xp[index];
            data.knockback=index==2?0:index==10?3:index==4||index==9?8:5;
            data.knockbackScale=index==1||index==9?0.5f:index==2||index==10?1.5f:index==4?0.75f:index==8||index>=11?0:1;
            data.canAffix=index!=6&&index!=8&&index!=10&&index<11;data.canElite=index!=8&&index!=10&&index<11;
            data.startShieldShare=index==9?0.5f:index==12?0.1f:0;
            data.colour=Color.HSVToRGB((0.02f+index*0.077f)%1f,0.55f,0.9f);
        }
    }
}
