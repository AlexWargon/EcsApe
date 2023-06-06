using System;
using UnityEditor;

namespace Wargon.Ecsape.Editor {
    public abstract class UpdatableEditor : UnityEditor.Editor {
        private DateTime timeFromLastFrame;
        private TimeSpan timeSpan;
        public const int msLock = 60;
        public float deltaTime;
        protected void OnEnable() {
            EditorApplication.update += Update;
        }

        protected void OnDisable() {
            EditorApplication.update -= Update;
        }
        
        private void Update() {
            timeSpan = DateTime.Now - timeFromLastFrame;
            if (timeSpan.Milliseconds <= msLock) return;
            OnUpdate();
            deltaTime = (float)timeSpan.TotalSeconds;
            timeFromLastFrame = DateTime.Now;
        }

        protected abstract void OnUpdate();
    }
}