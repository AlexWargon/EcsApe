using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Wargon.Ecsape {
    public static class Inspectors {
        private static Dictionary<Type, BaseInspector> inspectors = new ();
        static bool inited = false;

        private static void Init() {
            if(inited) return;
            
            Add<int>(new IntInspector().Create());
            Add<string>(new StringInspector().Create());
            Add<Object>(new ObjectInspector().Create());
            Add<float>(new FloatInspector().Create());
            Add<Vector2>(new Vector2Inspector().Create());
            Add<Vector3>(new Vector3Inspector().Create());
            Add<Vector4>(new Vector4Inspector().Create());
            Add<Quaternion>(new QuaternionInspector().Create());
            Add<bool>(new BoolInspector().Create());
            Add<AnimationCurve>(new CurveInspector().Create());
            
            inited = true;
        }

        static Inspectors() {
            Init();
        }

        public static void Clear() {
            inited = false;
        }
        public static BaseInspector Create(Type type) {
            //Debug.Log($"TRY CREATE INSPECTOR TYPE OF {type.Name}");
            var typeToSearch = type;
            if (typeToSearch.IsSubclassOf(typeof(Object)))
                typeToSearch = typeof(Object);
            if (inspectors.TryGetValue(typeToSearch, out var inspector)) {
                var typeToCreate = inspector.GetType();
                var newInspector = (BaseInspector)Activator.CreateInstance(typeToCreate);
                newInspector.Create();
                return newInspector;
            }

            return null;
        }

        private static void Add<T>(BaseInspector inspector) {
            if (!inspectors.ContainsKey(typeof(T))) {
                inspectors.Add(typeof(T), inspector);
            }
        }
        public static BaseInspector GetInspector<T>() {
            if (inspectors.TryGetValue(typeof(T), out var inspector))
                return inspector;
            return null;
        }

        public static void Draw(Type fieldType, Type objType, VisualElement root, string name, object obj) {
            var typeToSearch = fieldType;
            if (typeToSearch.IsSubclassOf(typeof(Object)))
                typeToSearch = typeof(Object);
            if (inspectors.TryGetValue(typeToSearch, out var inspector)) {
                //inspector.Init(obj, objType.GetField(name) ,name, root);
            }
        }
    }
    
    public static class ComponentInspectors {
        private static readonly Dictionary<Type, UniversalComponentInspector> inspectors = new();
        public static UniversalComponentInspector Get(Type type) {
            if (inspectors.TryGetValue(type, out var inspector))
                return inspector;
            var newInspector = new UniversalComponentInspector();
            newInspector.AddVisualTree("Assets/EcsApe/Editor/Inspectors/ComponentInspector.uxml");
            newInspector.AddStyleSheet("Assets/EcsApe/Editor/Inspectors/ComponentInspector.uss");
            newInspector.Init(type);
            inspectors.Add(type, newInspector);
            return newInspector;
        }

        public static void Clear() {
            inspectors.Clear();
        }
    }
    
    public abstract class BaseInspector : VisualElement {
        private object data;
        private Entity entity;
        protected string fieldName = "field";

        private bool initialized;
        protected Func<object, object> onChange;
        private VisualElement previousRoot;
        private bool runtimeMode;
        private object target;
        private Object targetObject;
        protected abstract VisualElement GetField();

        private void SetTarget(object component, Object targetObj) {
            target = component;
            targetObject = targetObj;
        }

        private void AddToRoot(VisualElement root) {
            var field = GetField();
            if (field == null) return;

            if (previousRoot != null) previousRoot.Remove(field);
            if (root != null) {
                previousRoot = root;
                previousRoot.Add(field);
            }
        }

        public void Init(object obj, Object targetObj, FieldInfo fieldInfo, string fName, VisualElement root) {
            if (root == previousRoot) return;
            if (initialized) return;
            AddToRoot(root);
            SetTarget(obj, targetObj);
            fieldName = fName;
            onChange = x => {
                data = x;
                fieldInfo.SetValue(target, data);
                if (targetObject != null) EditorUtility.SetDirty(targetObject);
                if (runtimeMode)
                    entity.SetBoxed(target);
                return data;
            };
            if (data != null)
                OnDraw(data, false, fieldInfo.FieldType);
            initialized = true;
        }

        protected abstract void OnDraw(object value, bool runTime, Type targetType);

        public void Draw(object value, string fName, bool runTime, Type targetType, Entity e) {
            runtimeMode = runTime;
            fieldName = fName;
            entity = e;
            OnDraw(value, runTime, targetType);
        }

        protected abstract void OnCreate();

        public BaseInspector Create() {
            OnCreate();
            return this;
        }
    }

    public class IntInspector : BaseInspector {
        private IntegerField field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((int) value);
        }

        protected override void OnCreate() {
            field = new IntegerField(fieldName);
            field.value = default;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
        }

        protected override VisualElement GetField() {
            return field;
        }
    }

    public class FloatInspector : BaseInspector {
        private FloatField field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((float) value);
        }

        protected override void OnCreate() {
            field = new FloatField(fieldName);
            field.value = default;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
        }

        protected override VisualElement GetField() {
            return field;
        }
    }

    public class Vector2Inspector : BaseInspector {
        private Vector2Field field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((Vector2) value);
        }

        protected override void OnCreate() {
            field = new Vector2Field(fieldName);
            field.value = default;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
        }

        protected override VisualElement GetField() {
            return field;
        }
    }

    public class Vector3Inspector : BaseInspector {
        private Vector3Field field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((Vector3) value);
        }

        protected override void OnCreate() {
            field = new Vector3Field(fieldName);
            field.value = default;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
        }

        protected override VisualElement GetField() {
            return field;
        }
    }

    public class StringInspector : BaseInspector {
        private TextField field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((string) value);
        }

        protected override void OnCreate() {
            field = new TextField(fieldName);
            field.value = string.Empty;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
        }

        protected override VisualElement GetField() {
            return field;
        }
    }

    public class Vector4Inspector : BaseInspector {
        private Vector4Field field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((Vector4) value);
        }

        protected override void OnCreate() {
            field = new Vector4Field(fieldName);
            field.value = default;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
        }

        protected override VisualElement GetField() {
            return field;
        }
    }
    public class QuaternionInspector : BaseInspector {
        private Vector3Field field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify(toVector3((Quaternion)value));
        }

        protected override void OnCreate() {
            field = new Vector3Field(fieldName);
            field.value = default;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(toQuaternion(x.newValue)); });
        }

        protected override VisualElement GetField() {
            return field;
        }
        private Vector4 toVector3(Quaternion q) {
            return q.eulerAngles;
        }
        private Vector4 toVector(Quaternion q) {
            return new Vector4(q.x, q.y, q.z, q.w);
        }
        private Quaternion toQuaternion(Vector3 v) {
            return Quaternion.Euler(v);
        }
    }
    public class BoolInspector : BaseInspector {
        private Toggle field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((bool) value);
        }

        protected override void OnCreate() {
            field = new Toggle(fieldName);
            field.value = default;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
        }

        protected override VisualElement GetField() {
            return field;
        }
    }

    public class ObjectInspector : BaseInspector {
        private ObjectField field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((Object) value);
            field.objectType = targetType;
        }

        protected override void OnCreate() {
            field = new ObjectField(fieldName);

            field.value = null;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
        }

        protected override VisualElement GetField() {
            return field;
        }
    }

    public class CurveInspector : BaseInspector {
        private CurveField field;

        protected override VisualElement GetField() {
            return field;
        }

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((AnimationCurve) value);
        }

        protected override void OnCreate() {
            field = new CurveField(fieldName);

            field.value = null;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
        }
    }
}