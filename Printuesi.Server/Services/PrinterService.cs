using System.Net.Sockets;
using System.Diagnostics;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Services
{
    public class PrinterService : PrinterService_Interface
    {
        private readonly string _printerIP;
        private readonly int _lprport = 515;
        private readonly int _rawport = 9100;
        private readonly string _printerQueue;
        private readonly ILogger<PrinterService> _logger;
        private readonly IConfiguration _config;

        public PrinterService(
        IConfiguration config,
        ILogger<PrinterService> logger)
        {
            _config = config;
            _logger = logger;

            _printerIP = config["Printer:Ip"] ?? "192.168.88.93";
            _printerQueue = config["Printer:Queue"] ?? "PASSTHRU";
        }
        // hap TCP socket ne porten 515 dhe perpiqet te lidhet per 5 seconda me pas con cfare ke ne radhe gje
        // nese po nuk vazhdon, nese jo gati kjo eshte per te pare nese eshte gati ose jo
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var client = new TcpClient();

                var connectionTask = client.ConnectAsync(_printerIP, _lprport);
                if (await Task.WhenAny(connectionTask, Task.Delay(5000)) != connectionTask)
                    return false;
                var stream = client.GetStream();

                var queueCmd = new byte[] { 0x04 }
                    .Concat(System.Text.Encoding.ASCII.GetBytes(_printerQueue))
                    .Concat(new byte[] { 0x0A })
                    .ToArray();

                await stream.WriteAsync(queueCmd);
                await Task.Delay(500);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SendDriverAsync(string driverPath)
        {
            if (!File.Exists(driverPath))
                return false;

            try
            {
                var data = await File.ReadAllBytesAsync(driverPath);
                var fileName = Path.GetFileName(driverPath);

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(_printerIP, _lprport);
                if (await Task.WhenAny(connectTask, Task.Delay(10000)) != connectTask)
                    return false;

                var stream = client.GetStream();

                var receiveJobCmd = new byte[] { 0x02 }
                    .Concat(System.Text.Encoding.ASCII.GetBytes(_printerQueue))
                    .Concat(new byte[] { 0x0A })
                    .ToArray();
                await stream.WriteAsync(receiveJobCmd);
                await Task.Delay(200);

                var controlContent = $"Hhost\nPuser\nfdfA001host\nUdfA001host\nN{fileName}\n";
                var controlBytes = System.Text.Encoding.ASCII.GetBytes(controlContent);
                var controlHeader = System.Text.Encoding.ASCII.GetBytes(
                    $"\x02{controlBytes.Length} cfA001host\n");

                await stream.WriteAsync(controlHeader);
                await Task.Delay(100);
                await stream.WriteAsync(controlBytes);
                await stream.WriteAsync(new byte[] { 0x00 });
                await Task.Delay(200);

                var dataHeader = System.Text.Encoding.ASCII.GetBytes(
                    $"\x03{data.Length} dfA001host\n");

                await stream.WriteAsync(dataHeader);
                await Task.Delay(100);
                await stream.WriteAsync(data);
                await stream.WriteAsync(new byte[] { 0x00 });

                await Task.Delay(1500); // give printer time to apply settings
                return true;
            }
            catch
            {
                return false;
            }
        }

        /*public async Task<bool> SendFileLprAsync(string filePath)
        {

            //si fillim shikon nese file ca cuam ekziston apo jo ne kundert ndalon
            if (!File.Exists(filePath))
                return false;

            try
            {

                //lexon pdfne si bytes 
                var data = await File.ReadAllBytesAsync(filePath);
                var fileName = Path.GetFileName(filePath);
                // nxjerr emrin e etiketes qe ta ruajme per me vone ku eshte 

                using var client = new TcpClient();
                //nis lidhja
                var connectTask = client.ConnectAsync(_printerIP, _lprport);
                if (await Task.WhenAny(connectTask, Task.Delay(10000)) != connectTask)
                    return false;
                //nese printeri nuk kthen pergjigje per 10 sec kthen false 

                var stream = client.GetStream();

                // lidhet me printerin dhe dergon nepermjet stream

                //0x02 tregon qe dua te coj nje printim pastaj 0x0A qe ben nje rresht te ri
                var receiveJobCmd = new byte[] { 0x02 }
                    .Concat(System.Text.Encoding.ASCII.GetBytes(_printerQueue))
                    .Concat(new byte[] { 0x0A })
                    .ToArray();
                await stream.WriteAsync(receiveJobCmd);
                await Task.Delay(200);

                //kjo eshte kush qysh tek po printon formati, dhe e con file si byte me x02
                var controlContent = $"Hhost\nPuser\nfdfA001host\nUdfA001host\nN{fileName}\n";
                var controlBytes = System.Text.Encoding.ASCII.GetBytes(controlContent);
                var controlHeader = System.Text.Encoding.ASCII.GetBytes(
                    $"\x02{controlBytes.Length} cfA001host\n");

                await stream.WriteAsync(controlHeader);
                await Task.Delay(100);
                await stream.WriteAsync(controlBytes);
                await stream.WriteAsync(new byte[] { 0x00 });
                await Task.Delay(200);

                // send pdf
                var dataHeader = System.Text.Encoding.ASCII.GetBytes(
                    $"\x03{data.Length} dfA001host\n");

                //announce the data file and send all bytes
                await stream.WriteAsync(dataHeader);
                await Task.Delay(100);
                await stream.WriteAsync(data);
                await stream.WriteAsync(new byte[] { 0x00 });

                await Task.Delay(2000);
                return true;
            }
            catch
            {
                return false;
            }
        }*/

        public async Task<bool> SendFileRawAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(_printerIP, _rawport);
                if (await Task.WhenAny(connectTask, Task.Delay(10000)) != connectTask)
                    return false;

                var data = await File.ReadAllBytesAsync(filePath);
                var stream = client.GetStream();

                await stream.WriteAsync(data);
                await Task.Delay(2000);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SendFileLprAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                var data = await File.ReadAllBytesAsync(filePath);
                var fileName = Path.GetFileName(filePath);

                using var client = new TcpClient();

                var connectTask = client.ConnectAsync(_printerIP, _lprport);
                if (await Task.WhenAny(connectTask, Task.Delay(10000)) != connectTask)
                    return false;

                await connectTask;

                var stream = client.GetStream();

                var receiveJobCmd = new byte[] { 0x02 }
                    .Concat(System.Text.Encoding.ASCII.GetBytes(_printerQueue))
                    .Concat(new byte[] { 0x0A })
                    .ToArray();

                await stream.WriteAsync(receiveJobCmd);
                await Task.Delay(200);

                var controlContent =
                    $"Hhost\n" +
                    $"Puser\n" +
                    $"fdfA001host\n" +
                    $"UdfA001host\n" +
                    $"N{fileName}\n";

                var controlBytes = System.Text.Encoding.ASCII.GetBytes(controlContent);

                var controlHeader = System.Text.Encoding.ASCII.GetBytes(
                    $"\x02{controlBytes.Length} cfA001host\n");

                await stream.WriteAsync(controlHeader);
                await Task.Delay(100);
                await stream.WriteAsync(controlBytes);
                await stream.WriteAsync(new byte[] { 0x00 });
                await Task.Delay(200);

                var dataHeader = System.Text.Encoding.ASCII.GetBytes(
                    $"\x03{data.Length} dfA001host\n");

                await stream.WriteAsync(dataHeader);
                await Task.Delay(100);
                await stream.WriteAsync(data);
                await stream.WriteAsync(new byte[] { 0x00 });

                await Task.Delay(2000);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> PrintPdfWithCupsAsync(
    string pdfPath,
    int copies,
    PaperSize paperSize)
        {
            if (!File.Exists(pdfPath))
            {
                _logger.LogError("CUPS print failed. PDF not found: {PdfPath}", pdfPath);
                return false;
            }

            if (copies < 1)
            {
                _logger.LogError("CUPS print failed. Invalid copy count: {Copies}", copies);
                return false;
            }

            var mode = _config["Printing:Mode"];

            if (string.IsNullOrWhiteSpace(mode))
                mode = OperatingSystem.IsWindows() ? "RemoteCupsSsh" : "LocalCups";

            if (mode.Equals("RemoteCupsSsh", StringComparison.OrdinalIgnoreCase))
            {
                return await PrintPdfWithRemoteCupsSshAsync(pdfPath, copies, paperSize);
            }

            return await PrintPdfWithLocalCupsAsync(pdfPath, copies, paperSize);
        }

        private async Task<bool> PrintPdfWithLocalCupsAsync(
            string pdfPath,
            int copies,
            PaperSize paperSize)
        {
            var args = BuildLpArguments(pdfPath, copies, paperSize);

            var result = await RunProcessAsync("lp", args, timeoutSeconds: 60);

            if (result.ExitCode != 0)
            {
                _logger.LogError(
                    "Local CUPS print failed. ExitCode: {ExitCode}. STDOUT: {Stdout}. STDERR: {Stderr}",
                    result.ExitCode,
                    result.Stdout,
                    result.Stderr);

                return false;
            }

            _logger.LogInformation(
                "Local CUPS print accepted. File: {PdfPath}. STDOUT: {Stdout}. STDERR: {Stderr}",
                pdfPath,
                result.Stdout,
                result.Stderr);

            return true;
        }

        private async Task<bool> PrintPdfWithRemoteCupsSshAsync(
            string pdfPath,
            int copies,
            PaperSize paperSize)
        {
            var host = _config["RemoteCups:Host"] ?? throw new InvalidOperationException("RemoteCups:Host is required.");
            var user = _config["RemoteCups:User"] ?? "root";
            var port = _config.GetValue<int?>("RemoteCups:Port") ?? 22;
            var remoteFolder = _config["RemoteCups:RemoteFolder"] ?? "/tmp/printuesi-jobs";
            var sshPath = _config["RemoteCups:SshPath"] ?? "ssh";
            var scpPath = _config["RemoteCups:ScpPath"] ?? "scp";
            var keepRemoteFiles = _config.GetValue<bool?>("RemoteCups:KeepRemoteFiles") ?? true;

            var target = $"{user}@{host}";
            var remoteFileName = $"{Path.GetFileNameWithoutExtension(pdfPath)}_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}.pdf";
            remoteFileName = MakeLinuxSafeFileName(remoteFileName);

            var remotePath = $"{remoteFolder.TrimEnd('/')}/{remoteFileName}";

            _logger.LogInformation(
                "Remote CUPS print started. LocalFile: {LocalFile}, Remote: {Target}:{RemotePath}, Copies: {Copies}, PaperSize: {PaperSize}",
                pdfPath,
                target,
                remotePath,
                copies,
                paperSize);

            // 1. Create remote folder.
            var mkdirResult = await RunProcessAsync(
                sshPath,
                new[]
                {
            "-p", port.ToString(),
            target,
            $"mkdir -p {ShellQuote(remoteFolder)}"
                },
                timeoutSeconds: 30);

            if (mkdirResult.ExitCode != 0)
            {
                _logger.LogError(
                    "Remote CUPS mkdir failed. ExitCode: {ExitCode}. STDOUT: {Stdout}. STDERR: {Stderr}",
                    mkdirResult.ExitCode,
                    mkdirResult.Stdout,
                    mkdirResult.Stderr);

                return false;
            }

            // 2. Copy PDF to Linux VM.
            var scpResult = await RunProcessAsync(
                scpPath,
                new[]
                {
            "-P", port.ToString(),
            pdfPath,
            $"{target}:{remotePath}"
                },
                timeoutSeconds: 120);

            if (scpResult.ExitCode != 0)
            {
                _logger.LogError(
                    "Remote CUPS SCP failed. ExitCode: {ExitCode}. STDOUT: {Stdout}. STDERR: {Stderr}",
                    scpResult.ExitCode,
                    scpResult.Stdout,
                    scpResult.Stderr);

                return false;
            }

            // 3. Run lp on Linux VM.
            var lpArgs = BuildLpArguments(remotePath, copies, paperSize);
            var remoteCommand = "lp " + string.Join(" ", lpArgs.Select(ShellQuote));

            var printResult = await RunProcessAsync(
                sshPath,
                new[]
                {
            "-p", port.ToString(),
            target,
            remoteCommand
                },
                timeoutSeconds: 60);

            if (printResult.ExitCode != 0)
            {
                _logger.LogError(
                    "Remote CUPS lp failed. ExitCode: {ExitCode}. Command: {Command}. STDOUT: {Stdout}. STDERR: {Stderr}",
                    printResult.ExitCode,
                    remoteCommand,
                    printResult.Stdout,
                    printResult.Stderr);

                return false;
            }

            _logger.LogInformation(
                "Remote CUPS print accepted. Command: {Command}. STDOUT: {Stdout}. STDERR: {Stderr}",
                remoteCommand,
                printResult.Stdout,
                printResult.Stderr);

            // 4. Optional cleanup.
            if (!keepRemoteFiles)
            {
                _ = await RunProcessAsync(
                    sshPath,
                    new[]
                    {
                "-p", port.ToString(),
                target,
                $"rm -f {ShellQuote(remotePath)}"
                    },
                    timeoutSeconds: 30);
            }

            return true;
        }

        private List<string> BuildLpArguments(
            string pdfPath,
            int copies,
            PaperSize paperSize)
        {
            var queueName = _config["Cups:QueueName"] ?? "EpsonLabel";
            var profilePath = $"Cups:PaperProfiles:{paperSize}";

            var pageSize = _config[$"{profilePath}:PageSize"] ??
                paperSize switch
                {
                    PaperSize.Normale => "Custom.43x149.5mm",
                    PaperSize.Mini => "Custom.100x20mm",
                    PaperSize.Prodhim => "Custom.80x110mm",
                    PaperSize.Rrethi => "Custom.45x45mm",
                    _ => "Custom.43x149.5mm"
                };

            var mediaType = _config[$"{profilePath}:MediaType"] ?? "GrossyPaper";
            var printQuality = _config[$"{profilePath}:PrintQuality"] ?? "Quality";
            var actionMode = _config[$"{profilePath}:ActionMode"] ?? "AutoCutCollateEnd";
            var cutInterval = _config[$"{profilePath}:CutInterval"] ?? "1";
            var buzzer = _config[$"{profilePath}:Buzzer"] ?? "None";
            var pause = _config[$"{profilePath}:Pause"] ?? "None";

            return new List<string>
    {
        "-d", queueName,
        "-n", copies.ToString(),

        "-o", $"PageSize={pageSize}",
        "-o", "fit-to-page",
        "-o", $"MediaType={mediaType}",
        "-o", $"PrintQuality={printQuality}",
        "-o", $"ActionMode={actionMode}",
        "-o", $"CutInterval={cutInterval}",
        "-o", $"Buzzer={buzzer}",
        "-o", $"Pause={pause}",

        pdfPath
    };
        }

        private static string ShellQuote(string value)
        {
            return "'" + value.Replace("'", "'\"'\"'") + "'";
        }

        private static string MakeLinuxSafeFileName(string fileName)
        {
            var safe = new string(fileName.Select(ch =>
                char.IsLetterOrDigit(ch) ||
                ch == '.' ||
                ch == '-' ||
                ch == '_'
                    ? ch
                    : '_').ToArray());

            return safe.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? safe
                : safe + ".pdf";
        }

        private async Task<ProcessRunResult> RunProcessAsync(
            string fileName,
            IEnumerable<string> arguments,
            int timeoutSeconds)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
                psi.ArgumentList.Add(argument);

            using var process = Process.Start(psi);

            if (process == null)
            {
                return new ProcessRunResult
                {
                    ExitCode = -1,
                    Stdout = "",
                    Stderr = $"Failed to start process: {fileName}"
                };
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignored
                }

                return new ProcessRunResult
                {
                    ExitCode = -1,
                    Stdout = await stdoutTask,
                    Stderr = $"Process timeout after {timeoutSeconds} seconds: {fileName}"
                };
            }

            return new ProcessRunResult
            {
                ExitCode = process.ExitCode,
                Stdout = await stdoutTask,
                Stderr = await stderrTask
            };
        }

        private class ProcessRunResult
        {
            public int ExitCode { get; set; }
            public string Stdout { get; set; } = string.Empty;
            public string Stderr { get; set; } = string.Empty;
        }
    }
}
