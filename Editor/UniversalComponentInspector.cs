﻿using System;
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

    public static class VisualElementsExt {
        public static void AddStyleSheetEx(this VisualElement element, StyleSheet asset) {
            element.styleSheets.Add(asset);
        }
        public static void AddVisualTreeEx(this VisualElement element, VisualTreeAsset asset) {
            var labelFromUxml = asset.Instantiate();
            element.Add(labelFromUxml);
        }
    }
    public class UniversalComponentInspector : BaseVisualElement {
        private readonly Dictionary<string, BaseInspector> _inspectors = new ();
        private Type _componentType;
        private VisualElement _inspectorRoot;
        private Foldout _foldout;
        private VisualElement _children;
        private Label _label;
        private Action _onClickRemove;
        
        public void Init(Type componentType) {
            _componentType = componentType;
            var fields = _componentType.GetFields();
            foreach (var fieldInfo in fields) {
                var newInspector = Inspectors.New(fieldInfo.FieldType);
                if (newInspector != null) {
                    rootVisualElement.Add(newInspector);
                    _inspectors.Add(fieldInfo.Name, newInspector);
                }
            }
            _inspectorRoot = rootVisualElement.Q<VisualElement>("ComponentInspector");
            _foldout = rootVisualElement.Q<Foldout>("Foldout");

            if (ComponentEditor.TryGetColor(_componentType.Name, out var color))
                _foldout.style.backgroundColor = color;
            else
                _foldout.style.backgroundColor = new Color(0.27f, 0.27f, 0.29f);
            
            // if (ComponentEditor.TryGetColor(_componentType.Name, out var color))
            //     _inspectorRoot.style.backgroundColor = color;
            // else
            //     _inspectorRoot.style.backgroundColor = new Color(0.27f, 0.27f, 0.29f);
            
            _children = _inspectorRoot.Q<VisualElement>("Children");
            //_children = new VisualElement();
            //_foldout.Add(_children);
            var remove = _inspectorRoot.Q<Button>("Close");
            remove.clickable.clicked += () => _onClickRemove.Invoke();
        }

        public void DrawEditor(object component, VisualElement root, Object target, IList collection) {
            CheckRoot(root);
            var type = component.GetType();
            var typeInfos = type.GetFields();
            _foldout.text = type.Name;
            
            _onClickRemove = () => {
                collection.Remove(component);
                root.Remove(_inspectorRoot);
            };
            
            foreach (var fieldInfo in typeInfos) {
                if (!_inspectors.TryGetValue(fieldInfo.Name, out var inspector)) continue;
                inspector.UpdateData(component,target, fieldInfo ,fieldInfo.Name, _children, false);
                inspector.Draw(fieldInfo.GetValue(component), fieldInfo.Name,false, fieldInfo.FieldType, default);
            }
        }
        public void DrawRunTime(object component, VisualElement root, Entity entity) {
            CheckRoot(root);
            var type = component.GetType();
            var fields = type.GetFields();
            _foldout.text = type.Name;
            _onClickRemove = () => {
                entity.Remove(type);
                root.Remove(_inspectorRoot);
            };
            foreach (var fieldInfo in fields) {
                if (!_inspectors.TryGetValue(fieldInfo.Name, out var inspector)) continue;
                inspector.UpdateData(component,null, fieldInfo ,name, _children, true);
                inspector.Draw(fieldInfo.GetValue(component), fieldInfo.Name,true, fieldInfo.FieldType, entity);
            }
        }
        
        private void CheckRoot(VisualElement root) {
            if(_inspectorRoot == null) return;
            if(root == null) return;
            if (!root.Contains(_inspectorRoot)) {
                root.Add(_inspectorRoot);
            }
        }
    }


    public class ComponentInspector : VisualElement {
        private VisualElement _parent;

        public ComponentInspector(VisualElement parent, Type componentType) {
            _parent = parent;
        }
        
    }
}