using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FlightLab
{
    public static class F22CityBuilder
    {
        public const string Folder = "Assets/FlightLab/RaptorValley";
        public const string ScenePath = Folder + "/RaptorValley.unity";
        public const string ModelPath = "Assets/Models/F22/F22-Raptor.glb";
        public static string DiagnosticsFolder
        {
            get
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/FlightLabValidation"));
                Directory.CreateDirectory(path);
                return path;
            }
        }
        private static string Work => DiagnosticsFolder;
        private static System.Random random;
        private static int meshId;

        [MenuItem("Flight Lab/Open Raptor Valley")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building the scene.");
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("The F-22 GLB importer is not ready yet.");
            if (AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Raptor Valley already exists. Open it instead of overwriting it.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene current = SceneManager.GetSceneAt(i);
                if (current.isDirty)
                {
                    string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/FlightLab/BeforeRaptorUpgrade.unity");
                    EditorSceneManager.SaveScene(current, backup, true);
                }
            }
            AssetDatabase.CreateFolder("Assets/FlightLab", "RaptorValley");
            AssetDatabase.CreateFolder(Folder, "Materials");
            AssetDatabase.CreateFolder(Folder, "Meshes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            random = new System.Random(2206); meshId = 0;
            var ground = MakeTerrain();
            Material runway = Material("Asphalt", new Color(0.105f, 0.12f, 0.135f));
            Material concrete = Material("Concrete", new Color(0.47f, 0.49f, 0.46f));
            Material white = Material("Runway paint", new Color(0.91f, 0.91f, 0.84f));
            Material yellow = Material("Taxiway paint", new Color(0.97f, 0.68f, 0.18f));
            Material roof = Material("Roof", new Color(0.29f, 0.34f, 0.38f));
            Material trunk = Material("Tree trunks", new Color(0.22f, 0.16f, 0.105f));
            Material[] leaves = {
                Material("Pine deep", new Color(0.13f, 0.25f, 0.17f)),
                Material("Pine green", new Color(0.2f, 0.34f, 0.21f)),
                Material("Pine sage", new Color(0.29f, 0.39f, 0.23f)) };

            var airport = new GameObject("Airport / runway 36").transform;
            Box("3000 m runway", airport, new Vector3(0, 0.01f, 500), new Vector3(58, 0.22f, 3000), runway);
            Box("Apron", airport, new Vector3(-175, -0.01f, 50), new Vector3(240, 0.18f, 1600), concrete);
            Box("Parallel taxiway", airport, new Vector3(-95, 0.005f, 500), new Vector3(25, 0.2f, 3000), runway);
            var paint = new Geometry();
            for (int z = -970; z < 2000; z += 55) paint.Box(new Vector3(0, 0.135f, z), new Vector3(1.3f, 0.025f, 27));
            paint.Box(new Vector3(-27, 0.135f, 500), new Vector3(0.7f, 0.025f, 2960));
            paint.Box(new Vector3(27, 0.135f, 500), new Vector3(0.7f, 0.025f, 2960));
            for (int x = -23; x <= 23; x += 4)
            { paint.Box(new Vector3(x, 0.14f, -940), new Vector3(2, 0.03f, 35)); paint.Box(new Vector3(x, 0.14f, 1940), new Vector3(2, 0.03f, 35)); }
            for (int sign = -1; sign <= 1; sign += 2)
                for (int z = -700; z < 100; z += 150) paint.Box(new Vector3(sign * 14, 0.14f, z), new Vector3(7, 0.03f, 35));
            paint.Save("Runway markings", airport, white);
            var taxiPaint = new Geometry(); taxiPaint.Box(new Vector3(-95, 0.125f, 500), new Vector3(0.4f, 0.025f, 3000));
            taxiPaint.Save("Taxiway centerline", airport, yellow);
            Material lightMat = Material("Runway edge lights", new Color(0.8f, 0.89f, 1));
            lightMat.EnableKeyword("_EMISSION"); lightMat.SetColor("_EmissionColor", new Color(0.5f, 0.72f, 1f) * 2);
            var edge = new Geometry();
            for (int z = -1000; z <= 2000; z += 60)
                foreach (int sign in new[] { -1, 1 }) edge.Box(new Vector3(sign * 31, 0.38f, z), new Vector3(0.35f, 0.5f, 0.35f));
            edge.Save("Runway lights", airport, lightMat);
            for (int i = 0; i < 7; i++)
            {
                float z = -600 + i * 220;
                Box("Hangar " + (i + 1), airport, new Vector3(-240, 13, z), new Vector3(95, 26, 90), concrete);
                Box("Hangar roof", airport, new Vector3(-240, 27, z), new Vector3(101, 2, 96), roof, false);
                Box("Hangar door", airport, new Vector3(-191.9f, 10, z), new Vector3(0.25f, 20, 70), roof, false);
            }
            Box("Control tower shaft", airport, new Vector3(-320, 28, -740), new Vector3(12, 56, 12), concrete);
            Box("Control tower cab", airport, new Vector3(-320, 59, -740), new Vector3(26, 10, 24), roof);

            Texture2D facade = FacadeTexture();
            Material[] facades = new Material[6];
            Color[] tones = { new Color(0.8f, 0.86f, 0.9f), new Color(0.94f, 0.87f, 0.75f),
                new Color(0.67f, 0.77f, 0.83f), new Color(0.92f, 0.91f, 0.88f), new Color(0.7f, 0.72f, 0.73f), new Color(0.79f, 0.67f, 0.57f) };
            for (int i = 0; i < facades.Length; i++)
            {
                facades[i] = Material("City facade " + i, tones[i]); facades[i].mainTexture = facade;
                facades[i].SetFloat("_Smoothness", 0.38f);
            }
            var city = new GameObject("City / streets and neighborhoods").transform;
            var foliagePositions = new List<Vector3>();
            int buildingCount = CityDistrict(city, 500, -700, 10, 13, 1, facades, roof, runway, concrete, white, foliagePositions);
            buildingCount += CityDistrict(city, -550, -500, 7, 11, -1, facades, roof, runway, concrete, white, foliagePositions);
            for (int i = 0; i < 1000; i++)
            {
                float angle = Range(0, Mathf.PI * 2), radius = Range(3150, 6200);
                float x = Mathf.Cos(angle) * radius, z = Mathf.Sin(angle) * radius + 700;
                foliagePositions.Add(new Vector3(x, Height(x, z), z));
            }
            for (int z = -900; z < 2100; z += 85)
            { foliagePositions.Add(new Vector3(155 + Range(-15, 15), 0, z)); }
            BuildTrees(foliagePositions, trunk, leaves);

            GameObject plane = MakeAircraft(model);
            SetupLightingAndCamera(plane);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var existing = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Select(s => new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(existing).ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = plane;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(plane.transform.position, Quaternion.Euler(18, 150, 0), 55);
            File.WriteAllText(Path.Combine(Work, "build-summary.txt"),
                $"Scene: {ScenePath}\nBuildings: {buildingCount}\nTrees: {foliagePositions.Count}\nTerrain: 16 x 16 km\nRunway: 3000 m\n");
        }

        private static GameObject MakeAircraft(GameObject model)
        {
            GameObject root = new GameObject("Aircraft - F-22 Raptor");
            root.transform.position = new Vector3(0, 2.28f, -850);
            root.AddComponent<Rigidbody>().mass = 29000f;
            var input = root.AddComponent<FlightInput>(); input.startingThrottle = 0;
            var flight = root.AddComponent<AirplanePhysics>(); flight.startingAirspeed = 0;
            var rig = root.AddComponent<LandingGearRig>(); flight.landingGear = rig;
            GameObject visuals = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visuals.name = "F-22 / original 4k model";
            visuals.transform.SetParent(root.transform, false);
            visuals.transform.localRotation = Quaternion.Euler(0, 90, 0) * model.transform.localRotation;
            visuals.transform.localScale = Vector3.one * 0.1f;
            visuals.transform.localPosition = new Vector3(0, 0, -4.0448f);
            foreach (var rb in visuals.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var c in visuals.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            rig.extendedVisuals = visuals.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("F-22-landingOn", StringComparison.Ordinal)).Select(t => t.gameObject).ToArray();
            rig.retractedVisuals = visuals.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("F-22-landingOff", StringComparison.Ordinal)).Select(t => t.gameObject).ToArray();
            BoxCollider fuselage = root.AddComponent<BoxCollider>();
            fuselage.center = new Vector3(0, 0.3f, 0); fuselage.size = new Vector3(2.6f, 1.4f, 14.5f);
            var wings = new GameObject("Wing collision envelope"); wings.transform.SetParent(root.transform, false);
            BoxCollider wing = wings.AddComponent<BoxCollider>(); wing.center = new Vector3(0, 0.15f, -1.8f); wing.size = new Vector3(12f, 0.4f, 4.5f);
            rig.noseWheel = Wheel(root.transform, "Nose wheel", new Vector3(0, -1.48f, 4.55f), 0.5f);
            rig.mainWheels = new[] { Wheel(root.transform, "Left main wheel", new Vector3(-2.43f, -1.48f, -1.95f), 0.5f),
                Wheel(root.transform, "Right main wheel", new Vector3(2.43f, -1.48f, -1.95f), 0.5f) };
            rig.ResetGear();
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/F22-Player.prefab");
            return root;
        }

        private static WheelCollider Wheel(Transform root, string name, Vector3 position, float radius)
        {
            var obj = new GameObject(name); obj.transform.SetParent(root, false); obj.transform.localPosition = position;
            var wheel = obj.AddComponent<WheelCollider>(); wheel.radius = radius; wheel.mass = 120;
            wheel.suspensionDistance = 0.48f; wheel.forceAppPointDistance = 0.5f;
            wheel.suspensionSpring = new JointSpring { spring = 700000, damper = 105000, targetPosition = 0.55f };
            WheelFrictionCurve forward = wheel.forwardFriction; forward.stiffness = 1.1f; wheel.forwardFriction = forward;
            WheelFrictionCurve sideways = wheel.sidewaysFriction; sideways.stiffness = 1.8f; wheel.sidewaysFriction = sideways;
            wheel.ConfigureVehicleSubsteps(10, 5, 8); return wheel;
        }

        private static void SetupLightingAndCamera(GameObject plane)
        {
            var sunObject = new GameObject("Late afternoon sun", typeof(Light));
            Light sun = sunObject.GetComponent<Light>(); sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.82f); sun.intensity = 2.3f; sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(32, -25, 0);
            RenderSettings.sun = sun;
            Material sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetColor("_SkyTint", new Color(0.53f, 0.62f, 0.72f)); sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetFloat("_Exposure", 1.15f); sky.SetFloat("_SunSize", 0.025f);
            AssetDatabase.CreateAsset(sky, Folder + "/Materials/Valley sky.mat"); RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.51f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.26f, 0.2f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.67f, 0.76f, 0.83f); RenderSettings.fogDensity = 0.000075f;
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>(); camera.fieldOfView = 62; camera.farClipPlane = 18000; camera.nearClipPlane = 0.15f;
            cameraObject.transform.position = plane.transform.position + new Vector3(0, 10, -33);
            cameraObject.transform.LookAt(plane.transform.position + Vector3.forward * 12);
            cameraObject.AddComponent<ChaseCamera>().target = plane.transform;
            var hud = cameraObject.AddComponent<FlightHUD>(); hud.aircraft = plane.GetComponent<AirplanePhysics>(); hud.controls = plane.GetComponent<FlightInput>();
            var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
            var volumeObject = new GameObject("Valley atmosphere", typeof(Volume));
            var volume = volumeObject.GetComponent<Volume>(); volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var colors = profile.Add<ColorAdjustments>(true); colors.postExposure.Override(0.3f); colors.contrast.Override(8f); colors.saturation.Override(-6f);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.12f); bloom.threshold.Override(1.4f);
            AssetDatabase.CreateAsset(profile, Folder + "/Atmosphere.asset");
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            volume.sharedProfile = profile;
            var probeObject = new GameObject("Airport reflections", typeof(ReflectionProbe));
            probeObject.transform.position = new Vector3(0, 40, -700);
            var probe = probeObject.GetComponent<ReflectionProbe>(); probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake; probe.resolution = 128; probe.size = Vector3.one * 10000; probe.farClipPlane = 10000;
        }

        private static Terrain MakeTerrain()
        {
            var data = new TerrainData { heightmapResolution = 513, size = new Vector3(16000, 2400, 16000), alphamapResolution = 256, baseMapResolution = 1024 };
            var heights = new float[513, 513];
            for (int z = 0; z < 513; z++) for (int x = 0; x < 513; x++) heights[z, x] = Height(-8000 + x * 31.25f, -7000 + z * 31.25f) / 2400f;
            data.SetHeights(0, 0, heights);
            Color[] colors = { new Color(0.29f, 0.38f, 0.24f), new Color(0.42f, 0.40f, 0.34f), new Color(0.8f, 0.82f, 0.81f) };
            var layers = new TerrainLayer[3];
            for (int i = 0; i < 3; i++)
            {
                var texture = new Texture2D(128, 128, TextureFormat.RGB24, true); texture.name = "Terrain grain " + i;
                var pixels = new Color[128 * 128];
                for (int p = 0; p < pixels.Length; p++) pixels[p] = colors[i] * Range(0.82f, 1.17f);
                texture.SetPixels(pixels); texture.Apply(); texture.wrapMode = TextureWrapMode.Repeat;
                AssetDatabase.CreateAsset(texture, Folder + "/Materials/TerrainTexture" + i + ".asset");
                layers[i] = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(28, 28), smoothness = 0.05f };
                AssetDatabase.CreateAsset(layers[i], Folder + "/Materials/TerrainLayer" + i + ".terrainlayer");
            }
            data.terrainLayers = layers;
            var alpha = new float[256, 256, 3];
            for (int z = 0; z < 256; z++) for (int x = 0; x < 256; x++)
            {
                float h = Height(-8000 + x * 16000f / 255, -7000 + z * 16000f / 255);
                float rock = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(550, 1300, h));
                float snow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1600, 2050, h));
                alpha[z, x, 0] = 1 - rock; alpha[z, x, 1] = rock * (1 - snow); alpha[z, x, 2] = rock * snow;
            }
            data.SetAlphamaps(0, 0, alpha);
            AssetDatabase.CreateAsset(data, Folder + "/ValleyTerrain.asset");
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data); terrainObject.name = "Valley terrain / mountains";
            terrainObject.transform.position = new Vector3(-8000, -0.1f, -7000);
            Terrain terrain = terrainObject.GetComponent<Terrain>(); terrain.heightmapPixelError = 8; terrain.basemapDistance = 3000;
            var terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            AssetDatabase.CreateAsset(terrainMaterial, Folder + "/Materials/Terrain.mat"); terrain.materialTemplate = terrainMaterial;
            return terrain;
        }

        private static float Height(float x, float z)
        {
            float radius = new Vector2(x, z - 700).magnitude;
            float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3300, 4700, radius));
            float ridge = 1 - Mathf.Abs(Mathf.PerlinNoise(x * 0.00038f + 70, z * 0.00038f + 30) * 2 - 1);
            float detail = Mathf.PerlinNoise(x * 0.0013f + 12, z * 0.0013f + 16);
            float peaks = 950 * Mathf.Exp(-((x - 4500) * (x - 4500) + (z - 4700) * (z - 4700)) / 3500000f) +
                          1050 * Mathf.Exp(-((x + 4800) * (x + 4800) + (z - 3500) * (z - 3500)) / 2800000f);
            return Mathf.Min(2250, edge * (250 + 850 * ridge * ridge + detail * 260 + peaks));
        }

        private static int CityDistrict(Transform parent, float startX, float startZ, int columns, int rows, int direction,
            Material[] facades, Material roof, Material asphalt, Material sidewalk, Material paint, List<Vector3> trees)
        {
            var walls = facades.Select(_ => new Geometry()).ToArray(); var roofs = new Geometry(); var streets = new Geometry(); var pavements = new Geometry(); var lines = new Geometry();
            const float spacing = 190, block = 154; int count = 0;
            float endX = startX + direction * columns * spacing;
            for (int x = 0; x <= columns; x++) streets.Box(new Vector3(startX + direction * x * spacing, -0.04f, startZ + rows * spacing / 2), new Vector3(27, 0.06f, rows * spacing + 27));
            for (int z = 0; z <= rows; z++) streets.Box(new Vector3((startX + endX) / 2, -0.039f, startZ + z * spacing), new Vector3(columns * spacing + 27, 0.06f, 27));
            var collision = new GameObject("Building colliders").transform; collision.SetParent(parent, false);
            for (int col = 0; col < columns; col++) for (int row = 0; row < rows; row++)
            {
                float cx = startX + direction * (col + 0.5f) * spacing, cz = startZ + (row + 0.5f) * spacing;
                bool park = (col * 7 + row * 3) % 13 == 0;
                if (!park) pavements.Box(new Vector3(cx, -0.04f, cz), new Vector3(block + 8, 0.1f, block + 8));
                for (int t = 0; t < (park ? 18 : 5); t++)
                    trees.Add(new Vector3(cx + Range(-72, 72), 0, cz + (park ? Range(-72, 72) : (t % 2 == 0 ? -71 : 71))));
                if (park) continue;
                float downtown = direction > 0 ? Mathf.Exp(-((cx - 1700) * (cx - 1700) + (cz - 1050) * (cz - 1050)) / 460000f) : 0;
                for (int b = 0; b < 3; b++)
                {
                    float bx = cx + (b == 0 ? -37 : 35), bz = cz + (b == 2 ? 37 : -34);
                    float w = Range(38, 61), depth = Range(38, 62), h = Range(16, 62) + downtown * Range(90, 210);
                    int style = random.Next(facades.Length);
                    Vector3 center = new Vector3(bx, h / 2, bz), size = new Vector3(w, h, depth);
                    walls[style].Box(center, size, true, false);
                    roofs.Box(new Vector3(bx, h + 0.3f, bz), new Vector3(w + 0.5f, 0.6f, depth + 0.5f));
                    roofs.Box(new Vector3(bx + w * 0.12f, h + 2, bz), new Vector3(w * 0.32f, 3, depth * 0.26f));
                    var colliderObject = new GameObject("Building " + count++); colliderObject.transform.SetParent(collision, false);
                    colliderObject.transform.localPosition = center; colliderObject.AddComponent<BoxCollider>().size = size;
                }
            }
            for (int c = 0; c <= columns; c++) for (float z = startZ + 35; z < startZ + rows * spacing; z += 22)
                if ((z - startZ) % spacing > 22 && (z - startZ) % spacing < 168)
                    lines.Box(new Vector3(startX + direction * c * spacing, 0.007f, z), new Vector3(0.35f, 0.012f, 9));
            for (int i = 0; i < walls.Length; i++) walls[i].Save("Facades " + direction + " " + i, parent, facades[i]);
            roofs.Save("Rooftops " + direction, parent, roof); streets.Save("Streets " + direction, parent, asphalt);
            pavements.Save("Sidewalks " + direction, parent, sidewalk); lines.Save("Road markings " + direction, parent, paint);
            return count;
        }

        private static void BuildTrees(List<Vector3> positions, Material trunk, Material[] foliage)
        {
            var parent = new GameObject("Trees / parks and mountain forests").transform;
            for (int first = 0; first < positions.Count; first += 160)
            {
                var trunks = new Geometry(); var leaves = foliage.Select(_ => new Geometry()).ToArray();
                for (int i = first; i < Mathf.Min(first + 160, positions.Count); i++)
                {
                    Vector3 p = positions[i]; float h = Range(9, 22), radius = h * Range(0.2f, 0.3f);
                    trunks.Box(p + Vector3.up * h * 0.2f, new Vector3(0.6f, h * 0.4f, 0.6f));
                    Geometry batch = leaves[random.Next(leaves.Length)];
                    batch.Cone(p + Vector3.up * h * 0.2f, radius, h * 0.55f);
                    batch.Cone(p + Vector3.up * h * 0.44f, radius * 0.78f, h * 0.56f);
                }
                trunks.Save("Trunks " + first, parent, trunk);
                for (int i = 0; i < leaves.Length; i++) leaves[i].Save("Foliage " + first + " " + i, parent, foliage[i]);
            }
        }

        private static Texture2D FacadeTexture()
        {
            var texture = new Texture2D(128, 128, TextureFormat.RGB24, true);
            var colors = new Color[128 * 128];
            for (int row = 0; row < 8; row++) for (int col = 0; col < 8; col++)
            {
                Color glass = Color.Lerp(new Color(0.23f, 0.34f, 0.4f), new Color(0.65f, 0.77f, 0.83f), Range(0, 1));
                if (random.NextDouble() < 0.07) glass = new Color(0.85f, 0.73f, 0.5f);
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    bool window = x >= 3 && x <= 12 && y >= 4 && y <= 12;
                    colors[(row * 16 + y) * 128 + col * 16 + x] = window ? glass : new Color(0.48f, 0.49f, 0.48f);
                }
            }
            texture.SetPixels(colors); texture.Apply(); texture.wrapMode = TextureWrapMode.Repeat; texture.anisoLevel = 8;
            AssetDatabase.CreateAsset(texture, Folder + "/Materials/FacadeWindows.asset"); return texture;
        }

        private static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color, enableInstancing = true };
            material.SetFloat("_Smoothness", 0.16f); AssetDatabase.CreateAsset(material, Folder + "/Materials/" + name + ".mat"); return material;
        }
        private static float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collide = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent, false);
            box.transform.localPosition = position; box.transform.localScale = size; box.GetComponent<Renderer>().sharedMaterial = material;
            if (!collide) Object.DestroyImmediate(box.GetComponent<Collider>()); return box;
        }

        public static void InspectModel()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null) throw new InvalidOperationException("GLB not imported yet");
            GameObject obj = Object.Instantiate(asset); obj.hideFlags = HideFlags.HideAndDontSave;
            var lines = obj.GetComponentsInChildren<Renderer>(true).Select(r => r.name + ": bounds=" + r.bounds + " shader=" + r.sharedMaterial.shader.name);
            File.WriteAllText(Path.Combine(Work, "model-inspection.txt"), string.Join("\n", lines)); Object.DestroyImmediate(obj);
        }

        [MenuItem("Flight Lab/Capture Raptor Valley previews")]
        public static void Capture()
        {
            var plane = Object.FindAnyObjectByType<AirplanePhysics>();
            Camera camera = Camera.main;
            if (plane == null || camera == null) throw new InvalidOperationException("Open Raptor Valley first.");
            if (EditorApplication.isPlaying) ScreenCapture.CaptureScreenshot(Path.Combine(Work, "game-view.png"));
            Vector3 oldPosition = camera.transform.position; Quaternion oldRotation = camera.transform.rotation;
            CaptureCamera(camera, Path.Combine(Work, "runway-preview.png"));
            camera.transform.position = plane.transform.position + new Vector3(28, 11, 25);
            camera.transform.LookAt(plane.transform.position + Vector3.up);
            CaptureCamera(camera, Path.Combine(Work, "f22-preview.png"));
            camera.transform.position = new Vector3(-1800, 1450, -2100); camera.transform.LookAt(new Vector3(800, 100, 700));
            CaptureCamera(camera, Path.Combine(Work, "valley-preview.png"));
            camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
        }
        public static void CaptureCamera(Camera camera, string path)
        {
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32); rt.Create();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture old = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); RenderTexture.active = old; Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt);
        }
        [MenuItem("Flight Lab/Run flight checks")]
        public static void StartValidation() { RaptorValidation.Start(); }

        private class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u = 1, float v = 1)
            {
                int start = vertices.Count; vertices.AddRange(new[] { a, b, c, d });
                uv.AddRange(new[] { Vector2.zero, new Vector2(u, 0), new Vector2(u, v), new Vector2(0, v) });
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            public void Box(Vector3 center, Vector3 size, bool windows = false, bool top = true)
            {
                Vector3 lo = center - size / 2, hi = center + size / 2;
                float u = windows ? size.x / 32 : 1, z = windows ? size.z / 32 : 1, v = windows ? size.y / 32 : 1;
                Quad(new Vector3(lo.x,lo.y,hi.z),new Vector3(hi.x,lo.y,hi.z),new Vector3(hi.x,hi.y,hi.z),new Vector3(lo.x,hi.y,hi.z),u,v);
                Quad(new Vector3(hi.x,lo.y,lo.z),new Vector3(lo.x,lo.y,lo.z),new Vector3(lo.x,hi.y,lo.z),new Vector3(hi.x,hi.y,lo.z),u,v);
                Quad(new Vector3(hi.x,lo.y,hi.z),new Vector3(hi.x,lo.y,lo.z),new Vector3(hi.x,hi.y,lo.z),new Vector3(hi.x,hi.y,hi.z),z,v);
                Quad(new Vector3(lo.x,lo.y,lo.z),new Vector3(lo.x,lo.y,hi.z),new Vector3(lo.x,hi.y,hi.z),new Vector3(lo.x,hi.y,lo.z),z,v);
                if (top) Quad(new Vector3(lo.x,hi.y,hi.z),new Vector3(hi.x,hi.y,hi.z),new Vector3(hi.x,hi.y,lo.z),new Vector3(lo.x,hi.y,lo.z));
            }
            public void Cone(Vector3 basePosition, float radius, float height)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4, b = (i + 1) * Mathf.PI / 4;
                    Vector3 p = basePosition + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                    Vector3 q = basePosition + new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius);
                    Quad(q, p, basePosition + Vector3.up * height, basePosition + Vector3.up * height);
                }
            }
            public void Save(string name, Transform parent, Material material)
            {
                if (vertices.Count == 0) return;
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, Folder + "/Meshes/mesh-" + meshId++ + ".asset");
                var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(parent, false);
                obj.GetComponent<MeshFilter>().sharedMesh = mesh; obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
    }
}
