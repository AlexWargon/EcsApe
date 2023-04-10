using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Wargon.Ecsape.Editor {
    public abstract class UpdatableEditor : UnityEditor.Editor {
        private DateTime timeFromLastFrame;
        private TimeSpan timeSpan;
        public const int msLock = 60;
        public float deltaTime;
        private void OnEnable() {
            EditorApplication.update += Update;
        }

        private void OnDisable() {
            EditorApplication.update -= Update;
        }

        private void Update() {
            timeSpan = DateTime.Now - timeFromLastFrame;
            if (timeSpan.Milliseconds <= msLock) return;
            OnUpdate();
            deltaTime = (float)timeSpan.TotalSeconds;
            timeFromLastFrame = DateTime.Now;
        }

        protected abstract void OnUpdate();
    }
    [CustomEditor(typeof(EntityLink))]
    public class EntityLinkEditor : UpdatableEditor {
        private int archetypeCurrent;
        private int archetypePrevious;
        private object[] componentsCache;
        private VisualElement componentsRoot;
        private EntityLink entityLink;
        private BaseVisualElement rootContainer;
        private Label label;
        public void OnDestroy() {
            Inspectors.Clear();
            ComponentInspectors.Clear();
        }
        private void SetEntityIndex(string text) {
            label.text = text;
        }
        public override VisualElement CreateInspectorGUI() {
            
            entityLink = target as EntityLink;
            rootContainer = new BaseVisualElement();
            rootContainer.AddVisualTree("Assets/EcsApe/Editor/Inspectors/EntityLinkEditor.uxml");
            rootContainer.AddStyleSheet("Assets/EcsApe/Editor/Inspectors/EntityLinkEditor.uss");
            
            componentsRoot = new VisualElement();
            
            var entityInspector = rootContainer.Root.Q<VisualElement>("EntityInpector");
            label = entityInspector.Q<Label>("EntityIndex");
            
            var optionField = entityInspector.Q<EnumField>("Option");
            optionField.Init(ConvertOption.Stay);
            optionField.SetValueWithoutNotify(ConvertOption.Stay);
            optionField.RegisterValueChangedCallback(x => { entityLink.option = (ConvertOption) x.newValue; });

            
            var foldoutField = new PopupField<string>();
            foldoutField.choices = ComponentEditor.Names;
            foldoutField.formatSelectedValueCallback += s1 => {
                var add = ComponentEditor.Create(s1);
                var list = entityLink.Components;
                if (add != null) {
                    if (entityLink.linked) {
                        entityLink.Entity.AddBoxed(add);
                    }
                    else {
                        if (!list.ConstainsType(add))
                            list.Add(add);
                    }
                }
                return s1;
            };
            
            var addBtn = entityInspector.Q<Button>("Add");
            addBtn.clickable.clicked += () => {
                Debug.Log("Add");
                
                ComponentsListPopup.ShowExample(addBtn.LocalToWorld(addBtn.layout).center, entityLink);
            };
            rootContainer.Add(entityInspector);
            rootContainer.Add(componentsRoot);
            return rootContainer;
        }

        protected override void OnUpdate() {
            OnPreDraw();
            OnDraw();
        }
        
        private void OnPreDraw() {

        }
        
        private void OnDraw() {
            if (entityLink == null) return;
            if (entityLink.linked) {
                var e = entityLink.Entity;
                
                if(!e.IsNull())
                    SetEntityIndex($"ENTITY:{e.Index:0000000} ARCHETYPE:{e.GetArchetype().id}");
                else 
                    SetEntityIndex("DEAD");
                
                var archetype = e.GetArchetype();
                archetypeCurrent = archetype.id;
                if (archetypeCurrent != archetypePrevious) componentsRoot.Clear();
                componentsCache = archetype.GetComponents(in e);
                foreach (var component in componentsCache) {
                    if (component == null) {
                        entityLink.Components.Remove(component);
                        continue;
                    }
                    var inspector = ComponentInspectors.Get(component.GetType());
                    inspector.DrawRunTime(component, componentsRoot, e);
                }

                archetypePrevious = archetypeCurrent;
            }
            else {
                foreach (var component in entityLink.Components) {
                    if (component == null) {
                        entityLink.Components.Remove(component);
                        continue;
                    }
                    ComponentInspectors.Get(component.GetType())
                        .DrawEditor(component, componentsRoot, entityLink, entityLink.Components);
                }
            }
        }
    }
}