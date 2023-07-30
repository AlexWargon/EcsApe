using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Wargon.Ecsape.Components;
using Wargon.Ecsape.Editor;
using Object = UnityEngine.Object;

namespace Wargon.Ecsape.Editor {
    public static class Inspectors {
        private static Dictionary<Type, BaseInspector> inspectors = new ();
        static bool inited = false;

        private static void Init() {
            if(inited) return;
            
            Add<int>(new IntInspector());
            Add<byte>(new ByteInspector());
            
            Add<string>(new StringInspector());
            Add<Object>(new ObjectInspector());
            Add<float>(new FloatInspector());
            Add<Vector2>(new Vector2Inspector());
            Add<Vector3>(new Vector3Inspector());
            Add<Vector4>(new Vector4Inspector());
            Add<Quaternion>(new QuaternionInspector());
            Add<bool>(new BoolInspector());
            Add<AnimationCurve>(new CurveInspector());
            Add<LayerMask>(new LayerMaskInspector());
            Add<List<Entity>>(new ListOfEntitiesInpsector());
            Add<Entity>(new EntityInspector());
            Add<List<EntityLink>>(new ListOfEntityLinksInspector());
            Add<Enum>(new EnumInspector());
            Add<ObjectReference<Object>>(new ObjectReferenceInspector());
            foreach (var inspectorsValue in inspectors.Values) {
                inspectorsValue.Create();
            }
            inited = true;
        }

        static Inspectors() {
            Init();
        }

        public static void Clear() {
            inited = false;
        }

        private static bool IsObjectReference(Type type) {
            if (type.IsGenericType) {
                var s = type.GetGenericTypeDefinition();
                return s == typeof(ObjectReference<>);
            }
            return false;
        }
        public static BaseInspector New(Type type) {
            //Debug.Log($"TRY CREATE INSPECTOR TYPE OF {type.Name}");
            var typeToSearch = type;
            if (typeToSearch.IsSubclassOf(typeof(Object)))
                typeToSearch = typeof(Object);
            if (typeToSearch.IsEnum)
                typeToSearch = typeof(Enum);
            if (IsObjectReference(typeToSearch)) {
                typeToSearch = typeof(ObjectReference<Object>);
            }
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
            newInspector.AddVisualTree(Styles.Confing.ComponentInspectorUXML);
            newInspector.AddStyleSheet(Styles.Confing.ComponentInspectorUSS);
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
        protected Entity TargetEntity;
        protected string fieldName = "field";
        protected bool runtimeMode;
        private bool initialized;
        protected bool blocked;
        protected Action<object> onChange;
        private VisualElement previousRoot;
        protected object targetComponent;
        protected EntityLink targetLink;
        private float updateDelayCounter;
        protected FieldInfo FieldInfo;
        protected abstract VisualElement GetField();
        private void SetTarget(object component, Object targetObj) {
            targetComponent = component;
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
            FieldInfo = fieldInfo;
            SetTarget(component, link);
            this.fieldName = fieldNameParam;
            onChange = x => {
                data = x;
                EditorUtility.SetDirty(targetLink);
                
                if (runtimeMode)
                    TargetEntity.SetBoxed(targetComponent);
                else
                    FieldInfo.SetValue(targetComponent, data);

                blocked = false;
            };
            if (data != null) {
                if (runTime) {
                    OnDraw(data, runTime, fieldInfo.FieldType);
                }
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
            var temp = (int)value;
            if(field.value !=temp)
                field.SetValueWithoutNotify(temp);
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
    public class ByteInspector : BaseInspector {
        private IntegerField field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.SetValueWithoutNotify((byte) value);
        }

        protected override void OnCreate() {
            field = new IntegerField(fieldName);
            field.value = default;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke((byte)x.newValue); });
        }

        protected override VisualElement GetField() {
            return field;
        }
    }
    public class FloatInspector : BaseInspector {
        private FloatField field;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            if(blocked) return;
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
            return q.eulerAngles.normalized;
        }
        private Vector4 toVector(Quaternion q) {
            return new Vector4(q.x, q.y, q.z, q.w);
        }
        private Quaternion toQuaternion(Vector3 v) {
            return Quaternion.Euler(v.normalized);
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
        private EntityField hybridEntityField;
        private Entity fieldValue;
        protected override VisualElement GetField() {
            if (!fieldValue.IsNull()) {
                if (fieldValue.Has<ViewLink>()) return hybridEntityField;
            }
            return pureEntityField;
        }

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            fieldValue = (Entity)value;
            //if(fieldValue.IsNull()) return;
            hybridEntityField.UpdateView(fieldValue);
        }

