using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SpriteSheetClipGeneratorWindow : EditorWindow
{
    private static readonly string[] DefaultDirectionNames8 =
    {
        "Down", "DownLeft", "Left", "UpLeft",
        "Up", "UpRight", "Right", "DownRight"
    };

    private Texture2D selectedTexture;
    private string clipBaseName = "Weapon2-Attack3";
    private int directions = 8;
    private int framesPerDirection = 6;
    private float fps = 12f;
    private bool loop = true;

    [MenuItem("Tools/Sprites/Clip Generator")]
    public static void OpenWindow()
    {
        GetWindow<SpriteSheetClipGeneratorWindow>("Clip Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Sprite Sheet Clip Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        selectedTexture = (Texture2D)EditorGUILayout.ObjectField(
            "Spritesheet Texture",
            selectedTexture,
            typeof(Texture2D),
            false
        );

        clipBaseName = EditorGUILayout.TextField("Clip Base Name", clipBaseName);
        directions = EditorGUILayout.IntField("Directions", directions);
        framesPerDirection = EditorGUILayout.IntField("Frames Per Direction", framesPerDirection);
        fps = EditorGUILayout.FloatField("FPS", fps);
        loop = EditorGUILayout.Toggle("Loop", loop);

        EditorGUILayout.Space();

        if (GUILayout.Button("Use Currently Selected Texture"))
        {
            selectedTexture = Selection.activeObject as Texture2D;
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Clips"))
        {
            GenerateClips();
        }
    }

    private void GenerateClips()
    {
        if (!selectedTexture)
        {
            EditorUtility.DisplayDialog(
                "Missing texture",
                "Please assign a spritesheet Texture2D or select one in the Project window.",
                "OK"
            );
            return;
        }

        if (directions <= 0 || framesPerDirection <= 0 || fps <= 0f)
        {
            EditorUtility.DisplayDialog(
                "Invalid values",
                "Directions, Frames Per Direction, and FPS must be greater than 0.",
                "OK"
            );
            return;
        }

        string texPath = AssetDatabase.GetAssetPath(selectedTexture);

        List<Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(texPath)
            .OfType<Sprite>()
            .ToList();

        int requiredCount = directions * framesPerDirection;

        if (sprites.Count < requiredCount)
        {
            EditorUtility.DisplayDialog(
                "Not enough sprites",
                $"Found {sprites.Count} sprites, but need at least {requiredCount}.\n\nCheck Sprite Mode = Multiple and slicing.",
                "OK"
            );
            return;
        }

        sprites.Sort((a, b) =>
        {
            Rect ar = a.rect;
            Rect br = b.rect;

            int yCompare = br.y.CompareTo(ar.y);
            if (yCompare != 0) return yCompare;

            return ar.x.CompareTo(br.x);
        });

        string baseDir = System.IO.Path.GetDirectoryName(texPath)?.Replace("\\", "/") ?? "Assets";
        string outFolder = $"{baseDir}/Animations_Generated";

        if (!AssetDatabase.IsValidFolder(outFolder))
        {
            AssetDatabase.CreateFolder(baseDir, "Animations_Generated");
        }

        for (int d = 0; d < directions; d++)
        {
            string dirName = GetDirectionName(d, directions);
            string clipPath = $"{outFolder}/{clipBaseName}_{dirName}.anim";

            AnimationClip clip = new AnimationClip
            {
                frameRate = fps
            };

            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            int start = d * framesPerDirection;
            Sprite[] frames = sprites.Skip(start).Take(framesPerDirection).ToArray();

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[framesPerDirection];

            for (int i = 0; i < framesPerDirection; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / fps,
                    value = frames[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(clipPath));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Done ✅",
            $"Created {directions} clips in:\n{outFolder}",
            "OK"
        );
    }

    private string GetDirectionName(int index, int totalDirections)
    {
        if (totalDirections == 8 && index < DefaultDirectionNames8.Length)
            return DefaultDirectionNames8[index];

        return $"Dir{index}";
    }
}