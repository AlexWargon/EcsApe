using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
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
            Add<LayerMask>(new LayerMaskInspector().Create());
            Add<List<Entity>>(new ListOfEntitiesInpsector().Create());
            Add<Entity>(new EntityInspector().Create());
            Add<List<EntityLink>>(new ListOfEntityLinksInspector().Create());
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

    public static class EditorUtils {
        public static void SetDirty(Object obj) {
            if(obj is not null)
                EditorUtility.SetDirty(obj);
        }
    }
    public abstract class BaseInspector : VisualElement {
        private object data;
        protected Entity TargetEntity;
        protected string fieldName = "field";
        protected bool runtimeMode;
        private bool initialized;
        protected Action<object> onChange;
        private VisualElement previousRoot;
        private object target;
        protected EntityLink targetLink;
        private float updateDelayCounter;
        protected abstract VisualElement GetField();

        private void SetTarget(object component, Object targetObj) {
            target = component;
            targetLink = (EntityLink)targetObj;
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

        public void UpdateData(object component, Object link, FieldInfo fieldInfo, string fieldNameParam, VisualElement root, bool runTime) {
            if (root == previousRoot) return;
            if (initialized) return;
            AddToRoot(root);
            SetTarget(component, link);
            this.fieldName = fieldNameParam;
            onChange = x => {
                data = x;
                fieldInfo.SetValue(target, data);
                if (targetLink != null) {
                    EditorUtility.SetDirty(targetLink);
                }
                if (runtimeMode)
                    TargetEntity.SetBoxed(target);
            };
            if (data != null) {
                if(runTime)
                    OnDraw(data, runTime, fieldInfo.FieldType);
            }
            initialized = true;
        }

        private int ticks = 60;
        protected int ticksCounter;
        private Action onUpdate;
        private void Update() {
            ticksCounter++;
            if (ticksCounter == ticks) {
                onUpdate?.Invoke();
                ticksCounter = 0;
            }
        }
        static async ValueTask SetDirtyAsync(Object obj) {
            await Task.Delay(200);
            EditorUtility.SetDirty(obj);
            //Debug.Log(200);
        }
        protected abstract void OnDraw(object value, bool runTime, Type targetType);

        public void Draw(object value, string fName, bool runTime, Type targetType, Entity e) {
            runtimeMode = runTime;
            fieldName = fName;
            TargetEntity = e;
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
    
    public class LayerMaskInspector : BaseInspector {
        private LayerMaskField field;

        protected override VisualElement GetField() {
            return field;
        }

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((LayerMask) value);
        }

        protected override void OnCreate() {
            field = new LayerMaskField(fieldName);

            field.value = default;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
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
    public class EntityInspector : BaseInspector {
        private Label pureEntityField;
        private ObjectField hybridEntityField;
        private Entity fieldValue;
        protected override VisualElement GetField() {
            if (!fieldValue.IsNull()) {
                if (fieldValue.Has<ViewLink>()) return hybridEntityField;
            }
            return pureEntityField;
        }

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            fieldValue = (Entity)value;
            if(fieldValue.IsNull()) return;
            if (fieldValue.Has<ViewLink>()) {
                hybridEntityField.label = fieldName;
                hybridEntityField.SetValueWithoutNotify(fieldValue.Get<ViewLink>().Link);
            }
            else {
                var s = fieldValue.IsNull() ? "DEAD" : fieldValue.Index.ToString();
                pureEntityField.text = $"{fieldName} {s}";
            }
        }

        protected override void OnCreate() {
            pureEntityField = new Label("entity X");
            hybridEntityField = new ObjectField("entity X");
            hybridEntityField.objectType = typeof(EntityLink);

            hybridEntityField.RegisterValueChangedCallback(x => {
                onChange?.Invoke(x.newValue);
            });
        }
    }

    public class ListOfEntityLinksInspector : BaseInspector {
        private ListView listView;
        private List<EntityLink> items;
        
        private const string ADD_BUTTON = "unity-list-view__add-button";
        private const string REMOVE_BUTTON = "unity-list-view__remove-button";


        protected override VisualElement GetField() {
            return listView;
        }

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            var list = (List<EntityLink>)value;
            items = list;
            listView.itemsSource = list;
            listView.headerTitle = fieldName;
            
        }
        int maked = 0;
        protected override void OnCreate() {
            if(listView is not null) return;
            

            VisualElement MakeItem() {
                var objectField = new ObjectField("Entity");
                objectField.label = fieldName;
                objectField.objectType = typeof(EntityLink);
                objectField.RegisterValueChangedCallback(x => {
                    EditorUtils.SetDirty(targetLink);
                    var index = (int)objectField.userData;
                    items[index] = (EntityLink)x.newValue;
                    //Debug.Log("1111");
                });
                
                return objectField;
            }
            
            void BindItems(VisualElement element, int index) {
                element.userData = index;
                ((ObjectField)element).value = items[index];
                if(items[index] is not null)
                    Debug.Log(items[index].name);
                //Debug.Log("2222");
            }

            
            const int itemHeight = 16;
            listView = new ListView(items, itemHeight, MakeItem, BindItems);
            listView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
            listView.showBoundCollectionSize = true;
            listView.showBorder = true;
            listView.showFoldoutHeader = true;
            listView.showAddRemoveFooter = true;
            
            listView.selectionType = SelectionType.Single;
            listView.onItemsChosen += objects => Debug.Log("chosen");
            listView.onSelectionChange += objects => Debug.Log("changed");

            listView.style.flexGrow = 4.0f;
            listView.reorderMode = ListViewReorderMode.Animated;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            
            listView.headerTitle = "MyList";
            
            listView.reorderMode = ListViewReorderMode.Animated;
            listView.Q<Button>(ADD_BUTTON).clicked  += () =>
            {

            };
            listView.Q<Button>(REMOVE_BUTTON).clicked +=() => {

            };
            listView.itemsAdded += x => {
                Debug.Log("2222");
            };
            listView.onSelectionChange += x => {
                Debug.Log("3333");
            };
            maked = 0;
        }
    }
    public class ListOfEntitiesInpsector : BaseInspector {
        private ListView listView;
        private List<Entity> items;
        private List<EntityLink> viewList;
        private IMGUIContainer IMGUIContainer;
        private Type listType;

        private const string ADD_BUTTON = "unity-list-view__add-button";
        private const string REMOVE_BUTTON = "unity-list-view__remove-button";
        protected override VisualElement GetField() {
            return listView;
        }

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            items = (List<Entity>)value;
            listView.itemsSource = items;
            listView.headerTitle = fieldName;
        }

        protected override void OnCreate() {

            Func<VisualElement> makeItem = () => {
                var fld = new ObjectField("Entity");
                fld.label = fieldName;
                fld.objectType = typeof(EntityLink);
                return fld;
            };

            void BindItems(VisualElement elements, int index) {
                if (runtimeMode) {
                    var entity = items[index];
                    if (!entity.IsNull()) {
                        if (entity.Has<ViewLink>()) {
                            var fld = new ObjectField($"Entity {entity.Index}");

                            fld.label = fieldName;
                            fld.objectType = typeof(EntityLink);
                            elements = fld;
                        }
                        else {
                            //((Label)elements).text = $"Entity {entity.GetArchetype()}";
                        }
                    }
                }
                else {
                    var entity = items[index];
                    if (!entity.IsNull()) {
                        var fld = new ObjectField($"Entity {entity.Index}");
                        fld.label = fieldName;
                        fld.objectType = typeof(EntityLink);
                        elements = fld;
                    }
                    else {
                        var fld = new ObjectField($"Entity");
                        fld.label = fieldName;
                        fld.objectType = typeof(EntityLink);
                        elements = fld;
                    }
                }
                
            }

            const int itemHeight = 16;
            listView = new ListView(items, itemHeight, makeItem, BindItems);
            
            listView.selectionType = SelectionType.Multiple;
            listView.onItemsChosen += objects => Debug.Log(objects);
            listView.onSelectionChange += objects => Debug.Log(objects);
            listView.style.flexGrow = 1.0f;
            listView.reorderMode = ListViewReorderMode.Animated;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            listView.showFoldoutHeader = true;
            listView.headerTitle = "MyList";
            listView.showAddRemoveFooter = true;
            listView.reorderMode = ListViewReorderMode.Animated;
            listView.Q<Button>(ADD_BUTTON).clickable = new Clickable(() =>
            {
                if (runtimeMode) {
                    items.Add(TargetEntity.GetWorld().CreateEntity());
                }
                else {
                    items.Add(new Entity());
                }
                listView.Rebuild();
            });
            listView.Q<Button>(REMOVE_BUTTON).clickable = new Clickable(() => {
                if(items.Count  == 0) return;
                items.Remove(items.Last());
                listView.Rebuild();
            });
            listView.RegisterCallback<UnityEngine.UIElements.DragPerformEvent>(x => {
                if (Selection.activeObject is EntityLink entityLink) {
                    items.Add(entityLink.Entity);
                    listView.Rebuild();
                }
            }, TrickleDown.TrickleDown);
            //_reorderableList = new ReorderableList(items, typeof(Entity));
            //_reorderableList.
        }

        void DrawItem() {
            
        }
        void BindItem(VisualElement item, int index)
        {
            var label = (Label)item;
            label.text = items[index].Index.ToString();
        }
    }
}