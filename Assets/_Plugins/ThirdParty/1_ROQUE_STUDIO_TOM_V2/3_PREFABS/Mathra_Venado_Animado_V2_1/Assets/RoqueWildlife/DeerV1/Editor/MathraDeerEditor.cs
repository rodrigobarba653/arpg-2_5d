using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

namespace Roque.Wildlife.Editor
{
    [CustomEditor(typeof(MathraDeer))]
    public sealed class MathraDeerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Play Mode: prueba las tres animaciones con estos botones. Arrastra Ax/Jyn a Threats para activar el susto por proximidad.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                MathraDeer deer = (MathraDeer)target;
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Beber")) deer.DrinkNow();
                if (GUILayout.Button("Observar")) deer.LookNow();
                if (GUILayout.Button("Asustar")) deer.ScareNow();
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("Volver al punto inicial")) deer.ResetDeer();
            }
        }

        private const string Root = "Assets/RoqueWildlife/DeerV1";
        private const string ReadyMarker = "MathraDeerV21NativeValidated";
        private static bool repairing;
        private static readonly string[] ClipNames = { "Drinking", "Looking", "Running", "Alert", "RaiseHead", "LowerHead", "StartRun" };

        [InitializeOnLoadMethod]
        private static void QueueV2BindingSetup()
        {
            EditorApplication.delayCall += SetupV2BindingsOnce;
        }

        private static void SetupV2BindingsOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += SetupV2BindingsOnce;
                return;
            }
            AssetImporter importer = AssetImporter.GetAtPath(Root + "/Animations/MathraDeer.controller");
            if (!importer || importer.userData == ReadyMarker || repairing) return;
            RebuildBindings();
        }

        [MenuItem("Roque/Wildlife/Deer/Select drinking prefab")]
        private static void SelectPrefab()
        {
            Object prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Venado_Bebiendo.prefab");
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        [MenuItem("Roque/Wildlife/Deer/Create isolated preview scene")]
        private static void PreviewScene()
        {
            if (Application.isPlaying) return;
            // Additive: existing user scenes stay open and are not edited.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = "Mathra Deer Preview (unsaved)";
            SceneManager.SetActiveScene(scene);
            var camObject = new GameObject("Deer Preview Camera");
            var cam = camObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.09f, 0.105f);
            cam.orthographic = true;
            cam.orthographicSize = 2.5f;
            cam.depth = 100f;
            cam.transform.position = new Vector3(0f, 2f, -8f);
            cam.transform.LookAt(new Vector3(0f, 0.9f, 0f));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Venado_Bebiendo.prefab");
            if (!prefab) { Debug.LogError("Deer prefab not found in " + Root); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var deer = instance.GetComponent<MathraDeer>();
            deer.viewCamera = cam;
            deer.followGround = false;
            deer.avoidObstacles = false;
            deer.runInPlace = true;
            deer.detectNearbyPlayers = false;
            Selection.activeGameObject = instance;
            if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("Preview ready. Enter Play and use Beber / Observar / Asustar in the inspector. The preview runs in place. Close this additive scene when finished.");
        }

        [MenuItem("Roque/Wildlife/Deer/Rebuild animation bindings")]
        [MenuItem("Roque/Wildlife/Deer/Repair NONE keyframes and prefabs")]
        private static void RebuildBindings()
        {
            if (repairing || EditorApplication.isPlayingOrWillChangePlaymode) return;
            repairing = true;
            try
            {
            PrepareSprites();
            BuildClip("Drinking", new[] {0,1,2,3,2,1}, new[] {0.22f,0.18f,0.22f,0.2f,0.18f,0.2f}, true);
            BuildClip("Looking", new[] {4,28,5,29,6,29,5,28,4,7,4}, new[] {0.65f,0.1f,0.4f,0.1f,0.55f,0.1f,0.35f,0.1f,0.6f,0.2f,0.4f}, true);
            BuildClip("Running", new[] {8,16,9,21,10,18,11,20,12,23,13,17,14,22,15,19}, new[] {0.045f,0.045f,0.045f,0.045f,0.045f,0.045f,0.045f,0.045f,0.045f,0.045f,0.045f,0.045f,0.045f,0.045f,0.045f,0.045f}, true);
            BuildClip("Alert", new[] {7}, new[] {1f}, false);
            BuildClip("RaiseHead", new[] {0,24,25,26,27,4}, new[] {0.04f,0.08f,0.1f,0.1f,0.1f,0.06f}, false);
            BuildClip("LowerHead", new[] {4,27,26,25,24,0}, new[] {0.04f,0.1f,0.1f,0.1f,0.08f,0.06f}, false);
            BuildClip("StartRun", new[] {7,30,31,8}, new[] {0.04f,0.08f,0.08f,0.04f}, false);
            AnimatorController controller = RebuildController();
            RepairPrefabs(controller);
            AssetDatabase.SaveAssets();
            ValidateAllKeys();
            AssetImporter importer = AssetImporter.GetAtPath(Root + "/Animations/MathraDeer.controller");
            importer.userData = ReadyMarker;
            importer.SaveAndReimport();
            Debug.Log("Mathra Deer V2.1: 7 clips validated, zero NONE sprite keyframes. Controller and 3 prefab references repaired.");
            }
            catch (System.Exception error)
            {
                Debug.LogError("Mathra Deer repair did not complete: " + error.Message + "\nRun Roque > Wildlife > Deer > Repair NONE keyframes and prefabs after imports finish.");
            }
            finally { repairing = false; }
        }

        private static Sprite LoadSprite(int index)
        {
            string path = Root + "/Sprites/Deer_" + index.ToString("D2") + ".png";
            // Resolve the imported Sprite object, never an assumed numeric fileID.
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) return sprite;
            throw new System.InvalidOperationException("No imported Sprite found: " + path);
        }

        private static void PrepareSprites()
        {
            for (int i = 0; i < 32; i++)
            {
                string path = Root + "/Sprites/Deer_" + i.ToString("D2") + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) throw new System.InvalidOperationException("Missing texture: " + path);
                if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 192f;
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(0.5f, 0.0625f);
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                LoadSprite(i);
            }
        }

        private static AnimatorController RebuildController()
        {
            string path = Root + "/Animations/MathraDeer.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) throw new System.InvalidOperationException("Cannot load controller: " + path);
            var machine = new AnimatorStateMachine { name = "Base Layer" };
            AssetDatabase.AddObjectToAsset(machine, controller);
            foreach (string name in ClipNames)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Animations/" + name + ".anim");
                if (!clip) throw new System.InvalidOperationException("Missing clip: " + name);
                AnimatorState state = machine.AddState(name);
                state.motion = clip;
                state.writeDefaultValues = false;
                if (name == "Drinking") machine.defaultState = state;
            }
            controller.layers = new[] { new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = machine } };
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void RepairPrefabs(AnimatorController controller)
        {
            foreach (string name in new[] { "Venado_Bebiendo", "Venado_Observando", "Venado_Huyendo" })
            {
                string path = Root + "/Prefabs/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var deer = root.GetComponent<MathraDeer>();
                    var renderer = root.GetComponentInChildren<SpriteRenderer>(true);
                    if (!deer || !renderer) throw new System.InvalidOperationException("Missing deer or renderer in " + name);
                    var animator = renderer.GetComponent<Animator>();
                    if (!animator) animator = renderer.gameObject.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.enabled = true;
                    renderer.sprite = LoadSprite(deer.startBehaviour == MathraDeer.Behaviour.Drinking ? 0 : 4);
                    renderer.enabled = true;
                    deer.animator = animator;
                    deer.spriteRenderer = renderer;
                    deer.visual = renderer.transform;
                    deer.fallbackSprite = renderer.sprite;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void ValidateAllKeys()
        {
            foreach (string name in ClipNames)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Animations/" + name + ".anim");
                var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys == null || keys.Length == 0) throw new System.InvalidOperationException("Empty clip: " + name);
                foreach (var key in keys)
                    if (!(key.value is Sprite)) throw new System.InvalidOperationException("NONE keyframe in " + name + " at " + key.time);
            }
        }

        private static void BuildClip(string clipName, int[] frames, float[] durations, bool loop)
        {
            string path = Root + "/Animations/" + clipName + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            float time = 0f;
            for (int i = 0; i < frames.Length; i++)
            {
                Sprite sprite = LoadSprite(frames[i]);
                keys[i] = new ObjectReferenceKeyframe { time = time, value = sprite };
                time += durations[i];
            }
            keys[frames.Length] = new ObjectReferenceKeyframe { time = time, value = loop ? keys[0].value : keys[frames.Length-1].value };
            clip.ClearCurves();
            clip.legacy = false;
            clip.frameRate = 200f;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.startTime = 0f;
            settings.stopTime = time;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }
    }
}
