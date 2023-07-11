using System;
using UnityEngine;

namespace Wargon.Ecsape {
    using debug = UnityEngine.Debug;

    [CreateAssetMenu]
    public class UnityObjecstCollectionStatic : SingletonSO<UnityObjecstCollectionStatic> {
        public int count;
        public UnityEngine.Object[] data = Array.Empty<UnityEngine.Object>();
        public T GetObject<T>(int id) where T : UnityEngine.Object {
            CheckResize(id);
            return (T)data[id];
        }
        
        // public void SetObject<T>(int id, T obj) where T : UnityEngine.Object {
        //     CheckResize(id);
        //     data[id] = obj;
        //     count++;
        // }
        
        public UnityEngine.Object GetObject(ref int id) {
            return data[id];
        }
        public UnityEngine.Object GetObject(int id) {
            CheckResize(id);
            return data[id];
        }
        public void SetObject<T>(ref int id, T obj) where T : UnityEngine.Object {
            id = count++;
            CheckResize(id);
            data[count] = obj;
        }

        private void CheckResize(int id) {
            if (data.Length - 1 <= count || data.Length - 1 <= id) {
                Array.Resize(ref data, id + 2);
            }
        }
    }
}
