using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Ecsape.Editor {
    [CreateAssetMenu(order = 0, fileName = nameof(StyleSheetConfing))]
    public class StyleSheetConfing : EditorScriptableResorces<StyleSheetConfing> {
        public VisualTreeAsset EntityLinkEditorUXML;
        public StyleSheet EntityLinkEditorUSS;
        [Space] 
        public VisualTreeAsset ComponentInspectorUXML;
        public StyleSheet ComponentInspectorUSS;
        [Space]
        public VisualTreeAsset ComponentsListPopupUXML;
    }

    public class EditorScriptableResorces<T> : ScriptableObject where T: ScriptableObject {
        private static T intance;
        public static T Instance {
            get {
                if (intance == null) {
                    var instancies = Resources.LoadAll<T>("");
                    intance = instancies[0];
                }
                return intance;
            }
        }
    }
    [InitializeOnLoad]
    public static class Styles {
        public static readonly StyleSheetConfing Confing;
        static Styles() {
            Confing = StyleSheetConfing.Instance;
            ComponentDrawer.ClearAll();
        }
    }
}