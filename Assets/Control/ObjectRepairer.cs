using System;
using System.Collections.Generic;
using System.Reflection;

public static class ObjectRepairer
{
    private static readonly HashSet<object> visited = new();

    public static void Repair(object obj)
    {
        visited.Clear();
        RepairInternal(obj);
        visited.Clear();
    }

    private static void RepairInternal(object obj)
    {
        if (obj == null) return;
        if (visited.Contains(obj)) return;

        visited.Add(obj);

        var type = obj.GetType();

        // Skip primitives / Unity-safe stop types
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
            return;

        // Handle Lists
        if (obj is System.Collections.IEnumerable enumerable && obj is not string)
        {
            foreach (var item in enumerable)
                RepairInternal(item);

            return;
        }

        var fields = type.GetFields(
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public);

        foreach (var f in fields)
        {
            var fieldType = f.FieldType;

            var value = f.GetValue(obj);

            if (value == null)
            {
                if (!fieldType.IsAbstract &&
                    fieldType.GetConstructor(Type.EmptyTypes) != null)
                {
                    var instance = Activator.CreateInstance(fieldType);
                    f.SetValue(obj, instance);

                    RepairInternal(instance);
                }
            }
            else
            {
                RepairInternal(value);
            }
        }
    }
}