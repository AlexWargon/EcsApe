using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Wargon.Ecsape.Editor {
    internal static class ReflectionUtils {

        public static Func<object, object> FieldGetter(this Type source, FieldInfo fieldInfo)
        {
            var sourceParam = Expression.Parameter(typeof(object));
            Expression returnExpression = Expression.Field(Expression.Convert(sourceParam, source), fieldInfo);
            if (!fieldInfo.FieldType.IsClass)
            {
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            }
            var lambda = Expression.Lambda(returnExpression, sourceParam);
            return (Func<object, object>)lambda.Compile();
        }
        // public static Func<object, object> CreateGetMethod(this FieldInfo fieldInfo)
        // {
        //     var sourceParam = Expression.Parameter(typeof(object));
        //     Expression returnExpression = Expression.Field(Expression.Convert(sourceParam, source), fieldInfo);
        //     if (!fieldInfo.FieldType.IsClass)
        //     {
        //         returnExpression = Expression.Convert(returnExpression, typeof(object));
        //     }
        //     var lambda = Expression.Lambda(returnExpression, sourceParam);
        //     return (Func<object, object>)lambda.Compile();
        // }
        public static SetterDelegate CreateSetMethod(this FieldInfo fieldInfo) {
            var ParamType = fieldInfo.FieldType;
            var setter = new DynamicMethod("", typeof(void), set_params,
                fieldInfo.ReflectedType.Module, true);
            var il = setter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldind_Ref);

            if (fieldInfo.DeclaringType.IsValueType) {
                il.DeclareLocal(fieldInfo.DeclaringType.MakeByRefType());
                il.Emit(OpCodes.Unbox, fieldInfo.DeclaringType);
                il.Emit(OpCodes.Stloc_0);
                il.Emit(OpCodes.Ldloc_0);
            }
            il.Emit(OpCodes.Ldarg_1);
            
            if (ParamType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, ParamType);

            il.Emit(OpCodes.Stfld, fieldInfo);

            if (fieldInfo.DeclaringType.IsValueType) {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldobj, fieldInfo.DeclaringType);
                il.Emit(OpCodes.Box, fieldInfo.DeclaringType);
                il.Emit(OpCodes.Stind_Ref);
            }

            il.Emit(OpCodes.Ret);

            return (SetterDelegate)setter.CreateDelegate(typeof(SetterDelegate));
        }
        
        public delegate void SetterDelegate(ref object target, object value);
        private static readonly Type[] set_params = {typeof(object).MakeByRefType(), typeof(object)};
        
    }
    public static class DynamicMethodBuilder
    {
        private static readonly AssemblyBuilder _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DynamicMethodBuilderAssembly"), AssemblyBuilderAccess.Run);
        private static readonly ModuleBuilder _moduleBuilder = _assemblyBuilder.DefineDynamicModule("DynamicModule");

        /// <summary>
        /// This provides an easy way to create a really fast DynamicMethod
        /// Regular DynamicMethod: ~ 6.6 ns
        /// This approach        : ~ 1.7 ns
        /// Nearly 4 times faster than regular DynamicMethods
        /// and as fast as other IL approaches
        /// </summary>
        public static T CreateFastDynamicMethod<T>(string name, Action<ILGenerator> ilWriter) where T : Delegate {
            var toBeImplementedMethodInfo = typeof(T).GetMethod("Invoke")!; // Invoke method of delegate
            var nameWithoutCollision = $"{name}_{Guid.NewGuid():N}";
            var typeBuilder = _moduleBuilder.DefineType(nameWithoutCollision, TypeAttributes.Public | TypeAttributes.Class);
            var methodBuilder = typeBuilder.DefineMethod("Invoke",
                MethodAttributes.Public | MethodAttributes.Static,
                toBeImplementedMethodInfo.ReturnType,
                toBeImplementedMethodInfo.GetParameters().Select(p => p.ParameterType).ToArray()
            );
            var il = methodBuilder.GetILGenerator();
            ilWriter(il);
            var type = typeBuilder.CreateType();
            return (T)type.GetMethod("Invoke")!.CreateDelegate(typeof(T));
        }
    }
}