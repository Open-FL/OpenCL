#region Using Directives

using System;
using System.Runtime.InteropServices;

using OpenCL.NET.Interop;
using OpenCL.NET.Interop.Kernels;
using OpenCL.NET.Memory;

#endregion

namespace OpenCL.NET.Kernels
{
    /// <summary>
    ///     Represents an OpenCL kernel.
    /// </summary>
    public class Kernel : HandleBase
    {

        #region Constructors

        /// <summary>
        ///     Initializes a new <see cref="Kernel" /> instance.
        /// </summary>
        /// <param name="handle">The handle to the OpenCL kernel.</param>
        internal Kernel(IntPtr handle)
            : base(handle, "Kernel", true)
        {
        }

        #endregion

        #region Private Methods

        /// <summary>
        ///     Retrieves the specified information about the OpenCL kernel.
        /// </summary>
        /// <typeparam name="T">
        ///     The type of the data that is to be returned.</param>
        ///     <param name="kernelInformation">The kind of information that is to be retrieved.</param>
        ///     <exception cref="OpenClException">
        ///         If the information could not be retrieved, then an <see cref="OpenClException" />
        ///         is thrown.
        ///     </exception>
        ///     <returns>Returns the specified information.</returns>
        private T GetKernelInformation<T>(KernelInformation kernelInformation)
        {
            // Retrieves the size of the return value in bytes, this is used to later get the full information
            Result result =
                KernelsNativeApi.GetKernelInformation(
                                                      Handle,
                                                      kernelInformation,
                                                      UIntPtr.Zero,
                                                      null,
                                                      out UIntPtr returnValueSize
                                                     );
            if (result != Result.Success)
            {
                throw new OpenClException("The kernel information could not be retrieved.", result);
            }

            // Allocates enough memory for the return value and retrieves it
            byte[] output = new byte[returnValueSize.ToUInt32()];
            result = KernelsNativeApi.GetKernelInformation(
                                                           Handle,
                                                           kernelInformation,
                                                           new UIntPtr((uint) output.Length),
                                                           output,
                                                           out returnValueSize
                                                          );
            if (result != Result.Success)
            {
                throw new OpenClException("The kernel information could not be retrieved.", result);
            }

            // Returns the output
            return InteropConverter.To<T>(output);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        ///     Disposes of the resources that have been acquired by the kernel.
        /// </summary>
        /// <param name="disposing">Determines whether managed object or managed and unmanaged resources should be disposed of.</param>
        public override void Dispose()
        {
            // Checks if the kernel has already been disposed of, if not, then it is disposed of
            if (!IsDisposed)
            {
                KernelsNativeApi.ReleaseKernel(Handle);
            }

            // Makes sure that the base class can execute its dispose logic
            base.Dispose();
        }

        #endregion

        #region Public Properties

        /// <summary>
        ///     Contains the function name of the OpenCL kernel.
        /// </summary>
        private string functionName;

        /// <summary>
        ///     Gets the function name of the OpenCL kernel.
        /// </summary>
        public string FunctionName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(functionName))
                {
                    functionName = GetKernelInformation<string>(KernelInformation.FunctionName);
                }

                return functionName;
            }
        }

        /// <summary>
        ///     Contains the number of arguments, that the kernel function has.
        /// </summary>
        private int? numberOfArguments;

        /// <summary>
        ///     Gets the number of arguments, that the kernel function has.
        /// </summary>
        public int NumberOfArguments
        {
            get
            {
                if (!numberOfArguments.HasValue)
                {
                    numberOfArguments = (int) GetKernelInformation<uint>(KernelInformation.NumberOfArguments);
                }

                return numberOfArguments.Value;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Sets the specified argument to the specified value.
        /// </summary>
        /// <param name="index">The index of the parameter.</param>
        /// <param name="memoryObject">The memory object that contains the value to which the kernel argument is to be set.</param>
        public void SetKernelArgument(int index, MemoryObject memoryObject)
        {
            // Checks if the index is positive, if not, then an exception is thrown
            if (index < 0)
            {
                throw new OpenClArgumentIndexOutOfRangeException(
                                                                 $"The specified index {index} is invalid. The index of the argument must always be greater or equal to 0."
                                                                );
            }

            // The set kernel argument method needs a pointer to the pointer, therefore the pointer is pinned, so that the garbage collector can not move it in memory
            GCHandle garbageCollectorHandle = GCHandle.Alloc(memoryObject.Handle, GCHandleType.Pinned);
            try
            {
                // Sets the kernel argument and checks if it was successful, if not, then an exception is thrown
                Result result = KernelsNativeApi.SetKernelArgument(
                                                                   Handle,
                                                                   (uint) index,
                                                                   new UIntPtr(
                                                                               (uint) Marshal.SizeOf(
                                                                                    memoryObject.Handle
                                                                                   )
                                                                              ),
                                                                   garbageCollectorHandle.AddrOfPinnedObject()
                                                                  );
                if (result != Result.Success)
                {
                    throw new OpenClException($"The kernel argument with the index {index} could not be set.", result);
                }
            }
            finally
            {
                garbageCollectorHandle.Free();
            }
        }

        public void SetKernelArgumentVal(int index, object value)
        {
            // Checks if the index is positive, if not, then an exception is thrown
            if (index < 0)
            {
                throw new OpenClArgumentIndexOutOfRangeException(
                                                                 $"The specified index {index} is invalid. The index of the argument must always be greater or equal to 0."
                                                                );
            }

            // The set kernel argument method needs a pointer to the pointer, therefore the pointer is pinned, so that the garbage collector can not move it in memory
            GCHandle garbageCollectorHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
            try
            {
                // Sets the kernel argument and checks if it was successful, if not, then an exception is thrown
                Result result = KernelsNativeApi.SetKernelArgument(
                                                                   Handle,
                                                                   (uint) index,
                                                                   new UIntPtr((uint) Marshal.SizeOf(value)),
                                                                   garbageCollectorHandle.AddrOfPinnedObject()
                                                                  );
                if (result != Result.Success)
                {
                    throw new OpenClException($"The kernel argument with the index {index} could not be set.", result);
                }
            }
            finally
            {
                garbageCollectorHandle.Free();
            }
        }

        /// <summary>
        ///     Sets the specified argument to the specified value.
        /// </summary>
        /// <param name="index">The index of the parameter.</param>
        /// <param name="value">The memory object that contains the value to which the kernel argument is to be set.</param>
        public void SetKernelArgumentGen<T>(int index, T value) where T : struct
        {
            SetKernelArgumentVal(index, value);
        }

        #endregion

    }
}