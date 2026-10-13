using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// A circle on the ground a hero has to fight from: the hold spot, the charging zone, the leash around its post or
    /// around the place where an attack-move met the enemy. Radius 0 = free (no limit).
    /// </summary>
    public readonly struct Tether
    {
        public readonly Vector3 Center;
        public readonly float Radius;

        public Tether(Vector3 center, float radius)
        {
            Center = center;
            Radius = Mathf.Max(0f, radius);
        }

        public static Tether Free => default;

        public bool Active => Radius > 0f;

        /// <summary>Horizontal distance from the centre (heights differ a lot on these maps, the circle is flat).</summary>
        public float Distance(Vector3 p)
        {
            Vector3 d = p - Center;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>True when the point is inside, or when the tether is free.</summary>
        public bool Contains(Vector3 p, float margin = 0f) => !Active || Distance(p) <= Radius + margin;

        /// <summary>The point pulled back into the circle (unchanged when inside or free); the height is kept.</summary>
        public Vector3 Clamp(Vector3 p)
        {
            if (!Active) return p;
            Vector3 d = p - Center;
            d.y = 0f;
            float distance = d.magnitude;
            if (distance <= Radius) return p;
            Vector3 inside = Center + d * (Radius / distance);
            inside.y = p.y;
            return inside;
        }
    }
}
