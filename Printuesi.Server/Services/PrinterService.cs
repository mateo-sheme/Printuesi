using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace Printuesi.Server.Services
{
    public class PrinterService : IPrinterService
    {
        private readonly string _printerIP;
        private readonly int _lprport = 515;
        private readonly int _rawport = 9100;
        private readonly string _printerQueue;
    
        public PrinterService(IConfiguration config)
        {
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

        public async Task<bool> SendFileLprAsync(string filePath)
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
        }

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
    }
}
