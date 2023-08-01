using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Wargon.Ecsape.Editor {
    public partial class ComponentDrawer : VisualElement {


        private readonly List<FieldData> fieldsData = new();
        private readonly List<VisualElement> fieldsView = new();
        private VisualElement fieldsRoot;
        private VisualElement parent;
        private readonly Type componentType;
        private EntityLink target;
        private object currentComponent;
        private ref Entity entity => ref target.Entity;
        private bool Runtime => target.IsLinked && !target.Entity.IsNull();
        public ComponentDrawer(){}

        private Dictionary<string, FieldInfo[]> nestedFieldInfo = new Dictionary<string, FieldInfo[]>();
        private Dictionary<string, List<VisualElement>> nestedFieldVies = new Dictionary<string, List<VisualElement>>();
        private IMGUIContainer iMGUIContainer;
        
        public ComponentDrawer(Type type, object component) {
            componentType = type;
            //var temp = new BaseVisualElement();
            this.AddVisualTreeEx(Styles.Confing.ComponentInspectorUXML);
            this.AddStyleSheetEx(Styles.Confing.ComponentInspectorUSS);
            var componentInspector = this.Q<VisualElement>("ComponentInspector");
            var foldout = componentInspector.Q<Foldout>("Foldout");
            
            foldout.text = type.Name;
            
            fieldsRoot = componentInspector.Q<VisualElement>("Children");
            
            if (ComponentEditor.TryGetColor(componentType.Name, out var color))
                foldout.style.backgroundColor = color;
            
            var remove = componentInspector.Q<Button>("Close");
            remove.clickable.clicked += OnClickRemoveComponent;

            
            var fieldsInfo = componentType.GetFields();
            //iMGUIContainer = new IMGUIContainer(DrawComponentFieldsIMGUI);
            //fieldsRoot.Add(iMGUIContainer);
            foreach (var fieldInfo in fieldsInfo) {
                // var fieldDrawer = CreateOrUpdateField(fieldInfo.GetValue(component), component, fieldInfo);
                // fieldsView.Add(fieldDrawer);
                // fieldsRoot.Add(fieldDrawer);
                // if (fieldInfo.FieldType.IsDefined(typeof(SerializableAttribute), false) && !fieldInfo.FieldType.IsPrimitive) {
                //     var nestedFoldout = new Foldout();
                //     nestedFoldout.text = fieldInfo.Name;
                //     var nestedFields = fieldInfo.FieldType.GetFields();
                //     nestedFieldInfo.Add(fieldInfo.Name, nestedFields);
                //     var nestedView = new List<VisualElement>();
                //     foreach (var nestedField in nestedFields) {
                //         var value = fieldInfo.GetValue(component);
                //         var valueNested = nestedField.GetValue(value);
                //         var nestedFieldDrawer = CreateOrUpdateField(valueNested, value, nestedField, null, fieldInfo, component);
                //         nestedView.Add(nestedFieldDrawer);
                //         nestedFoldout.Add(nestedFieldDrawer);
                //     }
                //     fieldsRoot.Add(nestedFoldout);
                //     nestedFieldVies.Add(fieldInfo.Name, nestedView);
                // }
                // else 
                //{
                var fdata = new FieldData(fieldInfo, componentType);
                fieldsData.Add(fdata);
                var fieldDrawer = CreateOrUpdateField(fieldInfo.GetValue(component), component, fdata);
                fieldsView.Add(fieldDrawer);
                fieldsRoot.Add(fieldDrawer);
                    
                //}
            }
        }

        private void DrawComponentFieldsIMGUI() {
            void SetValue(FieldInfo info, object value, object oldValue) {
                if(value==null) return;
                if(value.Equals(oldValue)) return;
                info.SetValue(currentComponent, value);
                if(Runtime) entity.SetBoxed(currentComponent);
            }

            if (currentComponent != null) {
                var fields = componentType.GetFields();
                foreach (var info in fields) {
                    var type = info.FieldType;
                    string label = info.Name;
                    object oldValue = info.GetValue(currentComponent);
                    if (TypesCache.Object.IsAssignableFrom(type)) SetValue(info, EditorGUILayout.ObjectField(label, (Object)oldValue, info.FieldType, true), oldValue);
                    else if (type == TypesCache.Integer) SetValue(info,EditorGUILayout.IntField(label, (int)oldValue), oldValue);
                    else if (type == TypesCache.Float)SetValue(info,EditorGUILayout.FloatField(label, (float)oldValue), oldValue);
                    else if (type == TypesCache.Douable)SetValue(info,EditorGUILayout.DoubleField(label, (double)oldValue), oldValue);
                    else if (type == TypesCache.Bool)SetValue(info,EditorGUILayout.Toggle(label, (bool)oldValue), oldValue);
                    else if (type == TypesCache.String)SetValue(info,EditorGUILayout.TextField(label, (string)oldValue), oldValue);
                    else if (type == TypesCache.Vector2)SetValue(info,EditorGUILayout.Vector2Field(label, (Vector2)oldValue), oldValue);
                    else if (type == TypesCache.Vector3)SetValue(info,EditorGUILayout.Vector3Field(label, (Vector3)oldValue), oldValue);
                    else if (type == TypesCache.Vector4)SetValue(info,EditorGUILayout.Vector4Field(label, (Vector4)oldValue), oldValue);
                }
            }
        }
        private void OnClickRemoveComponent() {
            if (Runtime)
                entity.Remove(componentType);
            else
                target.Components.Remove(currentComponent);
            
            this.parent.RemoveWithCheck(this);
        }
        
        public void UpdateData(object component, EntityLink target) {
            this.currentComponent = component;
            this.target = target;
            
            for (var index = 0; index < fieldsData.Count; index++) {
                var fieldInfo = fieldsData[index];
                var fieldValue = fieldInfo.GetValue(component);
                var drawer = fieldsView[index];
                CreateOrUpdateField(fieldValue, component, fieldInfo, drawer);
            }
            
            // fieldsRoot.Remove(iMGUIContainer);
            // iMGUIContainer = new IMGUIContainer(DrawComponentFieldsIMGUI);
            // fieldsRoot.Add(iMGUIContainer);
            // for (var index = 0; index < fieldsInfo.Length; index++) {
            //
            //     var fieldInfo = fieldsInfo[index];
            //     var fieldValue = fieldInfo.GetValue(component);
            //      if (nestedFieldVies.TryGetValue(fieldInfo.Name, out var list)) {
            //          var infos = nestedFieldInfo[fieldInfo.Name];
            //          for (var index2 = 0; index2 < list.Count; index2++) {
            //              var drawer2 = list[index2];
            //              var fieldInfo2 = infos[index2];
            //              var fieldValue2 = fieldInfo2.GetValue(fieldValue);
            //              CreateOrUpdateField(fieldValue2, fieldValue, fieldInfo2, drawer2, fieldInfo, component);
            //          }
            //      }
            //      else 
            //     {
            //         var drawer = fieldsView[index];
            //         CreateOrUpdateField(fieldValue, component, fieldInfo, drawer);
            //     }
            // }
            
            // for (var index = 0; index < fieldsInfo.Length; index++) {
            //     var fieldInfo = fieldsInfo[index];
            //     var fieldValue = fieldInfo.GetValue(component);
            //     var drawer = fieldsView[index];
            //     CreateOrUpdateField(fieldValue, component, fieldInfo, drawer);
            // }

        }

        public void SetParent(VisualElement parent) {
            if(ReferenceEquals(this.parent, parent)) return;
            this.parent?.RemoveWithCheck(this);
            this.parent = parent;
            this.parent.Add(this);
        }

        private void UpdateFieldWrapped<TField, TValue>(object field, TValue fieldValue)  where TField : BaseField<TValue> => UpdateField<TField, TValue>((TField)field, fieldValue);
        private void UpdateField<TField, TValue>(TField fieldDrawer, TValue fieldValue) where TField : BaseField<TValue>{
            if (fieldValue.Equals(fieldDrawer.value)) return;
            fieldDrawer.SetValueWithoutNotify(fieldValue);
        }

        private VisualElement ConfigureArrayElementFieldInternal<TField, TValue>(VisualElement drawer, object value, Array array = null, IList list = null, int elementIndex = -1) where TField : BaseField<TValue>, new() {
            TField fieldDrawer = drawer as TField;
            TValue fieldValue = (TValue)value;
            fieldDrawer ??= new TField();
            void SetValue(TValue newValue) {
                var idx = (int)fieldDrawer.userData;
                if (array != null) {
                    array.SetValue(newValue, idx);
                    return;
                }

                list[idx] = newValue;
            }

            fieldDrawer.label = $"element[{elementIndex}]";
            fieldDrawer.RegisterValueChangedCallback(evt => {
                if (evt.target != fieldDrawer)
                    return;
                if (fieldValue.Equals(evt.newValue)) return;
                fieldValue = evt.newValue;
                if (Runtime) {
                    fieldDrawer.SetValueWithoutNotify(evt.newValue);
                    SetValue(evt.newValue);
                    return;
                }
                SetValue(evt.newValue);
            });
            
            if (fieldValue.Equals(fieldDrawer.value)) return fieldDrawer;
            fieldDrawer.SetValueWithoutNotify(fieldValue);
            drawer = fieldDrawer;
            return drawer;
        }
        
        private VisualElement ConfigureArrayElementField(object value, Array array = null, IList list = null, int elementIndex = -1, VisualElement drawer = null, bool bind = false) {
            switch (value) {
                case int:
                    return ConfigureArrayElementFieldInternal<IntegerField, int>(drawer, value, array, list, elementIndex);
                case float:
                    return ConfigureArrayElementFieldInternal<FloatField, float>(drawer, value, array, list, elementIndex);
                case double:
                    return ConfigureArrayElementFieldInternal<DoubleField, double>(drawer, value, array, list, elementIndex);
                case bool:
                    return ConfigureArrayElementFieldInternal<Toggle, bool>(drawer, value, array, list, elementIndex);
                case string:
                    return ConfigureArrayElementFieldInternal<TextField, string>(drawer, value, array, list, elementIndex);
                case Vector2:
                    return ConfigureArrayElementFieldInternal<Vector2Field, Vector2>(drawer, value, array, list, elementIndex);
                case Vector3:
                    return ConfigureArrayElementFieldInternal<Vector3Field, Vector3>(drawer, value, array, list, elementIndex);
                case Vector4:
                    return ConfigureArrayElementFieldInternal<Vector4Field, Vector4>(drawer, value, array, list, elementIndex);
                case Object:
                    // if (drawer is not ObjectField field3)
                    //     field3 = new ObjectField();
                    // field3.objectType = value.GetType();
                    //Debug.Log($"bind = {bind} , index = {elementIndex}");
                    return ConfigureArrayElementFieldInternal<ObjectField, Object>(drawer, value, array, list, elementIndex);
                case Entity e:
                    if (drawer is not EntityField entityField) {
                        entityField = new EntityField();
                    }
                    
                    //EntityField fieldDrawer = (EntityField)ConfigureArrayElementField<EntityField, Entity>(drawer, value, array, list, elementIndex);
                    entityField.UpdateViewAsElement(e, elementIndex);
                    return entityField;
            }
            return (VisualElement)null;
        }

        static void Resize(ref Array array, int newSize) {        
            Type elementType = array.GetType().GetElementType();
            Array newArray = Array.CreateInstance(elementType, newSize);
            Array.Copy(array, newArray, Math.Min(array.Length, newArray.Length));
            array = newArray;
        }

        private object CreateNewElement(Type type) {
            using var temp = new TempReferense(type == typeof(string) ? string.Empty : Activator.CreateInstance(type));
            return temp.value;
        }

        public struct TempReferense : IDisposable {
            public object value;
            public TempReferense(object value) => this.value = value;
            public void Dispose() {
                value = null;
            }
        }
        private VisualElement ConfigureListView(object listField, IList listValue, Array arrayValue, object component, FieldData info, bool isArray, Type elementType) {

            const string ADD_BUTTON = "unity-list-view__add-button";
            const string REMOVE_BUTTON = "unity-list-view__remove-button";
            const int itemHeight = 16;
            var isUnityObjectElementType = typeof(Object).IsAssignableFrom(elementType);
            listValue ??= isArray ? Array.CreateInstance(elementType, 1) : (IList)Generic.New(typeof(List<>), elementType, 1);
            ListView listView = listField == null ? new ListView(listValue, itemHeight) : (ListView)listField;
            var counter = 0;
            VisualElement MakeItem() {
                var item = isUnityObjectElementType ? null : CreateNewElement(elementType);
                var element = CreateField(elementType);
                //_updateLabelMethod(element, $"element[{counter++.ToString()}]");
                ConfigureArrayElementField(item, arrayValue, listValue, counter++, element);
                return element;
            }
            void BindItems(VisualElement element, int index) {
                element.userData = index;
                ConfigureArrayElementField(listView.itemsSource[index], arrayValue, listValue, index, element);
            }

            // listView.onSelectedIndicesChange += ints => {
            //     var list = (List<VisualElement>)listView.userData;
            //     foreach (var i in ints) {
            //         ConfigureArrayElementField(listView.itemsSource[i], arrayValue, listValue, i, list[i]);
            //     }
            // };

            listView.makeItem = MakeItem;
            listView.bindItem = BindItems;

            listView.fixedItemHeight = itemHeight;
            listView.reorderMode = ListViewReorderMode.Animated;
            listView.showBorder = true;
            listView.showAddRemoveFooter = true;
            listView.showBoundCollectionSize = true;
            listView.showFoldoutHeader = true;
            listView.headerTitle = info.Name;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            listView.itemsSource = listValue;
            listView.showAlternatingRowBackgrounds = AlternatingRowBackground.None;
            listView.name = "unity-list-" + info.Name;

            listView.Q<Button>(ADD_BUTTON).clickable = new (() => {
                var newItem = elementType == typeof(string) ? string.Empty : isUnityObjectElementType ? null : Activator.CreateInstance(elementType);
                if (isArray) {
                    var index = listValue.Count + 1;
                    var array = Array.CreateInstance(elementType, index);
                    var idx = 0;
                    for (var i = 0; i < listView.itemsSource.Count; i++) {
                        array.SetValue(listView.itemsSource[i], i);
                    }
                    array.SetValue(newItem, index - 1);
                    listValue = array;
                    listView.itemsSource = listValue;
                    info.SetValue(component, array);
                }
                else {
                    listValue.Add(newItem);
                    info.SetValue(component, listValue);
                    listView.itemsSource = listValue;
                    
                }

                counter = 0;
                //listView.RefreshItems();
                listView.Rebuild();
            });
            listView.Q<Button>(REMOVE_BUTTON).clickable = new (() => {
                int removeIndex = listView.selectedIndex == -1 ? listView.itemsSource.Count - 1 : listView.selectedIndex;
                if (isArray) {
                    Array array = Array.CreateInstance(elementType, removeIndex);
                    for (var i = 0; i < listView.itemsSource.Count-1; i++) {
                        array.SetValue(listView.itemsSource[i], i);
                    }
                    listValue = array;
                    listView.itemsSource = listValue;
                    info.SetValue(component, array);
                }
                else {
                    listView.itemsSource.RemoveAt(removeIndex);
                    info.SetValue(component, listView.itemsSource);
                }

                counter = 0;
                listView.RefreshItems();
                listView.Rebuild();
                listView.SetSelection(listView.itemsSource.Count-1);
            });
            return listView;
        }

        private VisualElement CreateOrUpdateField(object fieldValue, object targetInstance, FieldData info, object fieldDrawer = null) {
            switch (fieldValue) {
                case Enum @enum:
                    var field = (EnumField)ConfigureFieldWrapped<EnumField, Enum>(fieldDrawer, @enum, targetInstance, info);
                    field.label = info.Name;
                    field.Init(@enum);
                    return field;
                case Entity e:
                    if (fieldDrawer == null) fieldDrawer = new EntityField(e);
                    ((EntityField)fieldDrawer).UpdateView(e, info.Name);
                    return (EntityField)fieldDrawer;
                // case byte:
                //     return ConfigureFieldWrapped<IntegerField, byte>(fieldDrawer, fieldValue, component, info);
                case int:
                    return ConfigureFieldWrapped<IntegerField, int>(fieldDrawer, fieldValue, targetInstance, info);
                case float:
                    return ConfigureFieldWrapped<FloatField, float>(fieldDrawer, fieldValue, targetInstance, info);
                case double:
                    return ConfigureFieldWrapped<DoubleField, double>(fieldDrawer, fieldValue, targetInstance, info);
                case bool:
                    return ConfigureFieldWrapped<Toggle, bool>(fieldDrawer, fieldValue, targetInstance, info);
                case string:
                    return ConfigureFieldWrapped<TextField, string>(fieldDrawer, fieldValue, targetInstance, info);
                case Vector2:
                    return ConfigureFieldWrapped<Vector2Field, Vector2>(fieldDrawer, fieldValue, targetInstance, info);
                case Vector3:
                    return ConfigureFieldWrapped<Vector3Field, Vector3>(fieldDrawer, fieldValue, targetInstance, info);
                case Vector4:
                    return ConfigureFieldWrapped<Vector4Field, Vector4>(fieldDrawer, fieldValue, targetInstance, info);
                case Object:
                    if (!(fieldDrawer is ObjectField field3))
                        field3 = new ObjectField();
                    field3.objectType = info.FieldType;
                    return ConfigureFieldWrapped<ObjectField, Object>(field3, fieldValue, targetInstance, info);
            }

            if (info.FieldType.IsArray || info.FieldType.IsList()) {
                var elementType = info.FieldType.IsArray
                    ? info.FieldType.GetElementType()
                    : info.FieldType.GetGenericArguments()[0];
                return ConfigureListView(fieldDrawer, (IList)fieldValue, fieldValue as Array, targetInstance, info, info.FieldType.IsArray, elementType);
            }
            return (VisualElement) null;
        }
        
        private VisualElement ConfigureFieldWrapped<TField, TValue>(object fieldDrawer, object fieldValue, object targetInstance, FieldData info, 
            (FieldInfo parentInfo, object parentInstance) parents = default)
            where TField : BaseField<TValue>, new() {
            return ConfigureField(fieldDrawer as TField, (TValue)fieldValue, targetInstance, info, () => new TField(), parents);
        }
        
        private VisualElement ConfigureField<TField, TValue>(TField fieldDrawer, TValue fieldValue, object targetInstance, 
            FieldData info, Func<TField> factory, (FieldInfo info, object instance) parents)
            where TField : BaseField<TValue> {

            fieldDrawer ??= factory();
            fieldDrawer.label = info.Name;
            //if (fieldValue.Equals(fieldDrawer.value)) return fieldDrawer;
            fieldDrawer.SetValueWithoutNotify(fieldValue);
            fieldDrawer.RegisterValueChangedCallback(evt => {
                if (evt.target != fieldDrawer)
                    return;
                if (evt.newValue.Equals(fieldValue)) return;
                
                fieldValue = evt.newValue;
                if (Runtime) {
                    fieldDrawer.SetValueWithoutNotify(evt.newValue);
                    info.SetValue(targetInstance, evt.newValue);
                    if (parents.instance != null) {
                        parents.info.SetValue(parents.instance, targetInstance);
                        entity.SetBoxed(parents.instance);
                    }
                    else {
                        entity.SetBoxed(targetInstance);
                    }
                }
                else {
                    info.SetValue(targetInstance, evt.newValue);

                    if (parents.instance != null) {
                        parents.info.SetValue(parents.instance, targetInstance);
                    }
                }
            });
            return fieldDrawer;
        }
        
        private void RegisterChangesOnCustomDrawerElement(VisualElement customDrawer, FieldInfo info, object target)
        {
            customDrawer.RegisterCallback<ChangeEvent<int>>((changeEvent => {
                info.SetValue(target, changeEvent.newValue);
            }));
            customDrawer.RegisterCallback<ChangeEvent<bool>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<float>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<double>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<string>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Color>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<UnityEngine.Object>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Enum>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Vector2>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Vector3>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Vector4>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Rect>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<AnimationCurve>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Bounds>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Gradient>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Quaternion>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Vector2Int>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Vector3Int>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Vector3Int>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<RectInt>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<BoundsInt>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Hash128>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
            customDrawer.RegisterCallback<ChangeEvent<Entity>>((changeEvent => info.SetValue(target, changeEvent.newValue)));
        }

        private VisualElement CreateField(Type type) {
            if (type == typeof(int)) return new IntegerField();
            if (type == typeof(float)) return new FloatField();
            if (type == typeof(string)) return new TextField();
            if (type == typeof(double)) return new DoubleField();
            if (type == typeof(bool)) return new Toggle();
            if (type == typeof(Vector2)) return new Vector2Field();
            if (type == typeof(Vector3)) return new Vector3Field();
            if (type == typeof(Vector4)) return new Vector4Field();
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) {
                var field = new ObjectField();
                field.objectType = type;
                return field;
            }
            return (VisualElement) null;
        }
        private VisualElement CreateField<TField, TValue>() where TField : BaseField<TValue> , new() => new TField();
        private UpdateLabelMethod _updateLabelMethod = (UpdateLabelMethod) Delegate.CreateDelegate(typeof(UpdateLabelMethod), null, typeof (Extensions).GetMethod (nameof (Extensions.SetLabel), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
        private delegate void UpdateLabelMethod(VisualElement element, string label);
    }

    public static partial class Extensions {
        public static void RemoveWithCheck(this VisualElement root, VisualElement element) {
            if (root.Contains(element)) {
                root.Remove(element);
            }
        }

        public static void SetLabel(this VisualElement element, string label)
            => element.GetType().GetProperty("label").SetValue(element,label);
        public static bool IsList(this Type type) {
            return typeof(IList).IsAssignableFrom(type);
        }

        public static bool IsUnityObject(this Type type) {
            return typeof(UnityEngine.Object).IsAssignableFrom(type);
        }
    }

    internal static class TypesCache {
        public static readonly Type String = typeof(string);
        public static readonly Type Integer = typeof(int);
        public static readonly Type Float = typeof(float);
        public static readonly Type Bool = typeof(bool);
        public static readonly Type Douable = typeof(double);
        public static readonly Type Vector2 = typeof(Vector2);
        public static readonly Type Vector3 = typeof(Vector3);
        public static readonly Type Vector4 = typeof(Vector4);
        public static readonly Type Vector2Int = typeof(Vector2Int);
        public static readonly Type Vector3Int = typeof(Vector3Int);
        public static readonly Type Entity = typeof(Entity);
        public static readonly Type Object = typeof(Object);
    }
    internal class FieldData {
        public string Name;
        public readonly Type FieldType;
        public readonly bool HasChilds;
        public readonly bool IsGeneric;
        public readonly Type GenericType;
        public readonly Type ParentType;
                
        private readonly ReflectionUtils.SetterDelegate setter;
        private readonly Func<object, object> getter;
        public FieldData(FieldInfo info, Type instanceType) {
            ParentType = instanceType;
            FieldType = info.FieldType;
            HasChilds = FieldType.GetFields().Length > 0;
            Name = info.Name;
            IsGeneric = FieldType.IsGenericType;
            GenericType = null;
            
            if(IsGeneric)
                GenericType = FieldType.GetGenericArguments()[0];
            setter = info.CreateSetMethod();
            getter = ParentType.FieldGetter(info);
        }

        public void SetValue(object obj, object value) {
            setter(ref obj, value);
        }

        public object GetValue(object obj) {
            return getter(obj);
        }
    }

    public partial class ComponentDrawer {
        private static readonly Dictionary<string, ComponentDrawer> Drawers = new();
        public static void Clear() => Drawers.Clear();
        public static ComponentDrawer GetDrawer(Type type) {
            return Drawers.TryGetValue(type.Name, out var drawer) ? drawer : null;
        }
        public static ComponentDrawer GetDrawer(int index) {
            var type = Component.GetTypeOfComponent(index);
            return Drawers.TryGetValue(type.Name, out var drawer) ? drawer : null;
        }
        public static ComponentDrawer GetDrawer(object component) {
            var type = component.GetType();
            if (Drawers.TryGetValue(type.Name, out var drawer)) return drawer;
            drawer = new ComponentDrawer(type, component);
            Drawers.Add(type.Name, drawer);
            Debug.Log($"Drawer for {drawer.componentType.Name} created");
            return drawer;
        }
    }
}