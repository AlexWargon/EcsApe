using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Wargon.Ecsape.Editor {
    [CustomEditor(typeof(EntityLink))]
    public class EntityLinkEditor : UpdatableEditor {
        private int archetypeCurrentID;
        private int archetypePreviousID;
        private object[] componentsCache;
        private VisualElement componentsRoot;
        private EntityLink targetEntityLink;
        private BaseVisualElement rootContainer;
        private Label label;
        private bool inited;

        private void OnDestroy() {
            //ComponentDrawer.Clear();
        }

        private void SetEntityIndex(string text) {
            if(label== null) return;
            label.text = text;
        }
        
        public override VisualElement CreateInspectorGUI() {
            targetEntityLink = target as EntityLink;
            if (targetEntityLink == null) return rootContainer;
            //entityLink.Components.RemoveAll(item => ReferenceEquals(item,null));
            
            rootContainer = new BaseVisualElement();

            rootContainer.AddVisualTree(Styles.Confing.EntityLinkEditorUXML);
            rootContainer.AddStyleSheet(Styles.Confing.EntityLinkEditorUSS);
            componentsRoot = new VisualElement();
            
            var entityInspector = rootContainer.Root.Q<VisualElement>("EntityInpector");
            label = entityInspector.Q<Label>("EntityIndex");
            
            var worldField = entityInspector.Q<TextField>("World");

            if (string.IsNullOrEmpty(targetEntityLink.WorldName))
                targetEntityLink.WorldName = World.DEFAULT;
            
            worldField.SetValueWithoutNotify(targetEntityLink.WorldName);
            worldField.RegisterValueChangedCallback(x => { targetEntityLink.WorldName = x.newValue; });
            
            var optionField = entityInspector.Q<EnumField>("Option");
            optionField.Init(ConvertOption.Stay);
            optionField.SetValueWithoutNotify(ConvertOption.Stay);
            optionField.RegisterValueChangedCallback(x => { targetEntityLink.option = (ConvertOption) x.newValue; });

            var addBtn = entityInspector.Q<Button>("Add");
            addBtn.clickable.clicked += () => {
                ComponentsListPopup.Show(addBtn.LocalToWorld(addBtn.layout).center, targetEntityLink, DrawEditor);
            };
            
            rootContainer.Add(entityInspector);
            rootContainer.Add(componentsRoot);
            DrawEditor();
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
            if (ReferenceEquals(targetEntityLink, null)) return;
            DrawRuntime();
        }
        
        private void DrawEditor() {
            SetEntityIndex("DEAD");
            for (var index = 0; index < targetEntityLink.Components.Count; index++) {
                var component = targetEntityLink.Components[index];
                // ComponentInspectors.Get(component.GetType())
                //     .DrawEditor(component, componentsRoot, entityLink, entityLink.Components);

                var drawer = ComponentDrawer.GetDrawer(component);
                drawer.SetParent(componentsRoot);
                drawer.UpdateData(component, targetEntityLink);
            }
        }


        private Archetype previousArch;
        private Archetype currentArch;
        protected override float Framerate => 29;

        private void DrawRuntime() {
            if (targetEntityLink.IsLinked) {
                var e = targetEntityLink.Entity;
                var archetype = e.GetArchetype();
                if(!e.IsNull())
                    SetEntityIndex($"ENTITY:{e.Index:0000000} ARCHETYPE:{archetype.id}");
                else 
                    SetEntityIndex("DEAD");
                
                
                currentArch = archetype;
                archetypeCurrentID = archetype.id;
                //if (archetypeCurrent != archetypePrevious) componentsRoot.Clear();
                if (archetypeCurrentID != archetypePreviousID && previousArch != null) {
                    (IEnumerable<int> delta, bool added) = archetype.GetDelta(previousArch);
                    if (added) {
                        foreach (var i in delta) {
                            var drawer = ComponentDrawer.GetDrawer(ComponentMeta.GetTypeOfComponent(i));
                            if(drawer!=null)
                                componentsRoot.Add(drawer);
                        }
                    }
                    else {
                        foreach (var i in delta) {
                            var drawer = ComponentDrawer.GetDrawer(ComponentMeta.GetTypeOfComponent(i));
                            if(drawer!=null)
                                componentsRoot.RemoveWithCheck(drawer);
                        }
                    }
                    
                }

                var count = archetype.GetAllComponents(e.Index, ref componentsCache);
                for (var index = 0; index < count; index++) {
                    var component = componentsCache[index];
                    // var inspector = ComponentInspectors.Get(component.GetType());
                    // inspector.DrawRunTime(component, componentsRoot, e);
                    //
                    //if(Component.GetComponentType(Component.GetIndex(component.GetType())).IsTag) continue;
                    var drawer = ComponentDrawer.GetDrawer(component);
                    drawer.SetParent(componentsRoot);
                    drawer.UpdateData(component, targetEntityLink);
                }

                previousArch = archetype;
                archetypePreviousID = archetypeCurrentID;
            }
        }
    }
}