using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GameVSM.Editor
{
    public static class BuildPassengers
    {
        const string Root = "Assets/GameVSM/Art/Characters/";
        [MenuItem("GameVSM/Art/Build animated passenger")]
        public static void Build()
        {
            foreach (string name in new[] { "Passenger", "PassengerWoman", "DepotWorker" }) BuildOne(name);
        }
        static void BuildOne(string name)
        {
            AssetDatabase.ImportAsset(Root + name + ".glb", ImportAssetOptions.ForceSynchronousImport);
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(Root + name + ".glb");
            if (imported == null) throw new InvalidOperationException("Passenger GLB must be generated first.");
            var clips = AssetDatabase.LoadAllAssetsAtPath(Root + name + ".glb").OfType<AnimationClip>().ToArray();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + name + ".controller");
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(Root + name + ".controller");
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            // PassengerAnimation owns playback on both LOD skeletons. Transitions here
            // would otherwise fight its starts, stops, turns and action states.
            string[] looping = { "Idle", "Walk", "CarryWalk", "Seated", "Phone", "Talk", "Work", "UnwellSeated" };
            foreach (string clipName in new[] { "Idle", "Walk", "CarryWalk", "StartWalk", "StopWalk", "StopWalkRight",
                "TurnLeft", "TurnRight", "SitDown", "StandUp", "Seated", "Phone", "Talk", "Work", "UnwellSeated" })
            {
                var clip = clips.Single(c => c.name == clipName);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = looping.Contains(clipName); AnimationUtility.SetAnimationClipSettings(clip, settings);
                var state = machine.AddState(clipName); state.motion = clip; state.writeDefaultValues = false;
            }
            var idle = machine.states.Single(s => s.state.name == "Idle").state;
            machine.defaultState = idle;
            var go = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(imported);
            model.transform.SetParent(go.transform, false);
            // glTFast's coordinate conversion maps the source forward to Unity +Z.
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponentInChildren<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var motion = go.AddComponent<PassengerMotion>(); motion.Animator = animator;
            var capsule = go.AddComponent<CapsuleCollider>(); capsule.radius = .23f; capsule.height = 1.68f;
            capsule.center = new Vector3(0, .84f, 0);
            var renderers = model.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = false;
            }
            var reduced = AssetDatabase.LoadAssetAtPath<GameObject>(Root + name + "-LOD.glb");
            if (reduced == null) throw new InvalidOperationException("Generate character LODs first.");
            var lower = (GameObject)PrefabUtility.InstantiatePrefab(reduced);
            lower.transform.SetParent(go.transform, false);
            var lowerAnimator = lower.GetComponentInChildren<Animator>();
            lowerAnimator.runtimeAnimatorController = controller;
            lowerAnimator.applyRootMotion = false;
            lowerAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            motion.DetailAnimators = new[] { animator, lowerAnimator };
            var animation = go.AddComponent<PassengerAnimation>(); animation.Animators = motion.DetailAnimators;
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(.12f, renderers), new LOD(.018f, lower.GetComponentsInChildren<Renderer>()) });
            lod.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(go, Root + name + ".prefab");
            if (name == "Passenger")
            {
                AddSuitcase(go);
                PrefabUtility.SaveAsPrefabAsset(go, Root + "PassengerWithBag.prefab");
            }
            UnityEngine.Object.DestroyImmediate(go);
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            Debug.Log("GAMEVSM_PASSENGER_READY " + name + " clips=" + string.Join(",", clips.Select(c => c.name)));
        }

        static void AddSuitcase(GameObject person)
        {
            var bag = LuggageVisual.Create(person.transform, LuggageKind.RollingCase);
            bag.transform.localPosition = new Vector3(.39f, 0, .02f);
            PersistLuggageAssets(bag);
            // PassengerAnimation uses Grip and the finger bones to solve hand contact.
            // No second AnimationRigging owner may overwrite that pose.
        }

        static void PersistLuggageAssets(LuggageVisual luggage)
        {
            const string directory = "Assets/GameVSM/Generated/Luggage";
            Directory.CreateDirectory(directory); AssetDatabase.Refresh();
            foreach (var filter in luggage.GetComponentsInChildren<MeshFilter>(true))
                filter.sharedMesh = Persist(filter.sharedMesh, directory + "/" + filter.sharedMesh.name + ".asset");
            foreach (var renderer in luggage.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    Persist(material, directory + "/" + material.name + ".mat")).ToArray();
        }
        static T Persist<T>(T source, string path) where T : UnityEngine.Object
        {
            if (AssetDatabase.Contains(source)) return source;
            var saved = AssetDatabase.LoadAssetAtPath<T>(path);
            if (saved == null) { AssetDatabase.CreateAsset(source, path); return source; }
            EditorUtility.CopySerialized(source, saved); EditorUtility.SetDirty(saved); return saved;
        }
    }
}
