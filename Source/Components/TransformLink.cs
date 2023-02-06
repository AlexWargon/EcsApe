using System;
using UnityEngine;


namespace Wargon.Ecsape.Components {
    public class TransformLink : ComponentLink<TransformReference> {
        public override void Link(ref Entity entity) {
            var translation = new Translation {
                position = value.value.position,
                scale = value.value.localScale,
                rotation = value.value.rotation
            };
            entity.Add(value);
            entity.Add(translation);
        }
    }
    [Serializable]
    public struct TransformReference : IComponent {
        public Transform value;
        public int instanceID;
    }
}