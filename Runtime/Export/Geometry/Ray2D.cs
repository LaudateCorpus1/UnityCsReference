// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Globalization;

namespace UnityEngine
{
    // Representation of 2D rays.
    public partial struct Ray2D : IFormattable
    {
        private Vector2 m_Origin;
        private Vector2 m_Direction;

        // Creates a ray starting at /origin/ along /direction/.
        public Ray2D(Vector2 origin, Vector2 direction) { m_Origin = origin; m_Direction = direction.normalized; }

        // The origin point of the ray.
        public Vector2 origin
        {
            get { return m_Origin; }
            set { m_Origin = value; }
        }

        // The direction of the ray.
        public Vector2 direction
        {
            get { return m_Direction; }
            set { m_Direction = value.normalized; }
        }

        // Returns a point at /distance/ units along the ray.
        public Vector2 GetPoint(float distance)
        {
            return m_Origin + m_Direction * distance;
        }

        public override string ToString()
        {
            return ToString(null, null);
        }

        public string ToString(string format)
        {
            return ToString(format, null);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format))
                format = "F2";
            if (formatProvider == null)
                formatProvider = CultureInfo.InvariantCulture.NumberFormat;
            return UnityString.Format("Origin: {0}, Dir: {1}", m_Origin.ToString(format, formatProvider), m_Direction.ToString(format, formatProvider));
        }
    }
}
