using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SpriteSheetClipGenerator
{
    // Change these if you want different names
    static readonly string[] DirectionNames8 =
{
    "Down", "DownLeft", "Left", "UpLeft",
    "Up", "UpRight", "Right", "DownRight"
};

    [MenuItem("Tools/Sprites/Generate 8x6 CombatRun Clips (from selected texture)")]
    public static void Generate8x6()
    {
        const int directions = 8;
        const int framesPerDir = 6;
        const float fps = 12f;

        // 1) You must select the spritesheet TEXTURE in the Project window
        var tex = Selection.activeObject as Texture2D;
        if (!tex)
        {
            EditorUtility.DisplayDialog(
                "Select spritesheet texture",
                "In the Project window, click your spritesheet Texture2D (the image), then run:\nTools > Sprites > Generate 8x6 CombatRun Clips",
                "OK"
            );
            return;
        }

        string texPath = AssetDatabase.GetAssetPath(tex);

        // 2) Load all sprites sliced from that texture
        var sprites = AssetDatabase.LoadAllAssetsAtPath(texPath)
            .OfType<Sprite>()
            .ToList();

        if (sprites.Count < directions * framesPerDir)
        {
            EditorUtility.DisplayDialog(
                "Not enough sprites",
                $"Found {sprites.Count} sprites, but need at least {directions * framesPerDir} (8 directions × 6 frames).\n\nCheck that Sprite Mode = Multiple and slicing is correct.",
                "OK"
            );
            return;
        }

        // 3) Sort by texture position:
        // - Higher Y first (top rows first)
        // - Lower X first (left to right)
        sprites.Sort((a, b) =>
        {
            var ar = a.rect;
            var br = b.rect;

            int y = br.y.CompareTo(ar.y); // descending Y
            if (y != 0) return y;

            return ar.x.CompareTo(br.x);  // ascending X
        });

        // 4) Make output folder next to the texture (or inside Animations)
        string baseDir = System.IO.Path.GetDirectoryName(texPath)?.Replace("\\", "/") ?? "Assets";
        string outFolder = $"{baseDir}/Animations_Generated";
        if (!AssetDatabase.IsValidFolder(outFolder))
        {
            AssetDatabase.CreateFolder(baseDir, "Animations_Generated");
        }

        // 5) Create clips
        for (int d = 0; d < directions; d++)
        {
            string dirName = (d < DirectionNames8.Length) ? DirectionNames8[d] : $"Dir{d}";
            string clipPath = $"{outFolder}/CombatRun_{dirName}.anim";

            var clip = new AnimationClip
            {
                frameRate = fps
            };

            // Turn on looping
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            // Frames for this direction
            int start = d * framesPerDir;
            var frames = sprites.Skip(start).Take(framesPerDir).ToArray();

            // Build keyframes for SpriteRenderer.sprite
            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "", // same GameObject
                propertyName = "m_Sprite"
            };

            var keys = new ObjectReferenceKeyframe[framesPerDir];
            for (int i = 0; i < framesPerDir; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / fps,
                    value = frames[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            // Save asset
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Done ✅",
            $"Created 8 CombatRun clips (6 frames each) in:\n{outFolder}\n\nIf the directions are in the wrong order, tell me how your sheet is laid out and I’ll adjust the direction mapping.",
            "OK"
        );
    }
}