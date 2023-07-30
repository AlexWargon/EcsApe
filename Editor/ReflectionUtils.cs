using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
namespace Wargon.Ecsape.Editor {
    internal static class ReflectionUtils {
        public delegate object GetDelegate(object obj);

        public static Func<object, object> CreateGetFieldDelegate(this Type type, string fieldName)
        {
            var instExp = Expression.Parameter(type);
            var fieldExp = Expression.Field(instExp, fieldName);
            return Expression.Lambda<Func<object, object>>(fieldExp, instExp).Compile();
        }
        public static Action<S, T> CreateSetFieldDelegate<S, T>(this Type type, string fieldName)
        {
            var instExp = Expression.Parameter(type);
            var fieldExp = Expression.Field(instExp, fieldName);
            return Expression.Lambda<Action<S, T>>(fieldExp, instExp).Compile();
        }
        public static Action<S, T> CreateSetter<S, T>(FieldInfo field)
        {
            var methodName = field.ReflectedType.FullName+".set_"+field.Name;
            var setterMethod = new DynamicMethod(methodName, null, new []{typeof(S),typeof(T)}, field.Module,true);
            var gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, field);
            }
            gen.Emit(OpCodes.Ret);
            
            return (Action<S, T>)setterMethod.CreateDelegate(typeof(Action<S, T>));
        }
        public static Func<S, T> CreateGetter<S, T>(FieldInfo field)
        {
            var methodName = field.ReflectedType.FullName + ".get_" + field.Name;
            var getMethod = new DynamicMethod(methodName, typeof(T), new[] { typeof(S) }, field.Module, true);
            //var setterMethod = new DynamicMethod(methodName, typeof(T), new [] { typeof(S) }, true);
            var gen = getMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
            }
            gen.Emit(OpCodes.Ret);
            
            return (Func<S, T>)getMethod.CreateDelegate(typeof(Func<S, T>));
        }
        
        public static Action<object, object> Setter() {
            var methodInfo = typeof (FieldInfo).GetMethod ("SetValue");
            return (Action<object, object>)methodInfo?.CreateDelegate(typeof(Action<object, object>));
        }
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
        public static Action<object, object> FieldSetter(this Type source, FieldInfo fieldInfo)
        {
            var sourceParam = Expression.Parameter(typeof(object));
            var valueParam = Expression.Parameter(typeof(object));
            var convertedValueExpr = Expression.Convert(valueParam, fieldInfo.FieldType);
            Expression returnExpression = Expression.Assign(Expression.Field(Expression.Convert(sourceParam, source), fieldInfo), convertedValueExpr);
            if (!fieldInfo.FieldType.IsClass)
            {
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            }
            var lambda = Expression.Lambda(typeof(Action<object, object>),
                returnExpression, sourceParam, valueParam);
            return (Action<object, object>)lambda.Compile();
        }
    }
}