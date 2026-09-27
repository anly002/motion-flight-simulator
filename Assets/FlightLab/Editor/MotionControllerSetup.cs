using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FlightLab
{
    public static class MotionControllerSetup
    {
        [MenuItem("Flight Lab/Configure MPU-6050 controller")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before configuring hardware.");
            var plane = UnityEngine.Object.FindAnyObjectByType<AirplanePhysics>();
            if (plane == null) throw new InvalidOperationException("Open the Raptor Valley scene first.");
            var controller = plane.GetComponent<SerialMotionController>();
            if (controller == null) controller = Undo.AddComponent<SerialMotionController>(plane.gameObject);
            Undo.RecordObject(plane.GetComponent<FlightInput>(), "Enable motion control");
            plane.GetComponent<FlightInput>().source = FlightInput.InputSource.Motion;
            EditorSceneManager.MarkSceneDirty(plane.gameObject.scene);
            EditorSceneManager.SaveScene(plane.gameObject.scene);
            string prefabPath = F22CityBuilder.Folder + "/F22-Player.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (prefab.GetComponent<SerialMotionController>() == null) prefab.AddComponent<SerialMotionController>();
                prefab.GetComponent<FlightInput>().source = FlightInput.InputSource.Motion;
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            Selection.activeGameObject = plane.gameObject;
        }
    }
}
