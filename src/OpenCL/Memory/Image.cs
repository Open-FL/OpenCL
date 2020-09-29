#region Using Directives

using System;

#endregion

namespace OpenCL.NET.Memory
{
    /// <summary>
    ///     Represents an OpenCL image.
    /// </summary>
    public class Image : MemoryObject
    {

        #region Constructors

        /// <summary>
        ///     Initializes a new <see cref="Image" /> instance.
        /// </summary>
        /// <param name="handle">The handle to the OpenCL image.</param>
        public Image(IntPtr handle, object handleIdentifier)
            : base(handle, handleIdentifier)
        {
        }

        #endregion

    }
}