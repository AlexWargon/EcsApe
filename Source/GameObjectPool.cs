using System;
using System.Collections.Generic;
using System.Linq;
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

    public static class EntityPoolExtensions {
        public static void FreeView(in this Entity entity) {
            entity.Remove<Active>();
        }
    }
    public struct Active : IComponent {
        public int id;
        public EntityLink view;
        public float lifeTime;
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
            private PoolAction onSpawn;
            private Action<T> onHide;
            private int size;
            private int count;
            public Pool(T prefab, int size, PoolAction onCreate, PoolAction onSpawn, Action<T> onHide) {
                buffer = new Queue<T>();
                this.prefab = prefab;
                this.onCreate = onCreate;
                this.onSpawn = onSpawn;
                this.onHide = onHide;
                for (int i = 0; i < size; i++) {
                    buffer.Enqueue(this.onCreate(prefab));
                }

                this.size = size;
                count = size;
            }
            public void Back(T item) {
                onHide(item);
                buffer.Enqueue(item);
            }

            public void Enqueue(T item) => buffer.Enqueue(item);
            public T Spawn() {

                var e = buffer.Dequeue();
                if (e.gameObject.activeSelf) {
                    
                    for (int i = 0; i < 16; i++) {
                        buffer.Enqueue(onCreate(prefab));
                    }
                    buffer.Enqueue(e);

                    return onSpawn(buffer.Dequeue());
                }
                return onSpawn(e);
            }
        }

        private Dictionary<int, Pool<EntityLink>> pools = new();
        private World world;
        public void SetWorld(World world) => this.world = world;
        public Transform Spawn(Transform prefab, Vector3 position, Quaternion rotation) {
            return null;
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation) {
            return null;
        }

        public EntityLink Spawn(EntityLink prefab, Vector3 position, Quaternion rotation) {
            var id = prefab.GetInstanceID();
            if (pools.TryGetValue(id, out var pool)) {
                return pool.Spawn();
            }
            pools.Add(id, new Pool<EntityLink>(prefab, 132,
                link => {
                var o = Object.Instantiate(link);
                
                if (!o.linked) {
                    var e = world.CreateEntity();
                    o.Link(ref e);
                }
                
                o.gameObject.SetActive(false);
                return o;
            }, link => {
   
                link.Entity.Add(new Active {
                    id = id,
                    view = link,
                    lifeTime = 0.4f
                });
                    
                ref var t = ref link.Entity.Get<Translation>();
                t.position = position;
                t.rotation = rotation;
                // link.transform.position = position;
                // link.transform.rotation = rotation;
                link.gameObject.SetActive(true);
                return link;
            }, link => {
                //link.gameObject.SetActive(false);
            }));
            return pools[id].Spawn();
        }

        public void Release(Transform view, int id) {
            
        }

        public void Release(GameObject view, int id) {
            
        }

        public void Release(EntityLink view, int id) {
            view.gameObject.SetActive(false);
            pools[id].Back(view);
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
                    return t;
                }, 
                x => x.gameObject.SetActive(true), 
                x=> x.gameObject.SetActive(false));
            transformPools.Add(id, newPool);
            return Spawn(prefab, position, rotation);
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
                return pool.Get();
            }
            
            var newPool = new ObjectPool<EntityLink>(
                () => {
                    var e = Object.Instantiate(prefab);
                    var ee = World.Default.CreateEntity();
                    e.Link(ref ee);

                    return e;
                },
                x => {
                    x.Entity.Add(new Active {
                        id = id,
                        view = x
                    });
                    
                    ref var t = ref x.Entity.Get<Translation>();
                    t.position = position;
                    t.rotation = rotation;
                    x.gameObject.SetActive(true);
                }, 
                x=> x.gameObject.SetActive(false));
            
            entityLinkPools.Add(id, newPool);
            return entityLinkPools[id].Get();
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
            //if(!entityLinkPools.ContainsKey(id)) return;
            entityLinkPools[id].Release(view);
        }
    }
}