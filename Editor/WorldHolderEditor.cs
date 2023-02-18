using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Wargon.Ecsape.Tween;

namespace Wargon.Ecsape.Editor {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(WorldView))]
    public class WorldHolderEditor : UpdatableEditor {
        private bool archetypesFold;
        private bool entitiesFold;
        private GUIStyle eStyle;
        private bool migrationsFold;

        private Action<WorldHolder> onTargetChanged;
        private bool poolsFold;
        private WorldHolder realTarget;
        private VisualElement rootContainer;
        private World world;

        private WorldHolder RealTarget {
            get {
                realTarget = target as WorldHolder;
                return realTarget;
            }
            set {
                onTargetChanged.Invoke(value);
                realTarget = value;
            }
        }

        private void Awake() { }

        public void OnDestroy() {
            Inspectors.Clear();
            ComponentInspectors.Clear();
        }

        public override bool RequiresConstantRepaint() {
            return true;
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            // eStyle = new GUIStyle(GUI.skin.box);
            // var texture = new Texture2D(2, 2);
            // for (int i = 0; i < 2; i++) {
            //     for (int j = 0; j < 2; j++) {
            //         texture.SetPixel(i,j, Color.red);
            //     }
            // }
            //eStyle.normal.background = texture;
            var worldHolder = (WorldView) target;
            if (worldHolder.WorldHolder == null) return;
            world = worldHolder.WorldHolder.World;
            if (world != null) {
                if (GUILayout.Button("Archetypes"))
                    archetypesFold = !archetypesFold;
                if (archetypesFold)
                    foreach (var archetype in world.ArchetypesInternal()) {
                        EditorGUILayout.BeginVertical();
                        GUILayout.Label($"{archetype}");
                        EditorGUILayout.EndVertical();
                    }

                if (GUILayout.Button("Pools"))
                    poolsFold = !poolsFold;
                if (poolsFold) {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    var pools = world.PoolsInternal();
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.BeginHorizontal();
                    //GUILayout.Box($"Pool<{pool.Info.Type.Name}> Count:({pool.Count}) Capacity:({pool.Capacity}) IsTag:({pool.Info.IsTag})");

                    var skin = new GUIStyle(GUI.skin.button);
                    skin.fixedWidth = 220f;
                    GUILayout.Button("     Pool        ", skin);
                    GUILayout.Button("     Count:      ", skin);
                    GUILayout.Button("     Capacity:   ", skin);
                    GUILayout.Button("     IsTag:      ", skin);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    for (var i = 0; i < world.PoolsCountInternal(); i++) {
                        var pool = pools[i];
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                        //GUILayout.Box($"Pool<{pool.Info.Type.Name}> Count:({pool.Count}) Capacity:({pool.Capacity}) IsTag:({pool.Info.IsTag})");
                        GUILayout.Button($"{pool.Info.Name}", skin);
                        GUILayout.Button($"{pool.Count}", skin);
                        GUILayout.Button($"{pool.Capacity}", skin);
                        GUILayout.Button($"{pool.Info.IsTag}", skin);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                    }

                    EditorGUILayout.EndVertical();
                }

                if (GUILayout.Button("Migrations"))
                    migrationsFold = !migrationsFold;

                if (migrationsFold) {
                    var migrations = world.MigrationsInternal();

                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.BeginHorizontal();
                    //GUILayout.Box($"Pool<{pool.Info.Type.Name}> Count:({pool.Count}) Capacity:({pool.Capacity}) IsTag:({pool.Info.IsTag})");

                    var skin = new GUIStyle(GUI.skin.button);
                    skin.fixedWidth = 220f;
                    skin.normal.background = GUI.skin.box.normal.background;
                    GUILayout.Button("     Archetype        ", skin);
                    GUILayout.Button("     Component:      ", skin);
                    GUILayout.Button("     Key:   ", skin);
                    GUILayout.Button("     IsEmpty:      ", skin);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();

                    foreach (var migration in migrations) {
                        var (archetype, componentType, key, isEmpty) = migration.GetData();
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label($"{archetype}", skin);
                        GUILayout.Button($"{componentType.Name}", skin);
                        GUILayout.Button($"{key}", skin);
                        GUILayout.Button($"{isEmpty}", skin);
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndVertical();
                }

                if (GUILayout.Button("Entities")) entitiesFold = !entitiesFold;
                var eskin = new GUIStyle(GUI.skin.button);
                eskin.fixedWidth = 120f;
                var xskin = new GUIStyle(GUI.skin.button);
                xskin.fixedWidth = 30f;
                if (entitiesFold) {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    var entities = GetEntities();
                    for (var i = 0; i < entities.Length; i++) {
                        ref var e = ref entities[i];
                        var componentsAmount = e.ComponentsAmount();
                        if (e.IsNull() || componentsAmount == 0 || e.Index == 0) continue;
                        var archetype = e.GetArchetype();
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                        var components = archetype.GetComponents(in e);

                        if (GUILayout.Button($"E.{e.Index}.{componentsAmount}", eskin))
                            if (e.Has<View>()) {
                                EditorGUIUtility.PingObject(e.Get<View>().GameObject);
                                e.doScale(1, 2, 0.1f).WithLoop(2, LoopType.Yoyo)
                                    .WithEasing(Easings.EasingType.BounceEaseIn);
                            }

                        if (GUILayout.Button("X", xskin)) e.Destroy();
                        foreach (var component in components)
                            if (GUILayout.Button($"{component.GetType().Name}")) { }

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                    }

                    EditorGUILayout.EndVertical();
                }
            }
        }

        private Foldout archetypesFoldout;
        public override VisualElement CreateInspectorGUI() {
            var worldView = target as WorldView;
            var world = worldView.WorldHolder.World;
            
            rootContainer = new VisualElement();
            if (world == null) return rootContainer;
            archetypesFoldout = new Foldout();

            archetypesFoldout.text = "Archetypes";
            var archetypesList = world.ArchetypesInternal();

            var listView = new ListView();
            listView.itemsSource = archetypesList;
            Func<VisualElement> makeItem = () => {
                var btn = new Label();
                return btn;
            };
            
            Action<VisualElement, int> bindItem = (e, i) => {
                e.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
                var components = archetypesList[i].GetComponentTypes();
                var label = new Button();
                foreach (var componentType in components) {
                    if (ComponentEditor.TryGetColor(componentType.Name, out var color)) {
                        label.style.backgroundColor = color;
                        Debug.Log(componentType.Name);
                    }
                    label.text = componentType.Name + archetypesList[i].id;
                }
                e.Add(label);
            };
            listView.makeItem = makeItem;
            listView.bindItem = bindItem;
            archetypesFoldout.Add(listView);
            var newFold = new Foldout();
            newFold.text = "Entities";
            var label = new Label("hello there");
            newFold.Add(label);
            rootContainer.Add(listView);
            rootContainer.Add(newFold);
            return rootContainer;
        }

        protected override void OnUpdate() {
            var s = target as WorldView;
            if (s.WorldHolder == null) return;
        }

        private Entity[] GetEntities() {
            return world.EntitiesInternal();
        }
    }


}