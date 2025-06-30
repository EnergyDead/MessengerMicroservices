namespace ChatService.DTOs;

public class ServiceLoginRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceSecret { get; set; } = string.Empty;
}