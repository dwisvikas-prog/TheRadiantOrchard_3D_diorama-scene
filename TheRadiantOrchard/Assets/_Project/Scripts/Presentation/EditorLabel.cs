using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// A name shown in the Scene view only (placeholder landmarks, path points). Not player-facing and not rendered in
    /// the Game view or builds, and it uses no font asset, so it never changes any asset on disk.
    /// </summary>
    public sealed class EditorLabel : MonoBehaviour
    {
        [SerializeField] private string _text = string.Empty;
        [SerializeField] private float _height = 0.6f;

        public void Set(string text, float height)
        {
            _text = text;
            _height = height;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!string.IsNullOrEmpty(_text))
                UnityEditor.Handles.Label(transform.position + Vector3.up * _height, _text);
        }
#endif
    }
}
