// Copyright © 2011 - Present RealDimensions Software, LLC
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
// You may obtain a copy of the License at
// 
// 	http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace chocolatey.infrastructure.commands
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using adapters;
    using filesystem;
    using Console = System.Console;
    using Environment = System.Environment;

    public sealed class PowershellExecutor
    {
        private static readonly IList<string> _powershellLocations = new List<string>
            {
                Environment.ExpandEnvironmentVariables("%windir%\\SysNative\\WindowsPowerShell\\v1.0\\powershell.exe"),
                Environment.ExpandEnvironmentVariables("%windir%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe"),
                "powershell.exe"
            };

        private static string _powershell = string.Empty;
        private static object _boxstarter;

        public static int execute(
            string command,
            IFileSystem fileSystem,
            int waitForExitSeconds,
            Action<object, EventArgs> stdOutAction,
            Action<object, EventArgs> stdErrAction
            )
        {
            if (string.IsNullOrWhiteSpace(_powershell)) _powershell = get_powershell_location(fileSystem);
            //-NoProfile -NoLogo -ExecutionPolicy unrestricted -Command "[System.Threading.Thread]::CurrentThread.CurrentCulture = ''; [System.Threading.Thread]::CurrentThread.CurrentUICulture = '';& '%DIR%chocolatey.ps1' %PS_ARGS%"
            string arguments = "-NoProfile -NoLogo -ExecutionPolicy Bypass -Command \"{0}\"".format_with(command);

            Runspace runspace;
            
            //if (_runspace != null)
            //{
                Console.Out.WriteLine("importing boxstarter modules");
                var initialSessionState = InitialSessionState.CreateDefault();
                initialSessionState.ImportPSModule(new[] { "c:\\dev\\boxstarter\\boxstarter.chocolatey\\boxstarter.chocolatey.psd1" });
                runspace = RunspaceFactory.CreateRunspace(initialSessionState);
                runspace.Open();
                Console.Out.WriteLine("setting boxstarter variable");
                runspace.SessionStateProxy.SetVariable("Boxstarter", _boxstarter);
                Console.Out.WriteLine("DONE importing boxstarter modules and var");

            //}
            Environment.CurrentDirectory = fileSystem.get_directory_name(Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", string.Empty));
            var pipeline = runspace.CreatePipeline(command);
            pipeline.Output.DataReady += new EventHandler(stdOutAction);
            pipeline.Error.DataReady += new EventHandler(stdErrAction);
            pipeline.Input.Close();
            pipeline.InvokeAsync();
            Console.Out.WriteLine("PS Installer called");

            //return CommandExecutor.execute(
            //    _powershell,
            //    arguments,
            //    waitForExitSeconds,
            //    workingDirectory: fileSystem.get_directory_name(Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", string.Empty)),
            //    stdOutAction: stdOutAction,
            //    stdErrAction: stdErrAction,
            //    updateProcessPath: true
            //    );

            while(!pipeline.Output.EndOfPipeline) {}

            return Environment.ExitCode;
        }

        public static string get_powershell_location(IFileSystem fileSystem)
        {
            foreach (var powershellLocation in _powershellLocations)
            {
                if (fileSystem.file_exists(powershellLocation))
                {
                    return powershellLocation;
                }
            }

            throw new FileNotFoundException("Unable to find suitable location for PowerShell. Searched the following locations: '{0}'".format_with(string.Join("; ", _powershellLocations)));
        }

        public static void set_runspace(object boxstarter)
        {
            _boxstarter = boxstarter;
        }
    }
}