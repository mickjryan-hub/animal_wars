using System.IO;
using System.Linq;
using AnimalWars;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalWars.EditorTools
{
    public static class BuildKangarooScene
    {
        const string ModelPath = "Assets/Characters/Kangaroo/Walking.fbx";
        const string DefaultPath = "Assets/Characters/Kangaroo/blender-current-mixamo.fbx";
        const string ControllerPath = "Assets/Characters/Kangaroo/Kangaroo.controller";
        const string ScenePath = "Assets/Scenes/Walk.unity";
        const float TargetHeight = 1.55f;

        public static void Build()
        {
            var log = new System.Text.StringBuilder();
            try
            {
                ConfigureModel(log);
                var clip = FindWalkClip();
                log.AppendLine("clip " + (clip != null ? clip.name + " " + clip.length + "s" : "MISSING"));
                var controller = CreateController(clip);
                BuildScene(controller, log);
                log.AppendLine("scene saved");
            }
            catch (System.Exception ex)
            {
                log.AppendLine("FAILED " + ex);
                Debug.LogException(ex);
            }

            File.WriteAllText("/tmp/kangaroo-setup.log", log.ToString());
            Debug.Log(log.ToString());
        }

        static void ConfigureModel(System.Text.StringBuilder log)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            // Generic keeps the Mixamo skeleton as authored. Humanoid was lifting the
            // walk off the ground and dropping the rest pose into the floor.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;
            log.AppendLine("takes " + (clips == null ? 0 : clips.Length));
            if (clips != null)
            {
                foreach (var clip in clips)
                {
                    log.AppendLine("take " + clip.name + " / " + clip.takeName + " " + clip.firstFrame + "-" + clip.lastFrame);
                    clip.name = "Walk";
                    clip.loopTime = true;
                }
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            log.AppendLine(avatar == null ? "avatar missing" : "avatar valid=" + avatar.isValid + " human=" + avatar.isHuman);
        }

        static AnimationClip FindWalkClip()
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__"));
        }

        static AnimatorController CreateController(AnimationClip walk)
        {
            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle");
            var walking = machine.AddState("Walk");
            walking.motion = walk;
            machine.defaultState = idle;

            var toWalk = idle.AddTransition(walking);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var toIdle = walking.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            return controller;
        }

        static void BuildScene(RuntimeAnimatorController controller, System.Text.StringBuilder log)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var groundMat = MakeMaterial("Assets/Materials/Ground.mat", new Color(0.34f, 0.52f, 0.27f));
            var dirtMat = MakeMaterial("Assets/Materials/Dirt.mat", new Color(0.45f, 0.34f, 0.22f));
            var rockMat = MakeMaterial("Assets/Materials/Rock.mat", new Color(0.45f, 0.46f, 0.44f));
            var trunkMat = MakeMaterial("Assets/Materials/Trunk.mat", new Color(0.36f, 0.24f, 0.14f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(12f, 1f, 12f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            Place(PrimitiveType.Cylinder, "Log", new Vector3(4f, 0.25f, 6f), new Vector3(3.2f, 0.25f, 0.4f), dirtMat, new Vector3(0f, 20f, 90f));
            Place(PrimitiveType.Cube, "Rock A", new Vector3(-5f, 0.4f, 3f), new Vector3(1.4f, 0.8f, 1.1f), rockMat, Vector3.zero);
            Place(PrimitiveType.Cube, "Rock B", new Vector3(7f, 0.55f, -4f), new Vector3(1.8f, 1.1f, 1.5f), rockMat, Vector3.zero);
            Place(PrimitiveType.Cylinder, "Tree", new Vector3(-6f, 1.6f, -7f), new Vector3(0.6f, 1.6f, 0.6f), trunkMat, Vector3.zero);
            Place(PrimitiveType.Sphere, "Canopy", new Vector3(-6f, 3.6f, -7f), new Vector3(2.4f, 1.6f, 2.4f), groundMat, Vector3.zero);

            PlaceOnGround(DefaultPath, "Kangaroo Default", new Vector3(-2.2f, 0f, 0f), log);

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var player = new GameObject("Kangaroo");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            model.name = "Model";
            model.transform.SetParent(player.transform, false);
            FixModelMaterials(model, log);

            var bounds = WorldBounds(model);
            log.AppendLine("imported height " + bounds.size.y);
            if (bounds.size.y > 0.01f)
                model.transform.localScale = Vector3.one * (TargetHeight / bounds.size.y);
            bounds = WorldBounds(model);
            player.transform.position = new Vector3(0f, -bounds.min.y, 0f);
            bounds = WorldBounds(model);
            log.AppendLine("placed height " + bounds.size.y + " feet " + bounds.min.y);

            var animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var body = player.AddComponent<CharacterController>();
            var center = player.transform.InverseTransformPoint(bounds.center);
            body.center = center;
            body.height = Mathf.Max(bounds.size.y, 0.5f);
            body.radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.45f, 0.15f, body.height * 0.35f);
            body.stepOffset = 0.3f;
            player.AddComponent<KangarooPlayer>().animator = animator;

            int playerLayer = LayerMask.NameToLayer("Ignore Raycast");
            SetLayer(player, playerLayer);

            var camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 2f, -4f), Quaternion.identity);
            var follow = camera.gameObject.AddComponent<ThirdPersonCamera>();
            follow.target = player.transform;
            follow.pivotHeight = bounds.size.y * 0.72f;
            follow.distance = Mathf.Max(3.2f, bounds.size.y * 2.6f);
            follow.collisionMask = ~(1 << playerLayer);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.8f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.28f, 0.2f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            var list = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (list != null)
                EditorSceneManager.playModeStartScene = null;
            AssetDatabase.SaveAssets();
        }

        static void FixModelMaterials(GameObject model, System.Text.StringBuilder log)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                for (var i = 0; i < shared.Length; i++)
                {
                    var source = shared[i];
                    if (source == null)
                        continue;
                    var shaderName = source.shader != null ? source.shader.name : "";
                    if (shaderName.Contains("Universal Render Pipeline"))
                        continue;
                    var texture = source.mainTexture;
                    if (texture == null && source.HasProperty("_BaseMap"))
                        texture = source.GetTexture("_BaseMap");
                    log.AppendLine("material " + source.name + " shader " + shaderName + " tex " + (texture != null ? texture.name : "none"));
                    var urp = new Material(shader);
                    if (texture != null)
                        urp.SetTexture("_BaseMap", texture);
                    var path = "Assets/Materials/" + renderer.gameObject.name + "_" + source.name.Replace(" ", "_") + "_URP.mat";
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (existing != null)
                        AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(urp, path);
                    shared[i] = urp;
                }
                renderer.sharedMaterials = shared;
            }
        }

        static void PlaceOnGround(string assetPath, string name, Vector3 where, System.Text.StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                log.AppendLine("missing " + assetPath);
                return;
            }

            var root = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            FixModelMaterials(model, log);
            var bounds = WorldBounds(model);
            if (bounds.size.y > 0.01f)
                model.transform.localScale = Vector3.one * (TargetHeight / bounds.size.y);
            bounds = WorldBounds(model);
            root.transform.position = new Vector3(where.x, -bounds.min.y, where.z);
            bounds = WorldBounds(model);
            log.AppendLine(name + " feet " + bounds.min.y + " height " + bounds.size.y);
        }

        static Bounds WorldBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static Material MakeMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void Place(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Vector3 euler)
        {
            var prop = GameObject.CreatePrimitive(type);
            prop.name = name;
            prop.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            prop.transform.localScale = scale;
            prop.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void SetLayer(GameObject root, int layer)
        {
            if (layer < 0)
                return;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                transform.gameObject.layer = layer;
        }
    }
}
