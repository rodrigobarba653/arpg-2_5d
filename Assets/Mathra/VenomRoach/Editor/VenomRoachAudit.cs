using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Mathra.Enemies.Editor
{
    // Samples the actual controller on a disposable, hidden prefab instance.
    internal static class VenomRoachAudit
    {
        [MenuItem("Tools/Mathra/Venom Roach/Check prefab and animation")]
        static void Check()
        {
            const string root="Assets/Mathra/VenomRoach/";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(root+"VenomRoach.prefab");
            GameObject copy=null;
            try
            {
                Need(prefab,"Prefab missing from "+root);
                copy=UnityEngine.Object.Instantiate(prefab);
                copy.hideFlags=HideFlags.HideAndDontSave;
                var enemy=copy.GetComponent<VenomRoach>();
                Need(enemy,"Missing VenomRoach component");
                var fields=new SerializedObject(enemy);
                var body=fields.FindProperty("body").objectReferenceValue as SpriteRenderer;
                var animator=fields.FindProperty("spriteAnimator").objectReferenceValue as Animator;
                var mat=fields.FindProperty("gasMaterial").objectReferenceValue as Material;
                Need(body && animator && body.gameObject==animator.gameObject,"Body/Animator binding");
                Need(mat && mat.shader && mat.shader.isSupported && !ShaderUtil.ShaderHasError(mat.shader),"Gas material or shader");
                var controller=animator.runtimeAnimatorController as AnimatorController;
                Need(controller && controller.layers.Length==1,"Missing base layer");
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                string[] dirs={"N","NE","E","SE","S","SW","W","NW"};
                int sampled=0;
                foreach(string action in new[]{"Walk","Spit","Death"})
                for(int d=0;d<dirs.Length;d++)
                {
                    string name=action+"_"+dirs[d];
                    int hash=Animator.StringToHash("Base Layer."+name);
                    Need(animator.HasState(0,hash),"Missing state "+name);
                    int count=action=="Walk"?8:action=="Spit"?18:16;
                    for(int f=0;f<count;f++)
                    {
                        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(root+"Frames/"+action+"_"+d+"_"+f+".png");
                        Need(sprite && sprite.rect.size==new Vector2(64,64),"Missing or mis-sized sprite "+name+" "+f);
                        animator.Play(hash,0,(f+.1f)/count);
                        animator.Update(0);
                        Need(body.sprite==sprite,"Wrong sampled sprite in "+name+" at frame "+f);
                        sampled++;
                    }
                }
                Debug.Log("Venom Roach passed: prefab, gas shader, 24 Animator states and "+sampled+" sampled sprite keys.",prefab);
            }
            catch(Exception ex){Debug.LogError("Venom Roach check failed: "+ex.Message,prefab);}
            finally{if(copy)UnityEngine.Object.DestroyImmediate(copy);}
        }
        static void Need(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
    }
}
