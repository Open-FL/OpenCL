using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using OpenCL.NET.Contexts;
using OpenCL.NET.Devices;
using OpenCL.NET.Platforms;

namespace OpenCL.NET.Tests
{
    public abstract class CLTestClass
    {

        protected Platform platform;
        protected Device device;
        protected Context context;

        [SetUp]
        public void Setup()
        {
            IEnumerable<Platform> platforms = Platform.GetPlatforms();
            platform = platforms.First();

            IEnumerable<Device> devices = platform.GetDevices(DeviceType.All);
            device = devices.First();

            foreach (Platform platform in platforms.Skip(1))
            {
                platform.Dispose();
            }

            foreach (Device device in devices.Skip(1))
            {
                device.Dispose();
            }

            context = Context.CreateContext(devices);
        }

        [TearDown]
        public void TearDown()
        {
            context.Dispose();
            device.Dispose();
            platform.Dispose();
        }

    }
}