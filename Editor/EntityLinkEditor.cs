using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Wargon.Ecsape.Editor {
    [CustomEditor(typeof(EntityLink))]
    public class EntityLinkEditor : UpdatableEditor {
        private int archetypeCurrent;
        private int archetypePrevious;
        private object[] componentsCache;
        private VisualElement componentsRoot;
        private EntityLink entityLink;
        private BaseVisualElement rootContainer;
        private Label label;
        private bool inited;
        public void OnDestroy() {
            Inspectors.Clear();
            ComponentInspectors.Clear();
        }
        private void SetEntityIndex(string text) {
            if(label== null) return;
            label.text = text;
        }
        
        public override VisualElement CreateInspectorGUI() {
            entityLink = target as EntityLink;
            rootContainer = new BaseVisualElement();

            rootContainer.AddVisualTree(Styles.Confing.EntityLinkEditorUXML);
            rootContainer.AddStyleSheet(Styles.Confing.EntityLinkEditorUSS);
            componentsRoot = new VisualElement();
            
            var entityInspector = rootContainer.Root.Q<VisualElement>("EntityInpector");
            label = entityInspector.Q<Label>("EntityIndex");
            
            var worldField = entityInspector.Q<TextField>("World");

            if (string.IsNullOrEmpty(entityLink.WorldName))
                entityLink.WorldName = World.DEFAULT;
            worldField.SetValueWithoutNotify(entityLink.WorldName);
            worldField.RegisterValueChangedCallback(x => { entityLink.WorldName = x.newValue; });
            
            var optionField = entityInspector.Q<EnumField>("Option");
            optionField.Init(ConvertOption.Stay);
            optionField.SetValueWithoutNotify(ConvertOption.Stay);
            optionField.RegisterValueChangedCallback(x => { entityLink.option = (ConvertOption) x.newValue; });

            
            var addBtn = entityInspector.Q<Button>("Add");
            addBtn.clickable.clicked += () => {
                ComponentsListPopup.Show(addBtn.LocalToWorld(addBtn.layout).center, entityLink);
            };
            rootContainer.Add(entityInspector);
            rootContainer.Add(componentsRoot);
            inited = true;
            return rootContainer;
        }

        protected override void OnUpdate() {
            if(!inited) return;
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
                for (var index = 0; index < componentsCache.Length; index++) {
                    var component = componentsCache[index];
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
                SetEntityIndex("DEAD");
                for (var index = 0; index < entityLink.Components.Count; index++) {
                    var component = entityLink.Components[index];
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