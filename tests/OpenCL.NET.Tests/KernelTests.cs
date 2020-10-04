using System;

using NUnit.Framework;

using OpenCL.NET.CommandQueues;
using OpenCL.NET.Kernels;
using OpenCL.NET.Memory;
using OpenCL.NET.Programs;

namespace OpenCL.NET.Tests
{
    public class KernelTests : CLTestClass
    {

        private const string TEST_KERNEL = @"
__kernel void set_value(__global uchar* arr, uchar value)
{
	int idx = get_global_id(0);
    arr[idx] = value;
}";

        [Test]
        public void CreateProgram()
        {
            Program program = context.CreateAndBuildProgramFromString(TEST_KERNEL);

            program.Dispose();
        }

        [Test]
        public void CreateKernel()
        {
            Program program = context.CreateAndBuildProgramFromString(TEST_KERNEL);

            Kernel kernel = program.CreateKernel("set_value");

            kernel.Dispose();

            program.Dispose();

        }

        [Test]
        public void RunKernel()
        {
            CommandQueue queue = CommandQueue.CreateCommandQueue(context, device);

            Program program = context.CreateAndBuildProgramFromString(TEST_KERNEL);

            Kernel kernel = program.CreateKernel("set_value");

            MemoryBuffer buffer = context.CreateBuffer(
                                                       MemoryFlag.CopyHostPointer | MemoryFlag.ReadWrite,
                                                       new byte[255],
                                                       "TestBufferA"
                                                      );

            kernel.SetKernelArgument(0, buffer);
            kernel.SetKernelArgumentGen(1, (byte)128);

            queue.EnqueueNDRangeKernel(kernel, 1, 255);

            byte[] result = queue.EnqueueReadBuffer<byte>(buffer, 255);

            foreach (byte b in result)
            {
                if (b != 128)
                {
                    Assert.Fail("Kernel did not execute Correctly.");
                }
            }

            buffer.Dispose();

            kernel.Dispose();

            program.Dispose();

            queue.Dispose();
        }

    }
}