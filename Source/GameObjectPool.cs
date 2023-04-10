using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
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

    public static class EntityPoolExtensions {
        public static void FreeView(in this Entity entity) {
            entity.Remove<Pooled>();
        }
    }
    [Serializable]
    public struct Pooled : IComponent {
        public int id;
        public float lifeTime;
        public float lifeTimeDefault;
    }

    public sealed class MyObjectPool : IObjectPool {
        public MyObjectPool() {
            IObjectPool.Instance = this;
        }
        private class Pool<T> where T : UnityEngine.Component {
            public delegate T PoolAction(T item);

            private T prefab;
            private Queue<T> buffer;
            private PoolAction onCreate;
            private Action<T> onSpawn;
            private Action<T> onHide;
            private int size;
            private int count;
            public Transform parent;
            public Pool(T prefab, int size) {
                parent = new GameObject($"[pool:{prefab.name}]").transform;
                buffer = new Queue<T>();
                this.prefab = prefab;
                this.size = size;
                count = size;
            }

            public Pool<T> Populate() {
                for (int i = 0; i < size; i++) {
                    buffer.Enqueue(onCreate(prefab));
                }
                return this;
            }

            public Pool<T> OnCreate(PoolAction onCreate) {
                this.onCreate = onCreate;
                return this;
            }

            public Pool<T> OnSpawn(Action<T> onSpawn) {
                this.onSpawn = onSpawn;
                return this;
            }

            public Pool<T> OnHide(Action<T> onHide) {
                this.onHide = onHide;
                return this;
            }
            public void Back(T item) {
                buffer.Enqueue(item);
                onHide(item);
            }

            public void Enqueue(T item) => buffer.Enqueue(item);

            private void AddSize(int add) {
                for (int i = 0; i < add; i++) {
                    buffer.Enqueue(onCreate(prefab));
                }
            }
            public T Spawn() {
                if(buffer.Count == 0) AddSize(16);
                var e = buffer.Dequeue();
                if (e.gameObject.activeInHierarchy) {
                    buffer.Enqueue(e);
                    AddSize(16);
                    return Spawn();
                }
                onSpawn(e);
                return e;
            }
        }

        private Dictionary<int, Pool<EntityLink>> pools = new();

        public Transform Spawn(Transform prefab, Vector3 position, Quaternion rotation) {
            return null;
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation) {
            return null;
        }

        public EntityLink Spawn(EntityLink prefab, Vector3 position, Quaternion rotation) {
            var id = prefab.GetInstanceID();
            if (pools.TryGetValue(id, out var pool)) {
                var x = pool.Spawn();
                ref var e = ref x.Entity;
                e.Get<Translation>().rotation = rotation;
                e.Get<Translation>().position = position;
                e.Remove<StaticTag>();
                ref var pooled = ref e.Get<Pooled>();
                pooled.lifeTime = pooled.lifeTimeDefault;
                pooled.id = id;
                e.Get<ViewLink>().Link = x;
                return x;
            }

            var newPool = new Pool<EntityLink>(prefab, 16);
            newPool.OnCreate(x => {
                var o = Object.Instantiate(x, newPool.parent);
                if (!o.linked) {
                    var e = World.Default.CreateEntity();
                    o.Link(ref e);
                }
                
                o.Entity.Add<StaticTag>();
                o.gameObject.SetActive(false);
                return o;
            }).OnSpawn(x => {
                x.gameObject.SetActive(true);

            }).OnHide(x => {
                x.Entity.Add<StaticTag>();
                x.gameObject.SetActive(false);
            }).Populate();
            pools.Add(id, newPool);
            return pools[id].Spawn();
        }

        public void Release(Transform view, int id) {
            
        }

        public void Release(GameObject view, int id) {
            
        }

        public void Release(EntityLink view, int id) {
            if(pools.TryGetValue(id, out var pool))
                pool.Back(view);
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
                    if (t.TryGetComponent(out EntityLink link)) {
                        var e = World.Default.CreateEntity();
                        link.Link(ref e);
                    }
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
                    if (!o.linked) {
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
}