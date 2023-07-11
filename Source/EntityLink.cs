using System;
using System.Collections.Generic;
using UnityEngine;
using Wargon.Ecsape.Components;

namespace Wargon.Ecsape
{
    [DisallowMultipleComponent]
    public class EntityLink : MonoBehaviour, IEntityLink, ISerializationCallbackReceiver {
        public string WorldName = "Default";
        public bool Linked => linked;
        private bool linked;
        public ConvertOption option = ConvertOption.Stay;
        private int id;
        public ref Entity Entity => ref World.GetOrCreate(WorldName).GetEntity(id);
        public World World => World.GetOrCreate(WorldName);
        [SerializeReference] public List<object> Components = new ();
        private void Start() {
            if(Linked) return;
            var e = World.CreateEntity();
            e.Add(new GameObjectSpawnedEvent{Link = this});
            
        }

        public void Link(ref Entity entity) {
            if(linked) return;
            id = entity.Index;
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

            linked = true;
        }
        //
        // private void OnDestroy() {
        //     if(option != ConvertOption.Destroy)
        //         if (!Entity.IsNull()) {
        //             Entity.Destroy();
        //         }
        // }

        public void OnBeforeSerialize() {
            Components.RemoveAll(item => ReferenceEquals(item,null));
        }

        public void OnAfterDeserialize() {
            
        }

        // public static EntityLink Spawn(EntityLink prefab, Vector3 position, Quaternion rotation, World world) {
        //     var obj = Instantiate(prefab, position, rotation);
        //     var e = world.CreateEntity();
        //     obj.Link(ref e);
        //     return obj;
        // }

        public static void Spawn(EntityLink prefab, Vector3 position, Quaternion rotation, World world) {
            var obj = Instantiate(prefab, position, rotation);
            var e = world.CreateEntity();
            e.Add(new GameObjectSpawnedEvent{Link = obj});
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

    public struct GameObjectSpawnedEvent : IComponent {
        public IEntityLink Link;
    }
    internal sealed class ConvertEntitySystem : ISystem, IOnCreate {
        private Query _gameObjects;
        private IPool<GameObjectSpawnedEvent> _pool;
        
        public void OnCreate(World world) {
            _gameObjects = world.GetQuery().With<GameObjectSpawnedEvent>();
        }
        
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

    // [System.Serializable]
    // public struct ObjectReference<T> where T : UnityEngine.Object {
    //
    //     public uint id;
    //
    //     public ObjectReference(T obj) {
    //         this.id = 0u;
    //         RuntimeObjectReference.GetObject(ref this.id, obj);
    //     }
    //
    //     public T Value => RuntimeObjectReference.GetObject<T>(ref this.id, null);
    //
    //     public static implicit operator ObjectReference<T>(T obj) {
    //         return new ObjectReference<T>(obj);
    //     }
    //
    //     public static implicit operator T(ObjectReference<T> reference) {
    //         return reference.Value;
    //     }
    //
    // }
    //
    // public class ObjectReferenceData {
    //
    //     private readonly System.Collections.Generic.Dictionary<int, uint> objectInstanceIdToIdx = new System.Collections.Generic.Dictionary<int, uint>();
    //     private UnityEngine.Object[] objects;
    //     private uint nextId = 1u;
    //
    //     public T ReadObject<T>(uint id) where T : UnityEngine.Object {
    //         var idx = id - 1u;
    //         if (id <= 0u || idx > this.objects.Length) return null;
    //         return (T)this.objects[idx];
    //     }
    //
    //     public T GetObject<T>(ref uint id, T obj) where T : UnityEngine.Object {
    //         if (id == 0) {
    //             if (obj == null) return null;
    //             var instanceId = obj.GetInstanceID();
    //             /*if (instanceId <= 0) {
    //                 throw new System.Exception("Persistent asset is required");
    //             }*/
    //             if (this.objectInstanceIdToIdx.TryGetValue(instanceId, out var index) == true) {
    //                 id = index + 1u;
    //                 return (T)this.objects[index];
    //             }
    //
    //             {
    //                 var size = this.nextId * 2u;
    //                 System.Array.Resize(ref this.objects, (int)size);
    //                 id = this.nextId++;
    //                 var idx = id - 1;
    //                 this.objectInstanceIdToIdx.Add(instanceId, idx);
    //                 this.objects[idx] = obj;
    //                 return obj;
    //             }
    //         } else {
    //             var idx = id - 1u;
    //             return (T)this.objects[idx];
    //         }
    //     }
    //     
    // }
    //
    // public static class RuntimeObjectReference {
    //
    //     private static ObjectReferenceData dataArr;
    //
    //     [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
    //     public static void Initialize() {
    //
    //         dataArr = null;
    //         
    //     }
    //     
    //     private static ObjectReferenceData GetData() {
    //
    //         if (dataArr == null) {
    //             dataArr = new ObjectReferenceData();
    //         }
    //         return dataArr;
    //
    //     }
    //
    //     public static T ReadObject<T>(uint id, ushort worldId) where T : UnityEngine.Object {
    //         if (worldId == 0) return null;
    //         var data = GetData();
    //         return data.ReadObject<T>(id);
    //     }
    //
    //
    //     public static T GetObject<T>(ref uint id, T obj) where T : UnityEngine.Object {
    //         var data = GetData();
    //         return data.GetObject(ref id, obj);
    //     }
    //
    // }
    
    
    
    
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
