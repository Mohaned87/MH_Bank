using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Sockets;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiscoveryController : ControllerBase
{
    /// <summary>
    /// Endpoint للكشف عن أن API يعمل
    /// يستخدمه التطبيق للتأكد من الاتصال
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        var localIP = GetLocalIPAddress();

        return Ok(new
        {
            Success = true,
            Message = "MH Bank API is running",
            ServerIP = localIP,
            ServerTime = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch
        {
            // ignored
        }
        return "Unknown";
    }
}