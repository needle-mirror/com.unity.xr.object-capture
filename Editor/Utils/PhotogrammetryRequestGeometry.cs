using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityEditor.XR.ObjectCapture
{
    /// <summary>
    /// Container to request geometry adjusts for a RealityKit photogrammetry request.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PhotogrammetryRequestGeometry : IEquatable<PhotogrammetryRequestGeometry>
    {
        /// <summary>
        /// The bounding box for the photogrammetry request.
        /// </summary>
        /// <value>
        /// The bounding box for the photogrammetry request.
        /// </value>
        /// <remarks>
        /// If the size/extents of the bounding box contain a negative value, the bounding box will not be used in the photogrammetry request.
        /// </remarks>
        public Bounds boundingBox
        {
            get => m_BoundingBox;
            set => m_BoundingBox = value;
        }

        Bounds m_BoundingBox;

        /// <summary>
        /// The scale for a transform for the photogrammetry request.
        /// </summary>
        /// <value>
        /// The scale for a transform for the photogrammetry request.
        /// </value>
        /// <remarks>
        /// If the scale contains a negative value, the transform (scale and pose) will not be used in the photogrammetry request.
        /// </remarks>
        public Vector3 scale
        {
            get => m_Scale;
            set => m_Scale = value;
        }

        Vector3 m_Scale;

        /// <summary>
        /// The pose for a transform for the photogrammetry request.
        /// </summary>
        /// <value>
        /// The pose for a transform for the photogrammetry request.
        /// </value>
        /// <remarks>
        /// If the <see cref="scale" /> contains a negative value, the transform (scale and pose) will not be used in the photogrammetry request.
        /// </remarks>
        public Pose pose
        {
            get => m_Pose;
            set => m_Pose = value;
        }

        Pose m_Pose;

        /// <summary>
        /// Construct geometry for a photogrammetry request using a <paramref name="boundingBox" /> and a transform (from by <paramref name="scale" /> and <paramref name="pose" />).
        /// </summary>
        /// <param name="boundingBox">The bounding box for the photogrammetry request.</param>
        /// <param name="scale">The scale for a transform for the photogrammetry request.</param>
        /// <param name="pose">The pose for a transform for the photogrammetry request.</param>
        public PhotogrammetryRequestGeometry(Bounds boundingBox, Vector3 scale, Pose pose)
        {
            m_BoundingBox = boundingBox;
            m_Scale = scale;
            m_Pose = pose;
        }

        /// <summary>
        /// Construct geometry for a photogrammetry request using a transform (from by <paramref name="scale" /> and <paramref name="pose" />).
        /// </summary>
        /// <param name="scale">The scale for a transform for the photogrammetry request.</param>
        /// <param name="pose">The pose for a transform for the photogrammetry request.</param>
        public PhotogrammetryRequestGeometry(Vector3 scale, Pose pose)
        {
            m_BoundingBox = new Bounds(Vector3.zero, Vector3.negativeInfinity);
            m_Scale = scale;
            m_Pose = pose;
        }

        /// <summary>
        /// Construct geometry for a photogrammetry request using a <paramref name="boundingBox" />.
        /// </summary>
        /// <param name="boundingBox">The bounding box for the photogrammetry request.</param>
        public PhotogrammetryRequestGeometry(Bounds boundingBox)
        {
            m_BoundingBox = boundingBox;
            m_Scale = Vector3.negativeInfinity;
            m_Pose = Pose.identity;
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="PhotogrammetryRequestGeometry"/> against which to compare.</param>
        /// <returns>
        /// `true` if every field in <paramref name="other"/> is equal to this <see cref="PhotogrammetryRequestGeometry"/>, otherwise `false`.
        /// </returns>
        public bool Equals(PhotogrammetryRequestGeometry other)
        {
            return m_BoundingBox.Equals(other.m_BoundingBox)
                && m_Scale.Equals(other.m_Scale)
                && m_Pose.Equals(other.m_Pose);
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="obj">The `object` to compare against.</param>
        /// <returns>
        /// `true` if <paramref name="obj"/> is of type <see cref="PhotogrammetryRequestGeometry"/> and <see cref="Equals(PhotogrammetryRequestGeometry)"/> also returns `true`; otherwise `false`.
        /// </returns>
        public override bool Equals(System.Object obj) => obj is PhotogrammetryRequestGeometry geometry && Equals(geometry);

        /// <summary>
        /// Tests for equality. Same as <see cref="Equals(PhotogrammetryRequestGeometry)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>
        /// `true` if <paramref name="lhs"/> is equal to <paramref name="rhs"/>, otherwise `false`.
        /// </returns>
        public static bool operator ==(PhotogrammetryRequestGeometry lhs, PhotogrammetryRequestGeometry rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Tests for inequality. Same as `!`<see cref="Equals(PhotogrammetryRequestGeometry)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>
        /// `true` if <paramref name="lhs"/> is not equal to <paramref name="rhs"/>, otherwise `false`.
        /// </returns>
        public static bool operator !=(PhotogrammetryRequestGeometry lhs, PhotogrammetryRequestGeometry rhs) => !lhs.Equals(rhs);

        /// <summary>
        /// Generates a hash suitable for use with containers like `HashSet` and `Dictionary`.
        /// </summary>
        /// <returns>
        /// A hash code generated from this object's fields.
        /// </returns>
        public override int GetHashCode()
        {
            var hashCode = 486187739;
            unchecked
            {
                hashCode = hashCode * 486187739 + m_BoundingBox.GetHashCode();
                hashCode = hashCode * 486187739 + m_Scale.GetHashCode();
                hashCode = hashCode * 486187739 + m_Pose.GetHashCode();
            }

            return hashCode;
        }

        /// <summary>
        /// Generates a string representation of this <see cref="PhotogrammetryRequestGeometry"/>.
        /// </summary>
        /// <returns>
        /// A string representation of this <see cref="PhotogrammetryRequestGeometry"/>.
        /// </returns>
        public override string ToString() => ToString("0.0000");

        /// <summary>
        /// Generates a string representation of this <see cref="PhotogrammetryRequestGeometry"/>. Floating point values use <paramref name="floatingPointFormat"/> to generate their string representations.
        /// </summary>
        /// <param name="floatingPointFormat">The format specifier used for floating point fields.</param>
        /// <returns>
        /// A string representation of this <see cref="PhotogrammetryRequestGeometry"/>.
        /// </returns>
        public string ToString(string floatingPointFormat) =>
            string.Format($"bounds:{m_BoundingBox.ToString(floatingPointFormat)} scale:{m_Scale.ToString(floatingPointFormat)} pose:{m_Pose.ToString(floatingPointFormat)}");
    }
}
