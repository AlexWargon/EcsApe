using UnityEditor;

namespace Wargon.Ecsape.Editor {
    public abstract class UpdatableEditor : UnityEditor.Editor {
        static double lastUpdateTime = 0;
        protected virtual float Framerate => 30;
        protected void OnEnable() {
            EditorApplication.update += Update;
        }

        protected void OnDisable() {
            EditorApplication.update -= Update;
        }
        
        protected void Update() {
            if (Framerate <= 0 || EditorApplication.timeSinceStartup > lastUpdateTime + 1 / Framerate) {
                lastUpdateTime = EditorApplication.timeSinceStartup;
                OnUpdate();
            }
        }

        protected abstract void OnUpdate();
    }
}