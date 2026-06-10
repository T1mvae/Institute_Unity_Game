using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight static service registry.
/// Replaces scattered FindObjectOfType calls with explicit registration.
/// NOT thread-safe — Unity is single-threaded, so no locking needed.
/// </summary>
public static class ServiceLocator
{
    // All registered services keyed by their type.
    private static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

    /// <summary>
    /// Register a service instance. Overwrites any previous registration of the same type.
    /// </summary>
    public static void Register<T>(T instance) where T : class
    {
        Type key = typeof(T);

        if (instance == null)
        {
            Debug.LogWarning($"[ServiceLocator] Attempted to register null for {key.Name}. Ignored.");
            return;
        }

        if (services.ContainsKey(key))
        {
            Debug.Log($"[ServiceLocator] Overwriting existing registration for {key.Name}.");
        }

        services[key] = instance;
    }

    /// <summary>
    /// Retrieve a previously registered service.
    /// Returns null if the service was never registered.
    /// </summary>
    public static T Get<T>() where T : class
    {
        Type key = typeof(T);

        if (services.TryGetValue(key, out object service))
        {
            return service as T;
        }

        return null;
    }

    /// <summary>
    /// Check whether a service of the given type is registered.
    /// </summary>
    public static bool Has<T>() where T : class
    {
        return services.ContainsKey(typeof(T));
    }

    /// <summary>
    /// Remove a specific service registration.
    /// </summary>
    public static void Unregister<T>() where T : class
    {
        services.Remove(typeof(T));
    }

    /// <summary>
    /// Clear all registrations. Call this on scene unload to prevent stale references.
    /// </summary>
    public static void Clear()
    {
        services.Clear();
    }
}
