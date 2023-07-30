using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Wargon.Ecsape.Components;
using Object = UnityEngine.Object;

namespace Wargon.Ecsape {
    public interface IObjectPool {
        static IObjectPool Instance { get; protected set; }
        Transform Spawn(Transform prefab, Vector3 position, Quaternion rotation);
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);
        EntityLink Spawn(EntityLink prefab, Vector3 position, Quaternion rotation);
        void Release(Transform view, int id);
        void Release(GameObject view, int id);
        void Release(EntityLink view, int id);
    }

    [Serializable]
    public struct Pooled : IComponent {
        public int id;
        public float lifeTime;
        public float lifeTimeDefault;
    }

    public sealed class FastQueue<T> {
        private T[] items;
        private int count;
        private int len;

        private Queue<int> Ints;
        public FastQueue() {
            len = 8;
            items = new T[len];
            count = 0;
        }

        private void Resize() {
            if (len <= count) {
                len *= 2;
                Array.Resize(ref items, len);
            }
        }
        public void Enqueue(T item) {
            Resize();
            items[count] = item;
            count++;
        }

        public T Dequeue() {
            if (count == 0) throw new Exception("Queue is empty");
            return items[count--];
        }
    }

    public sealed class GameObjectPool : IObjectPool {
        public GameObjectPool() {
            IObjectPool.Instance = this;
        }
        private readonly Dictionary<int, IObjectPool<Transform>> transformPools = new ();
        private readonly Dictionary<int, IObjectPool<GameObject>> gameObjectPools = new ();
        private readonly Dictionary<int, IObjectPool<EntityLink>> entityLinkPools = new();
        private Vector2 UnActivePosition = new (100000, 100000);
        public Transform Spawn(Transform prefab, Vector3 position, Quaternion rotation) {
            var id = prefab.GetInstanceID();
            if (transformPools.TryGetValue(id, out var pool)) {
                var item = pool.Get();
                item.position = position;
                item.rotation = rotation;
                return item;
            }
            var newPool = new ObjectPool<Transform>(
                () => {
                    var t = Object.Instantiate(prefab);
                    t.gameObject.SetActive(false);
                    return t;
                }, 
                x => x.gameObject.SetActive(true), 
                x=> x.gameObject.SetActive(false));
            transformPools.Add(id, newPool);
            return Spawn(prefab, position,rotation);
        }
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation) {
            var id = prefab.GetInstanceID();
            if (gameObjectPools.TryGetValue(id, out var pool)) {
                var item = pool.Get();
                item.transform.position = position;
                item.transform.rotation = rotation;
                return item;
            }
            var newPool = new ObjectPool<GameObject>(
                () => Object.Instantiate(prefab),
                x => x.SetActive(true),
                x=> x.SetActive(false));
            
            gameObjectPools.Add(id, newPool);
            return Spawn(prefab, position, rotation);
        }
        
        public EntityLink Spawn(EntityLink prefab, Vector3 position, Quaternion rotation) {
            var id = prefab.GetInstanceID();
            if (entityLinkPools.TryGetValue(id, out var pool)) {
                var x = pool.Get();
                ref var e = ref x.Entity;
                
                ref var translation = ref e.Get<Translation>();
                translation.rotation = rotation;
                translation.position = position;
                e.Remove<StaticTag>();
                
                ref var pooled = ref e.Get<Pooled>();
                pooled.lifeTime = pooled.lifeTimeDefault;
                pooled.id = id;
                
                e.Get<ViewLink>().Link = x;
                return x;
            }
            
            var newPool = new ObjectPool<EntityLink>(
                () => {
                    var o = Object.Instantiate(prefab);
                    if (!o.IsLinked) {
                        var e = World.Default.CreateEntity();
                        o.Link(ref e);
                    }
                
                    o.Entity.Add<StaticTag>();
                    o.gameObject.SetActive(false);
                    return o;
                    
                },
                x => {
                    x.gameObject.SetActive(true);
                },
                x => {
                    
                });
            
            entityLinkPools.Add(id, newPool);
            return Spawn(prefab, position, rotation);
        }

        public void Release(Transform view, int id) {
            if(!transformPools.ContainsKey(id)) return;
            //view.gameObject.SetActive(false);
            transformPools[id].Release(view);
        }
        public void Release(GameObject view, int id) {
            if(!gameObjectPools.ContainsKey(id)) return;
            //view.gameObject.SetActive(false);
            gameObjectPools[id].Release(view);
        }

        public void Release(EntityLink view, int id) {
            if (entityLinkPools.TryGetValue(id, out var pool)) {
                view.Entity.Add<StaticTag>();
                view.gameObject.SetActive(false);
                pool.Release(view);
            }
        }
    }

    public struct PoolView : IComponent {
        public Transform Value;
    }

}