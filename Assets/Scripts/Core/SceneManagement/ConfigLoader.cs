using UnityEngine;

public class ConfigLoader : MonoBehaviour
{
    public UIThemeConfig Theme { get; private set; }

    void Awake()
    {
        Theme = ThemeLoader.LoadOrCreateDefault();
        ServiceLocator.Register<ConfigLoader>(this);
    }

    void OnDestroy()
    {
        ServiceLocator.Unregister<ConfigLoader>();
    }
}
