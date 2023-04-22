using System;

using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Wargon.Ecsape {
    public static class JobsExtensions {
        /// <param name="entitiesSize"> query.Count</param>
        public static void ScheduleAndComplete<TJobParallelFor>(this TJobParallelFor job, Query query) where TJobParallelFor : struct, IJobParallelFor {
            job.Schedule(query.count, 64).Complete();
        }
        public static void Schedule<TJobParallelFor>(this TJobParallelFor job, Query query) where TJobParallelFor : struct, IJobParallelFor {
            job.Schedule(query.count, 64);
        }
        public unsafe static class ForEachJob<TComponent1, TComponent2>
            where TComponent1 : unmanaged, IComponent
            where TComponent2 : unmanaged, IComponent 
        {
            internal static GCHandle Handle;
            internal static IntPtr CachedFn;
            internal static FunctionPointer<JobForEachDelegateInternal>  CahcedFunc;
            [BurstCompile(CompileSynchronously = true)]
            internal struct JobDelegateRunner : IJobParallelFor{
                [NativeDisableUnsafePtrRestriction]
                public IntPtr fn;
                public FunctionPointer<JobForEachDelegateInternal> func;
                [NativeDisableUnsafePtrRestriction]
                public void* Pool1;
                [NativeDisableUnsafePtrRestriction]
                public void* Pool2;
                public NativeQuery Query;
                public void Execute(int index) {
                    var e = Query.GetEntity(index);
                    func.Invoke(fn,Pool1, Pool2, e);
                }

                [BurstCompile(CompileSynchronously = true)]
                public static void ForEachDelegate(IntPtr fn, void* poolPtr1, void* pool2Ptr, int entity) {
                    var del = new FunctionPointer<JobForEachDelegate>(fn);
                    ref var p1 = ref UnsafeUtility.AsRef<NativePool<TComponent1>>(poolPtr1);
                    ref var p2 = ref UnsafeUtility.AsRef<NativePool<TComponent2>>(pool2Ptr);
                    del.Invoke(ref p1.Get(entity),ref p2.Get(entity));
                }
            }
            public delegate void JobForEachDelegate(ref TComponent1 c1, ref TComponent2 c2);
        }
        internal unsafe delegate void JobForEachDelegateInternal(IntPtr fn, void* c1, void* c2, int entity);
        public unsafe static void ForEach<TComponent1, TComponent2>(this Query query, ForEachJob<TComponent1, TComponent2>.JobForEachDelegate action) 
            where TComponent1 : unmanaged, IComponent
            where TComponent2 : unmanaged, IComponent
        {
            if(!ForEachJob<TComponent1, TComponent2>.CahcedFunc.IsCreated)
            {
                ForEachJob<TComponent1, TComponent2>.Handle = GCHandle.Alloc(action);
                ForEachJob<TComponent1, TComponent2>.CachedFn = Marshal.GetFunctionPointerForDelegate(action);
                ForEachJob<TComponent1, TComponent2>.CahcedFunc =
                    BurstCompiler.CompileFunctionPointer<JobForEachDelegateInternal>(
                        ForEachJob<TComponent1, TComponent2>.JobDelegateRunner.ForEachDelegate);
            }
            var job = new ForEachJob<TComponent1, TComponent2>.JobDelegateRunner {
                fn = ForEachJob<TComponent1, TComponent2>.CachedFn,
                func = ForEachJob<TComponent1, TComponent2>.CahcedFunc,
                Query = query.AsNative(),
                Pool1 = query.WorldInternal.GetPool<TComponent1>().AsNative().GetPtr(),
                Pool2 = query.WorldInternal.GetPool<TComponent2>().AsNative().GetPtr()
            };
            job.Schedule(query.count, 1).Complete();
            //job.Run(query.Count);
        }

        public static class ForEachJob2<TComponent1, TComponent2>
            where TComponent1 : unmanaged, IComponent
            where TComponent2 : unmanaged, IComponent 
        {
            internal static GCHandle Handle;
            internal static IntPtr CachedFn;
            internal static FunctionPointer<JobForEachDelegate> FunctionPtr;
            internal static bool cached;
            [BurstCompile(CompileSynchronously = true)]
            internal struct JobDelegateRunner : IJobParallelFor{
                public FunctionPointer<JobForEachDelegate> FunctionPointer;
                public NativeQuery Query;
                public NativePool<TComponent1> NativePool1;
                public NativePool<TComponent2> NativePool2;
                [BurstCompile(CompileSynchronously = true)]
                public void Execute(int index) {
                    var e = Query.GetEntity(index);
                    ref var c1 = ref NativePool1.Get(e);
                    ref var c2 = ref NativePool2.Get(e);
                    //FunctionPointer.Invoke(ref c1,ref c2);
                    FunctionPointer.Invoke(ref c1, ref c2);
                }
            }
            public delegate void JobForEachDelegate(ref TComponent1 c1, ref TComponent2 c2);
        }
        public static void ForEach2<TComponent1, TComponent2>(this Query query, ForEachJob<TComponent1, TComponent2>.JobForEachDelegate action) 
            where TComponent1 : unmanaged, IComponent
            where TComponent2 : unmanaged, IComponent
        {
            if(!ForEachJob2<TComponent1, TComponent2>.cached)
            {
                ForEachJob2<TComponent1, TComponent2>.Handle = GCHandle.Alloc(action);
                ForEachJob2<TComponent1, TComponent2>.CachedFn = Marshal.GetFunctionPointerForDelegate(action);
                ForEachJob2<TComponent1, TComponent2>.FunctionPtr =
                    new FunctionPointer<ForEachJob2<TComponent1, TComponent2>.JobForEachDelegate>(
                        ForEachJob2<TComponent1, TComponent2>.CachedFn);
                ForEachJob2<TComponent1, TComponent2>.cached = true;
            }
            var job = new ForEachJob2<TComponent1, TComponent2>.JobDelegateRunner {
                //FunctionPointer = new FunctionPointer<ForEachJob2<TComponent1, TComponent2>.JobForEachDelegate>(ForEachJob2<TComponent1, TComponent2>.CachedFn),
                FunctionPointer = ForEachJob2<TComponent1, TComponent2>.FunctionPtr,
                //delegatePtr = ForEachJob2<TComponent1, TComponent2>.CachedFn,
                Query = query.AsNative(),
                NativePool1 = query.WorldInternal.GetPool<TComponent1>().AsNative(),
                NativePool2 = query.WorldInternal.GetPool<TComponent2>().AsNative(),
            };
            job.Schedule(query.count, 1).Complete();
        }
        
        
        
        public static void ForEachSingleManaged<TComponent1, TComponent2>(this Query query,
            ForEachJob<TComponent1, TComponent2>.JobForEachDelegate action) 
            where TComponent1 : unmanaged, IComponent
            where TComponent2 : unmanaged, IComponent {

            var pool1 = query.WorldInternal.GetPool<TComponent1>();
            var pool2 = query.WorldInternal.GetPool<TComponent2>();
            foreach (ref var entity in query) {
                action(ref pool1.Get(ref entity), ref pool2.Get(ref entity));
            }
        }

        public static void RunJob<TComponent1,TComponent2,TJob>(this Query query, TJob job) 
            where TJob : struct, IEntityJobParallel<TComponent1,TComponent2>
            where TComponent1 : unmanaged, IComponent
            where TComponent2 : unmanaged, IComponent
        {
            job.RunInternal<TJob>(query, query.WorldInternal);
        }
        public static void RunJob<TComponent1,TJob>(this Query query, TJob job) 
            where TJob : struct, IEntityJobParallel<TComponent1>
            where TComponent1 : unmanaged, IComponent
        {
            job.RunInternal<TJob>(query, query.WorldInternal);
        }
    }

    public interface IEntityJobParallel<TComponent1,TComponent2> 
        where TComponent1 : unmanaged, IComponent
        where TComponent2 : unmanaged, IComponent
    {
        void Execute(ref TComponent1 component1, ref TComponent2 component2);
        
        void RunInternal<TJob>(Query query, World world) where TJob : struct, IEntityJobParallel<TComponent1,TComponent2> {
            JobRunner<TJob> jobRunner;
            jobRunner.job = new TJob();
            jobRunner.Query = query.AsNative();
            jobRunner.Pool1 = world.GetPool<TComponent1>().AsNative();
            jobRunner.Pool2 = world.GetPool<TComponent2>().AsNative();
            jobRunner.Schedule(query.count, 64).Complete();
        }

        struct JobRunner<TJob> : IJobParallelFor where TJob : struct, IEntityJobParallel<TComponent1,TComponent2>
        {
            public NativePool<TComponent1> Pool1;
            public NativePool<TComponent2> Pool2;
            public NativeQuery Query;
            public TJob job;
            public void Execute(int index) {
                var e = Query.GetEntity(index);
                job.Execute(ref Pool1.Get(e), ref Pool2.Get(e));
            }
        }
    }
    public interface IEntityJobParallel<TComponent1> 
        where TComponent1 : unmanaged, IComponent
    {
        void Execute(int entity, ref TComponent1 component1);
        
        void RunInternal<TJob>(Query query, World world) where TJob : struct, IEntityJobParallel<TComponent1> {
            JobRunner<TJob> jobRunner;
            jobRunner.job = new TJob();
            jobRunner.Query = query.AsNative();
            jobRunner.Pool1 = world.GetPool<TComponent1>().AsNative();
            jobRunner.Schedule(query.count, 64).Complete();
        }

        struct JobRunner<TJob> : IJobParallelFor where TJob : struct, IEntityJobParallel<TComponent1>
        {
            public NativePool<TComponent1> Pool1;
            public NativeQuery Query;
            public TJob job;
            public void Execute(int index) {
                var e = Query.GetEntity(index);
                job.Execute(e,ref Pool1.Get(e));
            }
        }
    }
    public interface IEntityJobParallel<TComponent1,TComponent2,TComponent3> 
        where TComponent1 : unmanaged, IComponent 
        where TComponent2 : unmanaged, IComponent 
        where TComponent3 : unmanaged, IComponent 
    {
        void Execute(ref TComponent1 component1,ref TComponent2 component2,ref TComponent3 component3);
    }
    public interface IEntityJobParallel<TComponent1,TComponent2,TComponent3,TComponent4> 
        where TComponent1 : unmanaged, IComponent 
        where TComponent2 : unmanaged, IComponent 
        where TComponent3 : unmanaged, IComponent 
        where TComponent4 : unmanaged, IComponent 
    {
        void Execute(ref TComponent1 component1,ref TComponent2 component2,ref TComponent3 component3, ref TComponent4 component4);
    }

    public partial struct Entities {
        internal World world;
    }

    public static class EntitiesExtentions {
        public unsafe static class ForEachJob<TComponent1, TComponent2>
            where TComponent1 : unmanaged, IComponent
            where TComponent2 : unmanaged, IComponent 
        {
            internal static GCHandle Handle;
            internal static IntPtr CachedFn;
            internal static FunctionPointer<JobForEachDelegateInternal>  CahcedFunc;
            [BurstCompile] 
            internal struct JobDelegateRunner : IJobParallelFor{
                [NativeDisableUnsafePtrRestriction]
                public IntPtr fn;
                public FunctionPointer<JobForEachDelegateInternal> func;
                [NativeDisableUnsafePtrRestriction]
                public void* Pool1;
                [NativeDisableUnsafePtrRestriction]
                public void* Pool2;
                public NativeQuery Query;
                public void Execute(int index) {
                    var e = Query.GetEntity(index);
                    func.Invoke(fn,Pool1, Pool2, e);
                }

                [BurstCompile] 
                public static void ForEachDelegate(IntPtr fn, void* poolPtr1, void* pool2Ptr, int entity) {
                    var del = new FunctionPointer<JobForEachDelegate>(fn);
                    ref var p1 = ref UnsafeUtility.AsRef<NativePool<TComponent1>>(poolPtr1);
                    ref var p2 = ref UnsafeUtility.AsRef<NativePool<TComponent2>>(pool2Ptr);
                    del.Invoke(ref p1.Get(entity),ref p2.Get(entity));
                }
            }
            public delegate void JobForEachDelegate(ref TComponent1 c1, ref TComponent2 c2);
        }
        internal unsafe delegate void JobForEachDelegateInternal(IntPtr fn, void* c1, void* c2, int entity);
        public unsafe static void ForEach<TComponent1, TComponent2>(this ref Entities entities, ForEachJob<TComponent1, TComponent2>.JobForEachDelegate action) 
            where TComponent1 : unmanaged, IComponent
            where TComponent2 : unmanaged, IComponent
        {
            if(!ForEachJob<TComponent1, TComponent2>.CahcedFunc.IsCreated)
            {
                ForEachJob<TComponent1, TComponent2>.Handle = GCHandle.Alloc(action);
                ForEachJob<TComponent1, TComponent2>.CachedFn = Marshal.GetFunctionPointerForDelegate(action);
                ForEachJob<TComponent1, TComponent2>.CahcedFunc =
                    BurstCompiler.CompileFunctionPointer<JobForEachDelegateInternal>(
                        ForEachJob<TComponent1, TComponent2>.JobDelegateRunner.ForEachDelegate);
            }

            var query = entities.world.GetQuery<TComponent1, TComponent2>();
            var job = new ForEachJob<TComponent1, TComponent2>.JobDelegateRunner {
                fn = ForEachJob<TComponent1, TComponent2>.CachedFn,
                func = ForEachJob<TComponent1, TComponent2>.CahcedFunc,
                Query = query.AsNative(),
                Pool1 = query.WorldInternal.GetPool<TComponent1>().AsNative().GetPtr(),
                Pool2 = query.WorldInternal.GetPool<TComponent2>().AsNative().GetPtr()
            };
            job.Schedule(query.count, 64).Complete();
        }
        
        public static void forEach<TComponent1>(this ref Entities entities,
            Entities.ForEachDelegate<TComponent1> action)
            where TComponent1 : struct, IComponent {

            var q = entities.world.GetQuery<TComponent1>();
            var p1 = entities.world.GetPool<TComponent1>();
            
            for (var i = 0; i < q.count; i++) {
                ref var e = ref q.GetEntity(i);
                action(ref p1.Get(ref e));
            }
        }
        
        public static void forEach<TComponent1, TComponent2>(this ref Entities entities,
            Entities.ForEachDelegate<TComponent1, TComponent2> action)
            where TComponent1 : struct, IComponent
            where TComponent2 : struct, IComponent {

            var q = entities.world.GetQuery<TComponent1, TComponent2>();
            var p1 = entities.world.GetPool<TComponent1>();
            var p2 = entities.world.GetPool<TComponent2>();
            
            for (var i = 0; i < q.count; i++) {
                ref var e = ref q.GetEntity(i);
                action(ref p1.Get(ref e), ref p2.Get(ref e));
            }
        }
        public static void forEach<TComponent1, TComponent2, TComponent3>(this ref Entities entities,
            Entities.ForEachDelegate<TComponent1, TComponent2, TComponent3> action)
            where TComponent1 : struct, IComponent
            where TComponent2 : struct, IComponent
            where TComponent3 : struct, IComponent {

            var q = entities.world.GetQuery<TComponent1, TComponent2, TComponent3>();
            var p1 = entities.world.GetPool<TComponent1>();
            var p2 = entities.world.GetPool<TComponent2>();
            var p3 = entities.world.GetPool<TComponent3>();
            for (var i = 0; i < q.count; i++) {
                ref var e = ref q.GetEntity(i);
                action(ref p1.Get(ref e), ref p2.Get(ref e), ref p3.Get(ref e));
            }
        }
    }
    
    public partial struct Entities {
        public delegate void ForEachDelegate<TComponent1>
            (ref TComponent1 component1);
        public delegate void ForEachDelegate<TComponent1, TComponent2>
            (ref TComponent1 component1, ref TComponent2 component2);
        public delegate void ForEachDelegate<TComponent1,TComponent2,TComponent3>
            (ref TComponent1 component1, ref TComponent2 component2, ref TComponent3 component3);
        public delegate void ForEachDelegate<TComponent1,TComponent2,TComponent3,TComponent4>
            (ref TComponent1 component1, ref TComponent2 component2, ref TComponent3 component3, ref TComponent4 component4);
        public delegate void ForEachDelegate<TComponent1,TComponent2,TComponent3,TComponent4,TComponent5>
            (ref TComponent1 component1, ref TComponent2 component2, ref TComponent3 component3, ref TComponent4 component4, ref TComponent5 component5);
        public delegate void ForEachDelegate<TComponent1,TComponent2,TComponent3,TComponent4,TComponent5,TComponent6>
            (ref TComponent1 component1, ref TComponent2 component2, ref TComponent3 component3, ref TComponent4 component4, ref TComponent5 component5, ref TComponent6 component6);
        
    }
}