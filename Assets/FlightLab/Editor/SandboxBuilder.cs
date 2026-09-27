using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlightLab
{
    public static class SandboxBuilder
    {
        [MenuItem("Flight Lab/Create Flight Sandbox")]
        public static void CreateSandbox()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogWarning("Stop Play mode before creating a sandbox."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            CreateForBatch();
        }

        // Also usable from Unity -batchmode -executeMethod FlightLab.SandboxBuilder.CreateForBatch.
        public static void CreateForBatch()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/FlightLab/Generated");
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
            Material grass = Material(folder, "Grass", new Color(0.23f, 0.36f, 0.22f));
            Material asphalt = Material(folder, "Runway", new Color(0.1f, 0.12f, 0.14f));
            Material white = Material(folder, "Ivory", new Color(0.88f, 0.91f, 0.9f));
            Material orange = Material(folder, "Orange", new Color(0.97f, 0.31f, 0.08f));
            Material blue = Material(folder, "Canopy", new Color(0.1f, 0.25f, 0.34f));
            Material landmark = Material(folder, "Landmarks", new Color(0.36f, 0.41f, 0.39f));

            GameObject environment = new GameObject("Environment");
            Cube("Ground", environment.transform, new Vector3(0, -1, 500), new Vector3(4000, 2, 4000), grass);
            Cube("Runway", environment.transform, new Vector3(0, 0.06f, 300), new Vector3(40, 0.12f, 600), asphalt);
            for (int i = 0; i < 12; i++)
            { Cube("Runway marking", environment.transform, new Vector3(0, 0.13f, 20 + i * 48), new Vector3(1, 0.02f, 20), white, false); }
            System.Random random = new System.Random(17);
            for (int i = 0; i < 24; i++)
            {
                float x = (i % 2 == 0 ? 1f : -1f) * (120f + (float)random.NextDouble() * 1100f);
                float z = -400f + (float)random.NextDouble() * 2200f;
                float h = 10f + (float)random.NextDouble() * 80f;
                Cube("Landmark " + (i + 1), environment.transform, new Vector3(x, h / 2f, z),
                    new Vector3(35f, h, 35f), landmark);
            }

            GameObject plane = new GameObject("Aircraft");
            plane.transform.SetPositionAndRotation(new Vector3(0, 150, -200), Quaternion.Euler(-4, 0, 0));
            plane.AddComponent<Rigidbody>();
            FlightInput input = plane.AddComponent<FlightInput>();
            AirplanePhysics physics = plane.AddComponent<AirplanePhysics>();
            Transform visuals = new GameObject("Visuals").transform;
            visuals.SetParent(plane.transform, false);
            Cube("Fuselage", visuals, Vector3.zero, new Vector3(0.9f, 0.9f, 6), white);
            Cube("Wing", visuals, new Vector3(0, 0, 0.35f), new Vector3(10.5f, 0.18f, 1.45f), orange);
            Cube("Tailplane", visuals, new Vector3(0, 0.25f, -2.4f), new Vector3(3.2f, 0.15f, 0.9f), white);
            Cube("Fin", visuals, new Vector3(0, 0.8f, -2.4f), new Vector3(0.15f, 1.5f, 0.9f), orange);
            Cube("Canopy", visuals, new Vector3(0, 0.6f, 1), new Vector3(0.75f, 0.55f, 1.5f), blue, false);
            Cube("Nose marker", visuals, new Vector3(0, 0, 3.1f), new Vector3(0.5f, 0.5f, 0.3f), orange, false);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.53f, 0.72f, 0.87f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 3500f;
            camera.fieldOfView = 65f;
            cameraObject.transform.position = plane.transform.position + new Vector3(0, 6, -19);
            cameraObject.transform.LookAt(plane.transform.position + Vector3.forward * 8);
            cameraObject.AddComponent<ChaseCamera>().target = plane.transform;
            FlightHUD hud = cameraObject.AddComponent<FlightHUD>();
            hud.aircraft = physics;
            hud.controls = input;

            GameObject sun = new GameObject("Sun", typeof(Light));
            Light light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.67f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogDensity = 0.00065f;
            EditorSceneManager.SaveScene(scene, folder + "/FlightSandbox.unity");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = plane;
            Debug.Log("Flight Lab created: " + folder + "/FlightSandbox.unity. Press Play, then click Game view.");
        }

        private static Material Material(string folder, string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Smoothness")) { material.SetFloat("_Smoothness", 0.25f); }
            AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat");
            return material;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 position,
            Vector3 scale, Material material, bool collision = true)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) { Object.DestroyImmediate(cube.GetComponent<Collider>()); }
            return cube;
        }
    }
}
