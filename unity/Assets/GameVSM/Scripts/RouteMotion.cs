using UnityEngine;

namespace GameVSM
{
    public sealed class RouteMotion : MonoBehaviour
    {
        public Transform[] Scenery;
        public float Speed = 0;
        public float CruiseSpeed = 48;
        public float Length = 1280;
        public float Acceleration = 3.5f;
        public float Distance { get; private set; }
        void Update()
        {
            Speed = Mathf.MoveTowards(Speed, CruiseSpeed, Time.deltaTime * Acceleration);
            Distance += Speed * Time.deltaTime;
            foreach (var item in Scenery)
            {
                if (item == null) continue;
                var p = item.localPosition;
                p.x -= Speed * Time.deltaTime;
                if (p.x < -Length / 2) p.x += Length;
                item.localPosition = p;
            }
        }
    }
}
