using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EventStore.Common.Utils;
using Mono.Unix.Native;

namespace EventStore.Native {
	public static class ShellExecutorUnix {
        const int RLIMIT_NOFILE = 7;
        struct rlimit {
            public IntPtr rlimit_cur;
            public IntPtr rlimit_max;
        }

        [DllImport ("libc", SetLastError = true)]
        unsafe extern static int setrlimit(int resource, rlimit* rlim);

        [DllImport ("libc", SetLastError = true)]
        extern static int fork();

		public static string GetOutput(string command, string args = null, int numBytesToRead = 2048) {
			if(!Runtime.IsUnixOrMac)
				return null;

            int readside, writeside;
            Syscall.pipe(out readside, out writeside);
            int pid = fork();
            if(pid == 0){ //child process
				SetUlimitNOFILE(1024);

				Syscall.close(readside); /*unused read side*/
				var info = new ProcessStartInfo {
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					FileName = command,
					Arguments = args ?? string.Empty
				};

				string res;
				using (var process = Process.Start(info)) {
					res =  process.StandardOutput.ReadToEnd();
				}

                var bufptr = Marshal.StringToHGlobalAnsi(res);
                var written = Syscall.write(writeside, bufptr, (ulong)res.Length);
				Console.Out.WriteLine("Written bytes: {0}",written);
                Syscall.close(writeside);
				Marshal.FreeHGlobal(bufptr);
				Syscall.exit(0);
				return null;
			}
			else{ //parent process
				Syscall.close(writeside); /*unused write side*/
				var bufptr = Marshal.AllocHGlobal(numBytesToRead);
				var read = Syscall.read(readside, bufptr, (ulong)numBytesToRead);
				Console.Out.WriteLine("Read bytes: {0}",read);
				var result = Marshal.PtrToStringAnsi(bufptr);
				Console.Out.WriteLine("Command {0}\n{1}",command, result);
				Syscall.close(readside);

				Marshal.FreeHGlobal(bufptr);
				return result;
			}
		}

        private unsafe static void SetUlimitNOFILE(int limit)
        {
			rlimit data = new rlimit ();
			data.rlimit_cur = (IntPtr) limit;
			data.rlimit_max = (IntPtr) limit;
			setrlimit(RLIMIT_NOFILE, &data);
        }
    }
}
