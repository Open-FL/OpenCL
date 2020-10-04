using System.Linq;

using NUnit.Framework;

using OpenCL.NET.CommandQueues;
using OpenCL.NET.Memory;

namespace OpenCL.NET.Tests
{
    public class BufferTests : CLTestClass
    {

        [Test]
        public void CreateBuffer()
        {
            MemoryBuffer buff = context.CreateBuffer<byte>(
                                                           MemoryFlag.AllocateHostPointer | MemoryFlag.ReadWrite,
                                                           255,
                                                           "TestBufferA"
                                                          );

            buff.Dispose();

        }

        [Test]
        public void CreateCommandQueue()
        {
            CommandQueue queue = CommandQueue.CreateCommandQueue(context, device);
            queue.Dispose();
        }

        [Test]
        public void WriteBuffer()
        {
            CommandQueue queue = CommandQueue.CreateCommandQueue(context, device);

            MemoryBuffer buff = context.CreateBuffer<byte>(
                                                           MemoryFlag.AllocateHostPointer | MemoryFlag.ReadWrite,
                                                           255,
                                                           "TestBufferA"
                                                          );

            byte[] buf = new byte[255];

            queue.EnqueueWriteBuffer(buff, buf);

            buff.Dispose();

            queue.Dispose();

        }

        [Test]
        public void ReadBuffer()
        {
            CommandQueue queue = CommandQueue.CreateCommandQueue(context, device);

            MemoryBuffer buff = context.CreateBuffer<byte>(
                                                           MemoryFlag.AllocateHostPointer | MemoryFlag.ReadWrite,
                                                           255,
                                                           "TestBufferA"
                                                          );
            byte[] bufIn = Enumerable.Range(0, 255).Select(x => (byte)x).ToArray();

            queue.EnqueueWriteBuffer(buff, bufIn);

            byte[] bufOut = queue.EnqueueReadBuffer<byte>(buff, (int)buff.Size);

            for (int i = 0; i < bufIn.Length; i++)
            {
                if (bufIn[i] != bufOut[i])
                {
                    buff.Dispose();

                    queue.Dispose();
                    Assert.Fail("Buffer read back different data than was written into it.");
                    return;
                }
            }

            buff.Dispose();

            queue.Dispose();

        }
    }
}