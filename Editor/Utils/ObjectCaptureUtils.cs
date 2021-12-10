using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEditor.XR.ObjectCapture
{
    /// <summary>
    /// A static class containing the native entry points to the photogrammetry session.
    /// </summary>
    public static class ObjectCaptureUtils
    {
        /// <summary>
        /// The type of error codes that could be returned from the native photogrammetry code.
        /// </summary>
        public enum PhotogrammetryErrorCode
        {
            /// <summary>
            /// No error.
            /// </summary>
            [Description("None")]
            None = 0,

            /// <summary>
            /// The method was called on an unsupported platform.
            /// </summary>
            [Description("Unsupported platform")]
            UnsupportedPlatform = -1,

            /// <summary>
            /// The method was called on a system where they are unavailable.
            /// </summary>
            /// <remarks>
            /// This would typically be an older operating system without the photogrammetry support available.
            /// </remarks>
            [Description("Unavailable")]
            Unavailable = -2,

            /// <summary>
            /// The input directory was invalid.
            /// </summary>
            [Description("Invalid input directory")]
            InvalidInputDirectory = -3,

            /// <summary>
            /// The session ID provided does not exist in the native code.
            /// </summary>
            [Description("Invalid session ID")]
            InvalidSessionId = -4,

            /// <summary>
            /// The photogrammetry session could not be created.
            /// </summary>
            [Description("Cannot create photogrammetry session")]
            CannotCreatePhotogrammetrySession = -5,

            /// <summary>
            /// The photogrammetry session could not be started because it had not been created properly in native code.
            /// </summary>
            [Description("Cannot start photogrammetry session that has not been created")]
            CannotStartUncreatedPhotogrammetrySession = -6,

            /// <summary>
            /// The photogrammetry session could not be started because it was already running.
            /// </summary>
            [Description("Cannot start photogrammetry session that is already running")]
            CannotStartPhotogrammetrySessionAlreadyRunning = -7,

            /// <summary>
            /// The photogrammetry session could not be started because it contained no pending photogrammetry requests.
            /// </summary>
            [Description("Cannot start photogrammetry session without pending requests")]
            CannotStartPhotogrammetrySessionWithoutRequests = -8,

            /// <summary>
            /// The photogrammetry session could not be started for another reason.
            /// </summary>
            [Description("Cannot start photogrammetry session")]
            CannotStartPhotogrammetrySession = -9,
        }

        /// <summary>
        /// The level of detail for the model to be created by the photogrammetry request.
        /// </summary>
        public enum PhotogrammetryRequestDetail
        {
            /// <summary>
            /// A fast, low-quality object for previewing the final result.
            /// </summary>
            /// <remarks>
            /// Uncompressed texture size is 1024x1024 requiring ~10.7 MB
            /// </remarks>
            [Description("Preview")]
            Preview = 0,

            /// <summary>
            /// A low-quality object with low resource requirements.
            /// </summary>
            /// <remarks>
            /// Uncompressed texture size is 2048x2048 requiring ~42.7 MB
            /// </remarks>
            [Description("Reduced")]
            Reduced = 1,

            /// <summary>
            /// A medium-quality object with moderate resource requirements.
            /// </summary>
            /// <remarks>
            /// Uncompressed texture size is 4096x4096 requiring ~170.7 MB
            /// </remarks>
            [Description("Medium")]
            Medium = 2,

            /// <summary>
            /// The raw-created object at the highest possible resolution.
            /// </summary>
            /// <remarks>
            /// Uncompressed texture size is 8192x8192 requiring ~853.3 MB
            /// </remarks>
            [Description("Full")]
            Full = 3,

            /// <summary>
            /// A high-quality object with significant resource requirements.
            /// </summary>
            /// <remarks>
            /// Several uncompressed textures with size 8192x8192 requiring variable texture memory
            /// </remarks>
            [Description("Raw")]
            Raw = 4,
        }

        /// <summary>
        /// The type of error codes that could be returned from the native load image code.
        /// </summary>
        public enum LoadImageErrorCode
        {
            /// <summary>
            /// No error.
            /// </summary>
            [Description("None")]
            None = 0,

            /// <summary>
            /// The method was called on an unsupported platform.
            /// </summary>
            [Description("Unsupported platform")]
            UnsupportedPlatform = -1,

            /// <summary>
            /// The requested file could not be opened.
            /// </summary>
            [Description("Cannot open file")]
            CannotOpenFile = -2,

            /// <summary>
            /// The thumbnail could not be created.
            /// </summary>
            [Description("Cannot create thumbnail")]
            CannotCreateThumbnail = -3,

            /// <summary>
            /// The required context could not be created.
            /// </summary>
            [Description("Cannot create the required context")]
            CannotCreateContext = -4,
        }

        /// <summary>
        /// Loads an image from a local file with the given <paramref name ="inputImagePath" /> into a `NativeArray&lt;byte&gt;`.
        /// </summary>
        /// <param name="inputImagePath">The local file path for the input image to load.</param>
        /// <param name="maxDimension">If this value is positive, the returned image will be resized, preserving the aspect ratio, such that the larger of the image width and image height will be less
        /// than or equal to this maximum value. Otherwise, if this value is 0 or negative, the full image will be returned.
        /// </param>
        /// <param name="imageWidth">Upon success, the width in pixels of the returned image.</param>
        /// <param name="imageHeight">Upon success, the height in pixels of the returned image.</param>
        /// <param name="imageLoadErrorCode">This contains the image load error code if any.</param>
        /// <returns>
        /// Upon success, the `NativeArray&lt;byte&gt;` containing the loaded image where the raw image bytes are essentially a RGBA_8888 in the native array. Otherwise, an uncreated `NativeArray&lt;byte&gt;`
        /// will be returned with the <paramref name="imageLoadErrorCode" /> describing the error.
        /// </returns>
        public static unsafe NativeArray<byte> TryLoadImage(string inputImagePath, int maxDimension, out int imageWidth, out int imageHeight, out LoadImageErrorCode imageLoadErrorCode)
        {
            var image = ObjectCaptureUtils.TryLoadImage(inputImagePath, maxDimension, out imageWidth, out imageHeight, out var imageSize, out imageLoadErrorCode);

            NativeArray<byte> imageByteArray;
            try
            {
                imageByteArray = new NativeArray<byte>(imageSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                UnsafeUtility.MemCpy(imageByteArray.GetUnsafePtr(), image, imageSize * sizeof(byte));
            }
            finally
            {
                ObjectCaptureUtils.ReleaseImage(image);
            }

            return imageByteArray;
        }

#if UNITY_EDITOR_OSX

        /// <summary>
        /// Determines whether photogrammetry is available.
        /// </summary>
        /// <returns>
        /// `true` if photogrammetry is available; otherwise, `false`.
        /// </returns>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_Is_Photogrammetry_Available")]
        public static extern bool IsPhotogrammetryAvailable();

        /// <summary>
        /// Set whether logging to the desktop log file is enabled.
        /// </summary>
        /// <param name="isLoggingEnabled">`true` if desktop file logging should be enabled. Otherwise, `false`.</param>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_SetLoggingEnabled")]
        public static extern void SetLoggingEnabled(bool isLoggingEnabled);

        /// <summary>
        /// Creates a photogrammetry session using the given input directory path contains source photogrammetry image files.
        /// </summary>
        /// <param name="inputDirectoryPath">An input directory path containing the source photogrammetry image files for input.</param>
        /// <param name="sessionId">If a photogrammetry session was created, this will contain the ID for the created photogrammetry session.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry session was created; otherwise, `false`.
        /// </returns>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_TryCreatePhotogrammetrySession")]
        public static extern bool TryCreatePhotogrammetrySession([MarshalAs(UnmanagedType.LPUTF8Str)] string inputDirectoryPath, out Guid sessionId, out PhotogrammetryErrorCode errorCode);

        /// <summary>
        /// Appends a photogrammetry bounding box request to the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to which to append the photogrammetry bounding box request.</param>
        /// <param name="requestId">If a photogrammetry bounding box request was created, this will contain the ID for the created photogrammetry bounding box request.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry bounding request was appended to the photogrammetry session; otherwise, `false`.
        /// </returns>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_TryAppendPhotogrammetryBoundingBoxRequest")]
        public static extern bool TryAppendPhotogrammetryBoundingBoxRequest(Guid sessionId, out Guid requestId, out PhotogrammetryErrorCode errorCode);

        /// <summary>
        /// Appends a photogrammetry request to the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to which to append the photogrammetry request.</param>
        /// <param name="outputFileName">An output filename to which to write the resulting object constructed through photogrammetry of the session's input images.</param>
        /// <param name="photogrammetryRequestDetail">The level of detail for the photogrammetry request.</param>
        /// <param name="requestId">If a photogrammetry request was created, this will contain the ID for the created photogrammetry request.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry request was appended to the photogrammetry session; otherwise, `false`.
        /// </returns>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_TryAppendPhotogrammetryRequest")]
        public static extern bool TryAppendPhotogrammetryRequest(Guid sessionId, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputFileName,
            PhotogrammetryRequestDetail photogrammetryRequestDetail, out Guid requestId, out PhotogrammetryErrorCode errorCode);

        /// <summary>
        /// Appends a photogrammetry request to the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to which to append the photogrammetry request.</param>
        /// <param name="outputFileName">An output filename to which to write the resulting object constructed through photogrammetry of the session's input images.</param>
        /// <param name="photogrammetryRequestDetail">The level of detail for the photogrammetry request.</param>
        /// <param name="photogrammetryRequestGeometry">Geometry information, including a bounding box and/or transform,that will affect the result for the photogrammetry request.</param>
        /// <param name="requestId">If a photogrammetry request was created, this will contain the ID for the created photogrammetry request.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry request was appended to the photogrammetry session; otherwise, `false`.
        /// </returns>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_TryAppendPhotogrammetryGeometryRequest")]
        public static extern bool TryAppendPhotogrammetryGeometryRequest(Guid sessionId, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputFileName,
            PhotogrammetryRequestDetail photogrammetryRequestDetail, PhotogrammetryRequestGeometry photogrammetryRequestGeometry,
            out Guid requestId, out PhotogrammetryErrorCode errorCode);

        /// <summary>
        /// Starts the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to start.</param>
        /// <param name="photogrammetrySessionMessageAction">An action that will be called from the native code and that will contain messages about the progression of the photogrammetry session.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry session was started; otherwise, `false`.
        /// </returns>
        /// <remarks>
        /// The `Action` passed in as the <paramref name="photogrammetrySessionMessageAction"/> parameter must be kept alive until either the photogrammetry session completes, is canceled by calling
        /// <see cref="TryCancelPhotogrammetrySession(Guid, out PhotogrammetryErrorCode)"/>, is destroyed by calling <see cref="DestroyPhotogrammetrySession(Guid)"/>, or clears the callback by calling
        /// <see cref="TryClearPhotogrammetrySessionCallback(Guid, out PhotogrammetryErrorCode)"/>. Failure to keep the `Action` alive will result in the native code calling back into an object's `Action`
        /// callback that has already been deleted which would cause an application crash.
        /// </remarks>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_TryStartPhotogrammetrySession")]
        public static extern bool TryStartPhotogrammetrySession(Guid sessionId, Action<PhotogrammetrySessionMessage> photogrammetrySessionMessageAction, out PhotogrammetryErrorCode errorCode);

        /// <summary>
        /// Cancels the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to cancel.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry session was canceled; otherwise, `false`.
        /// </returns>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_TryCancelPhotogrammetrySession")]
        public static extern bool TryCancelPhotogrammetrySession(Guid sessionId, out PhotogrammetryErrorCode errorCode);

        /// <summary>
        /// Set the callback to the photogrammetry session.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to start.</param>
        /// <param name="photogrammetrySessionMessageAction">An action that will be called from the native code and that will contain messages about the progression of the photogrammetry session.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry session set the callback; otherwise, `false`.
        /// </returns>
        /// <remarks>
        /// The `Action` passed in as the <paramref name="photogrammetrySessionMessageAction"/> parameter must be kept alive until either the photogrammetry session completes, is canceled by calling
        /// <see cref="TryCancelPhotogrammetrySession(Guid, out PhotogrammetryErrorCode)"/>, is destroyed by calling <see cref="DestroyPhotogrammetrySession(Guid)"/>, or clears the callback by calling
        /// <see cref="TryClearPhotogrammetrySessionCallback(Guid, out PhotogrammetryErrorCode)"/>. Failure to keep the `Action` alive will result in the native code calling back into an object's `Action`
        /// callback that has already been deleted which would cause an application crash.
        /// </remarks>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_TrySetPhotogrammetrySessionCallback")]
        public static extern bool TrySetPhotogrammetrySessionCallback(Guid sessionId, Action<PhotogrammetrySessionMessage> photogrammetrySessionMessageAction, out PhotogrammetryErrorCode errorCode);

        /// <summary>
        /// Clear the callback on the photogrammetry session.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to start.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry session set clears callback; otherwise, `false`.
        /// </returns>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_TryClearPhotogrammetrySessionCallback")]
        public static extern bool TryClearPhotogrammetrySessionCallback(Guid sessionId, out PhotogrammetryErrorCode errorCode);

        /// <summary>
        /// Destroys the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to destroy.</param>
        /// <remarks>
        /// Destroying any active photogrammetry session will cancel any active requests if the session is active.
        /// </remarks>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_DestroyPhotogrammetrySession")]
        public static extern void DestroyPhotogrammetrySession(Guid sessionId);

        /// <summary>
        /// Loads an image from a local file with the given <paramref name ="imageFilePath" />.
        /// </summary>
        /// <param name="imageFilePath">The local file path for the input image to load.</param>
        /// <param name="maxDimension">If this value is positive, the returned image will be resized, preserving the aspect ratio, such that the larger of the image width and image height will be less
        /// than or equal to this maximum value. Otherwise, if this value is 0 or negative, the full image will be returned.
        /// </param>
        /// <param name="imageWidth">Upon success, the width in pixels of the returned image.</param>
        /// <param name="imageHeight">Upon success, the height in pixels of the returned image.</param>
        /// <param name="imageSize">Upon success, the memory size in bytes of the returned image.</param>
        /// <param name="imageLoadErrorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// Upon success, an `void*` into native memory containing the loaded image bytes that are essentially a RGBA_8888 format. Otherwise, `null`will be returned with the
        /// <paramref name="imageLoadErrorCode" /> describing the error.
        /// </returns>
        /// <remarks>
        /// If nonnull, the returned native must be released with <see cref="ReleaseImage" />. Otherwise, the native memory will be leaked.
        /// </remarks>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_TryLoadImage")]
        static extern unsafe void* TryLoadImage([MarshalAs(UnmanagedType.LPUTF8Str)] string imageFilePath, int maxDimension, out int imageWidth, out int imageHeight, out int imageSize, out LoadImageErrorCode imageLoadErrorCode);

        /// <summary>
        /// Releases the native memory.
        /// </summary>
        /// <param name="image">The native memory to be released.</param>
        [DllImport("ObjectCapture", EntryPoint = "Unity_XR_ObjectCapture_ReleaseImage")]
        static extern unsafe void ReleaseImage(void* image);

#else // UNITY_EDITOR_OSX

        /// <summary>
        /// Determines whether photogrammetry is available.
        /// </summary>
        /// <returns>
        /// `true` if photogrammetry is available; otherwise, `false`.
        /// </returns>
        public static bool IsPhotogrammetryAvailable() => false;

        /// <summary>
        /// Set whether logging to the desktop log file is enabled.
        /// </summary>
        /// <param name="isLoggingEnabled">`true` if desktop file logging should be enabled. Otherwise, `false`.</param>
        public static void SetLoggingEnabled(bool isLoggingEnabled) {}

        /// <summary>
        /// Creates a photogrammetry session using the given input directory path contains source photogrammetry image files.
        /// </summary>
        /// <param name="inputDirectoryPath">An input directory path containing the source photogrammetry image files for input.</param>
        /// <param name="sessionId">If a photogrammetry session was created, this will contain the ID for the created photogrammetry session.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry session was created; otherwise, `false`.
        /// </returns>
        public static bool TryCreatePhotogrammetrySession(string inputDirectoryPath, out Guid sessionId, out PhotogrammetryErrorCode errorCode)
        {
            sessionId = Guid.Empty;
            errorCode = PhotogrammetryErrorCode.UnsupportedPlatform;
            return false;
        }

        /// <summary>
        /// Appends a photogrammetry bounding box request to the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to which to append the photogrammetry bounding box request.</param>
        /// <param name="requestId">If a photogrammetry bounding box request was created, this will contain the ID for the created photogrammetry bounding box request.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry bounding request was appended to the photogrammetry session; otherwise, `false`.
        /// </returns>
        public static bool TryAppendPhotogrammetryBoundingBoxRequest(Guid sessionId, out Guid requestId, out PhotogrammetryErrorCode errorCode)
        {
            requestId = Guid.Empty;
            errorCode = PhotogrammetryErrorCode.UnsupportedPlatform;
            return false;
        }

        /// <summary>
        /// Appends a photogrammetry request to the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to which to append the photogrammetry request.</param>
        /// <param name="outputFileName">An output filename to which to write the resulting object constructed through photogrammetry of the session's input images.</param>
        /// <param name="photogrammetryRequestDetail">The level of detail for the photogrammetry request.</param>
        /// <param name="requestId">If a photogrammetry request was created, this will contain the ID for the created photogrammetry request.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry request was appended to the photogrammetry session; otherwise, `false`.
        /// </returns>
        public static bool TryAppendPhotogrammetryRequest(Guid sessionId, string outputFileName, PhotogrammetryRequestDetail photogrammetryRequestDetail, out Guid requestId, out PhotogrammetryErrorCode errorCode)
        {
            requestId = Guid.Empty;
            errorCode = PhotogrammetryErrorCode.UnsupportedPlatform;
            return false;
        }

        /// <summary>
        /// Appends a photogrammetry request to the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to which to append the photogrammetry request.</param>
        /// <param name="outputFileName">An output filename to which to write the resulting object constructed through photogrammetry of the session's input images.</param>
        /// <param name="photogrammetryRequestDetail">The level of detail for the photogrammetry request.</param>
        /// <param name="photogrammetryRequestGeometry">Geometry information, including a bounding box and/or transform,that will affect the result for the photogrammetry request.</param>
        /// <param name="requestId">If a photogrammetry request was created, this will contain the ID for the created photogrammetry request.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry request was appended to the photogrammetry session; otherwise, `false`.
        /// </returns>
        public static bool TryAppendPhotogrammetryGeometryRequest(Guid sessionId, string outputFileName, PhotogrammetryRequestDetail photogrammetryRequestDetail,
                                                                  PhotogrammetryRequestGeometry photogrammetryRequestGeometry, out Guid requestId, out PhotogrammetryErrorCode errorCode)
        {
            requestId = Guid.Empty;
            errorCode = PhotogrammetryErrorCode.UnsupportedPlatform;
            return false;
        }

        /// <summary>
        /// Starts the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to start.</param>
        /// <param name="photogrammetrySessionMessageAction">An action that will be called from the native code and that will contain messages about the progression of the photogrammetry session.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry session was started; otherwise, `false`.
        /// </returns>
        /// <remarks>
        /// The `Action` passed in as the <paramref name="photogrammetrySessionMessageAction"/> parameter must be kept alive until either the photogrammetry session completes, is canceled by calling
        /// <see cref="TryCancelPhotogrammetrySession(Guid, out PhotogrammetryErrorCode)"/>, is destroyed by calling <see cref="DestroyPhotogrammetrySession(Guid)"/>, or clears the callback by calling
        /// <see cref="TryClearPhotogrammetrySessionCallback(Guid, out PhotogrammetryErrorCode)"/>. Failure to keep the `Action` alive will result in the native code calling back into an object's `Action`
        /// callback that has already been deleted which would cause an application crash.
        /// </remarks>
        public static bool TryStartPhotogrammetrySession(Guid sessionId, Action<PhotogrammetrySessionMessage> photogrammetrySessionMessageAction, out PhotogrammetryErrorCode errorCode)
        {
            errorCode = PhotogrammetryErrorCode.UnsupportedPlatform;
            return false;
        }

        /// <summary>
        /// Cancels the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to cancel.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry session was canceled; otherwise, `false`.
        /// </returns>
        public static bool TryCancelPhotogrammetrySession(Guid sessionId, out PhotogrammetryErrorCode errorCode)
        {
            errorCode = PhotogrammetryErrorCode.UnsupportedPlatform;
            return false;
        }

        /// <summary>
        /// Set the callback to the photogrammetry session.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to start.</param>
        /// <param name="photogrammetrySessionMessageAction">An action that will be called from the native code and that will contain messages about the progression of the photogrammetry session.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry session set the callback; otherwise, `false`.
        /// </returns>
        /// <remarks>
        /// The `Action` passed in as the <paramref name="photogrammetrySessionMessageAction"/> parameter must be kept alive until either the photogrammetry session completes, is canceled by calling
        /// <see cref="TryCancelPhotogrammetrySession(Guid, out PhotogrammetryErrorCode)"/>, is destroyed by calling <see cref="DestroyPhotogrammetrySession(Guid)"/>, or clears the callback by calling
        /// <see cref="TryClearPhotogrammetrySessionCallback(Guid, out PhotogrammetryErrorCode)"/>. Failure to keep the `Action` alive will result in the native code calling back into an object's `Action`
        /// callback that has already been deleted which would cause an application crash.
        /// </remarks>
        public static bool TrySetPhotogrammetrySessionCallback(Guid sessionId, Action<PhotogrammetrySessionMessage> photogrammetrySessionMessageAction, out PhotogrammetryErrorCode errorCode)
        {
            errorCode = PhotogrammetryErrorCode.UnsupportedPlatform;
            return false;
        }

        /// <summary>
        /// Clear the callback on the photogrammetry session.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to start.</param>
        /// <param name="errorCode">This will contain the error code reporting more detail on any error encountered.</param>
        /// <returns>
        /// `true` if the photogrammetry session set clears callback; otherwise, `false`.
        /// </returns>
        public static bool TryClearPhotogrammetrySessionCallback(Guid sessionId, out PhotogrammetryErrorCode errorCode)
        {
            errorCode = PhotogrammetryErrorCode.UnsupportedPlatform;
            return false;
        }

        /// <summary>
        /// Destroys the photogrammetry session with the given ID.
        /// </summary>
        /// <param name="sessionId">The ID of the photogrammetry session to destroy.</param>
        /// <remarks>
        /// Destroying any active photogrammetry session will cancel any active requests if the session is active.
        /// </remarks>
        public static void DestroyPhotogrammetrySession(Guid sessionId) {}

        static unsafe void* TryLoadImage(string imageFilePath, int maxDimension, out int imageWidth, out int imageHeight, out int imageSize, out LoadImageErrorCode imageLoadErrorCode)
        {
            imageWidth = 0;
            imageHeight = 0;
            imageSize = 0;
            imageLoadErrorCode = LoadImageErrorCode.UnsupportedPlatform;
            return null;
        }

        static unsafe void ReleaseImage(void* image) {}

#endif // UNITY_EDITOR_OSX
    }
}
