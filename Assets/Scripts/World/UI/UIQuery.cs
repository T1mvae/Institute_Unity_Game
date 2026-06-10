using UnityEngine;
using UnityEngine.UIElements;

namespace Institute.World.UI
{
    /// <summary>
    /// Safe named-element lookups for UI Toolkit. <see cref="Require{T}"/> logs a clear, one-line error
    /// when an expected element is missing instead of throwing a null reference deep in a controller.
    /// </summary>
    public static class UIQuery
    {
        public static T Require<T>(VisualElement root, string name, string context = null) where T : VisualElement
        {
            T element = root != null ? root.Q<T>(name) : null;
            if (element == null)
                Debug.LogError($"UIQuery: missing <{typeof(T).Name} name=\"{name}\"> " +
                               (string.IsNullOrEmpty(context) ? "" : $"in {context}"));
            return element;
        }

        public static T Optional<T>(VisualElement root, string name) where T : VisualElement
        {
            return root != null ? root.Q<T>(name) : null;
        }

        /// <summary>Sets a label's text only if the label exists (no-op + safe otherwise).</summary>
        public static void SetText(Label label, string text)
        {
            if (label != null) label.text = text ?? string.Empty;
        }
    }
}
