using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityEditor.XR.ObjectCapture
{
    /// <summary>
    /// Container for a message sent from the RealityKit photogrammetry session.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PhotogrammetrySessionMessage : IEquatable<PhotogrammetrySessionMessage>
    {
        /// <summary>
        /// The types of possible messages.
        /// </summary>
        public enum MessageType
        {
            /// <summary>
            /// Unknown message type.
            /// </summary>
            [Description("Unknown")]
            Unknown = 0,

            /// <summary>
            /// Input processing of the images has started.
            /// </summary>
            [Description("Input started")]
            InputStarted = 1,

            /// <summary>
            /// Input processing of the images has completed.
            /// </summary>
            [Description("Input complete")]
            InputComplete = 2,

            /// <summary>
            /// An invalid sample image was reported.
            /// </summary>
            [Description("Invalid sample")]
            InvalidSample = 3,

            /// <summary>
            /// An sample image was skipped.
            /// </summary>
            [Description("Skipped sample")]
            SkippedSample = 4,

            /// <summary>
            /// The sample images were down-sampled, typically due to resource constraints.
            /// </summary>
            [Description("Automatic downsampling")]
            AutomaticDownSampling = 5,

            /// <summary>
            /// A photogrammetry request has completed.
            /// </summary>
            [Description("Request complete")]
            RequestComplete = 6,

            /// <summary>
            /// A photogrammetry request has updated its progress amount.
            /// </summary>
            [Description("Request progress")]
            RequestProgress = 7,

            /// <summary>
            /// A photogrammetry request has encountered an error.
            /// </summary>
            [Description("Request error")]
            RequestError = 8,

            /// <summary>
            /// All photogrammetry requests in the photogrammetry session have completed.
            /// </summary>
            [Description("Processing complete")]
            ProcessingComplete = 9,

            /// <summary>
            /// The photogrammetry session was cancelled. Any pending or in-progress photogrammetry requests were abandoned.
            /// </summary>
            [Description("Processing cancelled")]
            ProcessingCancelled = 10,
        }

        /// <summary>
        /// The photogrammetry session ID.
        /// </summary>
        /// <value>
        /// The photogrammetry session ID.
        /// </value>
        /// <remarks>
        /// This should always be a valid, non-empty GUID.
        /// </remarks>
        public Guid sessionId
        {
            get => m_SessionId;
            private set => m_SessionId = value;
        }

        Guid m_SessionId;

        /// <summary>
        /// The photogrammetry request ID.
        /// </summary>
        /// <value>
        /// The photogrammetry request ID.
        /// </value>
        /// <remarks>
        /// This will be an empty GUID, if the message type is not specific to a photogrammetry request.
        /// </remarks>
        public Guid requestId
        {
            get => m_RequestId;
            private set => m_RequestId = value;
        }

        Guid m_RequestId;

        /// <summary>
        /// The type of the photogrammetry session message.
        /// </summary>
        /// <value>
        /// The type of the photogrammetry session message.
        /// </value>
        public MessageType messageType
        {
            get => m_MessageType;
            private set => m_MessageType = value;
        }

        MessageType m_MessageType;

        /// <summary>
        /// The progress of the active photogrammetry request.
        /// </summary>
        /// <value>
        /// A value between 0 and 1 reflecting the progress of the active photogrammetry request. Otherwise, this value will be a negative value when no active photogrammetry request exists.
        /// </value>
        public float requestProgress
        {
            get => m_RequestProgress;
            private set => m_RequestProgress = value;
        }

        float m_RequestProgress;

        /// <summary>
        /// The bounding box of the successful completion of a photogrammetry bounding box request.
        /// </summary>
        /// <value>
        /// If the <see cref="messageType" /> is of type <see cref="MessageType.RequestComplete" /> and the corresponding request was a photogrammetry bounding request,
        /// this will contain the bounding box of the successful completion of the photogrammetry bounding box request. Otherwise, this will be an invalid bounding box with non-positive extents.
        /// </value>
        public Bounds resultBoundingBox
        {
            get => m_ResultBoundingBox;
            private set => m_ResultBoundingBox = value;
        }

        Bounds m_ResultBoundingBox;

        /// <summary>
        /// The text of the message, if any.
        /// </summary>
        /// <value>
        /// The text of the message, if any. If the message contains no text, `null` is returned.
        /// </value>
        /// <remarks>
        /// The memory for this message is stored in native code and will be released when the callback ends.
        /// If you wish to preserve this string outside the scope of the callback, you will need to make a copy of this marshaled string.
        /// </remarks>
        public string messageText => Marshal.PtrToStringUni(m_MessagePtr);

        readonly IntPtr m_MessagePtr;

        /// <summary>
        /// Whether the request ID is a valid, non-empty GUID.
        /// </summary>
        /// <returns>
        /// `true` if the request ID is a valid, non-empty GUID. Otherwise, `false`.
        /// </returns>
        public bool hasValidRequestId => m_RequestId != Guid.Empty;

        /// <summary>
        /// Whether the request progress is a valid.
        /// </summary>
        /// <returns>
        /// `true` if the request progress is valid. Otherwise, `false`.
        /// </returns>
        /// <remarks>
        /// The request progress will be a negative value when there is no active photogrammetry request.
        /// </remarks>
        public bool hasValidRequestProgress => m_RequestProgress >= 0.0f;

        /// <summary>
        /// Whether the message has message text.
        /// </summary>
        /// <returns>
        /// `true` if the message has message text. Otherwise, `false`.
        /// </returns>
        public bool hasMessageText => m_MessagePtr != IntPtr.Zero;

        /// <summary>
        /// Whether the bound box result is valid.
        /// </summary>
        /// <returns>
        /// `true` if the bound box is valid and has positive extents. Otherwise, `false`.
        /// </returns>
        public bool hasResultBoundingBox => m_ResultBoundingBox.extents.x > 0.0f && m_ResultBoundingBox.extents.y > 0.0f && m_ResultBoundingBox.extents.z > 0.0f;

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="PhotogrammetrySessionMessage"/> against which to compare.</param>
        /// <returns>
        /// `true` if every field in <paramref name="other"/> is equal to this <see cref="PhotogrammetrySessionMessage"/>, otherwise `false`.
        /// </returns>
        public bool Equals(PhotogrammetrySessionMessage other)
        {
            return m_MessageType == other.m_MessageType
                && m_SessionId == other.m_SessionId
                && m_RequestId == other.m_RequestId
                && m_RequestProgress == other.m_RequestProgress
                && m_ResultBoundingBox == other.m_ResultBoundingBox
                && m_MessagePtr == other.m_MessagePtr;
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="obj">The `object` to compare against.</param>
        /// <returns>
        /// `true` if <paramref name="obj"/> is of type <see cref="PhotogrammetrySessionMessage"/> and <see cref="Equals(PhotogrammetrySessionMessage)"/> also returns `true`; otherwise `false`.
        /// </returns>
        public override bool Equals(System.Object obj) => obj is PhotogrammetrySessionMessage message && Equals(message);

        /// <summary>
        /// Tests for equality. Same as <see cref="Equals(PhotogrammetrySessionMessage)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>
        /// `true` if <paramref name="lhs"/> is equal to <paramref name="rhs"/>, otherwise `false`.
        /// </returns>
        public static bool operator ==(PhotogrammetrySessionMessage lhs, PhotogrammetrySessionMessage rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Tests for inequality. Same as `!`<see cref="Equals(PhotogrammetrySessionMessage)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>
        /// `true` if <paramref name="lhs"/> is not equal to <paramref name="rhs"/>, otherwise `false`.
        /// </returns>
        public static bool operator !=(PhotogrammetrySessionMessage lhs, PhotogrammetrySessionMessage rhs) => !lhs.Equals(rhs);

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
                hashCode = hashCode * 486187739 + ((int) m_MessageType).GetHashCode();
                hashCode = hashCode * 486187739 + m_SessionId.GetHashCode();
                hashCode = hashCode * 486187739 + m_RequestId.GetHashCode();
                hashCode = hashCode * 486187739 + m_RequestProgress.GetHashCode();
                hashCode = hashCode * 486187739 + m_ResultBoundingBox.GetHashCode();
                hashCode = hashCode * 486187739 + m_MessagePtr.GetHashCode();
            }

            return hashCode;
        }

        /// <summary>
        /// Generates a string representation of this <see cref="PhotogrammetrySessionMessage"/>.
        /// </summary>
        /// <returns>
        /// A string representation of this <see cref="PhotogrammetrySessionMessage"/>.
        /// </returns>
        public override string ToString() => ToString("0.0000");

        /// <summary>
        /// Generates a string representation of this <see cref="PhotogrammetrySessionMessage"/>. Floating point values use <paramref name="floatingPointFormat"/> to generate their string representations.
        /// </summary>
        /// <param name="floatingPointFormat">The format specifier used for floating point fields.</param>
        /// <returns>
        /// A string representation of this <see cref="PhotogrammetrySessionMessage"/>.
        /// </returns>
        public string ToString(string floatingPointFormat) =>
            string.Format($"message:{m_MessageType} sessionId:{m_SessionId} requestId:{m_RequestId} requestProgress:{m_RequestProgress.ToString(floatingPointFormat)} resultBoundingBox:{m_ResultBoundingBox.ToString(floatingPointFormat)} messageText:{messageText}");
    }
}
