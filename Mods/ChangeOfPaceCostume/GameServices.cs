using System;
using System.Collections.Generic;
using Bulbul;
using NestopiSystem.DIContainers;
using UnityEngine;

namespace ChangeOfPaceCostume;

/// <summary>
/// Single place for obtaining game services. It prefers the game's VContainer
/// scopes (public API) and falls back to a scene search for MonoBehaviours when the
/// scope layout changes in a future update. Results are cached, and Unity "fake null"
/// references are detected so a scene rebuild triggers a fresh resolve.
/// </summary>
internal static class GameServices
{
    private static readonly Dictionary<Type, object> Cache = new Dictionary<Type, object>();
    private static readonly HashSet<Type> Warned = new HashSet<Type>();

    public static T Room<T>() where T : class
    {
        return Resolve<T>(() => RoomLifetimeScope.Resolve<T>(), null);
    }

    public static T RoomComponent<T>() where T : Component
    {
        return Resolve<T>(
            () => RoomLifetimeScope.Resolve<T>(),
            () => UnityEngine.Object.FindObjectOfType<T>());
    }

    public static T Project<T>() where T : class
    {
        return Resolve<T>(() => ProjectLifetimeScope.Resolve<T>(), null);
    }

    public static T ProjectComponent<T>() where T : Component
    {
        return Resolve<T>(
            () => ProjectLifetimeScope.Resolve<T>(),
            () => UnityEngine.Object.FindObjectOfType<T>());
    }

    /// <summary>Drops cached instances (used when the room scene is rebuilt).</summary>
    public static void Invalidate()
    {
        Cache.Clear();
        Warned.Clear();
    }

    private static T Resolve<T>(Func<T> primary, Func<T> fallback) where T : class
    {
        if (Cache.TryGetValue(typeof(T), out var cached) && cached is T value && IsAlive(value))
        {
            return value;
        }
        Cache.Remove(typeof(T));

        try
        {
            var resolved = primary();
            if (IsAlive(resolved))
            {
                Cache[typeof(T)] = resolved;
                return resolved;
            }
        }
        catch
        {
            // The scope may not be ready yet or its registration changed; try the fallback.
        }

        if (fallback != null)
        {
            try
            {
                var found = fallback();
                if (IsAlive(found))
                {
                    Cache[typeof(T)] = found;
                    Plugin.Log.LogInfo($"[Services] {typeof(T).Name} resolved by scene search (DI unavailable).");
                    return found;
                }
            }
            catch
            {
                // ignored
            }
        }

        if (Warned.Add(typeof(T)))
        {
            Plugin.Log.LogWarning($"[Services] {typeof(T).Name} not available (scope or scene changed?).");
        }
        return null;
    }

    private static bool IsAlive(object instance)
    {
        if (instance == null) return false;
        return !(instance is UnityEngine.Object unityObject) || unityObject != null;
    }
}
