using System;
using System.Collections.Generic;
using System.Reflection;

namespace Wargon.Ecsape {
    public class DependencyContainer : IDependencyContainer {
        private readonly Dictionary<Type, IDependencyContext> constexts;
        public DependencyContainer() {
            constexts = new Dictionary<Type, IDependencyContext>();
            Register<IDependencyContainer>().From(this);
        }

        public IDependencyContext Register<T>() where T : class {
            var context = new DependencyContext(this);
            constexts.Add(typeof(T), context);
            return context;
        }

        public IDependencyContext Register<T>(T item) where T : class {
            var context = new DependencyContext(this);
            constexts.Add(typeof(T), context);
            constexts[typeof(T)].From(item);
            return context;
        }

        public void Build(object instance) {
            var type = instance.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var fieldInfo in fields) {
                var fieldType = fieldInfo.FieldType;

                if (constexts.TryGetValue(fieldType, out var context1)) {
                    //Debug.Log($"ATTACH [{context1.GetInstance().GetType()} {fieldInfo.Name}] TO [{instance.GetType()}]");
                    fieldInfo.SetValue(instance, context1.GetInstance());
                }
            }

            var contructMethod = type.GetMethod(IDependencyContainer.CONSTRUCTOR);
            if (contructMethod != null) {
                contructMethod.Invoke(instance, null);
                Debug.Log($"{type.Name} Consruct");
            }
            // var parameters = contructMethod.GetParameters();
            // if (parameters.Length > 0) {
            //     var list = new List<object>();
            //     foreach (var parameterInfo in parameters) {
            //         if (constexts.TryGetValue(parameterInfo.ParameterType, out var paramContext)) {
            //             list.Add(paramContext.GetInstance());
            //         }
            //     }
            //     contructMethod.Invoke(instance, list.ToArray());
            // }
            // else {
            //     contructMethod.Invoke(instance, null);
            // }
        }

        public T Get<T>() where T : class {
            return (T)constexts[typeof(T)].GetInstance();
        }
    }
    
    public interface IDependencyContext {
        IDependencyContext From<T>() where T : class;
        IDependencyContext From<T>(T isntance) where T : class;
        object GetInstance();
    }

    public interface IDependencyContainer {
        void Build(object target);
        IDependencyContext Register<T>() where T : class;
        IDependencyContext Register<T>(T item) where T : class;
        T Get<T>() where T : class;
        public const string CONSTRUCTOR = "Construct";
    }
    
    public class DependencyContext : IDependencyContext {
        private object instance;
        private IDependencyContainer container;
        private object[] ContructorParams;
        internal DependencyContext(IDependencyContainer container) {
            this.container = container;

        }
        internal DependencyContext(IDependencyContainer container, object instance) {
            this.container = container;
            this.instance = instance;
        }
        IDependencyContext IDependencyContext.From<T>() {
            instance = Activator.CreateInstance(typeof(T));
            container.Build(instance);
            return this;
        }
        IDependencyContext IDependencyContext.From<T>(T instanceSource) {
            instance = instanceSource;
            container.Build(instance);
            return this;
        }

        public object GetInstance() {
            return instance;
        }
    }


    public static class DI {
        private static IDependencyContainer container;

        public static IDependencyContainer GetOrCreateContainer() {
            if (container == null)
                container = new DependencyContainer();
            return container;
        }
        public static IDependencyContainer GetOrCreateContainer<T>() where T : IDependencyContainer {
            if (container == null)
                container = Activator.CreateInstance<T>();
            return container;
        }
        public static IDependencyContext Register<T>() where T : class =>
            GetOrCreateContainer().Register<T>();

        public static IDependencyContext Register<T>(T instance) where T : class =>
            GetOrCreateContainer().Register(instance);

        public static T Get<T>() where T : class => GetOrCreateContainer().Get<T>();
        public static void Build(object instance) => GetOrCreateContainer().Build(instance);
    }
}