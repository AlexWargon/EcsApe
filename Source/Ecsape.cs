using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Wargon.Ecsape.Components;

namespace Wargon.Ecsape {
    public struct Option {
        public const int INLINE = 256;
    }

    public static class Generic {
        public static object New(Type genericType, Type elementsType, params object[] parameters) {
            return Activator.CreateInstance(genericType.MakeGenericType(elementsType), parameters);
        }
    }


    internal sealed class DirtyQueries {
        private readonly Query[] items;
        private int count;

        public DirtyQueries(int size) {
            items = new Query[size];
            count = 0;
        }

        public void Add(Query query) {
            items[count] = query;
            count++;
        }

        public void RemoveLast() {
            count--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateQueries() {
            if (count < 1) return;
            for (var i = 0; i < count; i++) items[i].Update();
            count = 0;
        }
    }


    namespace Serializable {
        [StructLayout(LayoutKind.Sequential)]
        [Serializable]
        public struct Vector2 {
            public float x;
            public float y;

            public Vector2(float x, float y) {
                this.x = x;
                this.y = y;
            }

            public static implicit operator UnityEngine.Vector3(Vector2 rValue) {
                return new UnityEngine.Vector3(rValue.x, rValue.y, 0F);
            }

            public static implicit operator UnityEngine.Vector2(Vector2 rValue) {
                return new UnityEngine.Vector2(rValue.x, rValue.y);
            }

            public static implicit operator Vector2(UnityEngine.Vector2 rValue) {
                return new Vector2(rValue.x, rValue.y);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [Serializable]
        public struct Vector3 {
            public float x;
            public float y;
            public float z;

            public Vector3(float x, float y, float z) {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public static implicit operator UnityEngine.Vector2(Vector3 rValue) {
                return new UnityEngine.Vector2(rValue.x, rValue.y);
            }

            public static implicit operator UnityEngine.Vector3(Vector3 rValue) {
                return new UnityEngine.Vector3(rValue.x, rValue.y, rValue.z);
            }

            public static implicit operator Vector3(UnityEngine.Vector3 rValue) {
                return new Vector3(rValue.x, rValue.y, rValue.z);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [Serializable]
        public struct Quaternion {
            public float x;
            public float y;
            public float z;
            public float w;

            public Quaternion(float rX, float rY, float rZ, float rW) {
                x = rX;
                y = rY;
                z = rZ;
                w = rW;
            }

            public static implicit operator UnityEngine.Quaternion(Quaternion rValue) {
                return new UnityEngine.Quaternion(rValue.x, rValue.y, rValue.z, rValue.w);
            }

            public static implicit operator Quaternion(UnityEngine.Quaternion rValue) {
                return new Quaternion(rValue.x, rValue.y, rValue.z, rValue.w);
            }
        }
    }


    public interface IAspect {
        IEnumerable<Type> Link();

        IEnumerable<Type> Create(params Type[] types) {
            return types;
        }
    }

    public interface IAspect<T> : IAspect where T : struct {
        ref T value { get; }
    }

    public struct PlayerAspect : IAspect {
        public Entity Entity;

        public Translation Translation;

        public IEnumerable<Type> Link() {
            return new[] { typeof(Translation), typeof(StaticTag) };
        }
    }

    public static class UnsafeHelp {
        public static unsafe void* ResizeUnsafeUtility<T>(void* ptr, int oldSize, int newSize, Allocator allocator)
            where T : unmanaged {
            var oldSizeInBtyes = UnsafeUtility.SizeOf(typeof(T)) * oldSize;
            var newSizeOnBytes = UnsafeUtility.SizeOf(typeof(T)) * newSize;
            var newPtr = UnsafeUtility.Malloc(newSizeOnBytes, UnsafeUtility.AlignOf<T>(), allocator);
            UnsafeUtility.MemCpy(newPtr, ptr, oldSizeInBtyes);
            UnsafeUtility.MemClear(ptr, oldSizeInBtyes);
            return newPtr;
        }

        public static unsafe void* Resize<T>(void* ptr, int oldSize, int newSize) where T : struct {
            var oldSizeInBytes = Marshal.SizeOf(typeof(T)) * oldSize;
            var newSizeOnBytes = Marshal.SizeOf(typeof(T)) * newSize;
            var newPtr = (void*)Marshal.AllocHGlobal(newSizeOnBytes);
            Buffer.MemoryCopy(ptr, newPtr, newSizeOnBytes, oldSizeInBytes);
            Marshal.FreeHGlobal((IntPtr)ptr);
            return newPtr;
        }

        public static unsafe T* Resize<T>(T* ptr, int oldSize, int newSize) where T : unmanaged {
            var oldSizeInBytes = sizeof(T) * oldSize;
            var newSizeOnBytes = sizeof(T) * newSize;
            var newPtr = (T*)Marshal.AllocHGlobal(newSizeOnBytes);
            Buffer.MemoryCopy(ptr, newPtr, newSizeOnBytes, oldSizeInBytes);
            Marshal.FreeHGlobal((IntPtr)ptr);
            return newPtr;
        }

        public static unsafe delegate*<T, void>* Resize<T>(delegate*<T, void>* ptr, int oldSize, int newSize) {
            var oldSizeInBytes = sizeof(delegate*<T, void>) * oldSize;
            var newSizeOnBytes = sizeof(delegate*<T, void>) * newSize;
            var newPtr = (delegate*<T, void>*)Marshal.AllocHGlobal(newSizeOnBytes);
            Buffer.MemoryCopy(ptr, newPtr, newSizeOnBytes, oldSizeInBytes);
            Marshal.FreeHGlobal((IntPtr)ptr);
            return newPtr;
        }

        public static unsafe delegate*<T1, T2, void>* Resize<T1, T2>(delegate*<T1, T2, void>* ptr, int oldSize,
            int newSize) {
            var oldSizeInBytes = sizeof(delegate*<T1, T2, void>) * oldSize;
            var newSizeOnBytes = sizeof(delegate*<T1, T2, void>) * newSize;
            var newPtr = (delegate*<T1, T2, void>*)Marshal.AllocHGlobal(newSizeOnBytes);
            Buffer.MemoryCopy(ptr, newPtr, newSizeOnBytes, oldSizeInBytes);
            Marshal.FreeHGlobal((IntPtr)ptr);
            return newPtr;
        }

        public static unsafe void AssertSize<T>(ref delegate*<T, void>* ptr, ref int capacity, int size) {
            if (size == capacity) {
                ptr = Resize(ptr, capacity, capacity * 2);
                capacity *= 2;
            }
        }

        public static unsafe void AssertSize<T1, T2>(ref delegate*<T1, T2, void>* ptr, ref int capacity, int size) {
            if (size == capacity) {
                ptr = Resize(ptr, capacity, capacity * 2);
                capacity *= 2;
            }
        }
    }

    internal static class Logs {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Show(object massage) {
            Debug.Log(massage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Error(object massage) {
            Debug.LogError(massage);
        }
    }
}