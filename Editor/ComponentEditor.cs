using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wargon.Ecsape.Components;

namespace Wargon.Ecsape {
    public static class ComponentEditor {
        private static readonly Dictionary<string, Type> components = new();
        private static readonly Dictionary<string, Color> colors = new(); 
        public static List<string> Names = new();

        private static readonly HashSet<Type> excludes = new HashSet<Type> {
            typeof(IComponent),
            typeof(TransformReference),
            typeof(Translation),
            typeof(DestroyEntity),
            
        };
        static ComponentEditor() {

            var types = FindAllTypeWithInterface(typeof(IComponent), type => !excludes.Contains(type));
            SortByName(types);
            var count = types.Length;
            for (var index = 0; index < count; index++) {
                var type1 = types[index];
                //add componentType
                components.Add(type1.Name, type1);
                //add component names;
                Names.Add(type1.Name);
                //add color
                var k = (float)index/count;
                var c = Color.HSVToRGB(k, 0.8F, 0.8F);
                c.a = 0.15f;
                colors.Add(type1.Name, c);
            }
        }

        private static Type[] FindAllTypeWithInterface(Type interfaceType, Func<Type, bool> comprasion = null) {
            if(comprasion!=null) return  AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => interfaceType.IsAssignableFrom(p) && comprasion(p) && p != interfaceType).ToArray();
            
            return  AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => interfaceType.IsAssignableFrom(p) && p != interfaceType).ToArray();
        }
        
        private static void SortByName(Type[] list) {
            Array.Sort(list, (x,y) => string.CompareOrdinal(x.Name, y.Name));
        }
        
        public static bool TryGetColor(string component, out Color color) {
            if (colors.TryGetValue(component, out var color2)) {
                color = color2;
                return true;
            }
            color = Color.grey;
            return false;
        }
        
        public static object Create(string name) {
            if (name == null) return null;
            if (components.ContainsKey(name))
                return Create(components[name]);
            return null;
        }
        
        public static object Create(Type type) {
            return Activator.CreateInstance(type);
        }
    }
}