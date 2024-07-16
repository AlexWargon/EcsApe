using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Wargon.Ecsape.Editor {
    public class BaseVisualElement : VisualElement {
        protected readonly VisualElement rootVisualElement;
        public VisualElement Root => rootVisualElement;
        public BaseVisualElement() {
            rootVisualElement = new VisualElement();
        }
        internal void AddVisualTree(string path) {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            var labelFromUxml = visualTree.Instantiate();
            rootVisualElement.Add(labelFromUxml);
        }

        internal void AddVisualTree(VisualTreeAsset asset) {
            var labelFromUxml = asset.Instantiate();
            rootVisualElement.Add(labelFromUxml);
        }
        
        internal void AddStyleSheet(string path) {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            rootVisualElement.styleSheets.Add(styleSheet);
        }
        
        internal void AddStyleSheet(StyleSheet asset) {
            rootVisualElement.styleSheets.Add(asset);
        }
    }


    
}