        protected override void OnCreate() {
            hybridEntityField = new EntityField(default);
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
        private int Count;
        private const string ADD_BUTTON = "unity-list-view__add-button";
        private const string REMOVE_BUTTON = "unity-list-view__remove-button";
        protected override VisualElement GetField() {
            return listView;
        }

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            if (value == null) {
                value = new List<Entity>();
            }
            items = (List<Entity>)value;
            Count = items.Count;
            listView.itemsSource = items;
            listView.headerTitle = fieldName;
        }

        protected override void OnCreate() {

            Func<VisualElement> makeItem = () => new EntityField(default);


            void BindItems(VisualElement element, int index) {
                var field = (EntityField)element;
                field.UpdateViewAsElement(items[index], index);
            }

            const int itemHeight = 16;
            listView = new ListView(items, itemHeight, makeItem, BindItems);
            
            listView.selectionType = SelectionType.Multiple;
            listView.style.flexGrow = 1.0f;
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
                items.RemoveLast();
                listView.Rebuild();
            });
            listView.RegisterCallback<DragPerformEvent>(x => {
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

    public class EntityField : VisualElement {
        public Entity value;
        private IntegerField _integerField;
        private ObjectField _objectField;

        public EntityField()  {
            _integerField = new IntegerField($"{value.Index}") {
                isReadOnly = true,
                focusable = false
            };
            _objectField = new ObjectField($"{value.Index}") {
                objectType = typeof(EntityLink)
            };
        }
        public EntityField(Entity entity) {
            value = entity;
            _integerField = new IntegerField($"{value.Index}") {
                isReadOnly = true,
                focusable = false
            };
            _objectField = new ObjectField($"{value.Index}") {
                objectType = typeof(EntityLink)
            };
        }
        
        public void UpdateView(Entity entity, string labelName = null) {
            value = entity;
            if (!value.IsNull()) {
                if (value.Has<ViewLink>()) {
                    var link = value.Get<ViewLink>().Link;
                    if (!Contains(_objectField)) {
                        if(Contains(_integerField))
                            Remove(_integerField);
                        _objectField.value = link;
                        if (labelName != null) {
                            _objectField.label = labelName;
                        }
                        Add(_objectField);
                    }
                }
                else {
                    if (!Contains(_integerField)) {
                        if(Contains(_objectField))
                            Remove(_objectField);
                        //_integerField.name = $"{value.Index}";
                        if (labelName != null) {
                            _integerField.label = labelName;
                        }
                        Add(_integerField);
                    }
                }
            }
            else {
                if (!Contains(_integerField)) {
                    if(Contains(_objectField))
                        Remove(_objectField);
                    _integerField.name = $"{value.Index}";
                    Add(_integerField);
                }
            }
        }

        private static string GetLabel(int index) {
            return $"element[{index}]";
        }
        public void UpdateViewAsElement(Entity entity, int index) {
            value = entity;
            if (!value.IsNull()) {
                if (value.Has<ViewLink>()) {
                    var link = value.Get<ViewLink>().Link;
                    if (Contains(_objectField)) return;
                    if(Contains(_integerField))
                        Remove(_integerField);
                    _objectField.value = link;
                    _objectField.label = GetLabel(index);
                    Add(_objectField);
                }
                else {
                    if (Contains(_integerField)) return;
                    if(Contains(_objectField))
                        Remove(_objectField);
                    _integerField.label = GetLabel(index);
                    _integerField.value = value.Index;
                    Add(_integerField);
                }
            }
            else {
                if (Contains(_integerField)) return;
                if(Contains(_objectField))
                    Remove(_objectField);
                _integerField.label = GetLabel(index);
                _integerField.value = value.Index;
                Add(_integerField);
            }
        }

    }
    
    public class EnumInspector : BaseInspector {
        private EnumField field;

        protected override VisualElement GetField() {
            return field;
        }

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            field.label = fieldName;
            field.Init((Enum) value);
        }

        protected override void OnCreate() {
            field = new EnumField(fieldName);
            field.value = default;
            field.RegisterValueChangedCallback(x => { onChange?.Invoke(x.newValue); });
        }
    }

    public class ObjectReferenceInspector : BaseInspector {
        private ObjectField field;
        private object cached;

        protected override void OnDraw(object value, bool runTime, Type targetType) {
            cached = value;
            field.label = fieldName;
            field.objectType = targetType.GenericTypeArguments[0];
            var id = (int)targetType.GetField("id").GetValue(value);
            field.SetValueWithoutNotify(UnityObjecstCollectionStatic.Instance.GetObject(id));
        }

        protected override void OnCreate() {
            field = new ObjectField(fieldName);
            field.value = null;
            field.RegisterValueChangedCallback(x => {
                if(x.newValue == x.previousValue) return;
                object newValue = Generic.New(typeof(ObjectReference<>), field.objectType, x.newValue);
                onChange?.Invoke(newValue);
                MarkDirtyRepaint();
            });
        }

        protected override VisualElement GetField() {
            return field;
        }
    }
}