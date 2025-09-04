using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MiddleMousePan.Common;

/// <summary>
/// Additional Harmony capabilities
/// </summary>
public static class HarmonyUtils
{
    private static readonly List<Type> auxilaryTypes =
    [
        typeof(HarmonyPrepare),
        typeof(HarmonyCleanup),
        typeof(HarmonyTargetMethod),
        typeof(HarmonyTargetMethods)
    ];

    /// <summary>
    /// Unpatches all methods defined in a class.
    /// Reverse of #<see cref="PatchClassProcessor.Patch"/>
    /// </summary>
    /// <param name="instance">The Harmony instance</param>
    /// <param name="type">The class to process (need to have at least a [HarmonyPatch] attribute)</param>
    public static void UnpatchClass(Harmony instance, Type type)
    {
        if (instance == null)
        {
            throw new ArgumentNullException("instance");
        }

        if (type is null)
        {
            throw new ArgumentNullException("type");
        }
        var containerType = type;
        List<HarmonyMethod> fromType = HarmonyMethodExtensions.GetFromType(type);
        if (fromType == null || fromType.Count == 0)
        {
            return;
        }
        var containerAttributes = HarmonyMethod.Merge(fromType);
        MethodType? methodType = containerAttributes.methodType;
        if (!methodType.HasValue)
        {
            containerAttributes.methodType = MethodType.Normal;
        }

        var auxilaryMethods = new Dictionary<Type, MethodInfo>();
        foreach (Type auxilaryType in auxilaryTypes)
        {
            MethodInfo patchMethod = GetPatchMethod(containerType, auxilaryType.FullName);
            if (patchMethod is not null)
            {
                auxilaryMethods[auxilaryType] = patchMethod;
            }
        }

        var patchMethods = GetPatchMethods(containerType);

        foreach (var patchMethod2 in patchMethods)
        {
            MethodInfo method = patchMethod2.info.method;
            patchMethod2.info = containerAttributes.Merge(patchMethod2.info);
            patchMethod2.info.method = method;
        }

        foreach (var a in patchMethods)
        {
            var original = a.info.declaringType.GetMethod(a.info.methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            instance.Unpatch(original, a.info.method);
        }
    }

    private static MethodInfo GetPatchMethod(Type patchType, string attributeName)
    {
        MethodInfo methodInfo = patchType.GetMethods(AccessTools.all).FirstOrDefault((MethodInfo m) => m.GetCustomAttributes(inherit: true).Any((object a) => a.GetType().FullName == attributeName));
        if (methodInfo is null)
        {
            string name = attributeName.Replace("HarmonyLib.Harmony", "");
            methodInfo = patchType.GetMethod(name, AccessTools.all);
        }

        return methodInfo;
    }

    private static List<AttributePatchEC> GetPatchMethods(Type type)
    {
        return (from method in AccessTools.GetDeclaredMethods(type)
                select AttributePatchEC.Create(method) into attributePatch
                where attributePatch != null
                select attributePatch).ToList();
    }

    private class AttributePatchEC
    {
        private static readonly HarmonyPatchType[] allPatchTypes =
        [
            HarmonyPatchType.Prefix,
            HarmonyPatchType.Postfix,
            HarmonyPatchType.Transpiler,
            HarmonyPatchType.Finalizer,
            HarmonyPatchType.ReversePatch
        ];

        internal HarmonyMethod info;

        internal HarmonyPatchType? type;

        private static readonly string harmonyAttributeName = typeof(HarmonyAttribute).FullName;

        internal static AttributePatchEC Create(MethodInfo patch)
        {
            if (patch is null)
            {
                throw new NullReferenceException("Patch method cannot be null");
            }

            object[] customAttributes = patch.GetCustomAttributes(inherit: true);
            HarmonyPatchType? patchType = GetPatchType(patch.Name, customAttributes);
            if (!patchType.HasValue)
            {
                return null;
            }

            if (patchType != HarmonyPatchType.ReversePatch && !patch.IsStatic)
            {
                throw new ArgumentException("Patch method " + patch.FullDescription() + " must be static");
            }

            HarmonyMethod harmonyMethod = HarmonyMethod.Merge((from attr in customAttributes
                                                               where attr.GetType().BaseType.FullName == harmonyAttributeName
                                                               select AccessTools.Field(attr.GetType(), "info").GetValue(attr) into harmonyInfo
                                                               select AccessTools.MakeDeepCopy<HarmonyMethod>(harmonyInfo)).ToList());
            harmonyMethod.method = patch;
            return new AttributePatchEC
            {
                info = harmonyMethod,
                type = patchType
            };
        }

        private static HarmonyPatchType? GetPatchType(string methodName, object[] allAttributes)
        {
            HashSet<string> hashSet = new(from attr in allAttributes
                                          select attr.GetType().FullName into name
                                          where name.StartsWith("Harmony")
                                          select name);
            HarmonyPatchType? result = null;
            HarmonyPatchType[] array = allPatchTypes;
            for (int i = 0; i < array.Length; i++)
            {
                HarmonyPatchType value = array[i];
                string text = value.ToString();
                if (text == methodName || hashSet.Contains("HarmonyLib.Harmony" + text))
                {
                    result = value;
                    break;
                }
            }

            return result;
        }
    }
}
