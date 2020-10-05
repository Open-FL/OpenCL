#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using OpenCL.NET.Devices;
using OpenCL.NET.Interop;
using OpenCL.NET.Interop.Contexts;
using OpenCL.NET.Interop.Memory;
using OpenCL.NET.Interop.Programs;
using OpenCL.NET.Memory;
using OpenCL.NET.Programs;

using MemoryFlag = OpenCL.NET.Memory.MemoryFlag;

#endregion

namespace OpenCL.NET.Contexts
{
    /// <summary>
    ///     Represents an OpenCL context.
    /// </summary>
    public class Context : HandleBase
    {

        #region Constructors

        /// <summary>
        ///     Initializes a new <see cref="Context" /> instance.
        /// </summary>
        /// <param name="handle">The handle to the OpenCL context.</param>
        /// <param name="devices">The devices for which the context was created.</param>
        internal Context(IntPtr handle, IEnumerable<Device> devices)
            : base(handle, "Context", true)
        {
            Devices = devices;
        }

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets the devices for which the context was created.
        /// </summary>
        public IEnumerable<Device> Devices { get; }

        #endregion

        #region Private Methods

        /// <summary>
        ///     Retrieves the specified information about the program build.
        /// </summary>
        /// <typeparam name="T">
        ///     The type of the data that is to be returned.</param>
        ///     <param name="program">The handle to the program for which the build information is to be retrieved.</param>
        ///     <param name="device">The device for which the build information is to be retrieved.</param>
        ///     <param name="programBuildInformation">The kind of information that is to be retrieved.</param>
        ///     <exception cref="OpenClException">
        ///         If the information could not be retrieved, then an <see cref="OpenClException" />
        ///         is thrown.
        ///     </exception>
        ///     <returns>Returns the specified information.</returns>
        private T GetProgramBuildInformation<T>(
            IntPtr program, Device device,
            ProgramBuildInformation programBuildInformation)
        {
            // Retrieves the size of the return value in bytes, this is used to later get the full information
            Result result = ProgramsNativeApi.GetProgramBuildInformation(
                                                                         program,
                                                                         device.Handle,
                                                                         programBuildInformation,
                                                                         UIntPtr.Zero,
                                                                         null,
                                                                         out UIntPtr returnValueSize
                                                                        );
            if (result != Result.Success)
            {
                throw new OpenClException("The program build information could not be retrieved.", result);
            }

            // Allocates enough memory for the return value and retrieves it
            byte[] output = new byte[returnValueSize.ToUInt32()];
            result = ProgramsNativeApi.GetProgramBuildInformation(
                                                                  program,
                                                                  device.Handle,
                                                                  programBuildInformation,
                                                                  new UIntPtr((uint) output.Length),
                                                                  output,
                                                                  out returnValueSize
                                                                 );
            if (result != Result.Success)
            {
                throw new OpenClException("The program build information could not be retrieved.", result);
            }

            // Returns the output
            return InteropConverter.To<T>(output);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        ///     Disposes of the resources that have been acquired by the context.
        /// </summary>
        /// <param name="disposing">Determines whether managed object or managed and unmanaged resources should be disposed of.</param>
        public override void Dispose()
        {
            // Checks if the context has already been disposed of, if not, then the context is disposed of
            if (!IsDisposed)
            {
                ContextsNativeApi.ReleaseContext(Handle);
            }

            // Makes sure that the base class can execute its dispose logic
            base.Dispose();
        }

        #endregion

        #region Private Delegates

        /// <summary>
        ///     A delegate for the callback of <see cref="BuildProgram" />.
        /// </summary>
        /// <param name="program">The program that was compiled and linked.</param>
        /// <param name="userData">User-defined data that can be passed to the callback subscription.</param>
        private delegate void BuildProgramCallback(IntPtr program, IntPtr userData);

        #endregion

        #region Public Methods

        /// <summary>
        ///     Creates a program from the provided source codes asynchronously. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="sources">The source codes from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public Task<Program> CreateAndBuildProgramFromStringAsync(IEnumerable<string> sources)
        {
            // Creates a new task completion source, which is used to signal when the build has completed
            TaskCompletionSource<Program> taskCompletionSource = new TaskCompletionSource<Program>();

            // Loads the program from the specified source string
            IntPtr[] sourceList = sources.Select(source => Marshal.StringToHGlobalAnsi(source)).ToArray();
            uint[] sourceLengths = sources.Select(source => (uint) source.Length).ToArray();
            IntPtr programPointer =
                ProgramsNativeApi.CreateProgramWithSource(Handle, 1, sourceList, sourceLengths, out Result result);

            // Checks if the program creation was successful, if not, then an exception is thrown
            if (result != Result.Success)
            {
                throw new OpenClException("The program could not be created.", result);
            }

            // Builds (compiles and links) the program and checks if it was successful, if not, then an exception is thrown
            Result result1 = result;
            result = ProgramsNativeApi.BuildProgram(
                                                    programPointer,
                                                    0,
                                                    null,
                                                    null,
                                                    Marshal.GetFunctionPointerForDelegate(
                                                         new BuildProgramCallback(
                                                              (
                                                                  builtProgramPointer,
                                                                  userData) =>
                                                              {
                                                                  // Tries to validate the build, if not successful, then an exception is thrown
                                                                  try
                                                                  {
                                                                      // Cycles over all devices and retrieves the build log for each one, so that the errors that occurred can be added to the exception message (if any error occur during the retrieval, the exception is thrown without the log)
                                                                      Dictionary
                                                                      <string
                                                                        , string
                                                                      > buildLogs
                                                                          = new
                                                                              Dictionary
                                                                              <string
                                                                                , string
                                                                              >();
                                                                      foreach
                                                                      (Device
                                                                           device
                                                                          in
                                                                          Devices
                                                                      )
                                                                      {
                                                                          try
                                                                          {
                                                                              string
                                                                                  buildLog
                                                                                      = GetProgramBuildInformation
                                                                                      <string
                                                                                      >(
                                                                                           builtProgramPointer,
                                                                                           device,
                                                                                           ProgramBuildInformation
                                                                                               .Log
                                                                                          ).Trim();
                                                                              if
                                                                              (!string
                                                                                   .IsNullOrWhiteSpace(
                                                                                        buildLog
                                                                                       ))
                                                                              {
                                                                                  buildLogs
                                                                                      .Add(
                                                                                           device
                                                                                               .Name,
                                                                                           buildLog
                                                                                          );
                                                                              }
                                                                          }
                                                                          catch
                                                                          (OpenClException
                                                                          )
                                                                          {
                                                                          }
                                                                      }

                                                                      // Checks if there were any errors, if so then the build logs are compiled into a formatted string and integrates it into the exception message
                                                                      if
                                                                      (buildLogs
                                                                          .Any()
                                                                      )
                                                                      {
                                                                          string
                                                                              buildLogString
                                                                                  = string
                                                                                      .Join(
                                                                                           $"{Environment.NewLine}{Environment.NewLine}",
                                                                                           buildLogs
                                                                                               .Select(
                                                                                                    keyValuePair =>
                                                                                                        $" Build log for device \"{keyValuePair.Key}\":{Environment.NewLine}{keyValuePair.Value}"
                                                                                                   )
                                                                                          );
                                                                          taskCompletionSource
                                                                              .TrySetException(
                                                                                   new
                                                                                       OpenClException(
                                                                                            $"The program could not be compiled and linked.{Environment.NewLine}{Environment.NewLine}{buildLogString}",
                                                                                            result1
                                                                                           )
                                                                                  );
                                                                      }

                                                                      // Since the build was successful, the program is created and the task completion source is resolved with it Creates the new program and returns it
                                                                      taskCompletionSource
                                                                          .TrySetResult(
                                                                               new
                                                                                   Program(
                                                                                        builtProgramPointer
                                                                                       )
                                                                              );
                                                                  }
                                                                  catch
                                                                  (Exception
                                                                      exception
                                                                  )
                                                                  {
                                                                      taskCompletionSource
                                                                          .TrySetException(
                                                                               exception
                                                                              );
                                                                  }
                                                              }
                                                             )
                                                        ),
                                                    IntPtr.Zero
                                                   );

            // Checks if the build could be started successfully, if not, then an exception is thrown
            if (result != Result.Success)
            {
                if (result != Result.Success)
                {
                    taskCompletionSource.TrySetException(
                                                         new OpenClException(
                                                                             "The program could not be compiled and linked.",
                                                                             result
                                                                            )
                                                        );
                }
            }

            // Returns the task which is resolved when the program was build successful or not
            return taskCompletionSource.Task;
        }

        /// <summary>
        ///     Creates a program from the provided source codes. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="sources">The source codes from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public Program CreateAndBuildProgramFromString(IEnumerable<string> sources)
        {
            // Loads the program from the specified source string
            IntPtr[] sourceList = sources.Select(source => Marshal.StringToHGlobalAnsi(source)).ToArray();
            uint[] sourceLengths = sources.Select(source => (uint) source.Length).ToArray();
            IntPtr programPointer =
                ProgramsNativeApi.CreateProgramWithSource(Handle, 1, sourceList, sourceLengths, out Result result);

            // Checks if the program creation was successful, if not, then an exception is thrown
            if (result != Result.Success)
            {
                throw new OpenClException("The program could not be created.", result);
            }

            // Builds (compiles and links) the program and checks if it was successful, if not, then an exception is thrown
            result = ProgramsNativeApi.BuildProgram(programPointer, 0, null, null, IntPtr.Zero, IntPtr.Zero);
            if (result != Result.Success)
            {
                // Cycles over all devices and retrieves the build log for each one, so that the errors that occurred can be added to the exception message (if any error occur during the retrieval, the exception is thrown without the log)
                Dictionary<string, string> buildLogs = new Dictionary<string, string>();
                foreach (Device device in Devices)
                {
                    try
                    {
                        string buildLog =
                            GetProgramBuildInformation<string>(programPointer, device, ProgramBuildInformation.Log)
                                .Trim();
                        if (!string.IsNullOrWhiteSpace(buildLog))
                        {
                            buildLogs.Add(device.Name, buildLog);
                        }
                    }
                    catch (OpenClException)
                    {
                    }
                }

                // Compiles the build logs into a formatted string and integrates it into the exception message
                string buildLogString = string.Join(
                                                    $"{Environment.NewLine}{Environment.NewLine}",
                                                    buildLogs.Select(
                                                                     keyValuePair =>
                                                                         $" Build log for device \"{keyValuePair.Key}\":{Environment.NewLine}{keyValuePair.Value}"
                                                                    )
                                                   );
                throw new OpenClException(
                                          $"The program could not be compiled and linked.{Environment.NewLine}{Environment.NewLine}{buildLogString}",
                                          result
                                         );
            }

            // Creates the new program and returns it
            return new Program(programPointer);
        }

        /// <summary>
        ///     Creates a program from the provided source code asynchronously. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="source">The source code from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public Task<Program> CreateAndBuildProgramFromStringAsync(string source)
        {
            return CreateAndBuildProgramFromStringAsync(new List<string> { source });
        }

        /// <summary>
        ///     Creates a program from the provided source code. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="source">The source code from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public Program CreateAndBuildProgramFromString(string source)
        {
            return CreateAndBuildProgramFromString(new List<string> { source });
        }

        /// <summary>
        ///     Creates a program from the provided source streams asynchronously. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="streams">The source streams from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public async Task<Program> CreateAndBuildProgramFromStreamAsync(IEnumerable<Stream> streams)
        {
            // Uses a stream reader to read the all streams
            List<string> sourceList = new List<string>();
            foreach (Stream source in streams)
            {
                using (StreamReader stringReader = new StreamReader(source))
                {
                    sourceList.Add(await stringReader.ReadToEndAsync());
                }
            }

            // Compiles the loaded strings
            return await CreateAndBuildProgramFromStringAsync(sourceList);
        }

        /// <summary>
        ///     Creates a program from the provided source streams. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="streams">The source streams from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public Program CreateAndBuildProgramFromStream(IEnumerable<Stream> streams)
        {
            // Uses a stream reader to read the all streams
            List<string> sourceList = new List<string>();
            foreach (Stream source in streams)
            {
                using (StreamReader stringReader = new StreamReader(source))
                {
                    sourceList.Add(stringReader.ReadToEnd());
                }
            }

            // Compiles the loaded strings
            return CreateAndBuildProgramFromString(sourceList);
        }

        /// <summary>
        ///     Creates a program from the provided source stream asynchronously. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="stream">The source stream from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public Task<Program> CreateAndBuildProgramFromStreamAsync(Stream stream)
        {
            return CreateAndBuildProgramFromStreamAsync(new List<Stream> { stream });
        }

        /// <summary>
        ///     Creates a program from the provided source stream. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="stream">The source stream from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public Program CreateAndBuildProgramFromStream(Stream stream)
        {
            return CreateAndBuildProgramFromStream(new List<Stream> { stream });
        }

        /// <summary>
        ///     Creates a program from the provided source files asynchronously. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="fileNames">The source files from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public async Task<Program> CreateAndBuildProgramFromFileAsync(IEnumerable<string> fileNames)
        {
            // Loads all the source code files and reads them in
            List<string> sourceList = new List<string>();
            foreach (string fileName in fileNames)
            {
                using (StreamReader streamRreader = File.OpenText(fileName))
                {
                    sourceList.Add(await streamRreader.ReadToEndAsync());
                }
            }

            // Compiles and returnes the program
            return await CreateAndBuildProgramFromStringAsync(sourceList);
        }

        /// <summary>
        ///     Creates a program from the provided source files. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="fileNames">The source files from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public Program CreateAndBuildProgramFromFile(IEnumerable<string> fileNames)
        {
            // Loads all the source code files and reads them in
            List<string> sourceList = fileNames.Select(fileName => File.ReadAllText(fileName)).ToList();

            // Compiles and returnes the program
            return CreateAndBuildProgramFromString(sourceList);
        }

        /// <summary>
        ///     Creates a program from the provided source file asynchronously. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="fileName">The source file from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public Task<Program> CreateAndBuildProgramFromFileAsync(string fileName)
        {
            return CreateAndBuildProgramFromFileAsync(new List<string> { fileName });
        }

        /// <summary>
        ///     Creates a program from the provided source file. The program is created, compiled, and linked.
        /// </summary>
        /// <param name="fileName">The source file from which the program is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the program could not be created, compiled, or linked, then an
        ///     <see cref="OpenClException" /> is thrown.
        /// </exception>
        /// <returns>Returns the created program.</returns>
        public Program CreateAndBuildProgramFromFile(string fileName)
        {
            return CreateAndBuildProgramFromFile(new List<string> { fileName });
        }

        /// <summary>
        ///     Creates a new memory buffer with the specified flags and of the specified size.
        /// </summary>
        /// <param name="memoryFlags">The flags, that determines the how the memory buffer is created and how it can be accessed.</param>
        /// <param name="size">The size of memory that should be allocated for the memory buffer.</param>
        /// <exception cref="OpenClException">
        ///     If the memory buffer could not be created, then an <see cref="OpenClException" /> is
        ///     thrown.
        /// </exception>
        /// <returns>Returns the created memory buffer.</returns>
        public MemoryBuffer CreateBuffer(MemoryFlag memoryFlags, int size, object handleIdentifier)
        {
            // Creates a new memory buffer of the specified size and with the specified memory flags
            IntPtr memoryBufferPointer = MemoryNativeApi.CreateBuffer(
                                                                      Handle,
                                                                      (Interop.Memory.MemoryFlag) memoryFlags,
                                                                      new UIntPtr((uint) size),
                                                                      IntPtr.Zero,
                                                                      out Result result
                                                                     );

            // Checks if the creation of the memory buffer was successful, if not, then an exception is thrown
            if (result != Result.Success)
            {
                throw new OpenClException("The memory buffer could not be created.", result);
            }

            // Creates the memory buffer from the pointer to the memory buffer and returns it
            return new MemoryBuffer(memoryBufferPointer, handleIdentifier);
        }


        /// <summary>
        ///     Creates a new memory buffer with the specified flags. The size of memory allocated for the memory buffer is
        ///     determined by <see cref="T" /> and the number of elements.
        /// </summary>
        /// <typeparam name="T">The size of the memory buffer will be determined by the structure specified in the type parameter.</typeparam>
        /// <param name="memoryFlags">The flags, that determines the how the memory buffer is created and how it can be accessed.</param>
        /// <exception cref="OpenClException">
        ///     If the memory buffer could not be created, then an <see cref="OpenClException" /> is
        ///     thrown.
        /// </exception>
        /// <returns>Returns the created memory buffer.</returns>
        public MemoryBuffer CreateBuffer<T>(MemoryFlag memoryFlags, int size, object handleIdentifier)
            where T : struct
        {
            return CreateBuffer(memoryFlags, Marshal.SizeOf<T>() * size, handleIdentifier);
        }

        public MemoryBuffer CreateBuffer(MemoryFlag memoryFlags, Type t, object[] value, object handleIdentifier)
        {
            // Tries to create the memory buffer, if anything goes wrong, then it is crucial to free the allocated memory
            IntPtr hostBufferPointer = IntPtr.Zero;
            try
            {
                // Determines the size of the specified value and creates a pointer that points to the data inside the structure
                int size = Marshal.SizeOf(t) * value.Length;
                hostBufferPointer = Marshal.AllocHGlobal(size);
                for (int i = 0; i < value.Length; i++)
                {
                    Marshal.StructureToPtr(value[i], IntPtr.Add(hostBufferPointer, i * Marshal.SizeOf(t)), false);
                }

                // Creates a new memory buffer for the specified value
                IntPtr memoryBufferPointer = MemoryNativeApi.CreateBuffer(
                                                                          Handle,
                                                                          (Interop.Memory.MemoryFlag) memoryFlags,
                                                                          new UIntPtr((uint) size),
                                                                          hostBufferPointer,
                                                                          out Result result
                                                                         );

                // Checks if the creation of the memory buffer was successful, if not, then an exception is thrown
                if (result != Result.Success)
                {
                    throw new OpenClException("The memory buffer could not be created.", result);
                }

                // Creates the memory buffer from the pointer to the memory buffer and returns it
                return new MemoryBuffer(memoryBufferPointer, handleIdentifier);
            }
            finally
            {
                // Deallocates the host memory allocated for the value
                if (hostBufferPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(hostBufferPointer);
                }
            }
        }

        /// <summary>
        ///     Creates a new memory buffer with the specified flags for the specified array. The size of memory 1allocated for the
        ///     memory buffer is determined by <see cref="T" /> and the number of elements in the array.
        /// </summary>
        /// <typeparam name="T">The size of the memory buffer will be determined by the structure specified in the type parameter.</typeparam>
        /// <param name="memoryFlags">The flags, that determines the how the memory buffer is created and how it can be accessed.</param>
        /// <param name="value">The value that is to be copied over to the device.</param>
        /// <exception cref="OpenClException">
        ///     If the memory buffer could not be created, then an <see cref="OpenClException" /> is
        ///     thrown.
        /// </exception>
        /// <returns>Returns the created memory buffer.</returns>
        public MemoryBuffer CreateBuffer<T>(MemoryFlag memoryFlags, T[] value, object handleIdentifier)
            where T : struct
        {
            return CreateBuffer(memoryFlags, typeof(T), Array.ConvertAll(value, x => (object) x), handleIdentifier);
        }

        #endregion

        #region Public Static Methods

        /// <summary>
        ///     Creates a new context for the specified device.
        /// </summary>
        /// <param name="device">The device for which the context is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the context could not be created, then an <see cref="OpenClException" /> exception
        ///     is thrown.
        /// </exception>
        /// <returns>Returns the created context.</returns>
        public static Context CreateContext(Device device)
        {
            return CreateContext(new List<Device> { device });
        }

        /// <summary>
        ///     Creates a new context for the specified device.
        /// </summary>
        /// <param name="devices">The devices for which the context is to be created.</param>
        /// <exception cref="OpenClException">
        ///     If the context could not be created, then an <see cref="OpenClException" /> exception
        ///     is thrown.
        /// </exception>
        /// <returns>Returns the created context.</returns>
        public static Context CreateContext(IEnumerable<Device> devices)
        {
            // Creates the new context for the specified devices
            IntPtr contextPointer = ContextsNativeApi.CreateContext(
                                                                    IntPtr.Zero,
                                                                    (uint) devices.Count(),
                                                                    devices.Select(device => device.Handle).ToArray(),
                                                                    IntPtr.Zero,
                                                                    IntPtr.Zero,
                                                                    out Result result
                                                                   );

            // Checks if the device creation was successful, if not, then an exception is thrown
            if (result != Result.Success)
            {
                throw new OpenClException("The context could not be created.", result);
            }

            // Creates the new context object from the pointer and returns it
            return new Context(contextPointer, devices);
        }

        #endregion

    }
}