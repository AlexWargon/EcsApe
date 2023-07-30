using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Wargon.Ecsape {
    public readonly struct Component<T> where T : struct, IComponent {
        public static readonly int Index;
        public static readonly Type Type;
        public static readonly bool IsSingleTone;
        public static readonly bool IsTag;
        public static readonly bool IsEvent;
        public static readonly bool IsClearOnEnfOfFrame;
        public static readonly bool IsDisposable;
        public static readonly int SizeInBytes;
        public static readonly bool IsOnCreate;
        public static readonly bool IsUnmanaged;
        public static readonly bool HasUnityReference;

        static Component() {
            Type = typeof(T);
            Index = Component.GetIndex(Type);
            ref var componentType = ref Component.GetComponentType(Index);
            IsSingleTone = componentType.IsSingletone;
            IsTag = componentType.IsTag;
            IsEvent = componentType.IsEvent;
            IsClearOnEnfOfFrame = componentType.IsClearOnEnfOfFrame;
            IsDisposable = componentType.IsDisposable;
            SizeInBytes = componentType.SizeInBytes;
            IsOnCreate = componentType.IsOnCreate;
            IsUnmanaged = componentType.IsUnmanaged;
            HasUnityReference = componentType.HasUnityReference;
            if (IsClearOnEnfOfFrame) {
                DefaultClearSystems.Add<ClearEventsSystem<T>>();
            }
        }

        public static ComponentType AsComponentType() {
            return new ComponentType(Index, IsSingleTone, IsTag, IsEvent, IsClearOnEnfOfFrame, IsDisposable, Type.Name,
                SizeInBytes, IsOnCreate, IsUnmanaged, HasUnityReference);
        }
    }

    public interface IComponent { }

    public interface ISingletoneComponent { }

    public interface IEventComponent { }

    public interface IClearOnEndOfFrame { }

    public interface IOnAddToEntity {
        void OnAdd();
    }

    [Serializable]
    public readonly struct ComponentType : IEquatable<ComponentType> {
        public readonly int Index;
        public readonly bool IsSingletone;
        public readonly bool IsTag;
        public readonly bool IsEvent;
        public readonly bool IsClearOnEnfOfFrame;
        public readonly bool IsDisposable;
        public readonly string Name;
        public readonly int SizeInBytes;
        public readonly bool IsOnCreate;
        public readonly bool IsUnmanaged;
        public readonly bool HasUnityReference;

        public ComponentType(
            int index, bool isSingletone, bool isTag, bool isEvent, bool clearOnEnfOfFrame, bool disposable,
            string name, int size, bool isOnCreate, bool isUnmanaged, bool hasUnityReference) {
            Index = index;
            IsSingletone = isSingletone;
            IsTag = isTag;
            IsEvent = isEvent;
            IsClearOnEnfOfFrame = clearOnEnfOfFrame;
            IsDisposable = disposable;
            Name = name;
            SizeInBytes = size;
            IsOnCreate = isOnCreate;
            IsUnmanaged = isUnmanaged;
            HasUnityReference = hasUnityReference;
        }

        public bool Equals(ComponentType other) {
            return Index == other.Index;
        }

        public override int GetHashCode() {
            return Index;
        }
    }

    public struct Component {
        private static readonly Dictionary<int, Type> typeByIndex;
        private static readonly Dictionary<Type, int> indexByType;
        private static ComponentType[] componentTypes;
        private static int count;
        public const int DESTROY_ENTITY = 0;

        static Component() {
            typeByIndex = new Dictionary<int, Type>();
            indexByType = new Dictionary<Type, int>();
            componentTypes = new ComponentType[32];
        }

        public static int GetIndex(Type type) {
            if (indexByType.TryGetValue(type, out var idx)) return idx;
            var index = count;
            indexByType.Add(type, index);
            typeByIndex.Add(index, type);
            var componentType = new ComponentType
            (
                index,
                typeof(ISingletoneComponent).IsAssignableFrom(type),
                type.GetFields().Length == 0,
                typeof(IEventComponent).IsAssignableFrom(type),
                typeof(IClearOnEndOfFrame).IsAssignableFrom(type),
                typeof(IDisposable).IsAssignableFrom(type),
                type.Name,
                Marshal.SizeOf(type),
                typeof(IOnAddToEntity).IsAssignableFrom(type),
                type.IsUnManaged(),
                type.HasUnityReferenceFields()
            );
            AddInfo(ref componentType, index);
            count++;
            return index;
        }

        private static void AddInfo(ref ComponentType type, int index) {
            if (componentTypes.Length - 1 <= index) Array.Resize(ref componentTypes, index + 16);
            componentTypes[index] = type;
        }

        public static Type GetTypeOfComponent(int index) {
            return typeByIndex[index];
        }

        public static ref ComponentType GetComponentType(int index) {
            return ref componentTypes[index];
        }
    }

    public static class ComponentExtensions {
        internal static Type GetTypeFromIndex(this int index) {
            return Component.GetTypeOfComponent(index);
        }

        public static bool IsUnManaged(this Type t) {
            var result = false;

            if (t.IsPrimitive || t.IsPointer || t.IsEnum)
                result = true;
            else if (t.IsGenericType || !t.IsValueType)
                result = false;
            else
                result = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .All(x => x.FieldType.IsUnManaged());
            return result;
        }

        public static bool HasUnityReferenceFields(this Type type) {
            var fields = type.GetFields();
            foreach (var fieldInfo in fields) {
                if (fieldInfo.FieldType.IsSubclassOf(typeof(UnityEngine.Object))) {
                    return true;
                }
            }

            return false;
        }
    }
}