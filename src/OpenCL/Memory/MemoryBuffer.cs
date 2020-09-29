#region Using Directives

using System;

#endregion

namespace OpenCL.NET.Memory
{
    /// <summary>
    ///     Represents an OpenCL memory buffer.
    /// </summary>
    public class MemoryBuffer : MemoryObject
    {

        #region Constructors

        /// <summary>
        ///     Initializes a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <param name="handle">The handle to the OpenCL memory buffer.</param>
        /// <param name="bytes">Size of the Memory Object(For Statistics)</param>
        public MemoryBuffer(IntPtr handle, object handleIdentifier)
            : base(handle, handleIdentifier)
        {
        }

        #endregion

    }
}