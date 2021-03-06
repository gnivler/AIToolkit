﻿using System;
using System.Collections.Generic;
using Harmony;

namespace AIToolkit.Util
{
    public static class TypeUtil
    {
        private static readonly HashSet<Type> HasRunCacheFill = new HashSet<Type>();
        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        private static void TypeCacheFill(Type parentType)
        {
            var types = parentType.Assembly.GetTypes();
            foreach (var type in types)
            {
                if (type.IsSubclassOf(parentType) && !TypeCache.ContainsKey(type.Name))
                    TypeCache.Add(type.Name, type);
            }
        }

        public static Type GetTypeByName(string typeName, Type precacheParent = null)
        {
            if (precacheParent != null && !HasRunCacheFill.Contains(precacheParent))
            {
                TypeCacheFill(precacheParent);
                HasRunCacheFill.Add(precacheParent);
            }

            if (TypeCache.ContainsKey(typeName))
                return TypeCache[typeName];

            var type = AccessTools.TypeByName(typeName);

            TypeCache.Add(typeName, type);
            return TypeCache[typeName];
        }
    }
}
