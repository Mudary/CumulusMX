﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace CumulusMX
{
    internal class Program
    {
        public static Cumulus cumulus;
        public static bool exitSystem = false;

        private static void Main(string[] args)
        {
            //var ci = new CultureInfo("en-GB");
            //System.Threading.Thread.CurrentThread.CurrentCulture = ci;
            const string appGuid = "57190d2e-7e45-4efb-8c09-06a176cef3f3";

            if (Type.GetType("Mono.Runtime") != null)
            {
                // Use reflection, so no attempt to load Mono dll on Windows

                Assembly _posixAsm;
                Type _unixSignalType, _signumType;
                MethodInfo _unixSignalWaitAny;

                _posixAsm = Assembly.Load("Mono.Posix, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
                _unixSignalType = _posixAsm.GetType("Mono.Unix.UnixSignal");
                _unixSignalWaitAny = _unixSignalType.GetMethod("WaitAny", new[] { _unixSignalType.MakeArrayType() });
                _signumType = _posixAsm.GetType("Mono.Unix.Native.Signum");

                Array _signals = Array.CreateInstance(_unixSignalType, 1);
                _signals.SetValue(Activator.CreateInstance(_unixSignalType, _signumType.GetField("SIGTERM").GetValue(null)), 0);

                Thread signal_thread = new Thread(delegate ()
                {
                    while (!exitSystem)
                    {
                        // Wait for a signal to be delivered
                        var id = (int)_unixSignalWaitAny.Invoke(null, new object[] { _signals });

                        Console.WriteLine("\nExiting system due to external SIGTERM signal");

                        exitSystem = true;
                    }
                });

                signal_thread.Start();

                // Now we need to catch the console Ctrl-C
                Console.CancelKeyPress += (s, ev) =>
                {
                    Console.WriteLine("Ctrl+C pressed");
                    Console.WriteLine("\nCumulus terminating");
                    cumulus.Stop();
                    Trace.WriteLine("Cumulus has shutdown");
                    Trace.Flush();
                    ev.Cancel = true;
                    exitSystem = true;
                };

            }
            else
            {
                var exithandler = new exitHandler();
            }

            int httpport = 8998;
            bool debug = false;

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            for (int i = 0; i < args.Length; i++)
            {
                try
                {
                    if (args[i] == "-lang" && args.Length >= i)
                    {
                        var lang = args[++i];

                        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(lang);
                        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(lang);
                    }
                    else if (args[i] == "-port" && args.Length >= i)
                    {
                        httpport = Convert.ToInt32(args[++i]);
                    }
                    else if (args[i] == "-debug")
                    {
                        // Switch on debug and and data logging from the start
                        debug = true;
                    }
                    else if (args[i] == "-wsport")
                    {
                        i++;
                        Console.WriteLine("The use of the -wsport command line parameter is deprecated");
                    }
                    else
                    {
                        Console.WriteLine($"Invalid command line argument \"{args[i]}\"");
                        usage();
                    }
                }
                catch
                {
                    usage();
                }
            }

#if DEBUG
            debug = true;
#endif


            using (Mutex appMutex = new Mutex(false, "Global\\" + appGuid))
            {
                Console.WriteLine("Current culture: " + CultureInfo.CurrentCulture.DisplayName);

                cumulus = new Cumulus(httpport, debug, appMutex);

                DateTime now = DateTime.Now;

                Console.WriteLine(DateTime.Now.ToString("G"));

                Console.WriteLine("Type Ctrl-C to terminate");
                while (!exitSystem)
                {
                    Thread.Sleep(500);
                }

                Trace.Flush();
                Environment.Exit(0);
            }
        }

        private static void usage()
        {
            Console.WriteLine();
            Console.WriteLine("Valid arugments are:");
            Console.WriteLine(" -port <http_portnum> - Sets the HTTP port Cumulus will use (default 8998)");
            Console.WriteLine(" -lang <culture_name> - Sets the Language Cumulus will use (defaults to current user language)");
            Console.WriteLine(" -debug               - Switches on debug and data logging from Cumulus start");
            Console.WriteLine("\nCumulus terminating");
            Environment.Exit(1);
        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Trace.WriteLine("!!! Unhandled Exception !!!");
                Trace.WriteLine(e.ExceptionObject.ToString());

                Console.WriteLine(e.ExceptionObject.ToString());
                Console.WriteLine("**** An error has occurred - please zip up the MXdiags folder and post it in the forum ****");
                Console.WriteLine("Press Enter to terminate");
                Console.ReadLine();
                Trace.Flush();
                Environment.Exit(1);
            }
            catch (Exception)
            {
            }
        }
    }

    public class exitHandler
    {
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        //private Program program;

        private delegate bool EventHandler(CtrlType sig);

        private static EventHandler _handler;

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        public exitHandler()
        {
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);
        }

        private static bool Handler(CtrlType sig)
        {
            var reason = new string[] { "Ctrl-C", "Ctrl-Break", "Close Main Window", "unknown", "unknown", "User Logoff", "System Shutdown" };
            Console.WriteLine("Cumulus terminating");

            Trace.WriteLine("Exiting system due to external: " + reason[(int)sig]);

            Program.cumulus.Stop();

            Trace.WriteLine("Cumulus has shutdown");
            Console.WriteLine("Cumulus stopped");

            //allow main to run off
            Program.exitSystem = true;

            return true;
        }
    }
}
