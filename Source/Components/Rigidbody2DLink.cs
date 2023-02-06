using System;
using UnityEngine;

namespace Wargon.Ecsape.Components {
    public class Rigidbody2DLink : ComponentLink<Rigidbody2DReference> { }

    [Serializable]
    public struct Rigidbody2DReference : IComponent {
        public Rigidbody2D value;
    }
}