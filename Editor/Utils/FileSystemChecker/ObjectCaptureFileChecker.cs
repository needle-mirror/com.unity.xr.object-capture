using System;
using System.Collections;
using System.IO;

namespace UnityEditor.XR.ObjectCapture
{
    class ObjectCaptureFileChecker
    {
        bool m_Cancelled;
        bool m_Processing;

        internal ObjectCaptureFileChecker()
        {
            m_Cancelled = false;
        }

        // Doesnt check if file exists already.
        internal IEnumerator CheckFileCreated(string path, Action<string> onFileCreated, Action onFileCheckingCancelled = null)
        {
            m_Processing = true;
            while (!File.Exists(path) && !m_Cancelled)
                yield return null;

            if (m_Cancelled)
                onFileCheckingCancelled?.Invoke();
            else
                onFileCreated?.Invoke(path);

            ObjectCaptureWindow.DeleteFromCheckedFiles(path);
            m_Processing = false;
        }

        internal void CancelCheck()
        {
            if (m_Processing)
                m_Cancelled = true;
        }
    }
}
