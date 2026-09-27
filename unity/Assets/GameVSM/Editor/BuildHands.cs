using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameVSM.Editor
{
    public static class BuildHands
    {
        public static void Apply()
        {
            var player=UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            var old=player.View.transform.Find("Руки проводника");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameVSM/Art/Characters/DepotWorker.glb");
            var go=UnityEngine.Object.Instantiate(source,player.View.transform,false);go.name="Руки проводника";
            go.transform.localPosition=new Vector3(0,-1.55f,-.12f);
            var anim=go.GetComponentInChildren<Animator>();
            var idle=AssetDatabase.LoadAllAssetsAtPath("Assets/GameVSM/Art/Characters/DepotWorker.glb").OfType<AnimationClip>().First(c=>c.name=="Idle");
            idle.SampleAnimation(anim.gameObject,0);UnityEngine.Object.DestroyImmediate(anim);
            int index=0;
            foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=UnityEngine.Object.Instantiate(r.sharedMesh);
                var weights=mesh.boneWeights;var selected=new bool[r.bones.Length];
                for(int b=0;b<selected.Length;b++)selected[b]=r.bones[b].name.Contains("Arm")||r.bones[b].name.Contains("Hand");
                bool Arm(int v){var w=weights[v];return (selected[w.boneIndex0]?w.weight0:0)+(selected[w.boneIndex1]?w.weight1:0)+(selected[w.boneIndex2]?w.weight2:0)+(selected[w.boneIndex3]?w.weight3:0)>.7f;}
                int count=0;
                for(int s=0;s<mesh.subMeshCount;s++)
                {
                    var triangles=mesh.GetTriangles(s);var kept=new List<int>();
                    for(int t=0;t<triangles.Length;t+=3)if(Arm(triangles[t])&&Arm(triangles[t+1])&&Arm(triangles[t+2]))
                    {kept.Add(triangles[t]);kept.Add(triangles[t+1]);kept.Add(triangles[t+2]);}
                    mesh.SetTriangles(kept,s);count+=kept.Count;
                }
                if(count==0){UnityEngine.Object.DestroyImmediate(r);UnityEngine.Object.DestroyImmediate(mesh);continue;}
                string path="Assets/GameVSM/Generated/Hands"+(index++)+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
                r.sharedMesh=mesh;r.updateWhenOffscreen=true;r.shadowCastingMode=ShadowCastingMode.Off;r.enabled=false;
            }
            var hands=go.AddComponent<FirstPersonHands>();var transforms=go.GetComponentsInChildren<Transform>();
            Transform Bone(string name)=>transforms.First(t=>t.name.EndsWith(":"+name)||t.name.EndsWith("_"+name));
            hands.LeftArm=Bone("LeftArm");hands.LeftElbow=Bone("LeftForeArm");hands.LeftHand=Bone("LeftHand");
            hands.RightArm=Bone("RightArm");hands.RightElbow=Bone("RightForeArm");hands.RightHand=Bone("RightHand");
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }
    }
}
