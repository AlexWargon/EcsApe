using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


namespace Wargon.Ecsape.Editor {

    public class ComponentsListPopup : EditorWindow
    {
        private EntityLink target;
        private List<string> showList = new List<string>();
        private Action OnAddRemoveComponent;
        public static void Show(Vector2 pos, EntityLink target, Action onAddRemove) {
            ComponentsListPopup popup = GetWindow<ComponentsListPopup>();
            popup.SetTarget(target);
            popup.titleContent = new GUIContent("ComponentsListPopup");
            popup.position = new Rect(pos + new Vector2(500,0), new Vector2(300, 500));
            popup.OnAddRemoveComponent = onAddRemove;
        }

        private void SetTarget(EntityLink link) => target = link;
        
        public void CreateGUI()
        {
            var root = rootVisualElement;

            var visualTree = Styles.Confing.ComponentsListPopupUXML;
            var labelFromUxml = visualTree.Instantiate();
            
            root.Add(labelFromUxml);
            var seerchField = root.Q<TextField>("Search");
            showList = new List<string>(ComponentEditor.Names);
            var listView = new ListView();
            seerchField.RegisterValueChangedCallback(x => {
                showList = ComponentEditor.Names.Where(l => l.Contains(x.newValue, StringComparison.OrdinalIgnoreCase)).ToList();
                listView.itemsSource = showList;
                listView.RefreshItems();
            });
            var listRoot = labelFromUxml.Q("List");
            
            VisualElement MakeItem() => new Label();
            void BindItem(VisualElement e, int i) => ((Label) e).text = showList[i];
            listView.makeItem = MakeItem;
            listView.bindItem = BindItem;
            listView.itemsSource = ComponentEditor.Names;
            listView.fixedItemHeight = 20;
            listView.selectionType = SelectionType.Multiple;

            listView.onSelectionChange += x => {
                var componentToAddInstance = ComponentEditor.Create((string)x.FirstOrDefault());
                
                if (componentToAddInstance != null) {
                    if (target.IsLinked) {
                        target.Entity.AddBoxed(componentToAddInstance);
                        OnAddRemoveComponent?.Invoke();
                    }
                    else 
                    {
                        var list = target.Components;
                        if (!list.ConstainsType(componentToAddInstance)) {
                            list.Add(componentToAddInstance);
                            OnAddRemoveComponent?.Invoke();
                        }
                    }
                }
                Close();
            };
            listRoot.Add(listView);
        }
    }
}