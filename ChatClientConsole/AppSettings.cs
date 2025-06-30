namespace ChatClientConsole;

public class ApiSettings
{
    public string UserServiceUrl { get; set; } = string.Empty;
    public string MessageServiceUrl { get; set; } = string.Empty;
    public string ChatServiceUrl { get; set; } = string.Empty;
}

public class AppSettings
{
    public ApiSettings ApiSettings { get; set; } = new ApiSettings();
}