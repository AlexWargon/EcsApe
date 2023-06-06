using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


namespace Wargon.Ecsape.Editor {
    public class EmptyPopupWindow : VisualElement {
        public new class UxmlFactory : UxmlFactory<EmptyPopupWindow>{}

        public EmptyPopupWindow() {
            
        }
    }
    public class ComponentsListPopup : EditorWindow
    {
        private EntityLink target;
        private List<string> showList = new List<string>();
        [MenuItem("Window/UI Toolkit/ComponentsListPopup")]
        public static void ShowExample(Vector2 pos, EntityLink target)
        {
            ComponentsListPopup wnd = GetWindow<ComponentsListPopup>();
            wnd.SetTarget(target);
            wnd.titleContent = new GUIContent("ComponentsListPopup");
            wnd.position = new Rect(pos + new Vector2(500,0), new Vector2(300, 500));
        }

        public void SetTarget(EntityLink link) => target = link;
        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;

            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/EcsApe/Editor/Inspectors/ComponentsListPopup.uxml");
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
            void BindItem(VisualElement e, int i) => ((Label) e).text = ComponentEditor.Names[i];
            listView.makeItem = MakeItem;
            listView.bindItem = BindItem;
            listView.itemsSource = ComponentEditor.Names;
            listView.fixedItemHeight = 20;
            listView.selectionType = SelectionType.Multiple;

            listView.onSelectionChange += x => {
                var add = ComponentEditor.Create((string)x.FirstOrDefault());
                var list = target.Components;
                if (add != null) {
                    if (target.linked) {
                        target.Entity.AddBoxed(add);
                    }
                    else {
                        if (!list.ConstainsType(add))
                            list.Add(add);
                    }
                }
                Close();
            };
            listRoot.Add(listView);
            // VisualElement labelWithStyle = new Label("Hello World! With Style");
            // labelWithStyle.styleSheets.Add(styleSheet);
            // root.Add(labelWithStyle);
        }
    }
}