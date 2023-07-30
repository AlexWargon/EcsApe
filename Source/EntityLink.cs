using System;
using System.Collections.Generic;
using UnityEngine;
using Wargon.Ecsape.Components;

namespace Wargon.Ecsape
{
    [DisallowMultipleComponent]
    public class EntityLink : MonoBehaviour, IEntityLink, ISerializationCallbackReceiver {
        
        private int id;
        private bool _isLinked;
        public string WorldName = "Default";
        public ConvertOption option = ConvertOption.Stay;
        [SerializeReference] public List<object> Components;

        public int ComponentsCount = 0;
        public bool IsLinked => _isLinked;
        private Entity nullEntity;
        public ref Entity Entity => ref World.GetEntity(id);
        public World World;
        
        private void Start() {
            if(IsLinked) return;
            World = World.GetOrCreate(WorldName);
            var e = World.CreateEntity();
            e.Add(new EntityLinkSpawnedEvent{Link = this});
        }

        public void Link(ref Entity entity) {
            if(_isLinked) return;
            id = entity.Index;
            World = entity.World;
            foreach (var component in Components) {
                entity.AddBoxed(component);
            }
            
            switch (option) {
                case ConvertOption.Destroy:
                    Destroy(this);
                    break;
                case ConvertOption.DestroyComponents:
                    break;
                case ConvertOption.Stay:
                    entity.Add(new ViewLink{Link = this});
                    entity.Add(new ViewGO{GameObject = gameObject});
                    var cacheTrasform = this.transform;
                    entity.Add(new TransformReference{value = cacheTrasform});
                    entity.Add(new Translation{position = cacheTrasform.position, rotation = cacheTrasform.rotation, scale = cacheTrasform.localScale});
                    break;
            }

            _isLinked = true;
        }

        public void OnBeforeSerialize() {
            
            if (Components == null) Components = new List<object>();
            Components.RemoveAll(item => ReferenceEquals(item,null));
            ComponentsCount = Components.Count;
        }

        public void OnAfterDeserialize() {
            
        }

        public static Entity Spawn(EntityLink prefab, Vector3 position, Quaternion rotation, World world) {
            var obj = Instantiate(prefab, position, rotation);
            var e = world.CreateEntity();
            e.Add(new EntityLinkSpawnedEvent{Link = obj});
            return e;
        }
    }

    [Serializable]
    public struct ComponentData {
        [SerializeReference] public object Data;
        [SerializeReference] public Type ComponentType;

        public void Update(object component) {
            Data = component;
            ComponentType = component.GetType();
        }
    }
    
    public enum ConvertOption {
        Stay,
        Destroy,
        DestroyComponents
    }
    
    public interface IEntityLink {
        ref Entity Entity { get; }
        void Link(ref Entity entity);
    }

    public struct EntityLinkSpawnedEvent : IComponent {
        public IEntityLink Link;
    }
    internal sealed class ConvertEntitySystem : ISystem {
        [With(typeof(EntityLinkSpawnedEvent))] Query _gameObjects;
        IPool<EntityLinkSpawnedEvent> _pool;
        public void OnUpdate(float deltaTime) {
            if(_gameObjects.IsEmpty) return;
            foreach (ref var entity in _gameObjects) {
                ref var go = ref _pool.Get(ref entity);
                go.Link.Link(ref entity);
            }
        }
    }

    [Serializable]
    public struct ObjectReference<T> where T : UnityEngine.Object {
        public int id;
        public ObjectReference(T obj) {
            id = UnityObjecstCollectionStatic.Instance.count++;
            UnityObjecstCollectionStatic.Instance.SetObject(ref id, obj);
            UnityEngine.Debug.Log($"construct {id}");
        }
    
        public void Initialize() {
            id = UnityObjecstCollectionStatic.Instance.count++;
        }
        public T Value {
            get => UnityObjecstCollectionStatic.Instance.GetObject<T>(id);
            set => UnityObjecstCollectionStatic.Instance.SetObject(ref id, value);
        }
    
        public static implicit operator ObjectReference<T>(T obj) {
            return new ObjectReference<T>(obj);
        }
    
        public static implicit operator T(ObjectReference<T> reference) {
            return reference.Value;
        }
    }
    
    public abstract class SingletonSO<T> : ScriptableObject where T : ScriptableObject {
        private static T instance = null;
        public static T Instance {
            get {
                if (instance == null) {
                    var results = Resources.FindObjectsOfTypeAll<T>();
                    if (results.Length == 0) {
                        UnityEngine.Debug.LogError($"No any instances of {typeof(T)} in project");
                        return null;
                    }
                    if (results.Length > 1) {
                        UnityEngine.Debug.LogError($"More then one instance {typeof(T)} in project");
                        return null;
                    }

                    instance = results[0];
                    instance.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                return instance;
            }
        }
    }
}
