using System;
using UnityEngine;

namespace Wargon.Ecsape.Components {
    [Serializable]
    public struct Rigidbody2DReference : IComponent {
        public Rigidbody2D value;
    }
}