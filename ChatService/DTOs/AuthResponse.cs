﻿namespace ChatService.DTOs;

public class AuthResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset Expires { get; set; }
}