namespace AMLO.Project.Models;

public class AmloEndpoint
{
    public string Name { get; set; }
    public string VersionEndpoint { get; set; }
    public string DataEndpoint { get; set; }
    public string ListName { get; set; }
}

public class AmloConfig
{
    public List<AmloEndpoint> Endpoints { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string XApiKey { get; set; }
    public string CaPassword { get; set; }
    public string Keypassword { get; set; }
    public string CertBase64 { get; set; }
    public string KeyBase64 { get; set; }
}