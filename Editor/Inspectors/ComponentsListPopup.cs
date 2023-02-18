using System;
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
        [MenuItem("Window/UI Toolkit/ComponentsListPopup")]
        public static void ShowExample(Vector2 pos, EntityLink target)
        {
            ComponentsListPopup wnd = GetWindow<ComponentsListPopup>();
            wnd.SetTarget(target);
            wnd.titleContent = new GUIContent("ComponentsListPopup");
            wnd.position = new Rect(pos + new Vector2(500,0), new Vector2(300, 500));
        }

        private EntityLink target;
        public void SetTarget(EntityLink link) => target = link;
        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;

            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/EcsApe/Editor/Inspectors/ComponentsListPopup.uxml");
            var labelFromUxml = visualTree.Instantiate();
            root.Add(labelFromUxml);
            var listView = new ListView();
            var items = ComponentEditor.Names;
            VisualElement MakeItem() => new Label();
            void BindItem(VisualElement e, int i) => ((Label) e).text = items[i];
            listView.makeItem = MakeItem;
            listView.bindItem = BindItem;
            listView.itemsSource = items;
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
            root.Add(listView);
            // VisualElement labelWithStyle = new Label("Hello World! With Style");
            // labelWithStyle.styleSheets.Add(styleSheet);
            // root.Add(labelWithStyle);
        }
    }
}