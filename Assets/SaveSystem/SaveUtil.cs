// Copyright (c) 2021 Boppy Games
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace EntitySaveSystem
{
  public static class SaveUtil
  {
    public static bool IsIListType(Type type)
    {
      foreach (var inter in type.GetInterfaces())
        if (inter.IsGenericType && inter.GetGenericTypeDefinition() == typeof(IList<>))
          return true;
      return false;
    }

    public static Type GetIListType(Type type)
    {
      foreach (var inter in type.GetInterfaces())
        if (inter.IsGenericType && inter.GetGenericTypeDefinition() == typeof(IList<>))
          return inter.GetGenericArguments().Single();
      return null;
    }

    public static bool IsIDictionaryType(Type type)
    {
      foreach (var inter in type.GetInterfaces())
        if (inter.IsGenericType && inter.GetGenericTypeDefinition() == typeof(IDictionary<,>))
          return true;
      return false;
    }

    public static Type[] GetIDictionaryTypes(Type type)
    {
      foreach (var inter in type.GetInterfaces())
        if (inter.IsGenericType && inter.GetGenericTypeDefinition() == typeof(IDictionary<,>))
          return inter.GetGenericArguments();
      return null;
    }

    public static bool IsPrimitive(Type t)
    {
      return t.IsPrimitive || t == typeof(string) || t.IsEnum;
    }

    public static IEnumerable<FieldInfo> GetFields(Type t)
    {
      return t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(
        a => a.IsPublic || a.CustomAttributes.Any(b => b.AttributeType == typeof(SaveAttribute)));
    }

    public static bool IsComponentType(Type type)
    {
      while (type != typeof(object) && type != null)
      {
        if (type == typeof(Component))
          return true;
        type = type.BaseType;
      }

      return false;
    }

    public static int GetComponentIndex(SaveEntity saveEntity, Type componentType, object component)
    {
      if (component == null) return -1;
      var components = saveEntity.GetComponents(componentType);
      for (var x = 0; x < components.Length; x++)
        if (ReferenceEquals(components[x], component))
          return x;
      return -1;
    }

    public static bool IsPrefab(GameObject obj)
    {
#if UNITY_EDITOR
      return UnityEditor.PrefabUtility.IsPartOfPrefabAsset(obj);
#else
              return false;
#endif
    }
  }
}
